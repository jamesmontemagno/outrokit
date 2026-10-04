using System.Runtime.InteropServices;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

[Collection(MediaCollection.Name)]
public sealed class FfmpegDetectionTests(MediaFixture media)
{
    private static readonly string MissingFfmpeg = Path.Combine(Path.GetTempPath(), "no-such-folder", "ffmpeg");

    [CaptionFfmpegFact]
    public async Task Recognizes_an_ffmpeg_that_can_burn_captions()
    {
        Assert.True(await media.CreateBurnService().SupportsCaptionBurnAsync());
    }

    [Fact]
    public async Task Reports_a_missing_ffmpeg_as_not_found()
    {
        var service = new CaptionBurnService(new AppSettings { FfmpegPath = MissingFfmpeg });

        var error = await Assert.ThrowsAsync<FfmpegNotFoundException>(() => service.SupportsCaptionBurnAsync());

        Assert.Equal($"ffmpeg was not found at '{MissingFfmpeg}'. Configure it in Settings.", error.Message);
    }

    [FfmpegWithoutCaptionsFact]
    public async Task Recognizes_an_ffmpeg_built_without_the_subtitles_filter()
    {
        var path = TestFfmpeg.WithoutCaptionSupport;
        Assert.NotNull(path);
        var service = new CaptionBurnService(new AppSettings { FfmpegPath = path });
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");

        Assert.False(await service.SupportsCaptionBurnAsync());
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.BurnAsync(
            media.Landscape, media.Srt, output, new CaptionStyle(CaptionSize.Medium, CaptionPosition.Bottom)));

        Assert.Contains("libass", error.Message);
        Assert.False(File.Exists(output));
        // It can still do everything transcription needs.
        Assert.Equal(640, (await service.GetVideoDetailsAsync(media.Landscape)).Width);
    }

    [HomebrewFullFfmpegFact]
    public async Task The_Homebrew_path_the_app_suggests_is_where_ffmpeg_full_installs()
    {
        var path = CaptionBurnService.GetHomebrewFullFfmpegPath();

        Assert.NotNull(path);
        Assert.True(File.Exists(path), $"Expected Homebrew's ffmpeg-full at {path}.");
        Assert.True(await new CaptionBurnService(new AppSettings { FfmpegPath = path }).SupportsCaptionBurnAsync());
    }

    [Fact]
    public void Suggests_a_Homebrew_path_only_on_macOS()
    {
        var path = CaptionBurnService.GetHomebrewFullFfmpegPath();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            Assert.EndsWith("/opt/ffmpeg-full/bin/ffmpeg", path);
        }
        else
        {
            Assert.Null(path);
        }
    }
}
