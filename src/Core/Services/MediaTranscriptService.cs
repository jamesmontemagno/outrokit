using System.Globalization;
using System.Text;
using PodcastMetadataGenerator.Core.Models;
using Whisper.net;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Transcribes the audio of a video or audio file to SRT, locally, using ffmpeg and Whisper.
/// </summary>
public class MediaTranscriptService
{
    // whisper.cpp emits this marker instead of text for silent audio.
    private const string BlankAudioMarker = "[BLANK_AUDIO]";

    private static readonly HashSet<string> VideoExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4", ".mov", ".mkv", ".avi", ".webm", ".m4v", ".wmv", ".mpeg", ".mpg"
    };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp3", ".wav", ".wave"
    };

    private readonly AppSettings _settings;
    private readonly WhisperModelService _modelService;

    public MediaTranscriptService(AppSettings settings, WhisperModelService? modelService = null)
    {
        _settings = settings;
        _modelService = modelService ?? new WhisperModelService();
    }

    /// <summary>
    /// Whether the path has a recognized video or audio extension.
    /// Use <see cref="HasAudioStreamAsync"/> to confirm the content.
    /// </summary>
    public static bool HasMediaExtension(string path) =>
        HasVideoExtension(path) || AudioExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Whether the path has a recognized video extension.
    /// </summary>
    public static bool HasVideoExtension(string path) => VideoExtensions.Contains(Path.GetExtension(path));

    /// <summary>
    /// Whether ffmpeg can decode an audio stream from the file, which is what transcription needs
    /// whether the file is a video or audio-only.
    /// </summary>
    public async Task<bool> HasAudioStreamAsync(string path, CancellationToken cancellationToken = default)
    {
        EnsureInputExists(path);

        var result = await RunFfmpegAsync(
            ["-nostdin", "-hide_banner", "-loglevel", "error", "-i", path, "-map", "0:a:0", "-frames:a", "1", "-f", "null", "-"],
            cancellationToken);
        return result.ExitCode == 0;
    }

    public async Task<string> TranscribeToSrtAsync(
        string mediaPath,
        IProgress<MediaTranscriptionProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        EnsureInputExists(mediaPath);
        var modelPath = _modelService.GetInstalledModelPath(_settings)
            ?? throw new InvalidOperationException(
                "No initialized Whisper model is available. Install one from Settings before transcribing.");

        var temporaryWavPath = Path.Combine(Path.GetTempPath(), $"podcast-metadata-{Guid.NewGuid():N}.wav");
        Exception? transcriptionException = null;
        try
        {
            var extraction = await RunFfmpegAsync(
                [
                    "-nostdin", "-hide_banner", "-loglevel", "error", "-y", "-i", mediaPath,
                    "-vn", "-acodec", "pcm_s16le", "-ar", "16000", "-ac", "1", temporaryWavPath
                ],
                cancellationToken);

            if (extraction.ExitCode != 0)
            {
                throw new InvalidOperationException(
                    extraction.Error.Contains("does not contain any stream", StringComparison.OrdinalIgnoreCase)
                        ? "The file does not contain an audio track to transcribe."
                        : $"ffmpeg could not extract audio from the file: {extraction.Error}");
            }

            using var factory = WhisperFactory.FromPath(modelPath);
            await using var audioStream = File.OpenRead(temporaryWavPath);
            var audioDuration = GetWaveDuration(audioStream);
            // Disposed asynchronously and first: sync Dispose throws while whisper is still processing,
            // which is exactly the state a cancellation leaves it in.
            await using var processor = factory.CreateBuilder()
                .WithLanguage("auto")
                .Build();
            progress?.Report(new MediaTranscriptionProgress(TimeSpan.Zero, audioDuration));
            
            var srt = new StringBuilder();
            var segmentNumber = 1;
            await foreach (var segment in processor.ProcessAsync(audioStream, cancellationToken))
            {
                var position = segment.End < audioDuration ? segment.End : audioDuration;
                progress?.Report(new MediaTranscriptionProgress(position, audioDuration));
                var text = segment.Text.Trim();
                if (text.Length == 0 || text == BlankAudioMarker)
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
                throw new InvalidOperationException("Whisper did not detect any speech in the audio.");
            }

            progress?.Report(new MediaTranscriptionProgress(audioDuration, audioDuration));
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

    private Task<FfmpegResult> RunFfmpegAsync(
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken) =>
        FfmpegRunner.RunAsync(_settings.FfmpegPath, arguments, cancellationToken);

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
}

public sealed record MediaTranscriptionProgress(TimeSpan Position, TimeSpan Duration)
{
    public double Percentage => Duration <= TimeSpan.Zero
        ? 0
        : Math.Clamp(Position.TotalMilliseconds / Duration.TotalMilliseconds * 100, 0, 100);
}
