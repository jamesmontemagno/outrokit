using System.Text.Json;
using GitHub.Copilot;
using PodcastMetadataGenerator.Console.Cli;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"outrokit-cli-tests-{Guid.NewGuid():N}");

    public CliApplicationTests()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Path.Combine(_directory, "settings.json"), "{}");
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public void Preserves_interactive_startup_and_file_arguments()
    {
        var input = Write("input.txt", "A transcript");
        Assert.False(CliApplication.ShouldRun([]));
        Assert.False(CliApplication.ShouldRun([input]));
        Assert.True(CliApplication.ShouldRun(["metadata", input]));
        Assert.True(CliApplication.ShouldRun(["--help"]));
        Assert.True(CliApplication.ShouldRun(["missing-transcript.txt"]));
    }

    [Theory]
    [InlineData("metadata", "--unknown")]
    [InlineData("captions", "fix")]
    [InlineData("audio", "replace")]
    [InlineData("transcribe", "--whisper-model")]
    [InlineData("metadata", "--title-count", "0")]
    [InlineData("metadata", "--style", "99")]
    [InlineData("metadata", "--artifacts", "garbage")]
    [InlineData("models", "list", "--timeout-seconds", "0")]
    [InlineData("models", "list", "--timeout-seconds", "86401")]
    public async Task Argument_errors_are_JSON_and_never_prompt(params string[] args)
    {
        var result = await Run(args);
        Assert.Equal(2, result.ExitCode);
        Assert.False(result.Json.GetProperty("success").GetBoolean());
        Assert.Equal("invalid_arguments", result.Json.GetProperty("error").GetProperty("code").GetString());
        Assert.Contains("invalid_arguments", result.Error);
    }

    [Fact]
    public async Task Lists_models_without_Copilot_or_ffmpeg()
    {
        var result = await Run(["models", "list", "--ffmpeg", "does-not-exist"]);
        Assert.Equal(0, result.ExitCode);
        Assert.Empty(result.Error);
        Assert.Contains(result.Json.GetProperty("result").EnumerateArray(),
            model => model.GetProperty("id").GetString() == "Base");
    }

    [Fact]
    public async Task Converts_timed_transcript_to_SRT_without_AI()
    {
        var input = Write("transcript.txt", "00:00:00 - 00:00:02 Hello captions");
        var output = Path.Combine(_directory, "converted.srt");
        var result = await Run(["srt", "convert", input, "--output", output]);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(output, result.Json.GetProperty("result").GetProperty("output").GetString());
        Assert.Contains("00:00:00,000 --> 00:00:02,000", File.ReadAllText(output));
        Assert.Empty(Directory.GetFiles(_directory, "*.partial.srt"));
    }

    [Fact]
    public async Task Converting_crosstalk_keeps_timing_and_reports_only_the_reorder()
    {
        var input = Write("crosstalk.txt",
            "00:10.00\nJames\nFirst turn.\n\n00:12.00\nFrank\nSecond turn.\n\n" +
            "00:11.50\nJames\nLike this\n\n00:13.00\nFrank\nLast turn.\n");
        var output = Path.Combine(_directory, "crosstalk.srt");

        var result = await Run(["srt", "convert", input, "--output", output]);

        Assert.Equal(0, result.ExitCode);
        var warnings = result.Json.GetProperty("result").GetProperty("warnings");
        Assert.Equal("Warning: Segments were not in chronological order and have been sorted.",
            Assert.Single(warnings.EnumerateArray()).GetString());
        var srt = File.ReadAllText(output);
        Assert.Contains("00:00:11,500 --> 00:00:12,000" + Environment.NewLine + "James: Like this", srt);
        Assert.Contains("00:00:13,000 --> 00:00:18,000" + Environment.NewLine + "Frank: Last turn.", srt);
    }

    [Fact]
    public async Task Untimed_input_is_rejected_before_AI_or_SRT_writes()
    {
        var input = Write("transcript.txt", "Some untimed episode content.");
        var output = Path.Combine(_directory, "output.srt");
        var conversion = await Run(["srt", "convert", input, "--output", output]);
        Assert.Equal(2, conversion.ExitCode);
        Assert.False(File.Exists(output));
        var factoryCalled = false;
        var app = new CliApplication((_, _) =>
        {
            factoryCalled = true;
            throw new InvalidOperationException("Must not create an AI client.");
        });
        var chapters = await Run(["metadata", input, "--artifacts", "Chapters"], app);
        Assert.Equal(2, chapters.ExitCode);
        Assert.False(factoryCalled);
    }

    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 0)]
    public async Task Existing_output_requires_explicit_overwrite(bool overwrite, int exitCode)
    {
        var input = Write("input.srt", "1\n00:00:00,000 --> 00:00:02,000\nHello\n");
        var output = Write("output.srt", "Existing export");
        string[] args = ["srt", "convert", input, "--output", output];
        var result = await Run(overwrite ? [.. args, "--overwrite"] : args);
        Assert.Equal(exitCode, result.ExitCode);
        if (!overwrite)
            Assert.Equal("Existing export", File.ReadAllText(output));
        else
            Assert.Contains("Hello", File.ReadAllText(output));
    }

    [Fact]
    public async Task Overwrite_never_allows_destroying_an_input()
    {
        const string content = "1\n00:00:00,000 --> 00:00:02,000\nHello\n";
        var input = Write("source.srt", content);
        var result = await Run(["srt", "convert", input, "--output", input, "--overwrite"]);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal(content, File.ReadAllText(input));
    }

    [SymbolicLinkFact]
    public async Task Output_links_cannot_overwrite_a_source()
    {
        var input = Write("source.srt", "1\n00:00:00,000 --> 00:00:02,000\nHello\n");
        var link = Path.Combine(_directory, "link.srt");
        File.CreateSymbolicLink(link, input);
        var result = await Run(["srt", "convert", input, "--output", link, "--overwrite"]);
        Assert.Equal(2, result.ExitCode);
        Assert.Contains("Hello", File.ReadAllText(input));
    }

    [Fact]
    public async Task Reports_caption_findings_with_a_nonzero_exit_code()
    {
        var input = Write("fast.srt", "1\n00:00:01,000 --> 00:00:01,300\nHello captions\n");
        var result = await Run(["captions", "check", input]);
        Assert.Equal(4, result.ExitCode);
        Assert.Equal("quality_issues", result.Json.GetProperty("error").GetProperty("code").GetString());
        Assert.True(result.Json.GetProperty("result").GetProperty("report").GetProperty("hasIssues").GetBoolean());
    }

    [Fact]
    public async Task Fixes_SRT_into_a_separate_file()
    {
        const string content = "1\n00:00:00,000 --> 00:00:02,000\nHello   captions\n";
        var input = Write("original.srt", content);
        var output = Path.Combine(_directory, "fixed.srt");
        var result = await Run(["captions", "fix", input, "--output", output]);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("Hello captions", File.ReadAllText(output));
        Assert.Equal(content, File.ReadAllText(input));
    }

    [Fact]
    public async Task Saved_fixes_with_remaining_findings_are_not_reported_as_clean()
    {
        var input = Write("unfixable.srt",
            "1\n00:00:01,000 --> 00:00:01,300\nHello\n\n2\n00:00:01,500 --> 00:00:03,000\nLater\n");
        var output = Path.Combine(_directory, "fixed.srt");
        var result = await Run(["captions", "fix", input, "--output", output]);
        Assert.Equal(4, result.ExitCode);
        Assert.True(File.Exists(output));
        Assert.Equal(output, result.Json.GetProperty("result").GetProperty("output").GetString());
    }

    [Theory]
    [InlineData("")]
    [InlineData("not a subtitle file")]
    public async Task Empty_or_malformed_captions_are_errors(string content)
    {
        var input = Write("invalid.srt", content);
        var result = await Run(["captions", "check", input]);
        Assert.Equal(2, result.ExitCode);
    }

    [Fact]
    public async Task Invalid_settings_do_not_silently_fall_back()
    {
        Write("settings.json", "not JSON");
        var result = await Run(["models", "list"]);
        Assert.Equal(2, result.ExitCode);
        Assert.Equal("invalid_settings", result.Json.GetProperty("error").GetProperty("code").GetString());
    }

    [Fact]
    public async Task Missing_Whisper_models_fail_without_implicit_downloads()
    {
        var input = Write("input.wav", "placeholder");
        var result = await Run(["transcribe", input, "--output", Path.Combine(_directory, "out.srt"),
            "--whisper-model-path", Path.Combine(_directory, "missing.bin")]);
        Assert.Equal(2, result.ExitCode);
        Assert.False(File.Exists(Path.Combine(_directory, "out.srt")));
    }

    [Fact]
    public async Task Metadata_selects_only_requested_outputs_and_does_not_save_settings()
    {
        var input = Write("episode.txt", "An episode about recording setup.");
        var destination = Path.Combine(_directory, "metadata.json");
        var generator = new FakeGenerator();
        AppSettings? observed = null;
        var app = new CliApplication((settings, _) => { observed = settings; return generator; });
        var originalSettings = File.ReadAllText(Path.Combine(_directory, "settings.json"));
        var result = await Run(["metadata", input, "--artifacts", "Descriptions", "--lengths", "Short",
            "--title", "Recording setup", "--context", "A guest engineer", "--output", destination], app);
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, generator.TitleCalls);
        Assert.Equal(0, generator.ChapterCalls);
        Assert.Equal(new[] { DescriptionLength.Short }, generator.DescriptionCalls);
        Assert.Equal("Recording setup", generator.SelectedTitle);
        Assert.Equal("A guest engineer", observed!.EpisodeContext);
        Assert.True(generator.Disposed);
        Assert.Equal(originalSettings, File.ReadAllText(Path.Combine(_directory, "settings.json")));
        using var saved = JsonDocument.Parse(File.ReadAllText(destination));
        Assert.Equal("Short description", saved.RootElement.GetProperty("metadata").GetProperty("descriptions").GetProperty("Short").GetString());
        Assert.Single(result.Json.GetProperty("result").GetProperty("savedFiles").EnumerateArray());
    }

    [Fact]
    public async Task Failed_AI_generation_does_not_replace_an_existing_export()
    {
        var input = Write("episode.txt", "An episode about recording setup.");
        var destination = Write("metadata.json", "Existing export");
        var generator = new FakeGenerator { Fail = true };
        var result = await Run(["metadata", input, "--output", destination, "--overwrite"],
            new CliApplication((_, _) => generator));
        Assert.Equal(1, result.ExitCode);
        Assert.Equal("Existing export", File.ReadAllText(destination));
        Assert.True(generator.Disposed);
    }

    [Fact]
    public async Task Validates_AI_title_word_limits_before_saving()
    {
        var input = Write("episode.txt", "Some episode.");
        var generator = new FakeGenerator();
        var result = await Run(["metadata", input, "--artifacts", "Titles", "--title-max-words", "1"],
            new CliApplication((_, _) => generator));
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public async Task Checks_timed_chapters_without_generating_other_metadata()
    {
        var input = Write("episode.srt",
            "1\n00:00:00,000 --> 00:02:15,000\nOpening\n\n" +
            "2\n00:02:15,000 --> 00:10:40,000\nRecording\n\n" +
            "3\n00:10:40,000 --> 00:18:00,000\nEditing\n");
        var generator = new FakeGenerator();
        var result = await Run(["metadata", input, "--artifacts", "Chapters"],
            new CliApplication((_, _) => generator));
        Assert.Equal(0, result.ExitCode);
        Assert.Equal(0, generator.TitleCalls);
        Assert.Empty(generator.DescriptionCalls);
        Assert.Equal(1, generator.ChapterCalls);
        Assert.Equal(3, result.Json.GetProperty("result").GetProperty("metadata").GetProperty("chapters").GetArrayLength());
    }

    [Theory]
    [InlineData("17:55", 1080)]
    [InlineData("18:01", 1080)]
    [InlineData("02:15", 1080)]
    [InlineData("00:09", 1080)]
    [InlineData("10:99", 1080)]
    [InlineData("60:00", 4000)]
    public void Rejects_invalid_chapter_times(string lastTime, double duration)
    {
        Chapter[] chapters = [
            new() { Timestamp = "00:00", Title = "Opening" },
            new() { Timestamp = "02:15", Title = "Recording" },
            new() { Timestamp = lastTime, Title = "Editing" }];
        Assert.Throws<ArgumentException>(() => CliApplication.ValidateChapters(chapters, duration, 8));
    }

    [Fact]
    public async Task Cancellation_returns_JSON_without_writing()
    {
        var input = Write("input.srt", "1\n00:00:00,000 --> 00:00:02,000\nHello\n");
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await new CliApplication().RunAsync(
            ["srt", "convert", input, "--output", Path.Combine(_directory, "output.srt")],
            output, error, cancellation.Token);
        Assert.Equal(130, exitCode);
        using var response = JsonDocument.Parse(output.ToString());
        Assert.Equal("cancelled", response.RootElement.GetProperty("error").GetProperty("code").GetString());
        Assert.False(File.Exists(Path.Combine(_directory, "output.srt")));
    }

    [Fact]
    public async Task Permission_requests_are_escalated_not_auto_approved_or_denied()
    {
        var input = Write("input.txt", "An episode.");
        var app = new CliApplication((_, permissions) => new FakeGenerator
        {
            Permission = () => permissions(new PermissionRequestRead { Path = "/requested/file", Intention = "Read input" },
                new PermissionInvocation { SessionId = "session-one" })
        });
        var result = await Run(["metadata", input, "--artifacts", "Titles"], app);
        Assert.Equal(3, result.ExitCode);
        var request = result.Json.GetProperty("error").GetProperty("details");
        Assert.Equal(64, request.GetProperty("id").GetString()!.Length);
        Assert.Contains("/requested/file", request.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Exact_permission_approval_is_consumed_once_and_not_reused_for_another_action()
    {
        var request = new PermissionRequestRead { Path = "/requested/file", ToolCallId = "first", Intention = "Read input" };
        using var cancellation = new CancellationTokenSource();
        var gate = new CliPermissionGate([], cancellation);
        await Assert.ThrowsAsync<CliPermissionRequiredException>(
            () => gate.DecideAsync(request, new PermissionInvocation { SessionId = "one" }));
        var id = gate.Pending!.Request.Id;
        using var retryCancellation = new CancellationTokenSource();
        var retry = new CliPermissionGate([id], retryCancellation);
        Assert.True(await retry.DecideAsync(
            new PermissionRequestRead { Path = "/requested/file", ToolCallId = "second", Intention = "Read input" },
            new PermissionInvocation { SessionId = "two" }));
        await Assert.ThrowsAsync<CliPermissionRequiredException>(
            () => retry.DecideAsync(request, new PermissionInvocation { SessionId = "two" }));
        using var otherCancellation = new CancellationTokenSource();
        var other = new CliPermissionGate([id], otherCancellation);
        await Assert.ThrowsAsync<CliPermissionRequiredException>(() => other.DecideAsync(
            new PermissionRequestRead { Path = "/different/file", Intention = "Read input" }, new PermissionInvocation { SessionId = "three" }));
        using var denialCancellation = new CancellationTokenSource();
        var denied = new CliPermissionGate([], denialCancellation, [id]);
        Assert.False(await denied.DecideAsync(request, new PermissionInvocation { SessionId = "four" }));
        Assert.False(denialCancellation.IsCancellationRequested);
    }

    private string Write(string name, string content)
    {
        var path = Path.Combine(_directory, name);
        File.WriteAllText(path, content);
        return path;
    }

    private async Task<(int ExitCode, JsonElement Json, string Error)> Run(string[] args, CliApplication? app = null)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exitCode = await (app ?? new CliApplication()).RunAsync(
            [.. args, "--settings", Path.Combine(_directory, "settings.json")], output, error);
        using var response = JsonDocument.Parse(output.ToString());
        return (exitCode, response.RootElement.Clone(), error.ToString());
    }

    private sealed class FakeGenerator : IMetadataGenerator
    {
        public int TitleCalls { get; private set; }
        public int ChapterCalls { get; private set; }
        public List<DescriptionLength> DescriptionCalls { get; } = [];
        public string? SelectedTitle { get; private set; }
        public bool Disposed { get; private set; }
        public bool Fail { get; init; }
        public Func<Task<bool>>? Permission { get; init; }

        public async Task<List<string>> GenerateTitlesAsync(Transcript transcript, TitleStyle? style = null,
            Action<string>? onChunk = null, CancellationToken cancellationToken = default)
        {
            TitleCalls++;
            if (Permission is not null)
                await Permission();
            if (Fail)
                throw new InvalidOperationException("Generation failed.");
            return Enumerable.Range(1, 5).Select(index => $"Recording setup {index}").ToList();
        }

        public Task<string> GenerateDescriptionAsync(Transcript transcript, DescriptionLength length,
            string? selectedTitle = null, Action<string>? onChunk = null, CancellationToken cancellationToken = default)
        {
            DescriptionCalls.Add(length);
            SelectedTitle = selectedTitle;
            return Task.FromResult($"{length} description");
        }

        public Task<List<Chapter>> GenerateChaptersAsync(Transcript transcript, Action<string>? onChunk = null,
            CancellationToken cancellationToken = default)
        {
            ChapterCalls++;
            return Task.FromResult(new List<Chapter> {
                new() { Timestamp = "00:00", Title = "Opening" },
                new() { Timestamp = "02:15", Title = "Recording" },
                new() { Timestamp = "10:40", Title = "Editing" } });
        }

        public ValueTask DisposeAsync() { Disposed = true; return ValueTask.CompletedTask; }
    }
}
