using System.Text.RegularExpressions;
using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Parses transcript files in various formats into a unified Transcript model.
/// </summary>
public partial class TranscriptParser
{
    private readonly AppSettings _settings;
    
    // Zencastr format: MM:SS.ss or HH:MM:SS.ss (centiseconds)
    [GeneratedRegex(@"^(\d{1,2}):(\d{2})(?::(\d{2}))?\.(\d{2})$")]
    private static partial Regex ZencastrTimestampRegex();
    
    // Time range format: HH:MM:SS - HH:MM:SS or with arrows
    [GeneratedRegex(@"(\d{1,2}):(\d{2}):(\d{2})\s*[-–>]+\s*(\d{1,2}):(\d{2}):(\d{2})")]
    private static partial Regex TimeRangeRegex();
    
    // SRT timestamp format: HH:MM:SS,mmm --> HH:MM:SS,mmm
    [GeneratedRegex(@"(\d{2}):(\d{2}):(\d{2}),(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2}),(\d{3})")]
    private static partial Regex SrtTimestampRegex();
    
    // Speaker detection: short line, capitalized, no punctuation at start
    [GeneratedRegex(@"^[A-Z][a-zA-Z\s]{0,30}$")]
    private static partial Regex SpeakerRegex();
    
    public TranscriptParser(AppSettings settings)
    {
        _settings = settings;
    }
    
    /// <summary>
    /// Parses a transcript file and returns a Transcript object.
    /// Supports Zencastr, time-range, SRT formats, or falls back to plain text.
    /// </summary>
    public async Task<Transcript> ParseAsync(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Transcript file not found: {filePath}");
        
        var rawContent = await File.ReadAllTextAsync(filePath);
        return ParseContent(filePath, rawContent);
    }
    
    /// <summary>
    /// Parses transcript content from a string (useful for Blazor file uploads).
    /// </summary>
    public Transcript ParseContent(string fileName, string content)
    {
        var lines = content.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        var format = DetectFormat(lines);
        
        // Try to parse structured formats, fall back to plain text
        List<TranscriptSegment> segments = [];
        
        if (format != TranscriptFormat.PlainText)
        {
            segments = format switch
            {
                TranscriptFormat.Zencastr => ParseZencastr([.. lines]),
                TranscriptFormat.TimeRange => ParseTimeRange([.. lines]),
                TranscriptFormat.Srt => ParseSrt([.. lines]),
                _ => []
            };
        }
        
        return new Transcript
        {
            FilePath = fileName,
            Format = format,
            RawContent = content,
            Segments = segments
        };
    }
    
    /// <summary>
    /// Detects the format of the transcript by scanning the first 50 lines.
    /// Returns PlainText if no structured format is detected.
    /// </summary>
    private TranscriptFormat DetectFormat(string[] lines)
    {
        var linesToCheck = lines.Take(50).ToArray();
        
        // Check for SRT format first (most specific)
        if (linesToCheck.Any(l => SrtTimestampRegex().IsMatch(l)))
            return TranscriptFormat.Srt;
        
        // Check for Zencastr format
        foreach (var line in linesToCheck)
        {
            var trimmed = line.Trim();
            if (ZencastrTimestampRegex().IsMatch(trimmed))
            {
                return TranscriptFormat.Zencastr;
            }
        }
        
        // Check for time range format
        if (linesToCheck.Any(l => TimeRangeRegex().IsMatch(l)))
            return TranscriptFormat.TimeRange;
        
        // Default to plain text - just send the whole thing to the AI
        return TranscriptFormat.PlainText;
    }
    
    /// <summary>
    /// Parses Zencastr format transcripts.
    /// Format:
    /// 00:00.29
    /// James
    /// Welcome back everyone...
    /// </summary>
    private List<TranscriptSegment> ParseZencastr(string[] lines)
    {
        var turns = new List<(long StartTimeMs, List<string> Lines)>();
        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            var match = ZencastrTimestampRegex().Match(line);
            if (match.Success)
                turns.Add((ParseZencastrTimestamp(match), []));
            else if (turns.Count > 0 && line.Length > 0)
                turns[^1].Lines.Add(line);
        }
        
        // A lone speaker-like line is ambiguous, so it only counts as a label
        // (a turn with no text) if that name introduces text elsewhere.
        var knownSpeakers = turns
            .Where(turn => turn.Lines.Count > 1 && IsSpeakerLine(turn.Lines[0]))
            .Select(turn => turn.Lines[0])
            .ToHashSet();
        
        var segments = new List<TranscriptSegment>();
        foreach (var (startTimeMs, turnLines) in turns.Where(turn => turn.Lines.Count > 0))
        {
            var hasSpeaker = IsSpeakerLine(turnLines[0])
                && (turnLines.Count > 1 || knownSpeakers.Contains(turnLines[0]));
            var textLines = hasSpeaker ? turnLines.Skip(1).ToList() : turnLines;
            if (textLines.Count == 0)
                continue;
            
            segments.Add(new TranscriptSegment
            {
                StartTimeMs = startTimeMs,
                EndTimeMs = startTimeMs + _settings.DefaultSegmentDurationMs,
                Speaker = hasSpeaker ? turnLines[0] : null,
                Text = string.Join(" ", textLines)
            });
        }
        
        // Crosstalk can list a turn before one that started earlier, so a turn ends at the next
        // later start in time rather than at the next turn in the file. Without that, turns get
        // end times before their own starts, and repairing those delays every caption after them.
        var distinctStarts = segments.Select(segment => segment.StartTimeMs).Distinct().Order().ToList();
        var nextStarts = distinctStarts.Zip(distinctStarts.Skip(1)).ToDictionary(pair => pair.First, pair => pair.Second);
        
