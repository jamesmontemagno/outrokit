using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

public sealed class CaptionQualityServiceTests
{
    private readonly CaptionQualityService _service = new();

    [Fact]
    public void Reports_a_clean_srt_file()
    {
        var report = _service.Analyze(
            """
            1
            00:00:00,000 --> 00:00:02,000
            Welcome to the show.

            2
            00:00:02,500 --> 00:00:05,000
            Thanks for listening.
            """,
            ".srt");

        Assert.Equal(2, report.CueCount);
        Assert.Empty(report.Findings);
    }

    [Fact]
    public void Reports_captionstack_readability_findings()
    {
        var report = _service.Analyze(
            """
            1
            00:00:00,000 --> 00:00:00,500
            This cue is much too long for one line and should be reported.

            2
            00:00:00,400 --> 00:00:01,000
            Too fast
            """,
            ".srt");

        Assert.Equal(2, report.CueCount);
        Assert.Contains(report.Findings, finding => finding.Check == "Overlapping cues");
        Assert.Contains(report.Findings, finding => finding.Check == "Short duration");
        Assert.Contains(report.Findings, finding => finding.Check == "Long line");
        Assert.Contains(report.Findings, finding => finding.Check == "Reading speed");
        Assert.Equal(0, report.ErrorCount);
        Assert.True(report.WarningCount >= 4);
    }

    [Fact]
    public void Parses_vtt_and_ass_caption_text()
    {
        var vtt = _service.Analyze(
            """
            WEBVTT

            00:00:01.000 --> 00:00:03.000
            VTT caption
            """,
            ".vtt");
        var ass = _service.Analyze(
            """
            [Events]
            Format: Layer, Start, End, Style, Name, MarginL, MarginR, MarginV, Effect, Text
            Dialogue: 0,0:00:01.00,0:00:03.00,Default,,0,0,0,,ASS {\i1}caption
            """,
            ".ass");

        Assert.Equal(1, vtt.CueCount);
        Assert.Empty(vtt.Findings);
        Assert.Equal(1, ass.CueCount);
        Assert.Empty(ass.Findings);
    }

    [Fact]
    public void Reports_invalid_ass_timing_as_an_error()
    {
        var report = _service.Analyze(
            "Dialogue: 0,not-a-time,0:00:03.00,Default,,0,0,0,,Caption",
            ".ass");

        var finding = Assert.Single(report.Findings);
        Assert.Equal(CaptionQualitySeverity.Error, finding.Severity);
        Assert.Equal("Invalid timing", finding.Check);
    }

    [Fact]
    public void Applies_safe_srt_fixes_without_touching_unfixable_issues()
    {
        const string content =
            """
            1
            00:00:00,000 --> 00:00:02,000
              This   line has extra spaces and needs balanced wrapping for display.

            2
            00:00:01,800 --> 00:00:02,100

            3
            00:00:03,000 --> 00:00:03,400
            Brief
            """;

        var before = _service.Analyze(content, ".srt");
        var result = _service.ApplySafeFixes(content, ".srt");

        Assert.True(before.FixableCount >= 4);
        Assert.True(result.AppliedFixCount >= 4);
        Assert.DoesNotContain("  This   line", result.Content);
        Assert.DoesNotContain("\n2\n00:00:01,800", result.Content);
        Assert.Contains("00:00:03,000 --> 00:00:04,000", result.Content);
        Assert.DoesNotContain(result.Report.Findings, finding => finding.Check == "Whitespace");
        Assert.DoesNotContain(result.Report.Findings, finding => finding.Check == "Empty cue");
        Assert.DoesNotContain(result.Report.Findings, finding => finding.Check == "Short duration");
    }

    [Fact]
    public void Preserves_webvtt_header_notes_identifiers_and_settings()
    {
        const string content =
            """
            WEBVTT

            NOTE Keep this note

            introduction
            00:00:01.000 --> 00:00:03.000 align:start position:10%
            Text   with extra spaces
            """;

        var result = _service.ApplySafeFixes(content, ".vtt");

        Assert.Equal(1, result.AppliedFixCount);
        Assert.Contains("WEBVTT", result.Content);
        Assert.Contains("NOTE Keep this note", result.Content);
        Assert.Contains("introduction", result.Content);
        Assert.Contains("align:start position:10%", result.Content);
        Assert.Contains("Text with extra spaces", result.Content);
    }

    [Fact]
    public void Does_not_rewrite_styled_caption_formats()
    {
        const string content =
            "Dialogue: 0,0:00:01.00,0:00:01.20,Default,,0,0,0,,  Styled   caption";

        var report = _service.Analyze(content, ".ass");
        var result = _service.ApplySafeFixes(content, ".ass");

        Assert.Equal(0, report.FixableCount);
        Assert.Equal(0, result.AppliedFixCount);
        Assert.Equal(content, result.Content);
    }

    [Fact]
    public void Splits_an_oversized_cue_only_when_each_part_remains_readable()
    {
        const string roomy =
            """
            1
            00:00:00,000 --> 00:00:06,000
            This is a deliberately long caption that cannot fit within two lines, so it should be divided into multiple readable caption cues without changing the words.
            """;
        const string cramped =
            """
            1
            00:00:00,000 --> 00:00:01,000
            This is a deliberately long caption that cannot fit within two lines, but there is not enough time to split it safely.
            """;

        var roomyResult = _service.ApplySafeFixes(roomy, ".srt");
        var crampedResult = _service.ApplySafeFixes(cramped, ".srt");

        Assert.Contains("\n2\n", roomyResult.Content);
        Assert.DoesNotContain(roomyResult.Report.Findings, finding => finding.Check is "Long line" or "Too many lines");
        Assert.DoesNotContain("\n2\n", crampedResult.Content);
        Assert.Contains(crampedResult.Report.Findings, finding => finding.Check == "Long line");
    }

    [Fact]
    public void Reports_malformed_srt_timing_as_an_error()
    {
        var report = _service.Analyze(
            """
            1
            invalid --> 00:00:03,000
            Caption
            """,
            ".srt");

        var finding = Assert.Single(report.Findings);
        Assert.Equal(CaptionQualitySeverity.Error, finding.Severity);
        Assert.False(finding.CanAutoFix);
    }
}
