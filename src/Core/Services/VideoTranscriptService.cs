using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using PodcastMetadataGenerator.Core.Models;
using Whisper.net;

namespace PodcastMetadataGenerator.Core.Services;

public class VideoTranscriptService
{
    private readonly AppSettings _settings;
    private readonly WhisperModelService _modelService;

    public VideoTranscriptService(AppSettings settings, WhisperModelService? modelService = null)
    {
        _settings = settings;
        _modelService = modelService ?? new WhisperModelService();
    }

    public async Task<bool> IsVideoFileAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureInputExists(path);

        var result = await RunFfmpegAsync(
            ["-hide_banner", "-loglevel", "error", "-i", path, "-map", "0:v:0", "-frames:v", "1", "-f", "null", "-"],
            cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<string> TranscribeToSrtAsync(
        string videoPath,
        IProgress<VideoTranscriptionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInputExists(videoPath);
        var modelPath = _modelService.GetInstalledModelPath(_settings)
            ?? throw new InvalidOperationException(
                "No initialized Whisper model is available. Install one from Settings before transcribing video.");

        var temporaryWavPath = Path.Combine(Path.GetTempPath(), $"podcast-metadata-{Guid.NewGuid():N}.wav");
        Exception? transcriptionException = null;
        try
        {
            var extraction = await RunFfmpegAsync(
                [
                    "-hide_banner", "-loglevel", "error", "-y", "-i", videoPath,
                    "-vn", "-acodec", "pcm_s16le", "-ar", "16000", "-ac", "1", temporaryWavPath
                ],
                cancellationToken);

            if (extraction.ExitCode != 0)
            {
                throw new InvalidOperationException($"ffmpeg could not extract audio from the video: {extraction.Error}");
            }

            using var factory = WhisperFactory.FromPath(modelPath);
            using var processor = factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();
            await using var audioStream = File.OpenRead(temporaryWavPath);
            var audioDuration = GetWaveDuration(audioStream);
            progress?.Report(new VideoTranscriptionProgress(TimeSpan.Zero, audioDuration));
            
            var srt = new StringBuilder();
            var segmentNumber = 1;
            await foreach (var segment in processor.ProcessAsync(audioStream, cancellationToken))
            {
                progress?.Report(new VideoTranscriptionProgress(segment.End, audioDuration));
                var text = segment.Text.Trim();
                if (text.Length == 0)
                {
                    continue;
                }

                srt.AppendLine(segmentNumber.ToString(CultureInfo.InvariantCulture));
                srt.Append(FormatSrtTimestamp(segment.Start));
                srt.Append(" --> ");
                srt.AppendLine(FormatSrtTimestamp(segment.End));
                srt.AppendLine(text);
                srt.AppendLine();
                segmentNumber++;
            }

            if (segmentNumber == 1)
            {
                throw new InvalidOperationException("Whisper did not detect any speech in the video's audio.");
            }

            progress?.Report(new VideoTranscriptionProgress(audioDuration, audioDuration));
            return srt.ToString();
        }
        catch (Exception ex)
        {
            transcriptionException = ex;
            throw;
        }
        finally
        {
            TemporaryFileCleanup.Delete(temporaryWavPath, transcriptionException);
        }
    }

    private async Task<FfmpegResult> RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = _settings.FfmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
            {
                throw new InvalidOperationException($"Could not start ffmpeg at '{_settings.FfmpegPath}'.");
            }
        }
        catch (Win32Exception ex)
        {
            throw new InvalidOperationException(
                $"ffmpeg was not found at '{_settings.FfmpegPath}'. Configure it in Settings.",
                ex);
        }

        var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        try
        {
            await process.WaitForExitAsync(cancellationToken);
            await outputTask;
            return new FfmpegResult(process.ExitCode, (await errorTask).Trim());
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

    private static string FormatSrtTimestamp(TimeSpan timestamp)
    {
        var totalHours = (long)timestamp.TotalHours;
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{totalHours:00}:{timestamp.Minutes:00}:{timestamp.Seconds:00},{timestamp.Milliseconds:000}");
    }

    private static TimeSpan GetWaveDuration(Stream waveStream)
    {
        using var reader = new BinaryReader(waveStream, Encoding.ASCII, leaveOpen: true);
        waveStream.Position = 0;
        if (new string(reader.ReadChars(4)) != "RIFF")
        {
            throw new InvalidDataException("ffmpeg produced an invalid WAV file.");
        }

        reader.ReadUInt32();
        if (new string(reader.ReadChars(4)) != "WAVE")
        {
            throw new InvalidDataException("ffmpeg produced an invalid WAV file.");
        }

        uint byteRate = 0;
        uint dataSize = 0;
        while (waveStream.Position + 8 <= waveStream.Length)
        {
            var chunkId = new string(reader.ReadChars(4));
            var chunkSize = reader.ReadUInt32();
            var chunkStart = waveStream.Position;

            if (chunkId == "fmt " && chunkSize >= 12)
            {
                reader.ReadUInt16();
                reader.ReadUInt16();
                reader.ReadUInt32();
                byteRate = reader.ReadUInt32();
            }
            else if (chunkId == "data")
            {
                dataSize = chunkSize;
            }

            waveStream.Position = Math.Min(
                waveStream.Length,
                chunkStart + chunkSize + (chunkSize % 2));

            if (byteRate > 0 && dataSize > 0)
            {
                waveStream.Position = 0;
                return TimeSpan.FromSeconds(dataSize / (double)byteRate);
            }
        }

        waveStream.Position = 0;
        throw new InvalidDataException("Could not determine the extracted audio duration.");
    }

    private static void EnsureInputExists(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The selected input file was not found.", path);
        }
    }
    private sealed record FfmpegResult(int ExitCode, string Error);
}

public sealed record VideoTranscriptionProgress(TimeSpan Position, TimeSpan Duration)
{
    public double Percentage => Duration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds * 100, 0, 100);
}
