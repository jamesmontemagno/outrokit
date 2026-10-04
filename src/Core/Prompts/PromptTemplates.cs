using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Prompts;

/// <summary>
/// Contains all prompt templates for AI generation.
/// Based on prompts from https://github.com/jamesmontemagno/app-podcast-assistant/
/// </summary>
public static class PromptTemplates
{
    #region Title Generation
    
    public const string TitleSystemPrompt = """
        You are a creative producer who writes engaging, concise episode titles.
        You analyze transcripts carefully to identify the main topics and create titles that 
        accurately reflect the content while being compelling and SEO-friendly.
        """;
    
    public static string GetTitleUserPrompt(string transcript, AppSettings settings, TitleStyle? style = null)
    {
        var contextSection = BuildContextSection(settings);
        var styleGuidance = (style ?? settings.TitleStyle).GetPromptGuidance();
        
        return $$"""
            You are generating titles for an episode based on its transcript.

            Analyze the transcript and identify the topics that are discussed the most.
            Focus primarily on the main topics that take up the majority of the conversation.
            The title should reflect what listeners will spend most of their time hearing about.
            You can mention secondary topics briefly, but prioritize the core subject matter.
            {{contextSection}}
            Generate {{settings.TitleCount}} creative, concise titles for this episode.
            Keep titles under {{settings.TitleMaxWords}} words each.
            {{styleGuidance}}

            Return ONLY the {{settings.TitleCount}} titles, one per line, numbered 1-{{settings.TitleCount}}. No additional commentary.

            Episode Transcript:
            {{transcript}}
            """;
    }
    
    #endregion
    
    #region Description Generation
    
    public const string DescriptionSystemPrompt = """
        You are a producer who writes compelling episode descriptions.
        You craft descriptions that accurately summarize the content, highlight key insights,
        and entice listeners to tune in. Your descriptions are well-structured and engaging.
        """;
    
    public static string GetDescriptionUserPrompt(
        string transcript, 
        DescriptionLength length, 
        AppSettings settings,
        string? selectedTitle = null)
    {
        var (wordTarget, formatGuidance) = length switch
        {
            DescriptionLength.Short => (settings.ShortDescriptionWords, $"in 2-3 sentences (~{settings.ShortDescriptionWords} words)"),
            DescriptionLength.Medium => (settings.MediumDescriptionWords, $"in 1-2 paragraphs (~{settings.MediumDescriptionWords} words)"),
            DescriptionLength.Long => (settings.LongDescriptionWords, $"in 3-4 paragraphs (~{settings.LongDescriptionWords} words)"),
            _ => (settings.MediumDescriptionWords, $"in 1-2 paragraphs (~{settings.MediumDescriptionWords} words)")
        };
        
        var titleSection = string.IsNullOrEmpty(selectedTitle) 
            ? "" 
            : $"Episode title: {selectedTitle}\n\n";
        
        var contextSection = BuildContextSection(settings);
        
        return $$"""
            You are writing a compelling episode description based on its transcript.

            Analyze the transcript carefully and identify:
            - The main topics that dominate the conversation (what takes up most of the time)
            - Key insights, valuable takeaways, or unique perspectives shared
            - The overall narrative or flow of the discussion
            {{contextSection}}
            Write a description {{formatGuidance}} that:
            - Focuses primarily on the main topics discussed
            - Highlights the value and key takeaways for listeners
            - Uses engaging, conversational language
            - Captures what makes this episode worth listening to

            {{titleSection}}Return ONLY the description text, no additional commentary or labels.

            Episode Transcript:
            {{transcript}}
            """;
    }
    
    #endregion
    
    #region Chapter Generation
    
    public const string ChapterSystemPrompt = """
        You are an editor who creates chapter markers based on topic transitions.
        You identify natural breakpoints in conversations where topics shift significantly.
        You create descriptive chapter titles that help listeners navigate the episode.
        You must use actual timecodes from the transcript where topics shift.
        """;
    
    public static string GetChapterUserPrompt(
        string transcriptWithTimestamps, 
        double durationSeconds,
        AppSettings settings)
    {
        var targetChapters = settings.CalculateTargetChapters(durationSeconds / 60.0);
        var contextSection = BuildContextSection(settings);
        
        return $$"""
            Create chapter markers for this episode from the timestamped transcript below.
            {{contextSection}}
            Each line shows a time range followed by the speaker and text for that segment.
            Identify where MAJOR topic shifts occur and create chapters at those points.

            IMPORTANT: Be VERY selective - only create chapters for significant topic changes.
            Think of chapters as major sections viewers would skip to, not every minor topic.

            Guidelines:
            - Create approximately {{targetChapters}} chapters (minimum {{settings.MinChapters}}, maximum {{settings.MaxChapters}})
            - Only mark MAJOR topic transitions - ignore minor shifts
            - Each chapter should represent 5-10+ minutes of distinct content
            - First chapter MUST start at 00:00 (Introduction/Opening)
            - For each chapter, use the START timestamp from where the topic begins
            - Format timestamps as MM:SS or HH:MM:SS
            - Episode duration is about {{(int)durationSeconds}} seconds; do not emit timestamps beyond that
            - Distribute chapters evenly; avoid any gap > 10 minutes between chapters
            - Create descriptive titles (under {{settings.ChapterTitleMaxWords}} words)

            Return chapters in this EXACT format, one per line:
            MM:SS Title Here
            or
            HH:MM:SS Title Here

            Do not include any other text, numbering, or commentary.

            Timestamped Transcript:
            {{transcriptWithTimestamps}}
            """;
    }
    
    #endregion
    
    #region Helpers
    
    /// <summary>
    /// Builds the context section from settings (show name, hosts, episode context).
    /// </summary>
    private static string BuildContextSection(AppSettings settings)
    {
        var parts = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(settings.PodcastName))
            parts.Add($"Show: {settings.PodcastName}");
        
        if (!string.IsNullOrWhiteSpace(settings.HostNames))
            parts.Add($"Host(s): {settings.HostNames}");
        
        if (!string.IsNullOrWhiteSpace(settings.EpisodeContext))
            parts.Add($"Episode context: {settings.EpisodeContext}");
        
        if (parts.Count == 0)
            return "";
        
        return $"""

            Additional context:
            {string.Join("\n", parts)}

            """;
    }
    
    #endregion
}
