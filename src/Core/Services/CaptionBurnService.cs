using System.Globalization;
using System.Text.RegularExpressions;
using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Burns captions into the picture of a video with ffmpeg's subtitles filter.
/// </summary>
public partial class CaptionBurnService
{
    private const string SubtitlesFilter = "subtitles";

    private static readonly HashSet<string> CaptionExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".srt", ".vtt", ".ass", ".ssa"
    };

    // Formats that carry their own fonts, sizes, and positions.
    private static readonly HashSet<string> StyledCaptionExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".ass", ".ssa"
    };

    private static readonly string[] OutputContainers = [".mp4", ".mov", ".mkv", ".m4v"];

    [GeneratedRegex(@"Duration:\s*(\d+):(\d{2}):(\d{2}(?:\.\d+)?)")]
    private static partial Regex DurationRegex();

    [GeneratedRegex(@"Video:.*?,\s*(\d{2,5})x(\d{2,5})[\s,\[]")]
    private static partial Regex FrameSizeRegex();

    private readonly AppSettings _settings;

    public CaptionBurnService(AppSettings settings)
    {
        _settings = settings;
    }

    /// <summary>
    /// Whether the path has a caption format this service can burn.
    /// </summary>
    public static bool HasCaptionExtension(string path) => CaptionExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Whether the caption file defines its own look, in which case no <see cref="CaptionStyle"/> is applied.
    /// </summary>
    public static bool HasOwnStyling(string captionPath) =>
        StyledCaptionExtensions.Contains(Path.GetExtension(captionPath));

    /// <summary>
    /// The extensions a captioned video can be saved with, the best match for the source first.
    /// </summary>
    public static IReadOnlyList<string> GetOutputExtensions(string videoPath)
    {
        var preferred = GetPreferredOutputExtension(videoPath);
        return [preferred, .. OutputContainers.Where(extension => extension != preferred)];
    }

    /// <summary>
    /// Suggests a path next to the source video for the captioned copy.
    /// </summary>
    public static string GetDefaultOutputPath(string videoPath)
    {
        var fullPath = Path.GetFullPath(videoPath);
        var directory = Path.GetDirectoryName(fullPath) ?? Environment.CurrentDirectory;
        return Path.Combine(
            directory,
            $"{Path.GetFileNameWithoutExtension(fullPath)}-captioned{GetPreferredOutputExtension(fullPath)}");
    }

    /// <summary>
    /// Whether two paths name the same file, so a burn would overwrite its own input.
    /// </summary>
    public static bool IsSameFile(string firstPath, string secondPath) => string.Equals(
        Path.GetFullPath(firstPath),
        Path.GetFullPath(secondPath),
        StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Whether the configured ffmpeg was built with the subtitles filter, which needs libass.
    /// </summary>
    /// <exception cref="FfmpegNotFoundException">ffmpeg could not be launched.</exception>
    public async Task<bool> SupportsCaptionBurnAsync(CancellationToken cancellationToken = default)
    {
        // ffmpeg exits 0 whether or not it knows the filter, so the answer is in what it prints.
        var result = await RunFfmpegAsync(
            ["-nostdin", "-hide_banner", "-h", $"filter={SubtitlesFilter}"],
            cancellationToken);
        return result.ExitCode == 0
            && result.Output.TrimStart().StartsWith($"Filter {SubtitlesFilter}", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Confirms the file has a video stream ffmpeg can decode and reads what a burn needs to know about it.
    /// </summary>
    public async Task<VideoDetails> GetVideoDetailsAsync(string videoPath, CancellationToken cancellationToken = default)
    {
        EnsureFileExists(videoPath, "video");

        // Decoding one frame proves the stream is usable; the log carries the duration and the
        // frame size as ffmpeg will hand it to the filter, after any rotation is applied.
        var result = await RunFfmpegAsync(
            ["-nostdin", "-hide_banner", "-i", videoPath, "-map", "0:v:0", "-frames:v", "1", "-f", "null", "-"],
            cancellationToken);
        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException("The selected file does not contain a video ffmpeg can read.");
        }

        return ParseVideoDetails(result.Error);
    }

    /// <summary>
    /// Counts the captions ffmpeg can read from the file.
    /// </summary>
    public async Task<int> CountCaptionsAsync(string captionPath, CancellationToken cancellationToken = default)
    {
        EnsureFileExists(captionPath, "captions");

        var count = 0;
        var result = await FfmpegRunner.RunAsync(
            _settings.FfmpegPath,
            ["-nostdin", "-hide_banner", "-loglevel", "error", "-i", captionPath, "-map", "0:s:0", "-f", "srt", "-"],
            cancellationToken,
            onOutputLine: line =>
            {
                if (line.Contains("-->", StringComparison.Ordinal))
                {
                    count++;
                }
            });

        return result.ExitCode == 0 ? count : 0;
    }

    /// <summary>
    /// Writes a copy of the video with the captions drawn into the picture.
    /// </summary>
    /// <param name="style">Applied to plain caption formats; ignored when <see cref="HasOwnStyling"/>.</param>
    public async Task BurnAsync(
        string videoPath,
        string captionPath,
        string outputPath,
        CaptionStyle style,
        IProgress<CaptionBurnProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureFileExists(videoPath, "video");
        EnsureFileExists(captionPath, "captions");
        if (!HasCaptionExtension(captionPath))
        {
            throw new InvalidOperationException(
                "Captions must be an .srt, .vtt, .ass, or .ssa file.");
        }

        videoPath = Path.GetFullPath(videoPath);
        outputPath = Path.GetFullPath(outputPath);
        if (IsSameFile(videoPath, outputPath))
        {
            throw new InvalidOperationException("The captioned video cannot be saved over the original video.");
        }

        var outputExtension = Path.GetExtension(outputPath);
        if (!OutputContainers.Contains(outputExtension, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"The captioned video must be saved as {string.Join(", ", OutputContainers)}.");
        }

        var outputDirectory = Path.GetDirectoryName(outputPath);
        if (string.IsNullOrEmpty(outputDirectory) || !Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {outputDirectory}");
        }

        if (!await SupportsCaptionBurnAsync(cancellationToken))
        {
            throw new InvalidOperationException(
                $"The ffmpeg at '{_settings.FfmpegPath}' was built without the subtitles filter (libass).");
        }

        var video = await GetVideoDetailsAsync(videoPath, cancellationToken);
        if (await CountCaptionsAsync(captionPath, cancellationToken) == 0)
        {
            throw new InvalidOperationException("No captions could be read from the captions file.");
        }

        // ffmpeg reads the captions by a fixed name from its working directory, so the user's path
        // never has to survive filter-graph escaping (colons, quotes, brackets, drive letters).
        var workingDirectory = Path.Combine(Path.GetTempPath(), $"podcast-metadata-captions-{Guid.NewGuid():N}");
        var captionFileName = $"captions{Path.GetExtension(captionPath).ToLowerInvariant()}";
        // Encoding to a sibling file keeps a cancelled or failed burn from leaving a broken video
        // under the final name, or destroying a file the user agreed to replace.
        var partialPath = Path.Combine(
            outputDirectory,
            $".{Path.GetFileNameWithoutExtension(outputPath)}.{Guid.NewGuid():N}.partial{outputExtension}");
        Exception? burnException = null;
        try
        {
            Directory.CreateDirectory(workingDirectory);
            File.Copy(captionPath, Path.Combine(workingDirectory, captionFileName));

            progress?.Report(new CaptionBurnProgress(TimeSpan.Zero, video.Duration));
            var result = await FfmpegRunner.RunAsync(
                _settings.FfmpegPath,
                BuildBurnArguments(videoPath, captionFileName, partialPath, outputExtension, style, video),
                cancellationToken,
                workingDirectory,
                line =>
                {
                    if (TryParseProgress(line, out var position))
                    {
                        progress?.Report(new CaptionBurnProgress(
                            video.Duration > TimeSpan.Zero && position > video.Duration ? video.Duration : position,
                            video.Duration));
                    }
                });

            if (result.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg could not burn the captions: {result.Error}");
            }

            if (!File.Exists(partialPath) || new FileInfo(partialPath).Length == 0)
            {
                throw new InvalidOperationException("ffmpeg finished without writing the captioned video.");
            }

            File.Move(partialPath, outputPath, overwrite: true);
            progress?.Report(new CaptionBurnProgress(video.Duration, video.Duration));
        }
        catch (Exception ex)
        {
            burnException = ex;
            throw;
        }
        finally
        {
            TemporaryFileCleanup.Delete(partialPath, burnException);
            TemporaryFileCleanup.DeleteDirectory(workingDirectory, burnException);
        }
    }

    private static List<string> BuildBurnArguments(
        string videoPath,
        string captionFileName,
        string outputPath,
        string outputExtension,
        CaptionStyle style,
        VideoDetails video)
    {
        var filter = $"{SubtitlesFilter}={captionFileName}";
        if (!HasOwnStyling(captionFileName))
        {
            filter += $":force_style='{BuildForceStyle(style, video)}'";
        }

        List<string> arguments =
        [
            "-nostdin", "-hide_banner", "-loglevel", "error", "-nostats", "-progress", "pipe:1", "-y",
            "-i", videoPath,
            "-vf", filter,
            "-map", "0:v:0", "-map", "0:a?",
            "-c:v", "libx264", "-crf", "18", "-preset", "medium", "-pix_fmt", "yuv420p"
        ];

        // Audio is untouched when the container stays the same; a different container may not hold it.
        if (string.Equals(Path.GetExtension(videoPath), outputExtension, StringComparison.OrdinalIgnoreCase))
        {
            arguments.AddRange(["-c:a", "copy"]);
        }
        else
        {
            arguments.AddRange(["-c:a", "aac", "-b:a", "192k"]);
        }

        if (!string.Equals(outputExtension, ".mkv", StringComparison.OrdinalIgnoreCase))
        {
            arguments.AddRange(["-movflags", "+faststart"]);
        }

        arguments.Add(outputPath);
        return arguments;
    }

    private static string BuildForceStyle(CaptionStyle style, VideoDetails video)
    {
        // libass sizes text against a 288-unit-tall canvas, so a value is a share of the frame height
        // at any resolution. Scaling by the shorter side keeps portrait video from oversized text.
        var baseSize = style.Size switch
        {
            CaptionSize.Small => 14,
            CaptionSize.Large => 24,
            _ => 18
        };
        var fontSize = Math.Max(8, Math.Round(baseSize * video.ShortSideRatio, 1));

        // force_style takes libass's internal alignment values, not the numpad layout used inside
        // .ass files: 2 is bottom center and 6 is top center.
        var alignment = style.Position == CaptionPosition.Top ? 6 : 2;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"FontSize={fontSize},Alignment={alignment},MarginV=12,PrimaryColour=&H00FFFFFF,OutlineColour=&H00000000,BorderStyle=1,Outline=1.2,Shadow=0");
    }

    private static VideoDetails ParseVideoDetails(string ffmpegLog)
    {
        var duration = TimeSpan.Zero;
        var durationMatch = DurationRegex().Match(ffmpegLog);
        if (durationMatch.Success)
        {
            duration = new TimeSpan(
                    int.Parse(durationMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                    int.Parse(durationMatch.Groups[2].Value, CultureInfo.InvariantCulture),
                    0)
                + TimeSpan.FromSeconds(double.Parse(durationMatch.Groups[3].Value, CultureInfo.InvariantCulture));
        }

        // The output section describes frames as the filter will receive them.
        var outputIndex = ffmpegLog.IndexOf("Output #0", StringComparison.Ordinal);
        var sizeMatch = outputIndex < 0 ? Match.Empty : FrameSizeRegex().Match(ffmpegLog, outputIndex);
        return sizeMatch.Success
            ? new VideoDetails(
                duration,
                int.Parse(sizeMatch.Groups[1].Value, CultureInfo.InvariantCulture),
                int.Parse(sizeMatch.Groups[2].Value, CultureInfo.InvariantCulture))
            : new VideoDetails(duration, 0, 0);
    }

    private static bool TryParseProgress(string line, out TimeSpan position)
    {
        const string key = "out_time_us=";
        if (line.StartsWith(key, StringComparison.Ordinal)
            && long.TryParse(line.AsSpan(key.Length), NumberStyles.Integer, CultureInfo.InvariantCulture, out var microseconds)
            && microseconds >= 0)
        {
            position = TimeSpan.FromMicroseconds(microseconds);
            return true;
        }

        position = default;
        return false;
    }

    private static string GetPreferredOutputExtension(string videoPath)
    {
        var extension = Path.GetExtension(videoPath).ToLowerInvariant();
        return OutputContainers.Contains(extension) ? extension : OutputContainers[0];
    }

    private Task<FfmpegResult> RunFfmpegAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken) =>
        FfmpegRunner.RunAsync(_settings.FfmpegPath, arguments, cancellationToken);

    private static void EnsureFileExists(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The selected {description} file was not found.", path);
        }
    }
}

/// <summary>
/// What a caption burn needs to know about the source video. Width and height are 0 when unknown.
/// </summary>
public sealed record VideoDetails(TimeSpan Duration, int Width, int Height)
{
    /// <summary>
    /// The shorter side as a share of the height: 1 for landscape, less than 1 for portrait.
    /// </summary>
    public double ShortSideRatio => Width > 0 && Height > Width ? Width / (double)Height : 1;
}

/// <summary>
/// How far a caption burn has got. <see cref="Duration"/> is zero when the video's length is unknown.
/// </summary>
public sealed record CaptionBurnProgress(TimeSpan Position, TimeSpan Duration)
{
    public bool IsDurationKnown => Duration > TimeSpan.Zero;

    public double Percentage => IsDurationKnown
        ? Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds * 100, 0, 100)
        : 0;
}
