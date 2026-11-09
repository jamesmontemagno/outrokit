namespace PodcastMetadataGenerator.Core.Models;

/// <summary>
/// Detected format of the transcript file.
/// </summary>
public enum TranscriptFormat
{
    /// <summary>Plain text without timestamps (loaded as raw content).</summary>
    PlainText,
    /// <summary>Zencastr format with timestamps and speakers.</summary>
    Zencastr,
    /// <summary>Time range format (HH:MM:SS - HH:MM:SS).</summary>
    TimeRange,
    /// <summary>SRT subtitle format.</summary>
    Srt
}

/// <summary>
/// Represents a loaded transcript - either parsed with segments or as raw text.
/// </summary>
public class Transcript
{
    /// <summary>
    /// The original file path of the transcript.
    /// </summary>
    public required string FilePath { get; init; }
    
    /// <summary>
    /// The detected format of the transcript.
    /// </summary>
    public TranscriptFormat Format { get; init; }
    
    /// <summary>
    /// Raw text content of the transcript (always available).
    /// </summary>
    public string RawContent { get; init; } = string.Empty;
    
    /// <summary>
    /// Parsed segments (may be empty for PlainText format).
    /// </summary>
    public List<TranscriptSegment> Segments { get; init; } = [];
    
    /// <summary>
    /// Whether the transcript has parsed timestamp segments.
    /// </summary>
    public bool HasTimestamps => Segments.Count > 0;
    
    /// <summary>
    /// Total duration in milliseconds based on the latest segment end time, because the last
    /// segment in the file is not always the last in time. Returns 0 if no segments are parsed.
    /// </summary>
    public long DurationMs => Segments.Count > 0 ? Segments.Max(segment => segment.EndTimeMs) : 0;
    
    /// <summary>
    /// Total duration in seconds.
    /// </summary>
    public double DurationSeconds => DurationMs / 1000.0;
    
    /// <summary>
    /// Total duration in minutes. Estimates based on word count if no timestamps.
    /// </summary>
    public double DurationMinutes => HasTimestamps 
        ? DurationSeconds / 60.0 
        : EstimateDurationMinutes();
    
    /// <summary>
    /// Estimates duration based on word count (~150 words per minute for speech).
    /// </summary>
    private double EstimateDurationMinutes()
    {
        var wordCount = RawContent.Split([' ', '\n', '\r', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        return wordCount / 150.0; // Average speaking rate
    }
    
    /// <summary>
    /// Gets the full transcript text for sending to AI.
    /// </summary>
    public string GetFullText()
    {
        // For plain text, just return raw content
        if (!HasTimestamps)
            return RawContent;
        
        // For parsed transcripts, format with speaker labels
        return string.Join("\n\n", Segments.Select(s => 
            string.IsNullOrEmpty(s.Speaker) 
                ? s.Text 
                : $"{s.Speaker}: {s.Text}"));
    }
    
    /// <summary>
    /// Gets the transcript with timestamps for chapter generation.
    /// Falls back to raw content if no timestamps available.
    /// </summary>
    public string GetTextWithTimestamps()
    {
        if (!HasTimestamps)
            return RawContent;
        
        return string.Join("\n", Segments.Select(s =>
            $"[{s.StartTimeYouTube} - {FormatTimeForYouTube(s.EndTimeMs)}] " +
            (string.IsNullOrEmpty(s.Speaker) ? s.Text : $"{s.Speaker}: {s.Text}")));
    }
    
    private static string FormatTimeForYouTube(long totalMs)
    {
        var hours = totalMs / 3600000;
        var minutes = (totalMs % 3600000) / 60000;
        var seconds = (totalMs % 60000) / 1000;
        
        if (hours > 0)
            return $"{hours:D2}:{minutes:D2}:{seconds:D2}";
        return $"{minutes:D2}:{seconds:D2}";
    }
}
