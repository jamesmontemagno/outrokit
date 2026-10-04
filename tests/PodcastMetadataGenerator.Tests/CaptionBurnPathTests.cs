using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

public sealed class CaptionBurnPathTests
{
    private static readonly string Folder = Path.Combine(Path.GetTempPath(), "episodes");

    [Theory]
    [InlineData("episode.mp4", "episode-captioned.mp4")]
    [InlineData("episode.MOV", "episode-captioned.mov")]
    [InlineData("episode.mkv", "episode-captioned.mkv")]
    [InlineData("episode.webm", "episode-captioned.mp4")]
    [InlineData("episode.avi", "episode-captioned.mp4")]
    [InlineData("my episode [final].mp4", "my episode [final]-captioned.mp4")]
    public void Suggests_an_output_beside_the_video(string videoName, string expectedName)
    {
        var suggested = CaptionBurnService.GetDefaultOutputPath(Path.Combine(Folder, videoName));

        Assert.Equal(Path.Combine(Folder, expectedName), suggested);
    }

    [Fact]
    public void Offers_the_source_container_first()
    {
        Assert.Equal([".mkv", ".mp4", ".mov", ".m4v"], CaptionBurnService.GetOutputExtensions("episode.mkv"));
        Assert.Equal([".mp4", ".mov", ".mkv", ".m4v"], CaptionBurnService.GetOutputExtensions("episode.webm"));
    }

    [Theory]
    [InlineData("captions.srt", true, false)]
    [InlineData("captions.VTT", true, false)]
    [InlineData("captions.ass", true, true)]
    [InlineData("captions.ssa", true, true)]
    [InlineData("captions.txt", false, false)]
    [InlineData("episode.mp4", false, false)]
    public void Knows_caption_formats_and_which_carry_their_own_styling(string name, bool isCaptions, bool hasOwnStyling)
    {
        Assert.Equal(isCaptions, CaptionBurnService.HasCaptionExtension(name));
        Assert.Equal(hasOwnStyling, CaptionBurnService.HasOwnStyling(name));
    }

    [SymbolicLinkFact]
    public void Treats_links_to_a_file_as_the_file_itself()
    {
        var directory = Directory.CreateTempSubdirectory("pmg-links-").FullName;
        try
        {
            var folder = Directory.CreateDirectory(Path.Combine(directory, "videos")).FullName;
            var video = Path.Combine(folder, "episode.mp4");
            File.WriteAllText(video, "video");
            File.WriteAllText(Path.Combine(folder, "other.mp4"), "other");
            var fileLink = Path.Combine(directory, "link.mp4");
            File.CreateSymbolicLink(fileLink, video);
            var relativeLink = Path.Combine(folder, "relative-link.mp4");
            File.CreateSymbolicLink(relativeLink, "episode.mp4");
            var linkToLink = Path.Combine(directory, "link-to-link.mp4");
            File.CreateSymbolicLink(linkToLink, fileLink);
            var folderLink = Path.Combine(directory, "linked-videos");
            Directory.CreateSymbolicLink(folderLink, folder);

            Assert.True(CaptionBurnService.IsSameFile(fileLink, video));
            Assert.True(CaptionBurnService.IsSameFile(video, relativeLink));
            Assert.True(CaptionBurnService.IsSameFile(linkToLink, video));
            Assert.True(CaptionBurnService.IsSameFile(Path.Combine(folderLink, "episode.mp4"), video));
            Assert.True(CaptionBurnService.IsSameFile(Path.Combine(folderLink, "episode.mp4"), fileLink));

            Assert.False(CaptionBurnService.IsSameFile(fileLink, Path.Combine(folder, "other.mp4")));
            // A new file in a linked folder is still a new file.
            Assert.False(CaptionBurnService.IsSameFile(Path.Combine(folderLink, "episode-captioned.mp4"), video));
            Assert.True(CaptionBurnService.IsSameFile(
                Path.Combine(folderLink, "episode-captioned.mp4"),
                Path.Combine(folder, "episode-captioned.mp4")));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public void Treats_different_spellings_of_one_path_as_the_same_file()
    {
        var video = Path.Combine(Folder, "episode.mp4");

        Assert.True(CaptionBurnService.IsSameFile(video, Path.Combine(Folder, ".", "episode.mp4")));
        Assert.True(CaptionBurnService.IsSameFile(video, Path.Combine(Folder, "EPISODE.MP4")));
        Assert.False(CaptionBurnService.IsSameFile(video, Path.Combine(Folder, "episode-captioned.mp4")));
    }
}
