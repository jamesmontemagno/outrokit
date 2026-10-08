using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

[Collection(MediaCollection.Name)]
public sealed class CaptionBurnServiceTests(MediaFixture media)
{
    private static readonly CaptionStyle Default = new(CaptionSize.Medium, CaptionPosition.Bottom);

    [CaptionFfmpegFact]
    public async Task Reads_the_size_and_length_of_a_video()
    {
        var details = await media.CreateBurnService().GetVideoDetailsAsync(media.Landscape);

        Assert.Equal((640, 360), (details.Width, details.Height));
        Assert.InRange(details.Duration.TotalSeconds, MediaFixture.ClipSeconds - 0.2, MediaFixture.ClipSeconds + 0.2);
        Assert.Equal(1, details.ShortSideRatio);
    }

    [CaptionFfmpegFact]
    public async Task Reads_a_rotated_video_at_the_size_it_is_shown()
    {
        var service = media.CreateBurnService();

        var portrait = await service.GetVideoDetailsAsync(media.Portrait);
        var rotated = await service.GetVideoDetailsAsync(media.Rotated);

        Assert.Equal((360, 640), (portrait.Width, portrait.Height));
        Assert.Equal((360, 640), (rotated.Width, rotated.Height));
        Assert.Equal(0.5625, rotated.ShortSideRatio, precision: 4);
    }

