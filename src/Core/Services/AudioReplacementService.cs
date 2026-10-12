using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Extracts a video's audio and replaces it without re-encoding the video stream.
/// </summary>
public sealed class AudioReplacementService(AppSettings settings)
{
    public Task ExtractAudioAsync(
        string videoPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        EnsureInputExists(videoPath, "video");
        EnsureDifferentPaths(videoPath, outputPath);

        return RunToOutputAsync(
            settings.FfmpegPath,
            [
                "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-i", videoPath,
                "-vn", "-map", "0:a:0"
            ],
            outputPath,
            cancellationToken);
    }

    public Task ReplaceAudioAsync(
        string videoPath,
        string audioPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        EnsureInputExists(videoPath, "video");
        EnsureInputExists(audioPath, "replacement audio");
        if (!string.Equals(Path.GetExtension(outputPath), ".mp4", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The replacement video output must be an MP4 file.", nameof(outputPath));
        }

        EnsureDifferentPaths(videoPath, outputPath);
        EnsureDifferentPaths(audioPath, outputPath);

        return RunToOutputAsync(
            settings.FfmpegPath,
            [
                "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-i", videoPath,
                "-i", audioPath,
                "-filter_complex", "[1:a:0]apad[replacement]",
                "-map", "0:v:0",
                "-map", "[replacement]",
                "-c:v", "copy",
                "-c:a", "aac",
                "-movflags", "+faststart",
                "-shortest"
            ],
            outputPath,
            cancellationToken);
    }

    private static async Task RunToOutputAsync(
        string ffmpegPath,
        IReadOnlyList<string> arguments,
        string outputPath,
        CancellationToken cancellationToken)
    {
        var fullOutputPath = Path.GetFullPath(outputPath);
        var directory = Path.GetDirectoryName(fullOutputPath)
            ?? throw new ArgumentException("The output path must include a directory.", nameof(outputPath));
        var extension = Path.GetExtension(fullOutputPath);
        var partialPath = Path.Combine(
            directory,
            $".{Path.GetFileNameWithoutExtension(fullOutputPath)}.{Guid.NewGuid():N}.partial{extension}");
        var argumentsWithPartialPath = new List<string>(arguments) { partialPath };
        Exception? operationException = null;
        try
        {
            await FfmpegCommand.RunAsync(ffmpegPath, argumentsWithPartialPath, cancellationToken);
            if (!File.Exists(partialPath) || new FileInfo(partialPath).Length == 0)
            {
                throw new InvalidOperationException("ffmpeg finished without writing the output file.");
            }

            File.Move(partialPath, fullOutputPath, overwrite: true);
        }
        catch (Exception ex)
        {
            operationException = ex;
            throw;
        }
        finally
        {
            await TemporaryFileCleanup.DeleteAsync(partialPath, operationException);
        }
    }

    private static void EnsureInputExists(string path, string description)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException($"The {description} file was not found.", path);
        }
    }

    private static void EnsureDifferentPaths(string inputPath, string outputPath)
    {
        if (CaptionBurnService.IsSameFile(inputPath, outputPath))
        {
            throw new ArgumentException("The output path cannot overwrite an input file.", nameof(outputPath));
        }
    }
}
