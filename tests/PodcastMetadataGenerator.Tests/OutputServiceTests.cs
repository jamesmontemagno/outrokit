using System.Text.Json;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

public sealed class OutputServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"outrokit-output-tests-{Guid.NewGuid():N}");
    private readonly OutputService _service = new(new SrtConverter());

    public OutputServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    [Fact]
    public async Task Saves_only_selected_artifacts_and_omits_unselected_srt_from_manifest()
    {
        var outputDirectory = Path.Combine(_root, "output");
        var transcript = CreateTranscript();
        var result = new GenerationResult
        {
            Titles = ["First title", "Second title"],
            Descriptions =
            {
                [DescriptionLength.Short] = "Short description",
                [DescriptionLength.Long] = "Long description"
            },
            Chapters = [new Chapter { Timestamp = "00:00", Title = "Opening" }]
        };

        var savedFiles = await _service.SaveSelectedAsync(
            outputDirectory,
            transcript,
            result,
            new AppSettings(),
            [OutputArtifact.ShortDescription, OutputArtifact.Manifest]);

        Assert.Equal(
            ["episode_description_short.txt", "episode_manifest.json"],
            savedFiles.Select(Path.GetFileName));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "episode_titles.txt")));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "episode_description_long.txt")));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "episode.srt")));
        using var manifest = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(outputDirectory, "episode_manifest.json")));
        Assert.Equal(JsonValueKind.Null, manifest.RootElement.GetProperty("srtPath").ValueKind);
    }

    [Fact]
    public async Task Save_all_keeps_saving_every_available_artifact()
    {
        var outputDirectory = Path.Combine(_root, "all");
        var result = new GenerationResult
        {
            Titles = ["First title"],
            Descriptions = { [DescriptionLength.Short] = "Short description" },
            Chapters = [new Chapter { Timestamp = "00:00", Title = "Opening" }]
        };

        var savedFiles = await _service.SaveAllAsync(
            outputDirectory,
            CreateTranscript(),
            result,
            new AppSettings());

        Assert.Equal(
            [
                "episode_titles.txt",
                "episode_description_short.txt",
                "episode_chapters.txt",
                "episode.srt",
                "episode_manifest.json"
            ],
            savedFiles.Select(Path.GetFileName));
    }

    [Fact]
    public async Task Does_not_overwrite_source_srt_when_saving_next_to_it()
    {
        var outputDirectory = Path.Combine(_root, "source");
        Directory.CreateDirectory(outputDirectory);
        var sourcePath = Path.Combine(outputDirectory, "episode.srt");
        const string originalContent = "original subtitle file";
        await File.WriteAllTextAsync(sourcePath, originalContent);

        var savedFiles = await _service.SaveSelectedAsync(
            outputDirectory,
            CreateTranscript(sourcePath),
            new GenerationResult(),
            new AppSettings(),
            [OutputArtifact.Srt, OutputArtifact.Manifest]);

        Assert.Equal(originalContent, await File.ReadAllTextAsync(sourcePath));
        Assert.Contains(Path.Combine(outputDirectory, "episode_subtitles.srt"), savedFiles);
        using var manifest = JsonDocument.Parse(
            await File.ReadAllTextAsync(Path.Combine(outputDirectory, "episode_manifest.json")));
        Assert.Equal(
            "episode_subtitles.srt",
            manifest.RootElement.GetProperty("srtPath").GetString());
    }

    [SymbolicLinkFact]
    public async Task Does_not_overwrite_source_srt_through_a_fallback_symlink()
    {
        var outputDirectory = Path.Combine(_root, "linked-source");
        Directory.CreateDirectory(outputDirectory);
        var sourcePath = Path.Combine(outputDirectory, "episode.srt");
        const string originalContent = "original subtitle file";
        await File.WriteAllTextAsync(sourcePath, originalContent);
        File.CreateSymbolicLink(
            Path.Combine(outputDirectory, "episode_subtitles.srt"),
            sourcePath);

        await Assert.ThrowsAsync<IOException>(() => _service.SaveSelectedAsync(
            outputDirectory,
            CreateTranscript(sourcePath),
            new GenerationResult(),
            new AppSettings(),
            [OutputArtifact.Srt, OutputArtifact.Manifest]));

        Assert.Equal(originalContent, await File.ReadAllTextAsync(sourcePath));
        Assert.False(File.Exists(Path.Combine(outputDirectory, "episode_manifest.json")));
    }

    public void Dispose()
    {
        Directory.Delete(_root, recursive: true);
    }

    private Transcript CreateTranscript(string? filePath = null) => new()
    {
        FilePath = filePath ?? Path.Combine(_root, "episode.txt"),
        Format = TranscriptFormat.Zencastr,
        Segments =
        [
            new TranscriptSegment { StartTimeMs = 0, EndTimeMs = 2_000, Text = "Welcome" }
        ]
    };
}
