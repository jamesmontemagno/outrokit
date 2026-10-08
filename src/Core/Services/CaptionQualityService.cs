using System.Globalization;
using System.Text.RegularExpressions;
using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Checks caption timing and layout against practical readability thresholds before burning.
/// </summary>
public sealed partial class CaptionQualityService
{
    public const int MinimumDurationMs = 700;
    public const int MaximumLineLength = 42;
    public const int MaximumLines = 2;
    public const double MaximumCharactersPerSecond = 20;

    private static readonly Regex SrtTimingRegex = new(
        @"^(?<start>\d{2,}:\d{2}:\d{2}[,.]\d{3})\s*-->\s*(?<end>\d{2,}:\d{2}:\d{2}[,.]\d{3})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex VttTimingRegex = new(
        @"^(?<start>\d{1,2}:\d{2}(?::\d{2})?\.\d{3})\s*-->\s*(?<end>\d{1,2}:\d{2}(?::\d{2})?\.\d{3})",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AssDialogueRegex = new(
        @"^Dialogue:\s*(?<fields>.*)$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

    /// <summary>
    /// Analyzes a supported caption file without changing it.
    /// </summary>
    public async Task<CaptionQualityReport> AnalyzeAsync(
        string captionPath,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(captionPath))
        {
            throw new FileNotFoundException("Captions file not found.", captionPath);
        }

        var content = await File.ReadAllTextAsync(captionPath, cancellationToken);
        return Analyze(content, Path.GetExtension(captionPath));
    }

    /// <summary>
    /// Analyzes caption content using the parser associated with the file extension.
    /// </summary>
    public CaptionQualityReport Analyze(string content, string extension)
    {
        var cues = ParseCues(content, extension);
        var canRewrite = CanRewrite(extension);
        var findings = new List<CaptionQualityFinding>();
        CaptionCue? previous = null;

        for (var index = 0; index < cues.Count; index++)
        {
            var cue = cues[index];
            var cueNumber = index + 1;
            if (cue.Start is null || cue.End is null || cue.End <= cue.Start)
            {
                findings.Add(new(
                    cueNumber,
                    CaptionQualitySeverity.Error,
                    "Invalid timing",
                    "The cue does not have a valid start and end time."));
                continue;
            }

            if (previous is not null && cue.Start < previous.End)
            {
                var canTrimPrevious = canRewrite
                    && previous.Start is not null
                    && cue.Start - previous.Start >= TimeSpan.FromMilliseconds(MinimumDurationMs);
                findings.Add(new(
                    cueNumber,
                    CaptionQualitySeverity.Warning,
                    "Overlapping cues",
                    $"Starts at {FormatTime(cue.Start.Value)} before cue {cueNumber - 1} ends at {FormatTime(previous.End!.Value)}.",
                    canTrimPrevious));
            }

            var cleanedText = CleanText(cue.Text);
            if (cleanedText.Length == 0)
            {
                findings.Add(new(
                    cueNumber,
                    CaptionQualitySeverity.Warning,
                    "Empty cue",
                    "This cue has no caption text.",
                    canRewrite));
            }
            else
            {
                if (!string.Equals(cleanedText, cue.Text, StringComparison.Ordinal))
                {
                    findings.Add(new(
                        cueNumber,
                        CaptionQualitySeverity.Warning,
                        "Whitespace",
                        "Extra spaces or blank lines can be cleaned up.",
                        canRewrite));
                }

                var lines = cleanedText.Split('\n');
                var longestLine = lines.Max(line => line.Length);
                var rewrapped = WrapCueText(cleanedText);
                var splitParts = rewrapped is null ? SplitCueText(cleanedText) : null;
                var canSplit = splitParts is not null
                    && SplitTiming(cue.Start.Value, cue.End.Value, splitParts)
                        .All(timing => timing.End - timing.Start >= TimeSpan.FromMilliseconds(MinimumDurationMs));
                var canFixLayout = canRewrite && (rewrapped is not null || canSplit);
                if (longestLine > MaximumLineLength)
                {
                    findings.Add(new(
                        cueNumber,
                        CaptionQualitySeverity.Warning,
                        "Long line",
                        $"The longest line is {longestLine} characters; aim for {MaximumLineLength} or fewer.",
                        canFixLayout));
                }

                if (lines.Length > MaximumLines)
                {
                    findings.Add(new(
                        cueNumber,
                        CaptionQualitySeverity.Warning,
                        "Too many lines",
                        $"{lines.Length} lines of text; most players show {MaximumLines} comfortably.",
                        canFixLayout));
                }

                var duration = cue.End.Value - cue.Start.Value;
                var nextStart = index + 1 < cues.Count ? cues[index + 1].Start : null;
                var requiredReadingDuration = TimeSpan.FromSeconds(
                    CountCharacters(cleanedText) / MaximumCharactersPerSecond);
                var targetDuration = duration;
                if (duration < TimeSpan.FromMilliseconds(MinimumDurationMs))
                {
                    targetDuration = TimeSpan.FromMilliseconds(1000);
                }
                if (requiredReadingDuration > targetDuration)
                {
                    targetDuration = requiredReadingDuration;
                }
                var targetEnd = cue.Start.Value + targetDuration;
                var canExtend = canRewrite && (nextStart is null || targetEnd <= nextStart);

                if (duration < TimeSpan.FromMilliseconds(MinimumDurationMs))
                {
                    findings.Add(new(
                        cueNumber,
                        CaptionQualitySeverity.Warning,
                        "Short duration",
                        $"Only on screen for {duration.TotalMilliseconds:0} ms; aim for at least {MinimumDurationMs} ms.",
                        canExtend));
                }

                var charactersPerSecond = CountCharacters(cleanedText) / duration.TotalSeconds;
                if (charactersPerSecond > MaximumCharactersPerSecond)
                {
                    findings.Add(new(
                        cueNumber,
                        CaptionQualitySeverity.Warning,
                        "Reading speed",
                        $"{charactersPerSecond.ToString("0.0", CultureInfo.InvariantCulture)} characters per second; {MaximumCharactersPerSecond:0} or fewer is more comfortable.",
                        canExtend));
                }
            }

            previous = cue;
        }

        return new CaptionQualityReport(cues.Count, findings);
    }

    /// <summary>
    /// Applies deterministic fixes to SRT or WebVTT content and returns a corrected copy.
    /// </summary>
    public CaptionQualityFixResult ApplySafeFixes(string content, string extension)
    {
        if (!CanRewrite(extension))
        {
            return new(content, 0, Analyze(content, extension));
        }

        var document = ParseEditableDocument(content, extension);
        var cues = document.Blocks.Where(block => block.Cue is not null).Select(block => block.Cue!).ToList();
        var applied = 0;

        foreach (var cue in cues.Where(cue => CleanText(cue.Text).Length == 0).ToList())
        {
            cue.Removed = true;
            applied++;
        }

        cues = cues.Where(cue => !cue.Removed).ToList();
        foreach (var cue in cues)
        {
            var cleaned = CleanText(cue.Text);
            if (!string.Equals(cleaned, cue.Text, StringComparison.Ordinal))
            {
                cue.Text = cleaned;
                applied++;
            }
        }

        for (var index = 1; index < cues.Count; index++)
        {
            var previous = cues[index - 1];
            var current = cues[index];
            if (current.Start < previous.End
                && current.Start - previous.Start >= TimeSpan.FromMilliseconds(MinimumDurationMs))
            {
                previous.End = current.Start;
                applied++;
            }
        }

        foreach (var cue in cues.ToList())
        {
            var lines = cue.Text.Split('\n');
            if (lines.Length <= MaximumLines && lines.All(line => line.Length <= MaximumLineLength))
            {
                continue;
            }

            var wrapped = WrapCueText(cue.Text);
            if (wrapped is not null && !string.Equals(wrapped, cue.Text, StringComparison.Ordinal))
            {
                cue.Text = wrapped;
                applied++;
                continue;
            }

            var parts = SplitCueText(cue.Text);
            if (parts is null)
            {
                continue;
            }

            var timings = SplitTiming(cue.Start, cue.End, parts);
            if (timings.Min(timing => timing.End - timing.Start) < TimeSpan.FromMilliseconds(MinimumDurationMs))
            {
                continue;
            }

            var blockIndex = document.Blocks.FindIndex(block => ReferenceEquals(block.Cue, cue));
            cue.Start = timings[0].Start;
            cue.End = timings[0].End;
            cue.Text = parts[0];
            for (var part = 1; part < parts.Count; part++)
            {
                document.Blocks.Insert(
                    blockIndex + part,
                    new(
                        null,
                        new(
                            timings[part].Start,
                            timings[part].End,
                            parts[part],
                            identifier: null,
                            cue.Settings)));
            }
            applied++;
        }

        cues = document.Blocks
            .Where(block => block.Cue is { Removed: false })
            .Select(block => block.Cue!)
            .ToList();
        for (var index = 0; index < cues.Count; index++)
        {
            var cue = cues[index];
            var duration = cue.End - cue.Start;
            if (duration <= TimeSpan.Zero || cue.Text.Length == 0)
            {
                continue;
            }

            var targetDuration = duration;
            if (duration < TimeSpan.FromMilliseconds(MinimumDurationMs))
            {
                targetDuration = TimeSpan.FromMilliseconds(1000);
            }

            var readingDuration = TimeSpan.FromSeconds(CountCharacters(cue.Text) / MaximumCharactersPerSecond);
            if (readingDuration > targetDuration)
            {
                targetDuration = readingDuration;
            }

            if (targetDuration <= duration)
            {
                continue;
            }

            var targetEnd = cue.Start + targetDuration;
            var nextStart = index + 1 < cues.Count ? cues[index + 1].Start : (TimeSpan?)null;
            if (nextStart is null || targetEnd <= nextStart)
            {
                cue.End = targetEnd;
                applied++;
            }
        }

        var fixedContent = Serialize(document);
        return new(fixedContent, applied, Analyze(fixedContent, extension));
    }

    private static List<CaptionCue> ParseCues(string content, string extension)
    {
        var normalized = content.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n');
        return extension.ToLowerInvariant() switch
        {
            ".ass" or ".ssa" => ParseAss(normalized),
            ".vtt" => ParseTimedBlocks(normalized, VttTimingRegex, skipFirstHeader: true),
            _ => ParseTimedBlocks(normalized, SrtTimingRegex, skipFirstHeader: false)
        };
    }

    private static List<CaptionCue> ParseTimedBlocks(string content, Regex timingRegex, bool skipFirstHeader)
    {
        var cues = new List<CaptionCue>();
        var blocks = Regex.Split(content.Trim(), @"\n\s*\n");
        foreach (var block in blocks)
        {
            var lines = block.Split('\n');
            if (skipFirstHeader && lines.Length > 0 && lines[0].Trim().Equals("WEBVTT", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var timingIndex = Array.FindIndex(
                lines,
                line => timingRegex.IsMatch(line.Trim()) || line.Contains("-->", StringComparison.Ordinal));
            if (timingIndex < 0)
            {
                continue;
            }

            var match = timingRegex.Match(lines[timingIndex].Trim());
            if (!match.Success)
            {
                cues.Add(new(null, null, string.Join('\n', lines[(timingIndex + 1)..])));
                continue;
            }
            var text = string.Join('\n', lines[(timingIndex + 1)..]);
            cues.Add(new(
                ParseTime(match.Groups["start"].Value),
                ParseTime(match.Groups["end"].Value),
                text));
        }

        return cues;
    }

    private static EditableCaptionDocument ParseEditableDocument(string content, string extension)
    {
        var normalized = content.TrimStart('\uFEFF').Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        var isVtt = extension.Equals(".vtt", StringComparison.OrdinalIgnoreCase);
        var timingRegex = isVtt ? VttTimingRegex : SrtTimingRegex;
        var blocks = new List<EditableCaptionBlock>();

        foreach (var blockText in Regex.Split(normalized, @"\n\s*\n"))
        {
            var lines = blockText.Split('\n');
            var timingIndex = Array.FindIndex(lines, line => timingRegex.IsMatch(line.Trim()));
            if (timingIndex < 0)
            {
                blocks.Add(new(blockText, null));
                continue;
            }

            var match = timingRegex.Match(lines[timingIndex].Trim());
            var endToken = match.Groups["end"].Value;
            var endIndex = lines[timingIndex].IndexOf(endToken, StringComparison.Ordinal);
            var settings = endIndex >= 0
                ? lines[timingIndex][(endIndex + endToken.Length)..].Trim()
                : string.Empty;
            var identifier = timingIndex > 0 ? string.Join('\n', lines[..timingIndex]) : null;
            blocks.Add(new(
                null,
                new EditableCaptionCue(
                    ParseTime(match.Groups["start"].Value)!.Value,
                    ParseTime(match.Groups["end"].Value)!.Value,
                    string.Join('\n', lines[(timingIndex + 1)..]),
                    identifier,
                    settings)));
        }

        return new(isVtt, blocks);
    }

    private static List<CaptionCue> ParseAss(string content)
    {
        var cues = new List<CaptionCue>();
        foreach (var line in content.Split('\n'))
        {
            var match = AssDialogueRegex.Match(line.Trim());
            if (!match.Success)
            {
                continue;
            }

            var fields = match.Groups["fields"].Value.Split(',', 10);
            if (fields.Length < 10)
            {
                cues.Add(new(null, null, string.Empty));
                continue;
            }

            cues.Add(new(ParseAssTime(fields[1]), ParseAssTime(fields[2]), StripAssTags(fields[9])));
        }

        return cues;
    }

    private static TimeSpan? ParseTime(string value)
    {
        var normalized = value.Replace(',', '.');
        var parts = normalized.Split(':');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || minutes is < 0 or > 59
            || seconds is < 0 or >= 60)
        {
            return null;
        }

        return TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }

    private static TimeSpan? ParseAssTime(string value)
    {
        var parts = value.Trim().Split(':');
        if (parts.Length != 3
            || !int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var hours)
            || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes)
            || !double.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
            || minutes is < 0 or > 59
            || seconds is < 0 or >= 60)
        {
            return null;
        }

        return TimeSpan.FromHours(hours) + TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(seconds);
    }

