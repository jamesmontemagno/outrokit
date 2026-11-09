using System.Text;
using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Converts transcripts to valid SRT format and validates SRT files.
/// </summary>
public class SrtConverter
{
    /// <summary>
    /// Result of SRT conversion including the content and any validation errors.
    /// </summary>
    public record SrtConversionResult(string Content, List<string> Errors, bool IsValid);
    
    /// <summary>
    /// Converts a transcript to SRT format.
    /// </summary>
    public SrtConversionResult ConvertToSrt(Transcript transcript)
    {
        var errors = new List<string>();
        var sb = new StringBuilder();
        
        var segments = transcript.Segments;
        
        // Validate and fix segment order
        var sortedSegments = segments.OrderBy(s => s.StartTimeMs).ToList();
        if (!segments.SequenceEqual(sortedSegments))
        {
            errors.Add("Warning: Segments were not in chronological order and have been sorted.");
        }
        
        if (transcript.Format == TranscriptFormat.Zencastr)
        {
            sortedSegments = CombineTurnsSharingAStart(sortedSegments, errors);
        }
        
        long previousEndTime = 0;
        
        for (var i = 0; i < sortedSegments.Count; i++)
        {
            var segment = sortedSegments[i];
            var sequenceNumber = i + 1;
            
            // Validate start time is after previous end time
            if (segment.StartTimeMs < previousEndTime)
            {
                errors.Add($"Warning: Segment {sequenceNumber} overlaps with previous segment. Adjusted start time.");
                segment = segment with { StartTimeMs = previousEndTime };
            }
            
            // Validate end time is after start time
            if (segment.EndTimeMs <= segment.StartTimeMs)
            {
                errors.Add($"Warning: Segment {sequenceNumber} has invalid duration. Adjusted end time.");
                segment = segment with { EndTimeMs = segment.StartTimeMs + 5000 };
            }
            
            // Build SRT entry
            sb.AppendLine(sequenceNumber.ToString());
            sb.AppendLine($"{segment.StartTimeSrt} --> {segment.EndTimeSrt}");
            
            sb.AppendLine(CueText(segment));
            sb.AppendLine(); // Blank line separator
            
            previousEndTime = segment.EndTimeMs;
        }
        
        var content = sb.ToString().TrimEnd() + Environment.NewLine;
        
        return new SrtConversionResult(content, errors, errors.Count == 0 || errors.All(e => e.StartsWith("Warning:")));
    }
    
    /// <summary>
    /// Validates an existing SRT file content.
    /// </summary>
    public List<string> ValidateSrt(string srtContent)
    {
        var errors = new List<string>();
        var lines = srtContent.Split('\n', StringSplitOptions.None);
        
        var expectedSequence = 1;
        var previousEndTimeMs = 0L;
        var i = 0;
        
        while (i < lines.Length)
        {
            // Skip empty lines at the start
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;
            
            if (i >= lines.Length) break;
            
            // Check sequence number
            if (!int.TryParse(lines[i].Trim(), out var sequence))
            {
                errors.Add($"Line {i + 1}: Expected sequence number, got '{lines[i].Trim()}'");
                i++;
                continue;
            }
            
            if (sequence != expectedSequence)
            {
                errors.Add($"Line {i + 1}: Expected sequence {expectedSequence}, got {sequence}");
            }
            expectedSequence = sequence + 1;
            i++;
            
            if (i >= lines.Length)
            {
                errors.Add($"Unexpected end of file after sequence {sequence}");
                break;
            }
            
            // Check timestamp line
            var timestampLine = lines[i].Trim();
            var timestampMatch = System.Text.RegularExpressions.Regex.Match(
                timestampLine,
                @"(\d{2}):(\d{2}):(\d{2}),(\d{3})\s*-->\s*(\d{2}):(\d{2}):(\d{2}),(\d{3})");
            
            if (!timestampMatch.Success)
            {
                // Check for common mistake: using . instead of ,
                if (timestampLine.Contains('.') && timestampLine.Contains("-->"))
                {
                    errors.Add($"Line {i + 1}: Timestamp uses '.' for milliseconds. SRT requires ',' (e.g., 00:00:00,000)");
                }
                else
                {
                    errors.Add($"Line {i + 1}: Invalid timestamp format. Expected 'HH:MM:SS,mmm --> HH:MM:SS,mmm'");
                }
                i++;
                continue;
            }
            
            var startTimeMs = ParseSrtTime(timestampMatch, 1);
            var endTimeMs = ParseSrtTime(timestampMatch, 5);
            
            if (startTimeMs >= endTimeMs)
            {
                errors.Add($"Line {i + 1}: End time must be after start time");
            }
            
            if (startTimeMs < previousEndTimeMs)
            {
                errors.Add($"Line {i + 1}: Subtitle overlaps with previous subtitle");
            }
            
            previousEndTimeMs = endTimeMs;
            i++;
            
            // Check for subtitle text
            var hasText = false;
            while (i < lines.Length && !string.IsNullOrWhiteSpace(lines[i]))
            {
                hasText = true;
                i++;
            }
            
            if (!hasText)
            {
                errors.Add($"Sequence {sequence}: No subtitle text found");
            }
            
            // Skip blank line(s)
            while (i < lines.Length && string.IsNullOrWhiteSpace(lines[i]))
                i++;
        }
        
        return errors;
    }
    
    /// <summary>
    /// Formats chapters for YouTube description.
    /// </summary>
    public string FormatChaptersForYouTube(List<Chapter> chapters)
    {
        var sb = new StringBuilder();
        
        foreach (var chapter in chapters)
        {
            sb.AppendLine($"{chapter.Timestamp} {chapter.Title}");
        }
        
        return sb.ToString().TrimEnd();
    }
    
    /// <summary>
    /// Zencastr turns have no end time of their own, so turns that start together get no time
    /// between them. One cue per start keeps every line on its real timestamp, where separate
    /// cues would have to overlap or start late.
    /// </summary>
    private static List<TranscriptSegment> CombineTurnsSharingAStart(List<TranscriptSegment> sortedSegments, List<string> errors)
    {
        var combined = new List<TranscriptSegment>();
        foreach (var turns in sortedSegments.GroupBy(segment => segment.StartTimeMs))
        {
            if (turns.Count() == 1)
            {
                combined.Add(turns.First());
                continue;
            }
            
            combined.Add(new TranscriptSegment
            {
                StartTimeMs = turns.Key,
                EndTimeMs = turns.Max(turn => turn.EndTimeMs),
                Text = string.Join("\n", turns.Select(CueText))
            });
            errors.Add($"Warning: {turns.Count()} turns start at {combined[^1].StartTimeSrt} and were combined into cue {combined.Count}.");
        }
        
        return combined;
    }
    
    private static string CueText(TranscriptSegment segment) =>
        string.IsNullOrEmpty(segment.Speaker)
            ? segment.Text
            : $"{segment.Speaker}: {segment.Text}";
    
    private static long ParseSrtTime(System.Text.RegularExpressions.Match match, int startGroup)
    {
        var hours = int.Parse(match.Groups[startGroup].Value);
        var minutes = int.Parse(match.Groups[startGroup + 1].Value);
        var seconds = int.Parse(match.Groups[startGroup + 2].Value);
        var milliseconds = int.Parse(match.Groups[startGroup + 3].Value);
        
        return (hours * 3600000L) + (minutes * 60000L) + (seconds * 1000L) + milliseconds;
    }
}
