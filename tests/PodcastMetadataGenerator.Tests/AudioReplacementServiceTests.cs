using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

[Collection(MediaCollection.Name)]
public sealed class AudioReplacementServiceTests(MediaFixture media)
{
    private AudioReplacementService CreateService() =>
        new(new AppSettings { FfmpegPath = media.Ffmpeg });

    [CaptionFfmpegFact]
    public async Task Extracts_the_original_audio()
    {
        var output = Path.Combine(media.NewDirectory(), "original.wav");

        await CreateService().ExtractAudioAsync(media.Landscape, output);

        Assert.True(File.Exists(output));
        var transcriptService = new MediaTranscriptService(new AppSettings { FfmpegPath = media.Ffmpeg });
        Assert.True(await transcriptService.HasAudioStreamAsync(output));
    }

    [CaptionFfmpegFact]
    public async Task Reports_an_extraction_failure()
    {
        var directory = media.NewDirectory();
        var output = Path.Combine(directory, "original.wav");
        var originalContents = new byte[] { 1, 2, 3, 4 };
        await File.WriteAllBytesAsync(output, originalContents);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ExtractAudioAsync(media.Silent, output));

        Assert.Contains("ffmpeg exited with code", error.Message);
        Assert.Contains("Stream map", error.Message);
        Assert.Equal(originalContents, await File.ReadAllBytesAsync(output));
        Assert.Empty(Directory.GetFiles(directory, ".original.*.partial.wav"));
    }

    [CaptionFfmpegFact]
    public async Task Replaces_audio_in_an_mp4_without_changing_its_resolution()
    {
        var output = Path.Combine(media.NewDirectory(), "replaced.mp4");
        var service = CreateService();

        await service.ReplaceAudioAsync(media.Landscape, media.AudioOnly, output);

        Assert.True(File.Exists(output));
        var details = await media.CreateBurnService().GetVideoDetailsAsync(output);
        Assert.Equal((640, 360), (details.Width, details.Height));
        Assert.InRange(
            details.Duration.TotalSeconds,
            MediaFixture.ClipSeconds - 0.2,
            MediaFixture.ClipSeconds + 0.2);
        var transcriptService = new MediaTranscriptService(new AppSettings { FfmpegPath = media.Ffmpeg });
        Assert.True(await transcriptService.HasAudioStreamAsync(output));
    }

    [CaptionFfmpegFact]
    public async Task Stops_at_the_video_end_when_replacement_audio_is_longer()
    {
        var directory = media.NewDirectory();
        var longAudio = Path.Combine(directory, "long-audio.wav");
        var output = Path.Combine(directory, "replaced.mp4");
        await TestFfmpeg.RunAsync(
            media.Ffmpeg,
            "-f", "lavfi", "-i", "sine=frequency=660",
            "-t", "5", "-c:a", "pcm_s16le", longAudio);

        await CreateService().ReplaceAudioAsync(media.Landscape, longAudio, output);

        var details = await media.CreateBurnService().GetVideoDetailsAsync(output);
        Assert.InRange(
            details.Duration.TotalSeconds,
            MediaFixture.ClipSeconds - 0.2,
            MediaFixture.ClipSeconds + 0.2);
    }

    [CaptionFfmpegFact]
    public async Task Reports_a_remux_failure()
    {
        var directory = media.NewDirectory();
        var output = Path.Combine(directory, "replaced.mp4");
        var originalContents = new byte[] { 4, 3, 2, 1 };
        await File.WriteAllBytesAsync(output, originalContents);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ReplaceAudioAsync(media.Landscape, media.Silent, output));

        Assert.Contains("ffmpeg exited with code", error.Message);
        Assert.Contains("matches no streams", error.Message);
        Assert.Equal(originalContents, await File.ReadAllBytesAsync(output));
        Assert.Empty(Directory.GetFiles(directory, ".replaced.*.partial.mp4"));
    }

    [CaptionFfmpegFact]
    public async Task Rejects_a_non_mp4_output()
    {
        var output = Path.Combine(media.NewDirectory(), "replaced.mov");

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => CreateService().ReplaceAudioAsync(media.Landscape, media.AudioOnly, output));

        Assert.Contains("must be an MP4", error.Message);
        Assert.False(File.Exists(output));
    }

    [SymbolicLinkFact(NeedsCaptionFfmpeg = true)]
    public async Task Refuses_to_overwrite_inputs_reached_through_output_links()
    {
        var directory = media.NewDirectory();
        var video = Path.Combine(directory, "video.mp4");
        var outputLink = Path.Combine(directory, "output.mp4");
        File.Copy(media.Landscape, video);
        var originalContents = await File.ReadAllBytesAsync(video);
        File.CreateSymbolicLink(outputLink, video);
        var service = CreateService();

        var replacementError = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ReplaceAudioAsync(video, media.AudioOnly, outputLink));
        var extractionError = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExtractAudioAsync(video, outputLink));

        Assert.Contains("cannot overwrite", replacementError.Message);
        Assert.Contains("cannot overwrite", extractionError.Message);
        Assert.Equal(originalContents, await File.ReadAllBytesAsync(video));
    }

    [CaptionFfmpegFact]
    public async Task Command_wrapper_reports_ffmpeg_stderr_on_failure()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => FfmpegCommand.RunAsync(
            media.Ffmpeg,
            ["-nostdin", "-hide_banner", "-definitely-not-an-ffmpeg-option"],
            CancellationToken.None));

        Assert.Contains("ffmpeg exited with code", error.Message);
        Assert.Contains("definitely-not-an-ffmpeg-option", error.Message);
    }
}
