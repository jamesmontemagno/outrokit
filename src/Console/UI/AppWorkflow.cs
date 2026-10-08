using System.Collections.Concurrent;
using System.Text;
using GitHub.Copilot;
using Spectre.Console;
using Spectre.Console.Rendering;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Console.UI;

/// <summary>
/// Main application workflow using Spectre.Console.
/// </summary>
public class AppWorkflow
{
    private AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly TranscriptParser _parser;
    private readonly SrtConverter _srtConverter;
    private readonly OutputService _outputService;
    private readonly WhisperModelService _whisperModelService;
    private MetadataGenerator? _generator;
    
    private Transcript? _transcript;
    private GenerationResult _result = new();
    private List<DescriptionLength> _descriptionLengths = [.. Enum.GetValues<DescriptionLength>()];

    private readonly ConcurrentQueue<PendingPermission> _pendingPermissions = new();

    private sealed record PendingPermission(
        PermissionRequest Request,
        PermissionInvocation Invocation,
        TaskCompletionSource<bool> Completion);

    [Flags]
    private enum CopyScope
    {
        Titles = 1,
        Descriptions = 2,
        Chapters = 4,
        All = Titles | Descriptions | Chapters
    }

    private const string CopyDoneChoice = "✅ Done";

    private static IRenderable BuildStreamingView(string header, string content, int frame)
    {
        IRenderable body = string.IsNullOrEmpty(content)
            ? new Markup($"[grey]Waiting for response{new string('.', (frame / 6 % 3) + 1)}[/]")
            : new Text(content);

        return new Rows(new Markup($"[bold]{Markup.Escape(header)}[/]"), body);
    }
    
    public AppWorkflow()
    {
        _settings = new AppSettings();
        _settingsService = new SettingsService();
        _parser = new TranscriptParser(_settings);
        _srtConverter = new SrtConverter();
        _outputService = new OutputService(_srtConverter);
        _whisperModelService = new WhisperModelService();
    }
    
    /// <summary>
    /// Runs the application with optional CLI arguments.
    /// </summary>
    public async Task RunAsync(string[] args, CopilotAuthService.CopilotStatus? copilotStatus = null)
    {
        // Load saved settings
        await LoadSettingsAsync();
        
        // Show header with ASCII art and Copilot status
        ConsoleUI.ShowHeader(copilotStatus);
        if (PackageIdentity.IsLegacyPackage)
        {
            ConsoleUI.ShowRenamedPackageNotice();
        }
        
        // Check if Copilot is not ready
        if (copilotStatus != null && (!copilotStatus.IsInstalled || (!copilotStatus.IsTokenSet && !copilotStatus.IsAuthenticated)))
        {
            AnsiConsole.MarkupLine("[yellow]Press any key to exit...[/]");
            System.Console.ReadKey(true);
            return;
        }
        
        // If an input path is provided as an argument, route it through the matching flow.
        // Anything that is not a known video or audio type is loaded as a transcript, as before.
        if (args.Length > 0 && File.Exists(args[0]))
        {
            if (MediaTranscriptService.HasMediaExtension(args[0]))
            {
                await ProcessMediaAsync(args[0]);
            }
            else
            {
                await LoadTranscriptAsync(args[0]);
            }
        }
        
        await MainMenuLoopAsync();
    }
    
    private async Task LoadSettingsAsync()
    {
        try
        {
            _settings = await _settingsService.LoadAsync();
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowWarning($"Could not load settings: {ex.Message}. Using defaults.");
        }
    }
    
    private async Task SaveSettingsAsync()
    {
        try
        {
            await _settingsService.SaveAsync(_settings);
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowWarning($"Could not save settings: {ex.Message}");
        }
    }
    
    private async Task MainMenuLoopAsync()
    {
        while (true)
        {
            var choices = new List<string>();
            
            if (_transcript == null)
            {
                choices.Add("📂 Load Transcript, Video, or Audio");
            }
            else
            {
                choices.Add("📂 Load Different Transcript, Video, or Audio");
                choices.Add("🚀 Generate All Metadata");
                choices.Add("📝 Generate Titles");
                choices.Add("📄 Generate Descriptions");
                choices.Add("📑 Generate Chapters");
                choices.Add("🎬 Convert to SRT");
            }
            
            if (_result.Titles.Count > 0 || _result.Descriptions.Count > 0 || _result.Chapters.Count > 0)
            {
                choices.Add("👁️ View Results");
                choices.Add("📋 Copy to Clipboard");
                choices.Add("💾 Save Results");
            }
            
            choices.Add("🔥 Burn Captions into Video");
            choices.Add("✨ What's New");
            choices.Add("⚙️ Settings");
            choices.Add("❌ Exit");
            
            AnsiConsole.WriteLine();
            var action = ConsoleUI.SelectFromList("[bold]Main Menu[/]", choices);
            
            switch (action)
            {
                case "📂 Load Transcript, Video, or Audio":
                case "📂 Load Different Transcript, Video, or Audio":
                    await PromptAndLoadInputAsync();
                    break;
                    
                case "🚀 Generate All Metadata":
                    await GenerateAllAsync();
                    break;
                    
                case "📝 Generate Titles":
                    await GenerateTitlesAsync();
                    break;
                    
                case "📄 Generate Descriptions":
                    await GenerateDescriptionsAsync();
                    break;
                    
                case "📑 Generate Chapters":
                    await GenerateChaptersAsync();
                    break;
                    
                case "🎬 Convert to SRT":
                    ConvertToSrt();
                    break;
                    
                case "👁️ View Results":
                    await ViewResultsMenuAsync();
                    break;
                    
                case "📋 Copy to Clipboard":
                    await OfferCopyAsync(CopyScope.All);
                    break;
                    
                case "💾 Save Results":
                    await SaveResultsAsync();
                    break;
                    
                case "🔥 Burn Captions into Video":
                    await BurnCaptionsAsync();
                    break;
                    
                case "✨ What's New":
                    ConsoleUI.ShowWhatsNew();
                    ConsoleUI.WaitForKey();
                    break;
                    
                case "⚙️ Settings":
                    await SettingsMenuAsync();
                    break;
                    
                case "❌ Exit":
                    if (AnsiConsole.Confirm("Are you sure you want to exit?"))
                    {
                        await CleanupAsync();
                        return;
                    }
                    break;
            }
        }
    }
    
    private async Task PromptAndLoadInputAsync()
    {
        var inputType = ConsoleUI.SelectFromList(
            "What would you like to provide?",
            new[] { "📄 Transcript file", "🎬 Video or audio file" });

        if (inputType == "📄 Transcript file")
        {
            var transcriptPath = ConsoleUI.AskFilePath(
                "Select a transcript file:",
                mustExist: true,
                discoveryType: ConsoleUI.FileDiscoveryType.Transcript);
            await LoadTranscriptAsync(transcriptPath);
            return;
        }

        var mediaPath = ConsoleUI.AskFilePath(
            "Select a video or audio file:",
            mustExist: true,
            discoveryType: ConsoleUI.FileDiscoveryType.Media);
        await ProcessMediaAsync(mediaPath);
    }

