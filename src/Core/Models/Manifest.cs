using System.Text.Json.Serialization;

namespace PodcastMetadataGenerator.Core.Models;

/// <summary>
/// JSON manifest containing all generated metadata.
/// </summary>
public class Manifest
{
    /// <summary>
    /// Path to the original transcript file.
    /// </summary>
    [JsonPropertyName("transcriptPath")]
    public required string TranscriptPath { get; init; }
    
    /// <summary>
    /// When the metadata was generated.
    /// </summary>
    [JsonPropertyName("generatedAt")]
    public DateTime GeneratedAt { get; init; } = DateTime.UtcNow;
    
    /// <summary>
    /// AI model used for generation.
    /// </summary>
    [JsonPropertyName("model")]
    public required string Model { get; init; }
    
    /// <summary>
    /// User-provided episode context.
    /// </summary>
    [JsonPropertyName("episodeContext")]
    public string? EpisodeContext { get; init; }
    
    /// <summary>
    /// Generated title suggestions.
    /// </summary>
    [JsonPropertyName("titles")]
    public List<string> Titles { get; init; } = [];
    
    /// <summary>
    /// Style used to generate the titles.
    /// </summary>
    [JsonPropertyName("titleStyle")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TitleStyle? TitleStyle { get; init; }
    
    /// <summary>
    /// The selected/preferred title.
    /// </summary>
    [JsonPropertyName("selectedTitle")]
    public string? SelectedTitle { get; init; }
    
    /// <summary>
    /// Generated descriptions by length.
    /// </summary>
    [JsonPropertyName("descriptions")]
    public ManifestDescriptions Descriptions { get; init; } = new();
    
    /// <summary>
    /// Generated YouTube chapters.
    /// </summary>
    [JsonPropertyName("chapters")]
    public List<ManifestChapter> Chapters { get; init; } = [];
    
    /// <summary>
    /// Path to the generated SRT file.
    /// </summary>
    [JsonPropertyName("srtPath")]
    public string? SrtPath { get; init; }
    
    /// <summary>
    /// Episode duration in seconds.
    /// </summary>
    [JsonPropertyName("durationSeconds")]
    public double DurationSeconds { get; init; }
}

/// <summary>
/// Descriptions in the manifest.
/// </summary>
public class ManifestDescriptions
{
    [JsonPropertyName("short")]
    public string? Short { get; init; }
    
    [JsonPropertyName("medium")]
    public string? Medium { get; init; }
    
    [JsonPropertyName("long")]
    public string? Long { get; init; }
}

/// <summary>
/// Chapter entry in the manifest.
/// </summary>
public class ManifestChapter
{
    [JsonPropertyName("timestamp")]
    public required string Timestamp { get; init; }
    
    [JsonPropertyName("title")]
    public required string Title { get; init; }
    
    [JsonPropertyName("summary")]
    public string? Summary { get; init; }
}
