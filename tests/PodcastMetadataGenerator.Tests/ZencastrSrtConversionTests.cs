using System.Text.RegularExpressions;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

public sealed partial class ZencastrSrtConversionTests
{
    [Fact]
    public void Out_of_order_turns_keep_every_caption_on_its_own_timestamp()
    {
        var turns = new (string Time, long StartMs, string Text)[]
        {
            ("00:10.00", 10_000, "First turn is here."),
            ("00:12.00", 12_000, "Second turn talks."),
            ("00:11.50", 11_500, "Crosstalk one begins."),
            ("00:13.00", 13_000, "Third turn talks."),
            ("00:15.00", 15_000, "Fourth turn talks."),
            ("00:14.50", 14_500, "Crosstalk two begins."),
            ("00:16.00", 16_000, "Fifth turn talks."),
            ("00:17.00", 17_000, "Sixth turn talks."),
            ("00:18.00", 18_000, "Seventh turn talks."),
            ("1:00:00.25", 3_600_250, "Last turn, an hour in."),
        };
        var content = string.Concat(turns.Select(turn => Turn(turn.Time, "James", turn.Text)));

        var result = Convert(content);
        var cues = Cues(result.Content);

        Assert.Equal(turns.Length, cues.Count);
        foreach (var turn in turns)
        {
            var cue = Assert.Single(cues, cue => cue.Text == $"James: {turn.Text}");
            Assert.Equal(turn.StartMs, cue.StartMs);
        }

        Assert.Equal(cues.OrderBy(cue => cue.StartMs).Select(cue => cue.StartMs), cues.Select(cue => cue.StartMs));
        Assert.Empty(new SrtConverter().ValidateSrt(result.Content));

        // A file-order end time would be earlier than this cue's own start.
        var crosstalk = cues.Single(cue => cue.Text.Contains("Crosstalk one"));
        Assert.Equal(12_000, crosstalk.EndMs);
        Assert.Equal(13_000, cues.Single(cue => cue.Text.Contains("Second turn")).EndMs);

        Assert.Equal(
            ["Warning: Segments were not in chronological order and have been sorted."],
            result.Errors);
    }

    [Fact]
    public void Repeated_crosstalk_does_not_accumulate_drift()
    {
        var starts = Enumerable.Range(0, 100).Select(index => 10_000L + (index * 2_000L)).ToArray();
        for (var index = 9; index < starts.Length; index += 10)
        {
            (starts[index - 1], starts[index]) = (starts[index], starts[index - 1]);
        }

        var content = string.Concat(starts.Select(start =>
            Turn(TimeSpan.FromMilliseconds(start).ToString(@"mm\:ss\.ff"), "James", $"Words at {start}.")));

        var cues = Cues(Convert(content).Content);

        Assert.Equal(starts.Length, cues.Count);
        Assert.All(cues, cue => Assert.Equal($"James: Words at {cue.StartMs}.", cue.Text));
    }

    [Fact]
    public void Turns_sharing_a_start_become_one_cue_and_do_not_shift_later_captions()
    {
        var content =
            Turn("00:10.00", "James", "James starts talking.") +
            Turn("00:10.00", "Frank", "Frank starts too.") +
            Turn("00:20.00", "James", "Later turn stays put.") +
            Turn("00:30.00", "Frank", "Final turn stays put.");

        var result = Convert(content);
        var cues = Cues(result.Content);

        Assert.Equal(3, cues.Count);
        Assert.Equal((10_000, 20_000), (cues[0].StartMs, cues[0].EndMs));
        Assert.Equal("James: James starts talking.\nFrank: Frank starts too.", cues[0].Text);
        Assert.Equal(20_000, cues[1].StartMs);
        Assert.Equal(30_000, cues[2].StartMs);
        Assert.Empty(new SrtConverter().ValidateSrt(result.Content));
        Assert.Equal(["Warning: 2 turns start at 00:00:10,000 and were combined into cue 1."], result.Errors);
    }

    [Fact]
    public void Transcript_keeps_turns_sharing_a_start_as_separate_segments()
    {
        var transcript = Parse(
            Turn("00:10.00", "James", "James starts talking.") +
            Turn("00:10.00", "Frank", "Frank starts too.") +
            Turn("00:20.00", "James", "Later turn."));

        Assert.Equal(["James", "Frank", "James"], transcript.Segments.Select(segment => segment.Speaker));
        Assert.Equal([10_000L, 10_000L, 20_000L], transcript.Segments.Select(segment => segment.StartTimeMs));
        Assert.Equal([20_000L, 20_000L, 25_000L], transcript.Segments.Select(segment => segment.EndTimeMs));
    }

