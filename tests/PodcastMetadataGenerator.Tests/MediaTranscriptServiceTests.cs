using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

[Collection(MediaCollection.Name)]
public sealed class MediaTranscriptServiceTests(MediaFixture media)
{
    [CaptionFfmpegFact]
    public async Task Finds_sound_in_video_and_audio_files_and_none_in_a_silent_video()
    {
        var service = new MediaTranscriptService(new AppSettings { FfmpegPath = media.Ffmpeg });

        Assert.True(await service.HasAudioStreamAsync(media.Landscape));
        Assert.True(await service.HasAudioStreamAsync(media.AudioOnly));
        Assert.False(await service.HasAudioStreamAsync(media.Silent));
    }

    [Fact]
    public async Task Reports_a_missing_ffmpeg_as_not_found()
    {
        var input = Path.GetTempFileName();
        var ffmpeg = Path.Combine(Path.GetTempPath(), "no-such-folder", "ffmpeg");
        try
        {
            var service = new MediaTranscriptService(new AppSettings { FfmpegPath = ffmpeg });

            var error = await Assert.ThrowsAsync<FfmpegNotFoundException>(() => service.HasAudioStreamAsync(input));

            Assert.Equal($"ffmpeg was not found at '{ffmpeg}'. Configure it in Settings.", error.Message);
        }
        finally
        {
            File.Delete(input);
        }
    }

    [Theory]
    [InlineData("episode.mp4", true, true)]
    [InlineData("episode.MKV", true, true)]
    [InlineData("episode.webm", true, true)]
    [InlineData("episode.mp3", true, false)]
    [InlineData("episode.WAV", true, false)]
    [InlineData("episode.wave", true, false)]
    [InlineData("episode.srt", false, false)]
    [InlineData("episode.txt", false, false)]
    public void Tells_video_from_audio_by_extension(string name, bool isMedia, bool isVideo)
    {
        Assert.Equal(isMedia, MediaTranscriptService.HasMediaExtension(name));
        Assert.Equal(isVideo, MediaTranscriptService.HasVideoExtension(name));
    }
}
