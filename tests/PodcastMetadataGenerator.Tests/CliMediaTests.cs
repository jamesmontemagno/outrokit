using System.Text.Json;
using PodcastMetadataGenerator.Console.Cli;

namespace PodcastMetadataGenerator.Tests;

[Collection(MediaCollection.Name)]
public sealed class CliMediaTests(MediaFixture media)
{
    [CaptionFfmpegFact]
    public async Task Burns_styled_captions_with_JSON_results()
    {
        var output = Path.Combine(media.NewDirectory(), "captioned.mp4");
        var result = await Run(["captions", "burn", media.Landscape, "--captions", media.Srt,
            "--output", output, "--appearance", "ContrastPanel", "--size", "Large", "--position", "Top"]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(output, result.Json.GetProperty("result").GetProperty("output").GetString());
        var frame = await media.ReadFrameAsync(output, MediaFixture.DuringCaption);
        Assert.True(frame.BrightPixels(top: 0, bottom: 0.5) > 20);
    }

    [CaptionFfmpegFact]
    public async Task Extracts_and_replaces_audio_from_commands()
    {
        var directory = media.NewDirectory();
        var audio = Path.Combine(directory, "original.wav");
        var extracted = await Run(["audio", "extract", media.Landscape, "--output", audio]);
        Assert.Equal(0, extracted.ExitCode);
        Assert.True(File.Exists(audio));
        var output = Path.Combine(directory, "replaced.mp4");
        var replaced = await Run(["audio", "replace", media.Landscape, "--audio", media.AudioOnly, "--output", output]);
        Assert.Equal(0, replaced.ExitCode);
        var details = await media.CreateBurnService().GetVideoDetailsAsync(output);
        Assert.Equal((640, 360), (details.Width, details.Height));
        Assert.InRange(details.Duration.TotalSeconds, MediaFixture.ClipSeconds - 0.2, MediaFixture.ClipSeconds + 0.2);
        var originalFrame = await media.ReadFrameAsync(media.Landscape, 1);
        var replacedFrame = await media.ReadFrameAsync(output, 1);
        Assert.Equal(originalFrame.Pixels, replacedFrame.Pixels);
    }

    [CaptionFfmpegFact]
    public async Task Caption_quality_blocks_a_burn_until_explicitly_allowed()
    {
        var directory = media.NewDirectory();
        var captions = Path.Combine(directory, "fast.srt");
        await File.WriteAllTextAsync(captions,
            "1\n00:00:01,000 --> 00:00:01,300\nToo fast to read this caption\n");
        var output = Path.Combine(directory, "captioned.mp4");
        string[] args = ["captions", "burn", media.Landscape, "--captions", captions, "--output", output];
        var blocked = await Run(args);
        Assert.Equal(4, blocked.ExitCode);
        Assert.False(File.Exists(output));
        var allowed = await Run([.. args, "--allow-issues"]);
        Assert.Equal(0, allowed.ExitCode);
        Assert.True(allowed.Json.GetProperty("result").GetProperty("report").GetProperty("hasIssues").GetBoolean());
    }

    [CaptionFfmpegFact]
    public async Task Safe_burn_fixes_use_a_copy_and_leave_the_captions_unchanged()
    {
        var directory = media.NewDirectory();
        var captions = Path.Combine(directory, "spaces.srt");
        const string original = "1\n00:00:00,500 --> 00:00:02,000\nHello   captions\n";
        await File.WriteAllTextAsync(captions, original);
        var output = Path.Combine(directory, "captioned.mp4");
        var result = await Run(["captions", "burn", media.Landscape, "--captions", captions,
            "--output", output, "--fix"]);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Json.GetProperty("result").GetProperty("appliedFixCount").GetInt32() > 0);
        Assert.Equal(original, await File.ReadAllTextAsync(captions));
        Assert.Empty(Directory.GetFiles(directory, "*.partial.mp4"));
    }

    [CaptionFfmpegFact]
    public async Task ASS_styles_are_preserved_and_overrides_are_explicit_errors()
    {
        var output = Path.Combine(media.NewDirectory(), "styled.mp4");
        string[] args = ["captions", "burn", media.Landscape, "--captions", media.StyledAss, "--output", output];
        var invalid = await Run([.. args, "--appearance", "BoldImpact"]);
        Assert.Equal(2, invalid.ExitCode);
        Assert.False(File.Exists(output));
        var result = await Run(args);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.Json.GetProperty("result").GetProperty("embeddedStyling").GetBoolean());
    }

    [CaptionFfmpegFact]
    public async Task Media_failure_does_not_replace_existing_output_even_with_overwrite()
    {
        var output = Path.Combine(media.NewDirectory(), "original.wav");
        await File.WriteAllTextAsync(output, "Existing audio export");
        var result = await Run(["audio", "extract", media.Silent, "--output", output, "--overwrite"]);
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("Existing audio export", await File.ReadAllTextAsync(output));
        Assert.Empty(Directory.GetFiles(Path.GetDirectoryName(output)!, "*.partial.wav"));
    }

    [CaptionFfmpegFact]
    public async Task An_active_command_timeout_preserves_existing_output_and_cleans_partials()
    {
        var directory = media.NewDirectory();
        var destination = Path.Combine(directory, "captioned.mp4");
        await File.WriteAllTextAsync(destination, "Existing video export");
        var result = await Run(["captions", "burn", media.Long, "--captions", media.Srt,
            "--output", destination, "--overwrite", "--timeout-seconds", "1"]);
        Assert.Equal(130, result.ExitCode);
        Assert.Equal("Existing video export", await File.ReadAllTextAsync(destination));
        Assert.Single(Directory.GetFiles(directory));
    }

    private async Task<(int ExitCode, JsonElement Json)> Run(string[] args)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var settings = Path.Combine(media.NewDirectory(), "settings.json");
        await File.WriteAllTextAsync(settings, "{}");
        var exitCode = await new CliApplication().RunAsync(
            [.. args, "--ffmpeg", media.Ffmpeg, "--settings", settings], output, error);
        using var response = JsonDocument.Parse(output.ToString());
        return (exitCode, response.RootElement.Clone());
    }
}
