namespace PodcastMetadataGenerator.Core.Models;

/// <summary>
/// Length options for description generation.
/// </summary>
public enum DescriptionLength
{
    Short,
    Medium,
    Long
}

/// <summary>
/// Represents a chapter marker for YouTube.
/// </summary>
public record Chapter
{
    /// <summary>
    /// Start time formatted for YouTube (MM:SS or HH:MM:SS).
    /// </summary>
    public required string Timestamp { get; init; }
    
    /// <summary>
    /// Chapter title.
    /// </summary>
    public required string Title { get; init; }
    
    /// <summary>
    /// Optional summary of the chapter content.
    /// </summary>
    public string? Summary { get; init; }
}

/// <summary>
/// Holds all generated metadata for a podcast episode.
/// </summary>
public class GenerationResult
{
    /// <summary>
    /// List of generated title suggestions.
    /// </summary>
    public List<string> Titles { get; set; } = [];
    
    /// <summary>
    /// The style used to generate <see cref="Titles"/>.
    /// </summary>
    public TitleStyle? TitleStyle { get; set; }
    
    /// <summary>
    /// Generated descriptions by length.
    /// </summary>
    public Dictionary<DescriptionLength, string> Descriptions { get; set; } = [];
    
    /// <summary>
    /// Generated YouTube chapters.
    /// </summary>
    public List<Chapter> Chapters { get; set; } = [];
    
    /// <summary>
    /// Validated and formatted SRT content.
    /// </summary>
    public string? SrtContent { get; set; }
    
    /// <summary>
    /// Any validation errors from SRT conversion.
    /// </summary>
    public List<string> SrtValidationErrors { get; set; } = [];
    
    /// <summary>
    /// The selected title (after user choice).
    /// </summary>
    public string? SelectedTitle { get; set; }
}
