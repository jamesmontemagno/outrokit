namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Public wrapper for ffmpeg command execution and validation.
/// </summary>
public static class FfmpegCommand
{
    public static Task ValidateAsync(string ffmpegPath, CancellationToken cancellationToken = default)
        => RunAsync(ffmpegPath, ["-nostdin", "-hide_banner", "-version"], cancellationToken);

    public static async Task RunAsync(
        string ffmpegPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        Action<string>? onOutputLine = null)
    {
        var result = await FfmpegRunner.RunAsync(
            ffmpegPath,
            arguments,
            cancellationToken,
            workingDirectory,
            onOutputLine);
        if (result.ExitCode != 0)
        {
            var details = string.IsNullOrWhiteSpace(result.Error)
                ? result.Output.Trim()
                : result.Error;
            throw new InvalidOperationException(
                string.IsNullOrWhiteSpace(details)
                    ? $"ffmpeg exited with code {result.ExitCode}."
                    : $"ffmpeg exited with code {result.ExitCode}: {details}");
        }
    }
}
