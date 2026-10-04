using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Launches ffmpeg and collects its result for the services that depend on it.
/// </summary>
internal static class FfmpegRunner
{
    /// <summary>
    /// Runs ffmpeg to completion. A cancelled token stops the process and throws.
    /// </summary>
    /// <param name="onOutputLine">Receives each standard output line as it is written.</param>
    public static async Task<FfmpegResult> RunAsync(
        string ffmpegPath,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken,
        string? workingDirectory = null,
        Action<string>? onOutputLine = null)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        if (workingDirectory is not null)
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start ffmpeg at '{ffmpegPath}'.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new FfmpegNotFoundException(
                $"ffmpeg was not found at '{ffmpegPath}'. Configure it in Settings.",
                ex);
        }

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var outputTask = ReadOutputAsync(process.StandardOutput, onOutputLine, cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            var output = await outputTask;
            var error = (await errorTask).Trim();
            // Ctrl+C in a terminal also reaches ffmpeg, so it can exit with an error before the
            // token is observed. Report that as the cancellation it is, not as an ffmpeg failure.
            cancellationToken.ThrowIfCancellationRequested();
            return new FfmpegResult(process.ExitCode, error, output);
        }
        catch (OperationCanceledException)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }

            throw;
        }
    }

    private static async Task<string> ReadOutputAsync(
        StreamReader reader,
        Action<string>? onOutputLine,
        CancellationToken cancellationToken)
    {
        if (onOutputLine is null)
        {
            return await reader.ReadToEndAsync(cancellationToken);
        }

        var output = new StringBuilder();
        while (await reader.ReadLineAsync(cancellationToken) is { } line)
        {
            output.AppendLine(line);
            onOutputLine(line);
        }

        return output.ToString();
    }
}

internal sealed record FfmpegResult(int ExitCode, string Error, string Output);

/// <summary>
/// The configured ffmpeg executable could not be launched.
/// </summary>
public sealed class FfmpegNotFoundException(string message, Exception innerException)
    : InvalidOperationException(message, innerException);
