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
        var output = Path.Combine(media.NewDirectory(), "original.wav");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ExtractAudioAsync(media.Silent, output));

        Assert.Contains("ffmpeg exited with code", error.Message);
        Assert.Contains("Stream map", error.Message);
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
        var transcriptService = new MediaTranscriptService(new AppSettings { FfmpegPath = media.Ffmpeg });
        Assert.True(await transcriptService.HasAudioStreamAsync(output));
    }

    [CaptionFfmpegFact]
    public async Task Reports_a_remux_failure()
    {
        var output = Path.Combine(media.NewDirectory(), "replaced.mp4");

        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().ReplaceAudioAsync(media.Landscape, media.Silent, output));

        Assert.Contains("ffmpeg exited with code", error.Message);
        Assert.Contains("matches no streams", error.Message);
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