    private static string CleanText(string text) =>
        string.Join('\n', text
            .Split('\n')
            .Select(line => string.Join(' ', line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)).Trim())
            .Where(line => line.Length > 0));

    private static int CountCharacters(string text) =>
        text.Count(character => !char.IsWhiteSpace(character));

    private static string? WrapCueText(string text)
    {
        var words = CleanText(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0 || words.Any(word => word.Length > MaximumLineLength))
        {
            return null;
        }

        for (var lineCount = 1; lineCount <= MaximumLines; lineCount++)
        {
            var lines = BalanceWords(words, lineCount);
            if (lines.All(line => line.Length <= MaximumLineLength))
            {
                return string.Join('\n', lines);
            }
        }

        return null;
    }

    private static IReadOnlyList<string>? SplitCueText(string text)
    {
        var words = CleanText(text).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length < 2 || words.Any(word => word.Length > MaximumLineLength))
        {
            return null;
        }

        var minimumParts = Math.Max(
            2,
            (int)Math.Ceiling(string.Join(' ', words).Length / (double)(MaximumLineLength * MaximumLines)));
        for (var partCount = minimumParts; partCount <= Math.Min(64, words.Length); partCount++)
        {
            var groups = BalanceWordGroups(words, partCount);
            var parts = groups.Select(group => WrapCueText(string.Join(' ', group))).ToList();
            if (parts.All(part => part is not null))
            {
                return parts.Select(part => part!).ToList();
            }
        }

        return null;
    }

    private static string[] BalanceWords(string[] words, int lineCount) =>
        BalanceWordGroups(words, lineCount).Select(group => string.Join(' ', group)).ToArray();

    private static List<string[]> BalanceWordGroups(string[] words, int groupCount)
    {
        if (groupCount <= 1 || words.Length <= groupCount)
        {
            return groupCount <= 1 ? [words] : words.Select(word => new[] { word }).ToList();
        }

        var groups = new List<string[]>();
        var cursor = 0;
        for (var group = 1; group < groupCount; group++)
        {
            var remainingLength = string.Join(' ', words[cursor..]).Length;
            var remainingGroups = groupCount - group + 1;
            var target = remainingLength / (double)remainingGroups;
            var best = cursor + 1;
            var bestScore = double.PositiveInfinity;
            var length = -1;

            for (var index = cursor; index < words.Length - (groupCount - group); index++)
            {
                length += words[index].Length + 1;
                var punctuationBonus = words[index].EndsWithAny('.', '!', '?') ? 6
                    : words[index].EndsWithAny(',', ';', ':') ? 3
                    : 0;
                var score = Math.Abs(length - target) - punctuationBonus;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = index + 1;
                }
            }

            groups.Add(words[cursor..best]);
            cursor = best;
        }

        groups.Add(words[cursor..]);
        return groups;
    }

    private static List<(TimeSpan Start, TimeSpan End)> SplitTiming(
        TimeSpan start,
        TimeSpan end,
        IReadOnlyList<string> parts)
    {
        var weights = parts.Select(part => Math.Max(1, CountCharacters(part))).ToArray();
        var totalWeight = weights.Sum();
        var durationTicks = Math.Max(0, (end - start).Ticks);
        var cursor = start;
        var timings = new List<(TimeSpan Start, TimeSpan End)>();
        for (var index = 0; index < parts.Count; index++)
        {
            var next = index == parts.Count - 1
                ? end
                : cursor + TimeSpan.FromTicks((long)Math.Round(durationTicks * (weights[index] / (double)totalWeight)));
            timings.Add((cursor, next));
            cursor = next;
        }

        return timings;
    }

    private static string Serialize(EditableCaptionDocument document)
    {
        var blocks = new List<string>();
        var sequence = 1;
        foreach (var block in document.Blocks)
        {
            if (block.Cue is null)
            {
                blocks.Add(block.Raw!);
                continue;
            }

            if (block.Cue.Removed)
            {
                continue;
            }

            var cue = block.Cue;
            var start = FormatTimestamp(cue.Start, document.IsVtt);
            var end = FormatTimestamp(cue.End, document.IsVtt);
            var settings = cue.Settings.Length > 0 ? $" {cue.Settings}" : string.Empty;
            if (document.IsVtt)
            {
                var identifier = string.IsNullOrWhiteSpace(cue.Identifier) ? string.Empty : $"{cue.Identifier}\n";
                blocks.Add($"{identifier}{start} --> {end}{settings}\n{cue.Text}");
            }
            else
            {
                blocks.Add($"{sequence++}\n{start} --> {end}\n{cue.Text}");
            }
        }

        return string.Join("\n\n", blocks).TrimEnd() + Environment.NewLine;
    }

    private static string FormatTimestamp(TimeSpan time, bool isVtt) =>
        time.ToString(isVtt ? @"hh\:mm\:ss\.fff" : @"hh\:mm\:ss\,fff", CultureInfo.InvariantCulture);

    private static bool CanRewrite(string extension) =>
        extension.Equals(".srt", StringComparison.OrdinalIgnoreCase)
        || extension.Equals(".vtt", StringComparison.OrdinalIgnoreCase);

    private static string StripAssTags(string text) =>
        Regex.Replace(text.Replace("\\N", "\n"), @"\{[^}]*\}", string.Empty);

    private static string FormatTime(TimeSpan time) =>
        time.ToString(@"hh\:mm\:ss\.fff", CultureInfo.InvariantCulture);

    private sealed record CaptionCue(TimeSpan? Start, TimeSpan? End, string Text);

    private sealed record EditableCaptionDocument(bool IsVtt, List<EditableCaptionBlock> Blocks);

    private sealed record EditableCaptionBlock(string? Raw, EditableCaptionCue? Cue);

    private sealed class EditableCaptionCue(
        TimeSpan start,
        TimeSpan end,
        string text,
        string? identifier,
        string settings)
    {
        public TimeSpan Start { get; set; } = start;
        public TimeSpan End { get; set; } = end;
        public string Text { get; set; } = text;
        public string? Identifier { get; } = identifier;
        public string Settings { get; } = settings;
        public bool Removed { get; set; }
    }
}

file static class CaptionQualityTextExtensions
{
    public static bool EndsWithAny(this string value, params char[] endings) =>
        value.Length > 0 && endings.Contains(value[^1]);
}