    [CaptionFfmpegFact]
    public async Task Rejects_a_file_with_no_picture()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => media.CreateBurnService().GetVideoDetailsAsync(media.AudioOnly));

        Assert.Contains("does not contain a video", error.Message);
    }

    [CaptionFfmpegFact]
    public async Task Counts_captions_in_each_format_and_none_in_unreadable_files()
    {
        var service = media.CreateBurnService();

        Assert.Equal(1, await service.CountCaptionsAsync(media.Srt));
        Assert.Equal(1, await service.CountCaptionsAsync(media.Vtt));
        Assert.Equal(1, await service.CountCaptionsAsync(media.StyledAss));
        Assert.Equal(1, await service.CountCaptionsAsync(media.StyledSsa));
        Assert.Equal(0, await service.CountCaptionsAsync(media.GarbageSrt));
        Assert.Equal(0, await service.CountCaptionsAsync(media.EmptySrt));
    }

    [CaptionFfmpegTheory]
    [InlineData(CaptionPosition.Bottom)]
    [InlineData(CaptionPosition.Top)]
    public async Task Draws_captions_where_asked_and_only_while_they_are_due(CaptionPosition position)
    {
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");

        await media.CreateBurnService().BurnAsync(
            media.Landscape, media.Srt, output, new CaptionStyle(CaptionSize.Medium, position));

        var during = await media.ReadFrameAsync(output, MediaFixture.DuringCaption);
        var after = await media.ReadFrameAsync(output, MediaFixture.AfterCaption);
        var (expected, opposite) = position == CaptionPosition.Bottom
            ? (during.BottomThird, during.TopThird)
            : (during.TopThird, during.BottomThird);
        Assert.True(expected > 50, $"Expected caption text at the {position}, found {expected} bright pixels.");
        Assert.Equal(0, opposite);
        Assert.Equal(0, after.BrightPixels());
    }

    [CaptionFfmpegFact]
    public async Task Draws_larger_text_for_larger_sizes()
    {
        var directory = media.NewDirectory();
        var service = media.CreateBurnService();
        var bright = new Dictionary<CaptionSize, int>();

        foreach (var size in CaptionStyles.AllSizes)
        {
            var output = Path.Combine(directory, $"{size}.mp4");
            await service.BurnAsync(media.Landscape, media.Srt, output, new CaptionStyle(size, CaptionPosition.Bottom));
            bright[size] = (await media.ReadFrameAsync(output, MediaFixture.DuringCaption)).BrightPixels();
        }

        Assert.True(
            bright[CaptionSize.Small] < bright[CaptionSize.Medium] && bright[CaptionSize.Medium] < bright[CaptionSize.Large],
            $"Small {bright[CaptionSize.Small]}, Medium {bright[CaptionSize.Medium]}, Large {bright[CaptionSize.Large]}");
    }

    [CaptionFfmpegFact]
    public async Task Burns_four_visually_distinct_caption_appearances()
    {
        var directory = media.NewDirectory();
        var frameHashes = new HashSet<string>();

        foreach (var appearance in CaptionStyles.AllAppearances)
        {
            var output = Path.Combine(directory, $"{appearance}.mp4");
            await media.CreateBurnService().BurnAsync(
                media.Landscape,
                media.Srt,
                output,
                new CaptionStyle(CaptionSize.Medium, CaptionPosition.Bottom, appearance));

            var frame = await media.ReadFrameAsync(output, MediaFixture.DuringCaption);
            Assert.True(frame.BottomThird > 50, $"No visible text was rendered for {appearance}.");
            frameHashes.Add(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(frame.Pixels)));
        }

        Assert.Equal(4, frameHashes.Count);
    }

    [CaptionFfmpegFact]
    public async Task Burns_vtt_captions()
    {
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");

        await media.CreateBurnService().BurnAsync(media.Landscape, media.Vtt, output, Default);

        Assert.True((await media.ReadFrameAsync(output, MediaFixture.DuringCaption)).BottomThird > 50);
    }

    [CaptionFfmpegTheory]
    [InlineData("styled.ass")]
    [InlineData("styled.ssa")]
    public async Task Keeps_the_styling_of_a_file_that_has_its_own(string captionsName)
    {
        var captions = Path.Combine(Path.GetDirectoryName(media.Landscape)!, captionsName);
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");

        // The file puts its text top right; the requested bottom-centre style must not win.
        await media.CreateBurnService().BurnAsync(media.Landscape, captions, output, Default);

        var frame = await media.ReadFrameAsync(output, MediaFixture.DuringCaption);
        Assert.True(frame.BrightPixels(left: 0.5, bottom: 1 / 3.0) > 50);
        Assert.Equal(0, frame.BrightPixels(right: 0.5));
        Assert.Equal(0, frame.BottomThird);
    }

    [CaptionFfmpegFact]
    public async Task Burns_portrait_and_rotated_video_upright()
    {
        var directory = media.NewDirectory();
        var service = media.CreateBurnService();

        foreach (var source in new[] { media.Portrait, media.Rotated })
        {
            var output = Path.Combine(directory, $"{Path.GetFileNameWithoutExtension(source)}-captioned.mp4");
            await service.BurnAsync(source, media.Srt, output, Default);

            var frame = await media.ReadFrameAsync(output, MediaFixture.DuringCaption);
            Assert.Equal((360, 640), (frame.Width, frame.Height));
            Assert.True(frame.BottomThird > 50, $"No caption text at the bottom of {Path.GetFileName(output)}.");
            Assert.Equal(0, frame.TopThird);
        }
    }

    [CaptionFfmpegFact]
    public async Task Reports_progress_up_to_the_full_length()
    {
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");
        var reports = new List<CaptionBurnProgress>();

        await media.CreateBurnService().BurnAsync(
            media.Landscape, media.Srt, output, Default, new CallbackProgress<CaptionBurnProgress>(reports.Add));

        Assert.True(reports.Count >= 3, $"{reports.Count} progress reports");
        Assert.Equal(TimeSpan.Zero, reports[0].Position);
        Assert.Equal(100, reports[^1].Percentage);
        Assert.All(reports.Zip(reports.Skip(1)), pair => Assert.True(pair.Second.Position >= pair.First.Position));
        Assert.All(reports, report => Assert.True(report.IsDurationKnown));
    }

    [CaptionFfmpegFact]
    public async Task Keeps_the_length_and_sound_of_the_video()
    {
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");
        var service = media.CreateBurnService();

        await service.BurnAsync(media.Landscape, media.Srt, output, Default);

        var details = await service.GetVideoDetailsAsync(output);
        Assert.InRange(details.Duration.TotalSeconds, MediaFixture.ClipSeconds - 0.2, MediaFixture.ClipSeconds + 0.2);
        Assert.True(await HasAudioAsync(output));
    }

    [CaptionFfmpegFact]
    public async Task Burns_a_video_that_has_no_sound()
    {
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");

        await media.CreateBurnService().BurnAsync(media.Silent, media.Srt, output, Default);

        Assert.False(await HasAudioAsync(output));
        Assert.True((await media.ReadFrameAsync(output, MediaFixture.DuringCaption)).BottomThird > 50);
    }

    [CaptionFfmpegTheory]
    [InlineData("clip.avi", "captioned.mp4")]
    [InlineData("clip.mkv", "captioned.mkv")]
    [InlineData("landscape.mp4", "captioned.mov")]
    [InlineData("landscape.mp4", "captioned.m4v")]
    public async Task Saves_into_each_container_with_sound(string sourceName, string outputName)
    {
        var source = Path.Combine(Path.GetDirectoryName(media.Landscape)!, sourceName);
        var output = Path.Combine(media.NewDirectory(), outputName);

        await media.CreateBurnService().BurnAsync(source, media.Srt, output, Default);

        Assert.True(await HasAudioAsync(output));
        Assert.True((await media.ReadFrameAsync(output, MediaFixture.DuringCaption)).BottomThird > 50);
    }

    [CaptionFfmpegFact]
    public async Task Handles_spaces_brackets_and_quotes_in_every_path()
    {
        var directory = media.NewDirectory("odd [dir] it's here");
        var video = Path.Combine(directory, "my video, take #1 [final]; v=2.mp4");
        var captions = Path.Combine(directory, "caps, it's [en]; v=2.srt");
        var output = Path.Combine(directory, "out, it's [done]; v=2.mp4");
        File.Copy(media.Landscape, video);
        File.Copy(media.Srt, captions);

        await media.CreateBurnService().BurnAsync(video, captions, output, Default);

        Assert.True((await media.ReadFrameAsync(output, MediaFixture.DuringCaption)).BottomThird > 50);
        Assert.Equal(3, Directory.GetFileSystemEntries(directory).Length);
    }

    [CaptionFfmpegFact]
    public async Task Replaces_an_existing_file_only_when_the_burn_succeeds()
    {
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");
        await File.WriteAllTextAsync(output, "previous");

        await media.CreateBurnService().BurnAsync(media.Landscape, media.Srt, output, Default);

        Assert.True(new FileInfo(output).Length > 1000);
        Assert.Single(Directory.GetFileSystemEntries(Path.GetDirectoryName(output)!));
    }

    [CaptionFfmpegFact]
    public async Task Cancelling_keeps_the_existing_file_and_leaves_nothing_behind()
    {
        var directory = media.NewDirectory();
        var output = Path.Combine(directory, "captioned.mp4");
        await File.WriteAllTextAsync(output, "previous");
        var temporaryFoldersBefore = CountTemporaryCaptionFolders();
        var partialFilesSeen = 0;
        using var cancellation = new CancellationTokenSource();
        var progress = new CallbackProgress<CaptionBurnProgress>(update =>
        {
            if (update.Position > TimeSpan.Zero && !cancellation.IsCancellationRequested)
            {
                partialFilesSeen = Directory.GetFileSystemEntries(directory).Length - 1;
                _ = cancellation.CancelAsync();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => media.CreateBurnService().BurnAsync(
            media.Long, media.Srt, output, Default, progress, cancellation.Token));

        Assert.Equal(1, partialFilesSeen);
        Assert.Equal("previous", await File.ReadAllTextAsync(output));
        Assert.Single(Directory.GetFileSystemEntries(directory));
        Assert.Equal(temporaryFoldersBefore, CountTemporaryCaptionFolders());
    }

    [CaptionFfmpegFact]
    public async Task Refuses_to_save_over_the_original_video()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(
            () => media.CreateBurnService().BurnAsync(media.Landscape, media.Srt, media.Landscape, Default));

        Assert.Contains("original video", error.Message);
        Assert.True(await HasAudioAsync(media.Landscape));
    }

    [SymbolicLinkFact(NeedsCaptionFfmpeg = true)]
    public async Task Refuses_to_save_over_the_original_reached_through_a_link()
    {
        // The video is picked through links, and the output is the real file they lead to.
        var directory = media.NewDirectory();
        var original = Path.Combine(directory, "original.mp4");
        File.Copy(media.Landscape, original);
        var originalLength = new FileInfo(original).Length;
        var fileLink = Path.Combine(directory, "link-to-video.mp4");
        File.CreateSymbolicLink(fileLink, original);
        var folderLink = Path.Combine(media.NewDirectory(), "link-to-folder");
        Directory.CreateSymbolicLink(folderLink, directory);
        var service = media.CreateBurnService();

        foreach (var video in new[] { fileLink, Path.Combine(folderLink, "original.mp4") })
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.BurnAsync(video, media.Srt, original, Default));

            Assert.Contains("original video", error.Message);
            Assert.Equal(originalLength, new FileInfo(original).Length);
        }

        Assert.Equal(0, (await media.ReadFrameAsync(original, MediaFixture.DuringCaption)).BrightPixels());
    }

    [CaptionFfmpegTheory]
    [InlineData("garbage.srt", "No captions could be read")]
    [InlineData("empty.srt", "No captions could be read")]
    [InlineData("landscape.mp4", "Captions must be")]
    public async Task Rejects_unusable_captions_before_encoding(string captionsName, string expectedMessage)
    {
        var directory = media.NewDirectory();
        var captions = Path.Combine(Path.GetDirectoryName(media.Landscape)!, captionsName);

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => media.CreateBurnService().BurnAsync(
            media.Landscape, captions, Path.Combine(directory, "captioned.mp4"), Default));

        Assert.Contains(expectedMessage, error.Message);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [CaptionFfmpegFact]
    public async Task Rejects_an_audio_file_as_the_video()
    {
        var directory = media.NewDirectory();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => media.CreateBurnService().BurnAsync(
            media.AudioOnly, media.Srt, Path.Combine(directory, "captioned.mp4"), Default));

        Assert.Contains("does not contain a video", error.Message);
        Assert.Empty(Directory.GetFileSystemEntries(directory));
    }

    [CaptionFfmpegFact]
    public async Task Rejects_an_output_format_it_cannot_write()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => media.CreateBurnService().BurnAsync(
            media.Landscape, media.Srt, Path.Combine(media.NewDirectory(), "captioned.avi"), Default));

        Assert.Contains("must be saved as", error.Message);
    }

    [CaptionFfmpegFact]
    public async Task Rejects_an_output_folder_that_does_not_exist()
    {
        var output = Path.Combine(media.NewDirectory(), "missing", "captioned.mp4");

        await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => media.CreateBurnService().BurnAsync(media.Landscape, media.Srt, output, Default));
    }

    private Task<bool> HasAudioAsync(string path) =>
        new MediaTranscriptService(new AppSettings { FfmpegPath = media.Ffmpeg }).HasAudioStreamAsync(path);

    private static int CountTemporaryCaptionFolders() =>
        Directory.GetDirectories(Path.GetTempPath(), "podcast-metadata-captions-*").Length;
}
