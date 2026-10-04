using System.Text.Json.Serialization;

namespace PodcastMetadataGenerator.Core.Models;

/// <summary>
/// Text size for captions burned into a video.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaptionSize>))]
public enum CaptionSize
{
    Small,
    Medium,
    Large
}

/// <summary>
/// Where captions burned into a video are placed.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<CaptionPosition>))]
public enum CaptionPosition
{
    Bottom,
    Top
}

/// <summary>
/// How captions look when they are burned into a video.
/// </summary>
public sealed record CaptionStyle(CaptionSize Size, CaptionPosition Position);

/// <summary>
/// Display text for <see cref="CaptionSize"/> and <see cref="CaptionPosition"/>.
/// </summary>
public static class CaptionStyles
{
    public const CaptionSize DefaultSize = CaptionSize.Medium;

    public const CaptionPosition DefaultPosition = CaptionPosition.Bottom;

    public static IReadOnlyList<CaptionSize> AllSizes { get; } = Enum.GetValues<CaptionSize>();

    public static IReadOnlyList<CaptionPosition> AllPositions { get; } = Enum.GetValues<CaptionPosition>();

    public static string GetDisplayName(this CaptionSize size) => size.ToString();

    public static string GetDescription(this CaptionSize size) => size switch
    {
        CaptionSize.Small => "Unobtrusive, best for wide shots and screen recordings",
        CaptionSize.Medium => "Readable on most screens",
        CaptionSize.Large => "Easy to read on phones and in social clips",
        _ => string.Empty
    };

    public static string GetDisplayName(this CaptionPosition position) => position.ToString();

    public static string GetDescription(this CaptionPosition position) => position switch
    {
        CaptionPosition.Bottom => "Centered along the bottom edge",
        CaptionPosition.Top => "Centered along the top edge",
        _ => string.Empty
    };
}
