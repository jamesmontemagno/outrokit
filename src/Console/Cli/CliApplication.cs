using System.CommandLine;
using System.CommandLine.Parsing;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHub.Copilot;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Console.Cli;

public enum MetadataArtifact { Titles, Descriptions, Chapters }
public sealed record CliError(string Code, string Message, object? Details = null);
public sealed record CliResponse(bool Success, string Command, object? Result, CliError? Error);
internal sealed record CliOutcome(object? Result, int ExitCode = 0, CliError? Error = null);

public sealed class CliApplication
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly Func<AppSettings, Func<PermissionRequest, PermissionInvocation, Task<bool>>, IMetadataGenerator> _generatorFactory;

    public CliApplication(
        Func<AppSettings, Func<PermissionRequest, PermissionInvocation, Task<bool>>, IMetadataGenerator>? generatorFactory = null)
    {
        _generatorFactory = generatorFactory
            ?? ((settings, permissions) => new MetadataGenerator(settings, permissions, disableTools: true));
    }

    public static bool ShouldRun(string[] args) => args.Length > 0 && !File.Exists(args[0]);

    public async Task<int> RunAsync(
        string[] args, TextWriter output, TextWriter error, CancellationToken cancellationToken = default)
    {
        var root = new RootCommand("OutroKit: noninteractive episode, caption, and audio workflows. Commands return JSON.");
        var settingsPath = new Option<string?>("--settings") { Description = "Settings JSON file to read; never modified.", Recursive = true };
        var ffmpeg = new Option<string?>("--ffmpeg") { Description = "ffmpeg executable path or command.", Recursive = true };
        var overwrite = new Option<bool>("--overwrite") { Description = "Explicitly allow replacing an output, never an input.", Recursive = true };
        var timeout = new Option<int>("--timeout-seconds") { DefaultValueFactory = _ => 1800, Recursive = true };
        root.Options.Add(settingsPath);
        root.Options.Add(ffmpeg);
        root.Options.Add(overwrite);
        root.Options.Add(timeout);
        timeout.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int>() is <= 0 or > 86400)
                result.AddError("--timeout-seconds must be between 1 and 86400.");
        });

        void Register(Command command, string name,
            Func<ParseResult, AppSettings, CancellationToken, Task<CliOutcome>> action)
        {
            command.SetAction(async (parse, invocationToken) =>
            {
                using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(invocationToken, cancellationToken);
                cancellation.CancelAfter(TimeSpan.FromSeconds(parse.GetValue(timeout)));
                try
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    var path = parse.GetValue(settingsPath);
                    if (path is not null && !File.Exists(path))
                        throw new FileNotFoundException("The specified settings file was not found.", path);
                    var settings = await new SettingsService(path).LoadAsync(throwOnError: true);
                    if (parse.GetValue(ffmpeg) is { } ffmpegPath)
                        settings.FfmpegPath = ffmpegPath;
                    var result = await action(parse, settings, cancellation.Token);
                    await WriteResponseAsync(output, error, name, result);
                    return result.ExitCode;
                }
                catch (Exception ex)
                {
                    var result = Failure(ex);
                    await WriteResponseAsync(output, error, name, result);
                    return result.ExitCode;
                }
            });
        }

        var metadata = new Command("metadata", "Generate chosen metadata as JSON; media must be transcribed first.");
        var metadataInput = Input(metadata);
        var metadataOutput = Output(metadata, required: false);
        var artifacts = Choices("--artifacts", new[] { MetadataArtifact.Titles, MetadataArtifact.Descriptions });
        var lengths = Choices("--lengths", Enum.GetValues<DescriptionLength>());
        var style = Choice<TitleStyle>("--style");
        var model = new Option<string?>("--model");
        var title = new Option<string?>("--title");
        var show = new Option<string?>("--show-name");
        var hosts = new Option<string?>("--hosts");
        var context = new Option<string?>("--context");
        var titleCount = Positive("--title-count");
        var titleWords = Positive("--title-max-words");
        var minChapters = Positive("--min-chapters");
        var maxChapters = Positive("--max-chapters");
        var chapterRate = Positive("--chapters-per-30-min");
        var chapterWords = Positive("--chapter-max-words");
        var approvals = new Option<string[]>("--approve-permission")
        {
            Description = "One exact permission ID approved by the user; repeat for distinct requests.",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };
        var denials = new Option<string[]>("--deny-permission")
        {
            Description = "One exact permission ID denied by the user; repeat for distinct requests.",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };
        foreach (var option in new Option[] { artifacts, lengths, style, model, title, show, hosts, context,
                     titleCount, titleWords, minChapters, maxChapters, chapterRate, chapterWords, approvals, denials })
            metadata.Options.Add(option);
        root.Subcommands.Add(metadata);
        Register(metadata, "metadata", async (parse, settings, token) =>
        {
            var input = ExistingInput(parse, metadataInput);
            if (MediaTranscriptService.HasMediaExtension(input))
                throw new ArgumentException("Transcribe media first with 'outrokit transcribe', then pass its SRT to metadata.");
            var transcript = await new TranscriptParser(settings).ParseAsync(input);
            if (string.IsNullOrWhiteSpace(transcript.GetFullText()))
                throw new ArgumentException("The transcript is empty.");
            var selected = (parse.GetValue(artifacts) ?? []).Distinct().ToArray();
            if (selected.Contains(MetadataArtifact.Chapters) && !transcript.HasTimestamps)
                throw new ArgumentException("Chapters require real timestamps. Supply a timed transcript or transcribe the media first.");
            var destination = parse.GetValue(metadataOutput);
            if (destination is not null)
            {
                if (!string.Equals(Path.GetExtension(destination), ".json", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException("Metadata --output must be a .json file.");
                CliFileOutput.Validate(destination, parse.GetValue(overwrite), input);
            }

            settings.Model = parse.GetValue(model) ?? settings.Model;
            settings.TitleStyle = parse.GetValue(style) ?? settings.TitleStyle;
            settings.PodcastName = parse.GetValue(show) ?? settings.PodcastName;
            settings.HostNames = parse.GetValue(hosts) ?? settings.HostNames;
            settings.EpisodeContext = parse.GetValue(context);
            settings.TitleCount = parse.GetValue(titleCount) ?? settings.TitleCount;
            settings.TitleMaxWords = parse.GetValue(titleWords) ?? settings.TitleMaxWords;
            settings.MinChapters = parse.GetValue(minChapters) ?? settings.MinChapters;
            settings.MaxChapters = parse.GetValue(maxChapters) ?? settings.MaxChapters;
            settings.ChaptersPer30Min = parse.GetValue(chapterRate) ?? settings.ChaptersPer30Min;
            settings.ChapterTitleMaxWords = parse.GetValue(chapterWords) ?? settings.ChapterTitleMaxWords;
            if (settings.MinChapters > settings.MaxChapters)
                throw new ArgumentException("--min-chapters cannot exceed --max-chapters.");
            if (new[] { settings.TitleCount, settings.TitleMaxWords, settings.ShortDescriptionWords,
                    settings.MediumDescriptionWords, settings.LongDescriptionWords, settings.MinChapters,
                    settings.MaxChapters, settings.ChaptersPer30Min, settings.ChapterTitleMaxWords }.Any(value => value <= 0))
                throw new ArgumentException("Generation counts and word targets must be positive.");

            using var generationCancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
            var approvedIds = parse.GetValue(approvals) ?? [];
            var deniedIds = parse.GetValue(denials) ?? [];
            if (approvedIds.Intersect(deniedIds, StringComparer.OrdinalIgnoreCase).Any())
                throw new ArgumentException("A permission cannot be both approved and denied.");
            var gate = new CliPermissionGate(approvedIds, generationCancellation, deniedIds);
            var result = new GenerationResult
            {
                SelectedTitle = parse.GetValue(title),
                TitleStyle = selected.Contains(MetadataArtifact.Titles) ? settings.TitleStyle : null
            };
            try
            {
                await using var generator = _generatorFactory(settings, gate.DecideAsync);
                if (selected.Contains(MetadataArtifact.Titles))
                {
                    result.Titles = await generator.GenerateTitlesAsync(transcript, settings.TitleStyle,
                        _ => { }, generationCancellation.Token);
                    if (result.Titles.Count != settings.TitleCount || result.Titles.Any(value =>
                            string.IsNullOrWhiteSpace(value) || value.Split((char[]?)null,
                                StringSplitOptions.RemoveEmptyEntries).Length > settings.TitleMaxWords))
                        throw new InvalidOperationException("Copilot's titles do not meet the requested count and word limit.");
                }
                if (selected.Contains(MetadataArtifact.Descriptions))
                {
                    foreach (var length in (parse.GetValue(lengths) ?? []).Distinct())
                    {
                        var description = await generator.GenerateDescriptionAsync(
                            transcript, length, result.SelectedTitle, _ => { }, generationCancellation.Token);
                        if (string.IsNullOrWhiteSpace(description))
                            throw new InvalidOperationException($"Copilot returned an empty {length} description.");
                        result.Descriptions[length] = description;
                    }
                }
                if (selected.Contains(MetadataArtifact.Chapters))
                {
                    result.Chapters = await generator.GenerateChaptersAsync(transcript, _ => { }, generationCancellation.Token);
                    ValidateChapters(result.Chapters, transcript.DurationSeconds, settings.ChapterTitleMaxWords);
                    var startTimes = transcript.Segments.Select(segment => segment.StartTimeYouTube).ToHashSet();
                    if (result.Chapters.Skip(1).Any(chapter => !startTimes.Contains(chapter.Timestamp)))
                        throw new InvalidOperationException("Copilot returned chapter starts that are not supported by transcript timecodes.");
                }
                if (gate.Pending is not null)
                    throw gate.Pending;
            }
            catch (Exception) when (gate.Pending is not null)
            {
                throw gate.Pending;
            }
            var data = new { Input = input, Model = settings.Model, Metadata = result };
            string? saved = null;
            if (destination is not null)
                saved = await WriteTextAsync(destination, JsonSerializer.Serialize(data, JsonOptions),
                    parse.GetValue(overwrite), [input], token);
            return new(new { data.Input, data.Model, data.Metadata, SavedFiles = saved is null ? Array.Empty<string>() : [saved] });
        });

        var transcribe = new Command("transcribe", "Transcribe audio or video locally to an SRT file; no Copilot needed.");
        var mediaInput = Input(transcribe);
        var srtOutput = Output(transcribe);
        var whisper = new Option<string?>("--whisper-model") { Description = "Whisper model catalog ID." };
        var whisperPath = new Option<string?>("--whisper-model-path") { Description = "Existing Whisper GGML model file." };
        transcribe.Options.Add(whisper);
        transcribe.Options.Add(whisperPath);
        root.Subcommands.Add(transcribe);
        Register(transcribe, "transcribe", async (parse, settings, token) =>
        {
            var input = ExistingInput(parse, mediaInput);
            if (!MediaTranscriptService.HasMediaExtension(input))
                throw new ArgumentException("The input must be a supported audio or video file.");
            SetWhisperModel(settings, parse.GetValue(whisper), parse.GetValue(whisperPath));
            var destination = OutputPath(parse, srtOutput, ".srt");
            CliFileOutput.Validate(destination, parse.GetValue(overwrite), input);
            var models = new WhisperModelService();
            if (models.GetInstalledModelPath(settings) is null)
                throw new InvalidOperationException("Whisper model missing. Use 'outrokit models download' or --whisper-model-path.");
            var service = new MediaTranscriptService(settings, models);
            if (!await service.HasAudioStreamAsync(input, token))
                throw new ArgumentException("The input does not contain a readable audio track.");
            var content = await service.TranscribeToSrtAsync(input, cancellationToken: token);
            var saved = await WriteTextAsync(destination, content, parse.GetValue(overwrite), [input], token);
            return new(new { Input = input, Output = saved, Report = new CaptionQualityService().Analyze(content, ".srt") });
        });

        var srt = new Command("srt", "Convert an existing timed transcript.");
        var convert = new Command("convert", "Convert a timestamped transcript to SRT; no AI or guessed timing.");
        var convertInput = Input(convert);
        var convertOutput = Output(convert);
        srt.Subcommands.Add(convert);
        root.Subcommands.Add(srt);
        Register(convert, "srt convert", async (parse, settings, token) =>
        {
            var input = ExistingInput(parse, convertInput);
            var destination = OutputPath(parse, convertOutput, ".srt");
            CliFileOutput.Validate(destination, parse.GetValue(overwrite), input);
            var transcript = await new TranscriptParser(settings).ParseAsync(input);
            if (!transcript.HasTimestamps)
                throw new ArgumentException("SRT conversion requires a transcript with timestamps.");
            var result = new SrtConverter().ConvertToSrt(transcript);
            var saved = await WriteTextAsync(destination, result.Content, parse.GetValue(overwrite), [input], token);
            return new(new { Input = input, Output = saved, Warnings = result.Errors });
        });

        var captions = new Command("captions", "Check, safely fix, or burn captions.");
        var check = new Command("check", "Report readability findings; exit 4 if findings remain.");
        var checkInput = Input(check);
        captions.Subcommands.Add(check);
        Register(check, "captions check", async (parse, _, token) =>
        {
            var input = ExistingInput(parse, checkInput);
            var report = await ReadCaptionReportAsync(input, token);
            return QualityOutcome(new { Input = input, Report = report }, report);
        });
        var fix = new Command("fix", "Save safe SRT fixes to a new file; exit 4 if findings remain.");
        var fixInput = Input(fix);
        var fixOutput = Output(fix);
        captions.Subcommands.Add(fix);
        Register(fix, "captions fix", async (parse, _, token) =>
        {
            var input = ExistingInput(parse, fixInput);
            if (!string.Equals(Path.GetExtension(input), ".srt", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Standalone caption fixes require an SRT input.");
            var destination = OutputPath(parse, fixOutput, ".srt");
            CliFileOutput.Validate(destination, parse.GetValue(overwrite), input);
            await ReadCaptionReportAsync(input, token);
            var result = new CaptionQualityService().ApplySafeFixes(await File.ReadAllTextAsync(input, token), ".srt");
            if (result.Report.CueCount == 0)
                throw new ArgumentException("No caption text remains after removing empty cues.");
            var saved = await WriteTextAsync(destination, result.Content, parse.GetValue(overwrite), [input], token);
            return QualityOutcome(new { Input = input, Output = saved, result.AppliedFixCount, result.Report }, result.Report);
        });

        var burn = new Command("burn", "Burn captions into a new H.264 video. Quality findings block unless explicitly allowed.");
        var burnInput = Input(burn);
        var captionFile = new Option<string>("--captions") { Required = true };
        var burnOutput = Output(burn);
        var appearance = Choice<CaptionAppearance>("--appearance");
        var size = Choice<CaptionSize>("--size");
        var position = Choice<CaptionPosition>("--position");
        var safeFixes = new Option<bool>("--fix");
        var allowIssues = new Option<bool>("--allow-issues");
        foreach (var option in new Option[] { captionFile, appearance, size, position, safeFixes, allowIssues })
            burn.Options.Add(option);
        captions.Subcommands.Add(burn);
        root.Subcommands.Add(captions);
        Register(burn, "captions burn", async (parse, settings, token) =>
        {
            var input = ExistingInput(parse, burnInput);
            if (!MediaTranscriptService.HasVideoExtension(input))
                throw new ArgumentException("Caption burning requires a supported video file.");
            var captionPath = ExistingFile(parse.GetValue(captionFile));
            var destination = OutputPath(parse, burnOutput, ".mp4", ".mov", ".mkv", ".m4v");
            CliFileOutput.Validate(destination, parse.GetValue(overwrite), input, captionPath);
            var ownStyling = CaptionBurnService.HasOwnStyling(captionPath);
            if (ownStyling && (parse.GetValue(appearance) is not null || parse.GetValue(size) is not null ||
                               parse.GetValue(position) is not null || parse.GetValue(safeFixes)))
                throw new ArgumentException("ASS/SSA retain their own styling and cannot use --appearance, --size, --position, or --fix.");
            var report = await ReadCaptionReportAsync(captionPath, token);
            CaptionQualityFixResult? fixedCaptions = null;
            if (parse.GetValue(safeFixes))
            {
                fixedCaptions = new CaptionQualityService().ApplySafeFixes(
                    await File.ReadAllTextAsync(captionPath, token), Path.GetExtension(captionPath));
                report = fixedCaptions.Report;
                if (report.CueCount == 0)
                    throw new ArgumentException("No captions remain after safe fixes.");
            }
            if (report.HasIssues && !parse.GetValue(allowIssues))
                return QualityOutcome(new { Input = input, Report = report }, report);
            var captionStyle = new CaptionStyle(parse.GetValue(size) ?? settings.CaptionSize,
                parse.GetValue(position) ?? settings.CaptionPosition,
                parse.GetValue(appearance) ?? settings.CaptionAppearance);
            string? temporaryCaptions = null;
            try
            {
                if (fixedCaptions is not null)
                {
                    temporaryCaptions = Path.Combine(Path.GetTempPath(),
                        $"outrokit-cli-captions-{Guid.NewGuid():N}{Path.GetExtension(captionPath)}");
                    await File.WriteAllTextAsync(temporaryCaptions, fixedCaptions.Content, token);
                }
                var saved = await CliFileOutput.WriteAsync(destination, parse.GetValue(overwrite), [input, captionPath],
                    path => new CaptionBurnService(settings).BurnAsync(input, temporaryCaptions ?? captionPath,
                        path, captionStyle, cancellationToken: token), token);
                return new(new { Input = input, Output = saved, Report = report,
                    AppliedFixCount = fixedCaptions?.AppliedFixCount ?? 0, EmbeddedStyling = ownStyling, Style = ownStyling ? null : captionStyle });
            }
            finally
            {
                if (temporaryCaptions is not null)
                    File.Delete(temporaryCaptions);
            }
        });

        var audio = new Command("audio", "Extract or replace a video's audio without re-encoding its picture.");
        var extract = new Command("extract", "Save the original audio to a separate file.");
        var extractInput = Input(extract);
        var extractOutput = Output(extract);
        audio.Subcommands.Add(extract);
        Register(extract, "audio extract", async (parse, settings, token) =>
        {
            var input = ExistingInput(parse, extractInput);
            var destination = OutputPath(parse, extractOutput, ".wav", ".mp3", ".m4a", ".flac");
            var saved = await CliFileOutput.WriteAsync(destination, parse.GetValue(overwrite), [input],
                path => new AudioReplacementService(settings).ExtractAudioAsync(input, path, token), token);
            return new(new { Input = input, Output = saved });
        });
        var replace = new Command("replace", "Replace audio, pad or trim it to the video length, and save MP4.");
        var replaceInput = Input(replace);
        var audioFile = new Option<string>("--audio") { Required = true };
        var replaceOutput = Output(replace);
        replace.Options.Add(audioFile);
        audio.Subcommands.Add(replace);
        root.Subcommands.Add(audio);
        Register(replace, "audio replace", async (parse, settings, token) =>
        {
            var input = ExistingInput(parse, replaceInput);
            var replacement = ExistingFile(parse.GetValue(audioFile));
            var destination = OutputPath(parse, replaceOutput, ".mp4");
            var saved = await CliFileOutput.WriteAsync(destination, parse.GetValue(overwrite), [input, replacement],
                path => new AudioReplacementService(settings).ReplaceAudioAsync(input, replacement, path, token), token);
            return new(new { Input = input, Audio = replacement, Output = saved });
        });

        var modelsCommand = new Command("models", "Discover and explicitly download local Whisper models.");
        var list = new Command("list", "List Whisper model IDs; no network or Copilot needed.");
        modelsCommand.Subcommands.Add(list);
        Register(list, "models list", (_, _, _) => Task.FromResult(new CliOutcome(WhisperModelCatalog.All)));
        var download = new Command("download", "Download and initialize a Whisper model without changing settings.");
        var downloadModel = new Option<string>("--whisper-model") { Required = true };
        var modelsDirectory = new Option<string?>("--models-directory");
        download.Options.Add(downloadModel);
        download.Options.Add(modelsDirectory);
        modelsCommand.Subcommands.Add(download);
        root.Subcommands.Add(modelsCommand);
        Register(download, "models download", async (parse, settings, token) =>
        {
            SetWhisperModel(settings, parse.GetValue(downloadModel), null);
            var service = new WhisperModelService(parse.GetValue(modelsDirectory));
            var path = await service.DownloadAndInitializeAsync(settings, cancellationToken: token);
            return new(new { Model = settings.WhisperModel, ModelPath = Path.GetFullPath(path) });
        });

        var parsed = root.Parse(args);
        if (parsed.Errors.Count > 0)
        {
            var result = new CliOutcome(null, 2, new("invalid_arguments", string.Join(" ", parsed.Errors.Select(e => e.Message))));
            await WriteResponseAsync(output, error, "parse", result);
            return result.ExitCode;
        }
        return await parsed.InvokeAsync(new InvocationConfiguration
        {
            Output = output, Error = error, EnableDefaultExceptionHandler = false
        }, cancellationToken);
    }

    private static Argument<string> Input(Command command)
    {
        var input = new Argument<string>("input") { Description = "Input file path." };
        input.Validators.Add(result =>
        {
            foreach (var token in result.Tokens)
                if (token.Value.StartsWith('-') && !File.Exists(token.Value))
                    result.AddError($"Unknown option: {token.Value}. Use --help to see supported options.");
        });
        command.Arguments.Add(input);
        return input;
    }

    private static Option<string?> Output(Command command, bool required = true)
    {
        var option = new Option<string?>("--output") { Required = required, Description = "Explicit output file path." };
        command.Options.Add(option);
        return option;
    }

    private static Option<int?> Positive(string name)
    {
        var option = new Option<int?>(name);
        option.Validators.Add(result =>
        {
            if (result.GetValueOrDefault<int?>() is <= 0)
                result.AddError($"{name} must be positive.");
        });
        return option;
    }

    private static Option<T?> Choice<T>(string name) where T : struct, Enum
    {
        var option = new Option<T?>(name) { Description = $"Choices: {string.Join(", ", Enum.GetNames<T>())}." };
        option.Validators.Add(result => ValidateEnumTokens<T>(result));
        return option;
    }

    private static Option<T[]> Choices<T>(string name, T[] defaults) where T : struct, Enum
    {
        var option = new Option<T[]>(name)
        {
            DefaultValueFactory = _ => defaults, AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore, Description = $"Choices: {string.Join(", ", Enum.GetNames<T>())}."
        };
        option.Validators.Add(result => ValidateEnumTokens<T>(result));
        return option;
    }

    private static void ValidateEnumTokens<T>(OptionResult result) where T : struct, Enum
    {
        foreach (var token in result.Tokens)
            if (!Enum.TryParse<T>(token.Value, true, out var value) || !Enum.IsDefined(value) || int.TryParse(token.Value, out _))
                result.AddError($"Use a named value from: {string.Join(", ", Enum.GetNames<T>())}.");
    }

    private static string ExistingInput(ParseResult parse, Argument<string> input) => ExistingFile(parse.GetValue(input));

    private static string ExistingFile(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("An input file path is required.");
        if (!File.Exists(path))
            throw new FileNotFoundException($"Input file not found: {path}", path);
        return Path.GetFullPath(path);
    }

    private static string OutputPath(ParseResult parse, Option<string?> option, params string[] extensions)
    {
        var path = parse.GetValue(option);
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("--output is required.");
        if (!extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Output must use one of: {string.Join(", ", extensions)}.");
        return Path.GetFullPath(path);
    }

    private static Task<string> WriteTextAsync(string path, string content, bool overwrite, string[] inputs, CancellationToken token) =>
        CliFileOutput.WriteAsync(path, overwrite, inputs, temporary => File.WriteAllTextAsync(temporary, content, token), token);

    private static void SetWhisperModel(AppSettings settings, string? model, string? path)
    {
        if (model is not null)
        {
            if (!WhisperModelCatalog.TryGet(model, out var entry))
                throw new ArgumentException("Unknown Whisper model. Run 'outrokit models list' for valid IDs.");
            settings.WhisperModel = entry.Id;
            settings.WhisperModelPath = null;
        }
        if (path is not null)
            settings.WhisperModelPath = ExistingFile(path);
    }

    private static async Task<CaptionQualityReport> ReadCaptionReportAsync(string path, CancellationToken token)
    {
        if (!CaptionBurnService.HasCaptionExtension(path))
            throw new ArgumentException("Captions must be .srt, .vtt, .ass, or .ssa.");
        var report = await new CaptionQualityService().AnalyzeAsync(path, token);
        if (report.CueCount == 0)
            throw new ArgumentException("No captions could be read from the input.");
        return report;
    }

    private static CliOutcome QualityOutcome(object result, CaptionQualityReport report) =>
        report.HasIssues ? new(result, 4, new("quality_issues", "Caption quality findings require review.", report)) : new(result);

    public static void ValidateChapters(IReadOnlyList<Chapter> chapters, double durationSeconds, int titleMaxWords)
    {
        if (chapters.Count < 3)
            throw new ArgumentException("YouTube chapters need at least three timestamped topic boundaries.");
        var times = new List<double>();
        foreach (var chapter in chapters)
        {
            var parts = chapter.Timestamp.Split(':');
            if (parts.Length is not (2 or 3) || parts.Any(p => p.Length != 2 || p.Any(c => !char.IsAsciiDigit(c))) ||
                int.Parse(parts[^1]) >= 60 || int.Parse(parts[^2]) >= 60)
                throw new ArgumentException($"Invalid chapter timestamp: {chapter.Timestamp}");
            var time = parts.Length == 3
                ? int.Parse(parts[0]) * 3600 + int.Parse(parts[1]) * 60 + int.Parse(parts[2])
                : int.Parse(parts[0]) * 60 + int.Parse(parts[1]);
            if (time >= durationSeconds || (times.Count == 0 ? time != 0 : time - times[^1] < 10))
                throw new ArgumentException($"Chapter timestamp is out of range or chapters are less than 10 seconds apart: {chapter.Timestamp}");
            if (string.IsNullOrWhiteSpace(chapter.Title) ||
                chapter.Title.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries).Length > titleMaxWords)
                throw new ArgumentException("A chapter title is empty or exceeds the configured word limit.");
            times.Add(time);
        }
        if (durationSeconds - times[^1] < 10)
            throw new ArgumentException("The final chapter must last at least 10 seconds.");
    }

    private static CliOutcome Failure(Exception error) => error switch
    {
        CliPermissionRequiredException permission => new(null, 3, new("permission_required", permission.Message, permission.Request)),
        OperationCanceledException => new(null, 130, new("cancelled", "The operation was cancelled or its timeout expired.")),
        FileNotFoundException => new(null, 2, new("input_not_found", error.Message)),
        ArgumentException => new(null, 2, new("invalid_arguments", error.Message)),
        JsonException => new(null, 2, new("invalid_settings", error.Message)),
        _ => new(null, 1, new("operation_failed", error.Message))
    };

    private static async Task WriteResponseAsync(TextWriter output, TextWriter error, string command, CliOutcome outcome)
    {
        await output.WriteLineAsync(JsonSerializer.Serialize(
            new CliResponse(outcome.ExitCode == 0, command, outcome.Result, outcome.Error), JsonOptions));
        if (outcome.Error is not null)
            await error.WriteLineAsync($"{outcome.Error.Code}: {outcome.Error.Message}");
    }
}