    [Theory]
    [InlineData("\n")]
    [InlineData("\r\n")]
    public void A_turn_whose_only_text_looks_like_a_speaker_is_kept(string newline)
    {
        var content =
            Turn("00:01.00", "James", "Do you want it spelled out?") +
            Turn("00:03.00", "Frank", "Like this") +
            Turn("00:05.00", "James", "Yes") +
            Turn("00:07.00", "Frank", "Exactly right");

        var cues = Cues(Convert(content.Replace("\n", newline)).Content);

        Assert.Equal(
            ["James: Do you want it spelled out?", "Frank: Like this", "James: Yes", "Frank: Exactly right"],
            cues.Select(cue => cue.Text));
        Assert.Equal([1_000L, 3_000L, 5_000L, 7_000L], cues.Select(cue => cue.StartMs));
    }

    [Fact]
    public void A_speaker_like_last_line_of_longer_text_is_kept()
    {
        var content = "00:01.00\nJames\nHere is the first line.\nLike this\n\n00:03.00\nFrank\nGot it.\nThanks a lot\n";

        var cues = Cues(Convert(content).Content);

        Assert.Equal(
            ["James: Here is the first line. Like this", "Frank: Got it. Thanks a lot"],
            cues.Select(cue => cue.Text));
    }

    [Fact]
    public void A_lone_line_is_text_unless_it_names_a_known_speaker()
    {
        var content =
            Turn("00:01.00", "James", "Hello there.") +
            "00:03.00\nJames\n\n" +
            "00:05.00\nSounds good\n\n" +
            Turn("00:07.00", "Frank", "Goodbye.");

        var segments = Parse(content).Segments;

        Assert.Equal(["Hello there.", "Sounds good", "Goodbye."], segments.Select(segment => segment.Text));
        Assert.Equal(["James", null, "Frank"], segments.Select(segment => segment.Speaker));
    }

    [Fact]
    public void Duration_comes_from_the_latest_turn_when_the_file_ends_out_of_order()
    {
        var transcript = Parse(
            Turn("00:10.00", "James", "Second to last.") +
            Turn("00:09.50", "Frank", "Crosstalk at the very end."));

        Assert.Equal(15_000, transcript.DurationMs);
    }

    [Theory]
    [InlineData(
        "1\n00:00:10,000 --> 00:00:20,000\nFirst\n\n2\n00:00:10,000 --> 00:00:20,000\nSecond\n\n3\n00:00:20,000 --> 00:00:30,000\nThird\n",
        "00:00:10,000 --> 00:00:20,000|00:00:20,000 --> 00:00:25,000|00:00:25,000 --> 00:00:30,000")]
    [InlineData(
        "00:00:10 - 00:00:20 First\n00:00:15 - 00:00:18 Second\n00:00:19 - 00:00:40 Third\n",
        "00:00:10,000 --> 00:00:20,000|00:00:20,000 --> 00:00:25,000|00:00:25,000 --> 00:00:40,000")]
    public void Other_formats_keep_their_overlap_repair(string content, string expectedTimings)
    {
        var timings = TimingLine().Matches(Convert(content).Content).Select(match => match.Value);

        Assert.Equal(expectedTimings.Split('|'), timings);
    }

    private static string Turn(string time, string speaker, string text) => $"{time}\n{speaker}\n{text}\n\n";

    private static Transcript Parse(string content) =>
        new TranscriptParser(new AppSettings()).ParseContent("episode.txt", content);

    private static SrtConverter.SrtConversionResult Convert(string content) =>
        new SrtConverter().ConvertToSrt(Parse(content));

    private static List<Cue> Cues(string srt) =>
        srt.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(block => block.Split('\n', 3))
            .Select(parts =>
            {
                var times = parts[1].Split(" --> ");
                return new Cue(ToMs(times[0]), ToMs(times[1]), parts[2].Trim());
            })
            .ToList();

    private static long ToMs(string time) =>
        (long)TimeSpan.ParseExact(time, @"hh\:mm\:ss\,fff", null).TotalMilliseconds;

    private sealed record Cue(long StartMs, long EndMs, string Text);

    [GeneratedRegex(@"\d{2}:\d{2}:\d{2},\d{3} --> \d{2}:\d{2}:\d{2},\d{3}")]
    private static partial Regex TimingLine();
}