    private async Task ProcessMediaAsync(string mediaPath)
    {
        try
        {
            if (_whisperModelService.GetInstalledModelPath(_settings) is null)
            {
                ConsoleUI.ShowWarning("A Whisper model must be installed before a video or audio file can be transcribed.");
                if (!AnsiConsole.Confirm("Open transcription settings now?", defaultValue: true))
                {
                    return;
                }

                await EditTranscriptionSettingsAsync();
                if (_whisperModelService.GetInstalledModelPath(_settings) is null)
                {
                    ConsoleUI.ShowWarning("Transcription was cancelled because no Whisper model is installed.");
                    return;
                }
            }

            var transcriptService = new MediaTranscriptService(_settings, _whisperModelService);
            var hasAudio = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync("Checking the file for an audio track...", _ =>
                    transcriptService.HasAudioStreamAsync(mediaPath));

            if (!hasAudio)
            {
                ConsoleUI.ShowError("The selected file does not contain a readable audio track.");
                return;
            }

            ConsoleUI.ShowSuccess($"Found audio to transcribe: {Path.GetFileName(mediaPath)}");

            // Ask up front so the transcript is written the moment a long transcription finishes,
            // rather than being held in memory behind a prompt.
            var defaultDirectory = Path.GetDirectoryName(Path.GetFullPath(mediaPath)) ?? Environment.CurrentDirectory;
            var defaultPath = Path.Combine(defaultDirectory, $"{Path.GetFileNameWithoutExtension(mediaPath)}.srt");
            var transcriptPath = ConsoleUI.AskSaveFilePath(
                "Where should the transcript be saved?",
                defaultPath,
                [".srt"]);

            ConsoleUI.ShowInfo("Transcribing locally with Whisper. Press Ctrl+C to cancel.");

            var srt = await ConsoleCancellation.RunAsync(cancellationToken => AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .StartAsync(async context =>
                {
                    var task = context.AddTask(
                        "[blue]Preparing audio for Whisper...[/]",
                        maxValue: 100);
                    task.IsIndeterminate = true;

                    var progress = new InlineProgress<MediaTranscriptionProgress>(update =>
                    {
                        task.IsIndeterminate = false;
                        task.Value = update.Percentage;
                        task.Description =
                            $"[blue]Transcribing {FormatDuration(update.Position)} / {FormatDuration(update.Duration)}[/]";
                    });

                    return await transcriptService.TranscribeToSrtAsync(mediaPath, progress, cancellationToken);
                }));

            await File.WriteAllTextAsync(transcriptPath, srt, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            ConsoleUI.ShowSuccess($"Saved transcript: {transcriptPath}");
            await LoadTranscriptAsync(transcriptPath);
        }
        catch (OperationCanceledException)
        {
            ConsoleUI.ShowWarning("Transcription cancelled.");
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Transcription failed: {ex.Message}");
        }
    }
    
    private async Task BurnCaptionsAsync()
    {
        string? fixedCaptionPath = null;
        try
        {
            var burnService = new CaptionBurnService(_settings);
            if (!await EnsureCaptionBurnSupportedAsync(burnService))
            {
                return;
            }

            var videoPath = ConsoleUI.AskFilePath(
                "Select the video to add captions to:",
                mustExist: true,
                discoveryType: ConsoleUI.FileDiscoveryType.Video);
            if (!MediaTranscriptService.HasVideoExtension(videoPath))
            {
                ConsoleUI.ShowError(
                    "Captions can only be burned into a video file (.mp4, .mov, .mkv, .avi, .webm, .m4v, .wmv, .mpeg, or .mpg).");
                return;
            }

            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync("Checking the video...", _ => burnService.GetVideoDetailsAsync(videoPath));

            AnsiConsole.MarkupLine(
                $"[grey]Captions in another format, or that need more work first? Try CaptionStack: {CaptionBurnService.CaptionToolsUrl}[/]");
            var captionPath = ConsoleUI.AskFilePath(
                "Select the captions file (.srt, .vtt, .ass, or .ssa):",
                mustExist: true,
                startDirectory: Path.GetDirectoryName(Path.GetFullPath(videoPath)),
                discoveryType: ConsoleUI.FileDiscoveryType.Captions);
            if (!CaptionBurnService.HasCaptionExtension(captionPath))
            {
                ConsoleUI.ShowError("Captions must be an .srt, .vtt, .ass, or .ssa file.");
                ConsoleUI.ShowInfo($"CaptionStack converts other caption formats for free: {CaptionBurnService.CaptionToolsUrl}");
                return;
            }

            var captionCount = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync("Reading the captions...", _ => burnService.CountCaptionsAsync(captionPath));
            if (captionCount == 0)
            {
                ConsoleUI.ShowError("No captions could be read from that file, so there is nothing to burn in.");
                ConsoleUI.ShowInfo($"If it is in another caption format, convert it with CaptionStack: {CaptionBurnService.CaptionToolsUrl}");
                return;
            }

            ConsoleUI.ShowSuccess($"Found {captionCount} captions in {Path.GetFileName(captionPath)}");

            var qualityService = new CaptionQualityService();
            var qualityReport = await qualityService.AnalyzeAsync(captionPath);
            var qualityAction = ShowCaptionQualityReport(qualityReport);
            if (qualityAction == CaptionQualityAction.Cancel)
            {
                ConsoleUI.ShowInfo("Caption burn cancelled before encoding.");
                return;
            }

            if (qualityAction == CaptionQualityAction.ApplyFixes)
            {
                var originalContent = await File.ReadAllTextAsync(captionPath);
                var fixResult = qualityService.ApplySafeFixes(originalContent, Path.GetExtension(captionPath));
                if (fixResult.Report.CueCount == 0)
                {
                    ConsoleUI.ShowError("The safe fixes removed every empty cue, so there are no captions left to burn.");
                    return;
                }

                fixedCaptionPath = Path.Combine(
                    Path.GetTempPath(),
                    $"outrokit-fixed-captions-{Guid.NewGuid():N}{Path.GetExtension(captionPath).ToLowerInvariant()}");
                await File.WriteAllTextAsync(fixedCaptionPath, fixResult.Content);
                captionPath = fixedCaptionPath;

                ConsoleUI.ShowSuccess(
                    $"Applied {fixResult.AppliedFixCount} safe fixes to a temporary copy. The original captions were not changed.");
                if (fixResult.Report.HasIssues)
                {
                    ConsoleUI.ShowWarning(
                        $"{fixResult.Report.ErrorCount} errors and {fixResult.Report.WarningCount} warnings remain and may need manual review.");
                }
                else
                {
                    ConsoleUI.ShowSuccess("All caption quality checks now pass.");
                }
            }

            var style = new CaptionStyle(
                _settings.CaptionSize,
                _settings.CaptionPosition,
                _settings.CaptionAppearance);
            if (CaptionBurnService.HasOwnStyling(captionPath))
            {
                ConsoleUI.ShowInfo("This captions file sets its own fonts and positions, which are kept as they are.");
            }
            else
            {
                var appearance = ConsoleUI.SelectCaptionAppearance(_settings.CaptionAppearance);
                style = new CaptionStyle(
                    ConsoleUI.SelectCaptionSize(_settings.CaptionSize),
                    ConsoleUI.SelectCaptionPosition(_settings.CaptionPosition),
                    appearance);
            }

            var outputPath = ConsoleUI.AskSaveFilePath(
                "Where should the captioned video be saved?",
                CaptionBurnService.GetDefaultOutputPath(videoPath),
                CaptionBurnService.GetOutputExtensions(videoPath),
                path => CaptionBurnService.IsSameFile(path, videoPath)
                    ? "That is the original video. Choose a different name so it is not overwritten."
                    : null);

            ConsoleUI.ShowInfo("Burning captions with ffmpeg. The video is re-encoded, so this can take a while. Press Ctrl+C to cancel.");

            await ConsoleCancellation.RunAsync(cancellationToken => AnsiConsole.Progress()
                .AutoClear(false)
                .HideCompleted(false)
                .Columns(
                    new TaskDescriptionColumn(),
                    new ProgressBarColumn(),
                    new PercentageColumn(),
                    new RemainingTimeColumn(),
                    new SpinnerColumn())
                .StartAsync(async context =>
                {
                    var task = context.AddTask("[blue]Preparing the video...[/]", maxValue: 100);
                    task.IsIndeterminate = true;

                    var progress = new InlineProgress<CaptionBurnProgress>(update =>
                    {
                        if (update.IsDurationKnown)
                        {
                            task.IsIndeterminate = false;
                            task.Value = update.Percentage;
                            task.Description =
                                $"[blue]Burning captions {FormatDuration(update.Position)} / {FormatDuration(update.Duration)}[/]";
                        }
                        else
                        {
                            task.Description = $"[blue]Burning captions {FormatDuration(update.Position)}[/]";
                        }
                    });

                    await burnService.BurnAsync(videoPath, captionPath, outputPath, style, progress, cancellationToken);
                    task.IsIndeterminate = false;
                    task.Value = 100;
                    return true;
                }));

            ConsoleUI.ShowSuccess($"Saved captioned video: {outputPath}");
        }
        catch (OperationCanceledException)
        {
            ConsoleUI.ShowWarning("Caption burn cancelled. No video was saved.");
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Could not burn captions: {ex.Message}");
        }
        finally
        {
            if (fixedCaptionPath is not null)
            {
                try
                {
                    File.Delete(fixedCaptionPath);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    ConsoleUI.ShowWarning($"Could not remove the temporary fixed captions file: {ex.Message}");
                }
            }
        }
    }

    private static CaptionQualityAction ShowCaptionQualityReport(CaptionQualityReport report)
    {
        if (!report.HasIssues)
        {
            ConsoleUI.ShowSuccess($"Caption quality check passed: {report.CueCount} cues meet the basic readability checks.");
            return CaptionQualityAction.Continue;
        }

        var lines = new List<string>
        {
            $"{report.CueCount} cues checked: {report.ErrorCount} errors, {report.WarningCount} warnings.",
            report.FixableCount > 0
                ? $"{report.FixableCount} findings can be fixed safely in a temporary copy."
                : "These findings need manual review.",
            "The original caption file will not be changed."
        };
        lines.AddRange(report.Findings.Take(12).Select(finding =>
            $"Cue {finding.CueNumber}: {finding.Check} - {finding.Message}"));
        if (report.Findings.Count > 12)
        {
            lines.Add($"...and {report.Findings.Count - 12} more findings.");
        }

        ConsoleUI.ShowPanel("Caption quality check", string.Join(Environment.NewLine, lines), Color.Yellow);
        var choices = new List<string>();
        if (report.FixableCount > 0)
        {
            choices.Add($"Fix {report.FixableCount} findings and continue");
        }
        choices.Add("Continue unchanged");
        choices.Add("Cancel burn");
        var action = ConsoleUI.SelectFromList(
            "What would you like to do?",
            choices);
        return action switch
        {
            "Continue unchanged" => CaptionQualityAction.Continue,
            "Cancel burn" => CaptionQualityAction.Cancel,
            _ => CaptionQualityAction.ApplyFixes
        };
    }

    private enum CaptionQualityAction
    {
        Continue,
        ApplyFixes,
        Cancel
    }

    /// <summary>
    /// Checks that the configured ffmpeg can burn captions, offering to point at another one when it cannot.
    /// </summary>
    private async Task<bool> EnsureCaptionBurnSupportedAsync(CaptionBurnService burnService)
    {
        while (true)
        {
            try
            {
                var supported = await AnsiConsole.Status()
                    .Spinner(Spinner.Known.Dots)
                    .SpinnerStyle(Style.Parse("blue"))
                    .StartAsync("Checking ffmpeg...", _ => burnService.SupportsCaptionBurnAsync());
                if (supported)
                {
                    return true;
                }

                ConsoleUI.ShowWarning(
                    $"The ffmpeg at '{_settings.FfmpegPath}' cannot burn captions. It was built without the subtitles filter, which needs libass.");
            }
            catch (FfmpegNotFoundException ex)
            {
                ConsoleUI.ShowWarning(ex.Message);
            }

            var suggestedPath = CaptionBurnService.GetHomebrewFullFfmpegPath();
            if (suggestedPath is not null)
            {
                ConsoleUI.ShowInfo(
                    "Homebrew's standard ffmpeg leaves the subtitles filter out. Install the full build with: brew install ffmpeg-full");
                ConsoleUI.ShowInfo($"It installs beside your current ffmpeg, at {suggestedPath}");
            }
            else
            {
                ConsoleUI.ShowInfo(
                    "Install an ffmpeg build that includes libass (a \"full\" build), then point the app at it.");
            }

            if (!AnsiConsole.Confirm("Set the ffmpeg path now?", defaultValue: suggestedPath is not null && File.Exists(suggestedPath)))
            {
                return false;
            }

            _settings.FfmpegPath = ConsoleUI.AskText(
                "Enter the ffmpeg executable path or command:",
                defaultValue: suggestedPath is not null && File.Exists(suggestedPath) ? suggestedPath : _settings.FfmpegPath).Trim();
            await SaveSettingsAsync();
        }
    }

    private async Task LoadTranscriptAsync(string path)
    {
        try
        {
            await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync("Loading transcript...", async ctx =>
                {
                    _transcript = await _parser.ParseAsync(path);
                });
            
            _result = new GenerationResult(); // Reset results
            
            ConsoleUI.ShowSuccess($"Loaded transcript: {Path.GetFileName(path)}");
            ConsoleUI.ShowTranscriptInfo(_transcript!);
            
            // Prompt for episode context based on settings
            if (_settings.PromptForContextOnLoad)
            {
                if (AnsiConsole.Confirm("Would you like to add episode context (guest names, topics, etc.)?", defaultValue: false))
                {
                    _settings.EpisodeContext = ConsoleUI.AskText(
                        "Enter episode context:",
                        validator: _ => true);
                }
                else
                {
                    _settings.EpisodeContext = null;
                }
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Failed to load transcript: {ex.Message}");
        }
    }
    
    private async Task GenerateAllAsync()
    {
        if (!EnsureTranscriptLoaded()) return;
        
        try
        {
            var titleStyle = ConsoleUI.SelectTitleStyle(_settings.TitleStyle);
            var descriptionLengths = ConsoleUI.SelectDescriptionLengths(_descriptionLengths);
            _descriptionLengths = descriptionLengths;
            
            await EnsureGeneratorInitializedAsync();
            
            // Generate titles
            ConsoleUI.ShowInfo("Generating titles...");
            await GenerateTitlesInternalAsync(titleStyle);
            
            // Select a title for description context
            if (_result.Titles.Count > 0 && string.IsNullOrEmpty(_result.SelectedTitle))
            {
                _result.SelectedTitle = ConsoleUI.SelectTitle(_result.Titles);
            }
            
            // Generate descriptions
            if (descriptionLengths.Count > 0)
            {
                ConsoleUI.ShowInfo("Generating descriptions...");
                await GenerateDescriptionsInternalAsync(descriptionLengths);
            }
            else
            {
                _result.Descriptions.Clear();
            }
            
            // Generate chapters
            ConsoleUI.ShowInfo("Generating chapters...");
            await GenerateChaptersInternalAsync();
            
            // Convert to SRT
            ConsoleUI.ShowInfo("Converting to SRT...");
            ConvertToSrt();
            
            AnsiConsole.WriteLine();
            ConsoleUI.ShowSuccess("All metadata generated successfully!");
            
            // Show summary
            var summaryTable = new Table()
                .RoundedBorder()
                .BorderColor(Color.Green)
                .Title("[bold green]Generation Summary[/]")
                .AddColumn("Item")
                .AddColumn("Status");
            
            summaryTable.AddRow("Titles", $"[green]{_result.Titles.Count} generated[/]");
            summaryTable.AddRow("Descriptions", $"[green]{_result.Descriptions.Count} generated[/]");
            summaryTable.AddRow("Chapters", $"[green]{_result.Chapters.Count} generated[/]");
            summaryTable.AddRow("SRT", string.IsNullOrEmpty(_result.SrtContent) ? "[red]Not generated[/]" : "[green]Ready[/]");
            
            AnsiConsole.Write(summaryTable);
            
            await OfferCopyAsync(CopyScope.All);
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Generation failed: {ex.Message}");
            if (ex.InnerException != null)
            {
                ConsoleUI.ShowError($"  Inner: {ex.InnerException.Message}");
            }
#if DEBUG
            AnsiConsole.WriteException(ex);
#endif
        }
    }
    
    private async Task GenerateTitlesAsync()
    {
        if (!EnsureTranscriptLoaded()) return;
        
        try
        {
            var titleStyle = ConsoleUI.SelectTitleStyle(_settings.TitleStyle);
            
            await EnsureGeneratorInitializedAsync();
            await GenerateTitlesInternalAsync(titleStyle);
            
            // Show titles and allow selection
            _result.SelectedTitle = ConsoleUI.SelectTitle(_result.Titles);
            
            if (_result.SelectedTitle != null)
            {
                ConsoleUI.ShowSuccess($"Selected: {_result.SelectedTitle}");
            }
            
            await OfferCopyAsync(CopyScope.Titles);
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Failed to generate titles: {ex.Message}");
        }
    }
    
    private async Task GenerateTitlesInternalAsync(TitleStyle style)
    {
        _result.Titles = await RunStreamingGenerationAsync(
            $"Titles ({style.GetDisplayName()})",
            onChunk => _generator!.GenerateTitlesAsync(_transcript!, style, onChunk));
        
        // A previously selected title belongs to the old list
        _result.TitleStyle = style;
        _result.SelectedTitle = null;
        
        ConsoleUI.ShowSuccess($"Generated {_result.Titles.Count} title suggestions");
    }
    
    private async Task GenerateDescriptionsAsync()
    {
        if (!EnsureTranscriptLoaded()) return;
        
        try
        {
            var lengths = ConsoleUI.SelectDescriptionLengths(_descriptionLengths);
            _descriptionLengths = lengths;
            
            if (lengths.Count == 0)
            {
                ConsoleUI.ShowWarning("No descriptions selected.");
                return;
            }
            
            await EnsureGeneratorInitializedAsync();
            await GenerateDescriptionsInternalAsync(lengths);
            
            await OfferCopyAsync(CopyScope.Descriptions);
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Failed to generate descriptions: {ex.Message}");
        }
    }
    
    private async Task GenerateDescriptionsInternalAsync(IReadOnlyList<DescriptionLength> lengths)
    {
        // Descriptions from an earlier run would otherwise outlive the lengths the user just unchecked
        _result.Descriptions.Clear();
        
        foreach (var length in lengths)
        {
            _result.Descriptions[length] = await RunStreamingGenerationAsync(
                $"{length} Description",
                onChunk => _generator!.GenerateDescriptionAsync(
                    _transcript!,
                    length,
                    _result.SelectedTitle,
                    onChunk));
            
            ConsoleUI.ShowSuccess($"Generated {length.ToString().ToLower()} description");
        }
    }
    
    private async Task GenerateChaptersAsync()
    {
        if (!EnsureTranscriptLoaded()) return;
        
        try
        {
            await EnsureGeneratorInitializedAsync();
            await GenerateChaptersInternalAsync();
            
            ConsoleUI.ShowChapters(_result.Chapters);
            
            await OfferCopyAsync(CopyScope.Chapters);
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Failed to generate chapters: {ex.Message}");
        }
    }
    
    private async Task GenerateChaptersInternalAsync()
    {
        _result.Chapters = await RunStreamingGenerationAsync(
            "Chapters",
            onChunk => _generator!.GenerateChaptersAsync(_transcript!, onChunk));
        
        ConsoleUI.ShowSuccess($"Generated {_result.Chapters.Count} chapters");
    }
    
    /// <summary>
    /// Runs a generation while streaming the response into a live region, then replaces
    /// that region with the final text written unwrapped so it can be selected and copied
    /// from the terminal without hard line breaks.
    /// </summary>
    private async Task<T> RunStreamingGenerationAsync<T>(
        string label,
        Func<Action<string>, Task<T>> generate)
    {
        var response = new StringBuilder();
        var header = $"Generating {label}";
        var frame = 0;
        
        var generationTask = Task.Run(() => generate(chunk =>
        {
            lock (response)
            {
                response.Append(chunk);
            }
        }));
        
        try
        {
            while (!generationTask.IsCompleted)
            {
                await AnsiConsole.Live(BuildStreamingView(header, "", frame))
                    .AutoClear(true)
                    .Overflow(VerticalOverflow.Crop)
                    .Cropping(VerticalOverflowCropping.Top)
                    .StartAsync(async ctx =>
                    {
                        while (!generationTask.IsCompleted && _pendingPermissions.IsEmpty)
                        {
                            string currentText;
                            lock (response)
                            {
                                currentText = response.ToString();
                            }
                            
                            ctx.UpdateTarget(BuildStreamingView(header, currentText, frame++));
                            await Task.Delay(50);
                        }
                    });
                
                // Prompts cannot run inside a live display, so it is closed while the user decides
                while (_pendingPermissions.TryDequeue(out var pending))
                {
                    pending.Completion.TrySetResult(PromptForPermission(pending));
                }
            }
            
            var result = await generationTask;
            
            string finalText;
            lock (response)
            {
                finalText = response.ToString();
            }
            
            if (!string.IsNullOrWhiteSpace(finalText))
            {
                ConsoleUI.ShowCopyableBlock(label, finalText.Trim());
            }
            
            return result;
        }
        finally
        {
            while (_pendingPermissions.TryDequeue(out var pending))
            {
                pending.Completion.TrySetResult(false);
            }
        }
    }
    
    private async Task OfferCopyAsync(CopyScope scope)
    {
        if (BuildCopyOptions(scope).Count > 0)
        {
            AnsiConsole.WriteLine();
        }
        
        while (true)
        {
            var options = BuildCopyOptions(scope);
            if (options.Count == 0) return;
            
            var choices = options.ToDictionary(o => $"📋 {o.Name}", o => o);
            
            var choice = ConsoleUI.SelectFromList(
                "[bold]Copy to clipboard[/]",
                choices.Keys.Append(CopyDoneChoice));
            
            if (choice == CopyDoneChoice) return;
            
            var (name, text) = choices[choice];
            await ConsoleUI.CopyToClipboardAsync(name, text);
            
            if (options.Count == 1) return;
        }
    }
    
    private List<(string Name, string Text)> BuildCopyOptions(CopyScope scope)
    {
        var options = new List<(string Name, string Text)>();
        
        if (scope.HasFlag(CopyScope.Titles) && _result.Titles.Count > 0)
        {
            if (!string.IsNullOrWhiteSpace(_result.SelectedTitle))
                options.Add(("Selected title", _result.SelectedTitle));
            
            options.Add(("All titles", string.Join(Environment.NewLine, _result.Titles)));
        }
        
        if (scope.HasFlag(CopyScope.Descriptions))
        {
            foreach (var length in Enum.GetValues<DescriptionLength>())
            {
                if (_result.Descriptions.TryGetValue(length, out var description))
                    options.Add(($"{length} description", description));
            }
        }
        
        if (scope.HasFlag(CopyScope.Chapters) && _result.Chapters.Count > 0)
        {
            options.Add(("Chapters (YouTube format)", _srtConverter.FormatChaptersForYouTube(_result.Chapters)));
        }
        
        if (scope == CopyScope.All && options.Count > 1)
        {
            options.Add(("Everything", _outputService.FormatCombinedText(_result)));
        }
        
        return options;
    }
    
    private void ConvertToSrt()
    {
        if (!EnsureTranscriptLoaded()) return;
        
        try
        {
            var result = _srtConverter.ConvertToSrt(_transcript!);
            _result.SrtContent = result.Content;
            _result.SrtValidationErrors = result.Errors;
            
            if (result.Errors.Count > 0)
            {
                ConsoleUI.ShowWarning($"SRT converted with {result.Errors.Count} warnings");
                foreach (var error in result.Errors.Take(5))
                {
                    AnsiConsole.MarkupLine($"  [grey]• {Markup.Escape(error)}[/]");
                }
            }
            else
            {
                ConsoleUI.ShowSuccess("SRT converted successfully");
            }
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Failed to convert to SRT: {ex.Message}");
        }
    }
    
    private async Task ViewResultsMenuAsync()
    {
        while (true)
        {
            var choices = new List<string>();
            
            if (_result.Titles.Count > 0)
                choices.Add("📝 View Titles");
            if (_result.Descriptions.Count > 0)
                choices.Add("📄 View Descriptions");
            if (_result.Chapters.Count > 0)
                choices.Add("📑 View Chapters");
            if (!string.IsNullOrEmpty(_result.SrtContent))
                choices.Add("🎬 View SRT Preview");
            
            choices.Add("⬅️ Back to Main Menu");
            
            var action = ConsoleUI.SelectFromList("[bold]View Results[/]", choices);
            
            switch (action)
            {
                case "📝 View Titles":
                    _result.SelectedTitle = ConsoleUI.SelectTitle(_result.Titles);
                    break;
                    
                case "📄 View Descriptions":
                    ConsoleUI.ShowDescriptions(_result.Descriptions);
                    ConsoleUI.WaitForKey();
                    break;
                    
                case "📑 View Chapters":
                    ConsoleUI.ShowChapters(_result.Chapters);
                    ConsoleUI.WaitForKey();
                    break;
                    
                case "🎬 View SRT Preview":
                    var preview = string.Join("\n", _result.SrtContent!.Split('\n').Take(30));
                    if (_result.SrtContent!.Split('\n').Length > 30)
                        preview += "\n\n[grey]... (truncated)[/]";
                    ConsoleUI.ShowMarkupPanel("SRT Preview", Markup.Escape(preview), Color.Purple);
                    ConsoleUI.WaitForKey();
                    break;
                    
                case "⬅️ Back to Main Menu":
                    return;
            }
        }
    }
    
    private async Task SaveResultsAsync()
    {
        if (_transcript == null)
        {
            ConsoleUI.ShowError("No transcript loaded.");
            return;
        }
        
        var outputDir = ConsoleUI.AskText(
            "Enter output directory:",
            defaultValue: _settings.OutputDirectory,
            validator: path => !string.IsNullOrWhiteSpace(path));
        
        try
        {
            var savedFiles = await AnsiConsole.Status()
                .Spinner(Spinner.Known.Dots)
                .SpinnerStyle(Style.Parse("blue"))
                .StartAsync("Saving results...", async ctx =>
                {
                    return await _outputService.SaveAllAsync(
                        outputDir,
                        _transcript,
                        _result,
                        _settings);
                });
            
            ConsoleUI.ShowSuccess($"Saved {savedFiles.Count} files to: {outputDir}");
            
            var table = new Table()
                .RoundedBorder()
                .BorderColor(Color.Green)
                .AddColumn("Saved Files");
            
            foreach (var file in savedFiles)
            {
                table.AddRow(Markup.Escape(Path.GetFileName(file)));
            }
            
            AnsiConsole.Write(table);
        }
        catch (Exception ex)
        {
            ConsoleUI.ShowError($"Failed to save: {ex.Message}");
        }
    }
    
    private async Task SettingsMenuAsync()
    {
        while (true)
        {
            AnsiConsole.WriteLine();
            
            // Show current settings grouped by category
            var generalTable = new Table()
                .RoundedBorder()
                .BorderColor(Color.Blue)
                .Title("[bold]General Settings[/]")
                .HideHeaders()
                .AddColumn("Setting")
                .AddColumn("Value");
            
            generalTable.AddRow("[blue]Model[/]", Markup.Escape(_settings.Model));
            generalTable.AddRow("[blue]Output Directory[/]", Markup.Escape(_settings.OutputDirectory));
            generalTable.AddRow("[blue]ffmpeg[/]", Markup.Escape(_settings.FfmpegPath));
            var whisperModel = WhisperModelCatalog.Get(_settings.WhisperModel);
            var installedModelPath = _whisperModelService.GetInstalledModelPath(_settings);
            generalTable.AddRow(
                "[blue]Whisper Model[/]",
                installedModelPath is null
                    ? $"{Markup.Escape(whisperModel.DisplayName)} [yellow](not installed)[/]"
                    : $"{Markup.Escape(whisperModel.DisplayName)} [green](initialized)[/]");
            generalTable.AddRow(
                "[blue]Captions[/]",
                $"{Markup.Escape(_settings.CaptionAppearance.GetDisplayName())}, {_settings.CaptionSize.GetDisplayName()}, {_settings.CaptionPosition.GetDisplayName().ToLowerInvariant()} [grey](default, confirmed each time you burn captions)[/]");
            generalTable.AddRow("[blue]Show Name[/]", 
                string.IsNullOrEmpty(_settings.PodcastName) 
                    ? "[grey](not set)[/]" 
                    : Markup.Escape(_settings.PodcastName));
            generalTable.AddRow("[blue]Host Names[/]", 
                string.IsNullOrEmpty(_settings.HostNames) 
                    ? "[grey](not set)[/]" 
                    : Markup.Escape(_settings.HostNames));
            generalTable.AddRow("[blue]Prompt for Context[/]", _settings.PromptForContextOnLoad ? "[green]Yes[/]" : "[grey]No[/]");
            generalTable.AddRow("[blue]Episode Context[/]", 
                string.IsNullOrEmpty(_settings.EpisodeContext) 
                    ? "[grey](not set)[/]" 
                    : Markup.Escape(_settings.EpisodeContext.Length > 40 
                        ? _settings.EpisodeContext[..40] + "..." 
                        : _settings.EpisodeContext));
            
            AnsiConsole.Write(generalTable);
            
            var generationTable = new Table()
                .RoundedBorder()
                .BorderColor(Color.Yellow)
                .Title("[bold]Generation Settings[/]")
                .HideHeaders()
                .AddColumn("Setting")
                .AddColumn("Value");
            
            generationTable.AddRow("[yellow]Title Count[/]", $"{_settings.TitleCount} suggestions");
            generationTable.AddRow("[yellow]Title Max Words[/]", $"{_settings.TitleMaxWords} words");
            generationTable.AddRow("[yellow]Title Style[/]", $"{_settings.TitleStyle.GetDisplayName()} [grey](default, can be changed per generation)[/]");
            generationTable.AddRow("[yellow]Short Description[/]", $"~{_settings.ShortDescriptionWords} words");
            generationTable.AddRow("[yellow]Medium Description[/]", $"~{_settings.MediumDescriptionWords} words");
            generationTable.AddRow("[yellow]Long Description[/]", $"~{_settings.LongDescriptionWords} words");
            generationTable.AddRow("[yellow]Chapter Range[/]", $"{_settings.MinChapters}-{_settings.MaxChapters} chapters");
            generationTable.AddRow("[yellow]Chapters per 30min[/]", $"~{_settings.ChaptersPer30Min}");
            generationTable.AddRow("[yellow]Chapter Title Words[/]", $"max {_settings.ChapterTitleMaxWords} words");
            
            AnsiConsole.Write(generationTable);
            
            var action = ConsoleUI.SelectFromList(
                "[bold]Settings Menu[/]",
                new[] 
                { 
                    "🤖 Change Model", 
                    "🎧 Transcription Settings",
                    "🔥 Caption Settings",
                    "📁 Change Output Directory", 
                    "🎙️ Show Info (Name & Hosts)",
                    "📝 Episode Context",
                    "🔧 Generation Settings (Titles, Descriptions, Chapters)",
                    "💾 Save Settings",
                    "🔄 Reset to Defaults",
                    "⬅️ Back to Main Menu" 
                });
            
            switch (action)
            {
                case "🤖 Change Model":
                    var modelInfos = await AnsiConsole.Status()
                        .Spinner(Spinner.Known.Dots)
                        .SpinnerStyle(Style.Parse("blue"))
                        .StartAsync("Fetching available models from Copilot SDK...", async ctx =>
                        {
                            return await AvailableModels.GetModelsWithMetadataAsync();
                        });

                    if (modelInfos.Count == 0)
                    {
                        ConsoleUI.ShowWarning("No models were returned by the Copilot SDK.");
                        break;
                    }
                    
                    var selectedModel = ConsoleUI.SelectFromList(
                        "Select AI Model (multiplier shows relative cost):",
                        AvailableModels.BuildChoices(modelInfos),
                        choice => Markup.Escape(choice.Label));
                    
                    _settings.Model = selectedModel.Id;
                    ConsoleUI.ShowSuccess($"Model set to: {_settings.Model}");
                    break;
                    
                case "📁 Change Output Directory":
                    _settings.OutputDirectory = ConsoleUI.AskText(
                        "Enter output directory:",
                        defaultValue: _settings.OutputDirectory);
                    ConsoleUI.ShowSuccess($"Output directory set to: {_settings.OutputDirectory}");
                    break;

                case "🎧 Transcription Settings":
                    await EditTranscriptionSettingsAsync();
                    break;
                    
                case "🔥 Caption Settings":
                    await EditCaptionSettingsAsync();
                    break;
                    
                case "🎙️ Show Info (Name & Hosts)":
                    EditPodcastInfo();
                    break;
                    
                case "📝 Episode Context":
                    _settings.PromptForContextOnLoad = AnsiConsole.Confirm(
                        "Prompt for episode context when loading transcripts?",
                        defaultValue: _settings.PromptForContextOnLoad);
                    
                    _settings.EpisodeContext = ConsoleUI.AskText(
                        "Enter default episode context (guest names, topics, etc.):",
                        defaultValue: _settings.EpisodeContext ?? "",
                        validator: _ => true);
                    if (string.IsNullOrWhiteSpace(_settings.EpisodeContext))
                        _settings.EpisodeContext = null;
                    ConsoleUI.ShowSuccess("Episode context settings updated");
                    break;
                    
                case "🔧 Generation Settings (Titles, Descriptions, Chapters)":
                    EditGenerationSettings();
                    break;
                    
                case "💾 Save Settings":
                    await SaveSettingsAsync();
                    ConsoleUI.ShowSuccess($"Settings saved to: {SettingsService.GetDefaultSettingsPath()}");
                    break;
                    
                case "🔄 Reset to Defaults":
                    if (AnsiConsole.Confirm("Reset all settings to defaults?", defaultValue: false))
                    {
                        _settings = new AppSettings();
                        _settings.Model = await AvailableModels.ResolveModelAsync(_settings.Model);
                        ConsoleUI.ShowSuccess("Settings reset to defaults");
                    }
                    break;
                    
                case "⬅️ Back to Main Menu":
                    // Auto-save on exit from settings
                    await SaveSettingsAsync();
                    return;
            }
        }
    }

    private async Task EditTranscriptionSettingsAsync()
    {
        while (true)
        {
            var model = WhisperModelCatalog.Get(_settings.WhisperModel);
            var installedPath = _whisperModelService.GetInstalledModelPath(_settings);
            var action = ConsoleUI.SelectFromList(
                $"[bold]Video and Audio Transcription[/]\nffmpeg: [blue]{Markup.Escape(_settings.FfmpegPath)}[/]\n" +
                $"Model: [blue]{Markup.Escape(model.DisplayName)}[/] ({model.ApproximateSize}) " +
                (installedPath is null ? "[yellow]not installed[/]" : "[green]initialized[/]"),
                new[]
                {
                    "🛠️ Configure ffmpeg",
                    "🧠 Choose Whisper GGML Model",
                    "⬇️ Download and Initialize Selected Model",
                    "⬅️ Back"
                });

            switch (action)
            {
                case "🛠️ Configure ffmpeg":
                    _settings.FfmpegPath = ConsoleUI.AskText(
                        "Enter the ffmpeg executable path or command:",
                        defaultValue: _settings.FfmpegPath);
                    break;

                case "🧠 Choose Whisper GGML Model":
                    var selected = ConsoleUI.SelectFromList(
                        "Choose a GGML model (English variants only transcribe English):",
                        WhisperModelCatalog.All,
                        option => $"{option.DisplayName} - {option.ApproximateSize} - {option.Guidance}");
                    if (!string.Equals(_settings.WhisperModel, selected.Id, StringComparison.Ordinal))
                    {
                        _settings.WhisperModel = selected.Id;
                        _settings.WhisperModelPath = null;
                    }
                    break;

                case "⬇️ Download and Initialize Selected Model":
                    try
                    {
                        var statusPrefix = $"Downloading and initializing {Markup.Escape(model.DisplayName)}";
                        var modelPath = await ConsoleCancellation.RunAsync(cancellationToken => AnsiConsole.Status()
                            .Spinner(Spinner.Known.Dots)
                            .SpinnerStyle(Style.Parse("blue"))
                            .StartAsync(
                                $"{statusPrefix} ({model.ApproximateSize})... Ctrl+C to cancel",
                                context =>
                                {
                                    var downloadProgress = new InlineProgress<long>(bytes =>
                                        context.Status =
                                            $"{statusPrefix}: {FormatByteSize(bytes)} of ~{model.ApproximateSize}. Ctrl+C to cancel");
                                    return _whisperModelService.DownloadAndInitializeAsync(
                                        _settings,
                                        downloadProgress,
                                        cancellationToken);
                                }));
                        await SaveSettingsAsync();
                        ConsoleUI.ShowSuccess($"Whisper model initialized: {modelPath}");
                    }
                    catch (OperationCanceledException)
                    {
                        ConsoleUI.ShowWarning("Model download cancelled.");
                    }
                    catch (Exception ex)
                    {
                        ConsoleUI.ShowError($"Could not install the Whisper model: {ex.Message}");
                    }
                    break;

                case "⬅️ Back":
                    await SaveSettingsAsync();
                    return;
            }
        }
    }

    private async Task EditCaptionSettingsAsync()
    {
        while (true)
        {
            var action = ConsoleUI.SelectFromList(
                "[bold]Burning Captions into Video[/]\n" +
                $"Appearance: [blue]{Markup.Escape(_settings.CaptionAppearance.GetDisplayName())}[/]\n" +
                $"Text size: [blue]{Markup.Escape(_settings.CaptionSize.GetDisplayName())}[/]\n" +
                $"Position: [blue]{Markup.Escape(_settings.CaptionPosition.GetDisplayName())}[/]\n" +
                $"ffmpeg: [blue]{Markup.Escape(_settings.FfmpegPath)}[/]\n" +
                "[grey]You confirm appearance, size, and position each time you burn captions.[/]\n" +
                $"[grey]To convert or rework a captions file, try CaptionStack: {CaptionBurnService.CaptionToolsUrl}[/]",
                new[]
                {
                    "🎨 Default Appearance",
                    "🔠 Default Text Size",
                    "↕️ Default Position",
                    "🛠️ Configure ffmpeg",
                    "⬅️ Back"
                });

            switch (action)
            {
                case "🎨 Default Appearance":
                    _settings.CaptionAppearance = ConsoleUI.SelectCaptionAppearance(
                        _settings.CaptionAppearance,
                        "Default caption appearance:");
                    break;

                case "🔠 Default Text Size":
                    _settings.CaptionSize = ConsoleUI.SelectCaptionSize(
                        _settings.CaptionSize,
                        "Default caption text size:");
                    break;

                case "↕️ Default Position":
                    _settings.CaptionPosition = ConsoleUI.SelectCaptionPosition(
                        _settings.CaptionPosition,
                        "Default caption position:");
                    break;

                case "🛠️ Configure ffmpeg":
                    _settings.FfmpegPath = ConsoleUI.AskText(
                        "Enter the ffmpeg executable path or command:",
                        defaultValue: _settings.FfmpegPath);
                    break;

                case "⬅️ Back":
                    await SaveSettingsAsync();
                    return;
            }
        }
    }

    private static string FormatDuration(TimeSpan duration)
    {
        return duration.TotalHours >= 1
            ? duration.ToString(@"h\:mm\:ss")
            : duration.ToString(@"m\:ss");
    }

    private static string FormatByteSize(long bytes)
    {
        const double mebibyte = 1024 * 1024;
        const double gibibyte = mebibyte * 1024;
        return bytes >= gibibyte
            ? $"{bytes / gibibyte:0.0} GiB"
            : $"{bytes / mebibyte:0} MiB";
    }

    private sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
    
    private void EditPodcastInfo()
    {
        _settings.PodcastName = ConsoleUI.AskText(
            "Enter the name of your show (used in prompts for context):",
            defaultValue: _settings.PodcastName ?? "",
            validator: _ => true);
        if (string.IsNullOrWhiteSpace(_settings.PodcastName))
            _settings.PodcastName = null;
        
        _settings.HostNames = ConsoleUI.AskText(
            "Enter host names (comma-separated, used in prompts):",
            defaultValue: _settings.HostNames ?? "",
            validator: _ => true);
        if (string.IsNullOrWhiteSpace(_settings.HostNames))
            _settings.HostNames = null;
        
        ConsoleUI.ShowSuccess("Show info updated");
    }
    
    private void EditGenerationSettings()
    {
        while (true)
        {
            var action = ConsoleUI.SelectFromList(
                "[bold]Generation Settings[/]",
                new[]
                {
                    "📝 Title Settings",
                    "📄 Description Lengths",
                    "📑 Chapter Settings",
                    "⬅️ Back"
                });
            
            switch (action)
            {
                case "📝 Title Settings":
                    _settings.TitleCount = AnsiConsole.Prompt(
                        new TextPrompt<int>("Number of title suggestions to generate:")
                            .DefaultValue(_settings.TitleCount)
                            .Validate(n => n is >= 1 and <= 20 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 1 and 20")));
                    
                    _settings.TitleMaxWords = AnsiConsole.Prompt(
                        new TextPrompt<int>("Maximum words per title:")
                            .DefaultValue(_settings.TitleMaxWords)
                            .Validate(n => n is >= 3 and <= 25 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 3 and 25")));
                    
                    _settings.TitleStyle = ConsoleUI.SelectTitleStyle(
                        _settings.TitleStyle,
                        "Default title style (you can still pick a different one each time you generate):");
                    
                    ConsoleUI.ShowSuccess("Title settings updated");
                    break;
                    
                case "📄 Description Lengths":
                    _settings.ShortDescriptionWords = AnsiConsole.Prompt(
                        new TextPrompt<int>("Short description word count:")
                            .DefaultValue(_settings.ShortDescriptionWords)
                            .Validate(n => n is >= 20 and <= 100 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 20 and 100")));
                    
                    _settings.MediumDescriptionWords = AnsiConsole.Prompt(
                        new TextPrompt<int>("Medium description word count:")
                            .DefaultValue(_settings.MediumDescriptionWords)
                            .Validate(n => n is >= 50 and <= 300 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 50 and 300")));
                    
                    _settings.LongDescriptionWords = AnsiConsole.Prompt(
                        new TextPrompt<int>("Long description word count:")
                            .DefaultValue(_settings.LongDescriptionWords)
                            .Validate(n => n is >= 100 and <= 1000 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 100 and 1000")));
                    
                    ConsoleUI.ShowSuccess("Description lengths updated");
                    break;
                    
                case "📑 Chapter Settings":
                    _settings.MinChapters = AnsiConsole.Prompt(
                        new TextPrompt<int>("Minimum number of chapters:")
                            .DefaultValue(_settings.MinChapters)
                            .Validate(n => n is >= 1 and <= 10 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 1 and 10")));
                    
                    _settings.MaxChapters = AnsiConsole.Prompt(
                        new TextPrompt<int>("Maximum number of chapters:")
                            .DefaultValue(_settings.MaxChapters)
                            .Validate(n => n >= _settings.MinChapters && n <= 50 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error($"Must be between {_settings.MinChapters} and 50")));
                    
                    _settings.ChaptersPer30Min = AnsiConsole.Prompt(
                        new TextPrompt<int>("Target chapters per 30 minutes:")
                            .DefaultValue(_settings.ChaptersPer30Min)
                            .Validate(n => n is >= 1 and <= 15 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 1 and 15")));
                    
                    _settings.ChapterTitleMaxWords = AnsiConsole.Prompt(
                        new TextPrompt<int>("Maximum words per chapter title:")
                            .DefaultValue(_settings.ChapterTitleMaxWords)
                            .Validate(n => n is >= 2 and <= 15 
                                ? ValidationResult.Success() 
                                : ValidationResult.Error("Must be between 2 and 15")));
                    
                    ConsoleUI.ShowSuccess("Chapter settings updated");
                    break;
                    
                case "⬅️ Back":
                    return;
            }
        }
    }
    
    private bool EnsureTranscriptLoaded()
    {
        if (_transcript == null)
        {
            ConsoleUI.ShowError("Please load a transcript first.");
            return false;
        }
        return true;
    }
    
    private async Task EnsureGeneratorInitializedAsync()
    {
        if (_generator == null)
        {
            _generator = new MetadataGenerator(_settings, RequestPermissionAsync);
        }
        
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .SpinnerStyle(Style.Parse("blue"))
            .StartAsync("Initializing Copilot...", async ctx =>
            {
                await _generator.InitializeAsync();
            });
    }

    /// <summary>
    /// Called by the SDK on a background thread. The request is queued and answered by the
    /// streaming loop, which owns the console and can pause its live display to prompt.
    /// </summary>
    private Task<bool> RequestPermissionAsync(PermissionRequest request, PermissionInvocation invocation)
    {
        var completion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        _pendingPermissions.Enqueue(new PendingPermission(request, invocation, completion));
        return completion.Task;
    }

    private static bool PromptForPermission(PendingPermission pending)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Panel(new Text(MetadataGenerator.DescribePermissionRequest(pending.Request, pending.Invocation)))
        {
            Header = new PanelHeader("[yellow]Copilot Permission Request[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        });

        return AnsiConsole.Confirm("Approve this request?", defaultValue: false);
    }
    
    private async Task CleanupAsync()
    {
        if (_generator != null)
        {
            await _generator.DisposeAsync();
        }
    }
}
