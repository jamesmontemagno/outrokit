using System.Text.Json.Serialization;

namespace PodcastMetadataGenerator.Core.Models;

/// <summary>
/// Style options for generated episode titles.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<TitleStyle>))]
public enum TitleStyle
{
    Balanced,
    Descriptive,
    Curiosity,
    Question,
    HowTo,
    Playful,
    Professional,
    Seo,
    Mixed
}

/// <summary>
/// Display text and prompt guidance for each <see cref="TitleStyle"/>.
/// </summary>
public static class TitleStyles
{
    public const TitleStyle Default = TitleStyle.Balanced;

    public static IReadOnlyList<TitleStyle> All { get; } = Enum.GetValues<TitleStyle>();

    public static string GetDisplayName(this TitleStyle style) => style switch
    {
        TitleStyle.Balanced => "Balanced",
        TitleStyle.Descriptive => "Descriptive",
        TitleStyle.Curiosity => "Curiosity Hook",
        TitleStyle.Question => "Question",
        TitleStyle.HowTo => "How-To / Educational",
        TitleStyle.Playful => "Playful",
        TitleStyle.Professional => "Professional",
        TitleStyle.Seo => "SEO Keywords",
        TitleStyle.Mixed => "Mixed Variety",
        _ => style.ToString()
    };

    public static string GetDescription(this TitleStyle style) => style switch
    {
        TitleStyle.Balanced => "Engaging, descriptive, and SEO-friendly",
        TitleStyle.Descriptive => "Clear and straightforward, no hype",
        TitleStyle.Curiosity => "Teases a hook that makes people want to listen",
        TitleStyle.Question => "Phrased as a question the episode answers",
        TitleStyle.HowTo => "Outcome-focused: how-to, guide, lessons learned",
        TitleStyle.Playful => "Witty, with wordplay or humor",
        TitleStyle.Professional => "Polished and authoritative",
        TitleStyle.Seo => "Leads with the terms people search for",
        TitleStyle.Mixed => "A different style for each title",
        _ => string.Empty
    };

    /// <summary>
    /// Gets the instruction inserted into the title prompt for this style.
    /// </summary>
    public static string GetPromptGuidance(this TitleStyle style) => style switch
    {
        TitleStyle.Descriptive =>
            "Make them clear and straightforward: state plainly what the episode covers, with no hype or clickbait.",
        TitleStyle.Curiosity =>
            "Make them curiosity-driven: open with a compelling hook or tease a surprising insight that makes people want to listen, while staying accurate to the content (no misleading clickbait).",
        TitleStyle.Question =>
            "Phrase each title as a compelling question that the episode answers.",
        TitleStyle.HowTo =>
            "Make them educational and outcome-focused: use how-to, guide, or lessons-learned framing that tells listeners what they will learn.",
        TitleStyle.Playful =>
            "Make them playful and witty: use wordplay, puns, or humor while keeping the topic clear.",
        TitleStyle.Professional =>
            "Make them professional and authoritative: polished, credible, and suited to a business or industry audience.",
        TitleStyle.Seo =>
            "Make them keyword-forward for search: lead with the main topic or key terms people would search for, while keeping them natural and readable.",
        TitleStyle.Mixed =>
            "Use a different style for each title (for example descriptive, curiosity-driven, question, how-to, and playful) so there is a varied set to choose from.",
        _ =>
            "Make them engaging, descriptive, and SEO-friendly."
    };
}