        return segments
            .Select(segment => segment with { EndTimeMs = nextStarts.GetValueOrDefault(segment.StartTimeMs, segment.EndTimeMs) })
            .ToList();
    }
    
    private static bool IsSpeakerLine(string line) => SpeakerRegex().IsMatch(line) && line.Length < 30;
    
    /// <summary>
    /// Parses time range format transcripts.
    /// Format: 00:00:00 - 00:00:05 Welcome to the podcast
    /// </summary>
    private List<TranscriptSegment> ParseTimeRange(string[] lines)
    {
        var segments = new List<TranscriptSegment>();
        
        foreach (var line in lines)
        {
            var match = TimeRangeRegex().Match(line);
            if (!match.Success) continue;
            
            var startTimeMs = ParseTimeRangeTimestamp(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value));
            
            var endTimeMs = ParseTimeRangeTimestamp(
                int.Parse(match.Groups[4].Value),
                int.Parse(match.Groups[5].Value),
                int.Parse(match.Groups[6].Value));
            
            // Text is everything after the timestamp
            var text = line[(match.Index + match.Length)..].Trim();
            
            // Try to extract speaker from text (Speaker: text)
            string? speaker = null;
            var colonIndex = text.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 30)
            {
                var potentialSpeaker = text[..colonIndex];
                if (SpeakerRegex().IsMatch(potentialSpeaker))
                {
                    speaker = potentialSpeaker;
                    text = text[(colonIndex + 1)..].Trim();
                }
            }
            
            if (!string.IsNullOrEmpty(text))
            {
                segments.Add(new TranscriptSegment
                {
                    StartTimeMs = startTimeMs,
                    EndTimeMs = endTimeMs,
                    Speaker = speaker,
                    Text = text
                });
            }
        }
        
        return segments;
    }
    
    /// <summary>
    /// Parses SRT format transcripts.
    /// </summary>
    private List<TranscriptSegment> ParseSrt(string[] lines)
    {
        var segments = new List<TranscriptSegment>();
        var i = 0;
        
        while (i < lines.Length)
        {
            // Look for timestamp line; sequence numbers and stray lines are skipped
            var match = SrtTimestampRegex().Match(lines[i]);
            if (!match.Success)
            {
                i++;
                continue;
            }
            
            var startTimeMs = ParseSrtTimestamp(
                int.Parse(match.Groups[1].Value),
                int.Parse(match.Groups[2].Value),
                int.Parse(match.Groups[3].Value),
                int.Parse(match.Groups[4].Value));
            
            var endTimeMs = ParseSrtTimestamp(
                int.Parse(match.Groups[5].Value),
                int.Parse(match.Groups[6].Value),
                int.Parse(match.Groups[7].Value),
                int.Parse(match.Groups[8].Value));
            
            i++;
            
            // Collect text lines until the next cue starts
            var textLines = new List<string>();
            while (i < lines.Length && !IsSrtCueStart(lines, i))
            {
                if (!string.IsNullOrWhiteSpace(lines[i]))
                {
                    textLines.Add(lines[i].Trim());
                }
                i++;
            }
            
            var text = string.Join(" ", textLines);
            
            // Try to extract speaker from text
            string? speaker = null;
            var colonIndex = text.IndexOf(':');
            if (colonIndex > 0 && colonIndex < 30)
            {
                var potentialSpeaker = text[..colonIndex];
                if (SpeakerRegex().IsMatch(potentialSpeaker))
                {
                    speaker = potentialSpeaker;
                    text = text[(colonIndex + 1)..].Trim();
                }
            }
            
            if (!string.IsNullOrEmpty(text))
            {
                segments.Add(new TranscriptSegment
                {
                    StartTimeMs = startTimeMs,
                    EndTimeMs = endTimeMs,
                    Speaker = speaker,
                    Text = text
                });
            }
        }
        
        return segments;
    }

    /// <summary>
    /// Whether the line begins a new SRT cue: a timestamp line, or the sequence number before one.
    /// Blank lines are removed before parsing, so they cannot be used as the cue separator.
    /// </summary>
    private static bool IsSrtCueStart(string[] lines, int index)
    {
        if (SrtTimestampRegex().IsMatch(lines[index]))
        {
            return true;
        }

        return index + 1 < lines.Length
            && int.TryParse(lines[index].Trim(), out _)
            && SrtTimestampRegex().IsMatch(lines[index + 1]);
    }
    
    private static long ParseZencastrTimestamp(Match match)
    {
        // Groups: 1=first number, 2=second number, 3=optional third number, 4=centiseconds
        var g1 = int.Parse(match.Groups[1].Value);
        var g2 = int.Parse(match.Groups[2].Value);
        var g3 = match.Groups[3].Success ? int.Parse(match.Groups[3].Value) : -1;
        var centiseconds = int.Parse(match.Groups[4].Value);
        
        int hours, minutes, seconds;
        if (g3 >= 0)
        {
            // HH:MM:SS.cc format
            hours = g1;
            minutes = g2;
            seconds = g3;
        }
        else
        {
            // MM:SS.cc format
            hours = 0;
            minutes = g1;
            seconds = g2;
        }
        
        return (hours * 3600000L) + (minutes * 60000L) + (seconds * 1000L) + (centiseconds * 10L);
    }
    
    private static long ParseTimeRangeTimestamp(int hours, int minutes, int seconds)
    {
        return (hours * 3600000L) + (minutes * 60000L) + (seconds * 1000L);
    }
    
    private static long ParseSrtTimestamp(int hours, int minutes, int seconds, int milliseconds)
    {
        return (hours * 3600000L) + (minutes * 60000L) + (seconds * 1000L) + milliseconds;
    }
}
