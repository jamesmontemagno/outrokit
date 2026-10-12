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

        return FfmpegCommand.RunAsync(
            settings.FfmpegPath,
            [
                "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-i", videoPath,
                "-vn", "-map", "0:a:0",
                outputPath
            ],
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

        return FfmpegCommand.RunAsync(
            settings.FfmpegPath,
            [
                "-nostdin", "-hide_banner", "-loglevel", "error", "-y",
                "-i", videoPath,
                "-i", audioPath,
                "-map", "0:v:0",
                "-map", "1:a:0",
                "-c:v", "copy",
                "-c:a", "aac",
                "-movflags", "+faststart",
                outputPath
            ],
            cancellationToken);
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
        if (string.Equals(
            Path.GetFullPath(inputPath),
            Path.GetFullPath(outputPath),
            StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("The output path cannot overwrite an input file.", nameof(outputPath));
        }
    }
}
