using GitHub.Copilot;
using PodcastMetadataGenerator.Core.Copilot;

namespace PodcastMetadataGenerator.Core.Models;

/// <summary>
/// Available AI models from the GitHub Copilot SDK.
/// </summary>
public static class AvailableModels
{
    public const string PreferredDefaultModelId = "gpt-6-luna";

    /// <summary>
    /// Fetches the list of available models from the Copilot SDK.
    /// </summary>
    public static async Task<string[]> GetModelsFromCliAsync(CancellationToken cancellationToken = default)
        => (await GetModelsWithMetadataAsync(cancellationToken)).Select(m => m.Id).ToArray();

    /// <summary>
    /// Fetches the list of available models with full metadata from the Copilot SDK.
    /// </summary>
    public static async Task<List<ModelInfo>> GetModelsWithMetadataAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var client = CopilotClientFactory.CreateClient();
            await client.StartAsync();

            var models = await client.ListModelsAsync(cancellationToken);
            return models.ToList();
        }
        catch
        {
            return [];
        }
    }

    /// <summary>
    /// Resolves a valid model id from the Copilot SDK, preferring the provided selection.
    /// </summary>
    public static async Task<string> ResolveModelAsync(string? selectedModel, CancellationToken cancellationToken = default)
    {
        var models = await GetModelsWithMetadataAsync(cancellationToken);
        return SelectModel(models, selectedModel);
    }

    /// <summary>
    /// Picks a valid model id from an already-fetched model list, preferring the provided selection.
    /// </summary>
    public static string SelectModel(IReadOnlyList<ModelInfo> models, string? selectedModel)
    {
        var normalizedModel = selectedModel?.Trim();

        if (models.Count == 0)
        {
            return normalizedModel ?? string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(normalizedModel))
        {
            var matchingModel = models.FirstOrDefault(m =>
                string.Equals(m.Id, normalizedModel, StringComparison.OrdinalIgnoreCase));

            if (matchingModel is not null)
            {
                return matchingModel.Id;
            }
        }

        var preferredDefaultModel = models.FirstOrDefault(m =>
            string.Equals(m.Id, PreferredDefaultModelId, StringComparison.OrdinalIgnoreCase));

        if (preferredDefaultModel is not null)
        {
            return preferredDefaultModel.Id;
        }

        return models[0].Id;
    }

    /// <summary>
    /// Resolves a valid model id from the Copilot SDK, preferring the provided selection.
    /// </summary>
    public static string ResolveModel(string? selectedModel)
    {
        try
        {
            return ResolveModelAsync(selectedModel).ConfigureAwait(false).GetAwaiter().GetResult();
        }
        catch
        {
            return selectedModel?.Trim() ?? string.Empty;
        }
    }
}

/// <summary>
/// Application settings and user preferences.
/// Persisted to disk via SettingsService.
/// </summary>
public class AppSettings
{
    #region AI Settings
    
    /// <summary>
    /// Selected AI model for generation.
    /// </summary>
    public string Model { get; set; } = string.Empty;
    
    #endregion
    
    #region Output Settings
    
    /// <summary>
    /// Output directory for generated files.
    /// </summary>
    public string OutputDirectory { get; set; } = Environment.CurrentDirectory;
    
    #endregion
    
    #region Title Settings
    
    /// <summary>
    /// Number of title suggestions to generate.
    /// </summary>
    public int TitleCount { get; set; } = 5;
    
    /// <summary>
    /// Maximum words per title.
    /// </summary>
    public int TitleMaxWords { get; set; } = 10;
    
    /// <summary>
    /// Default style for generated titles. Can be overridden each time titles are generated.
    /// </summary>
    public TitleStyle TitleStyle { get; set; } = TitleStyles.Default;
    
    #endregion
    
    #region Description Settings
    
    /// <summary>
    /// Word count target for short descriptions.
    /// </summary>
    public int ShortDescriptionWords { get; set; } = 50;
    
    /// <summary>
    /// Word count target for medium descriptions.
    /// </summary>
    public int MediumDescriptionWords { get; set; } = 150;
    
    /// <summary>
    /// Word count target for long descriptions.
    /// </summary>
    public int LongDescriptionWords { get; set; } = 300;
    
    #endregion
    
    #region Chapter Settings
    
    /// <summary>
    /// Minimum number of chapters to generate.
    /// </summary>
    public int MinChapters { get; set; } = 3;
    
    /// <summary>
    /// Maximum number of chapters to generate.
    /// </summary>
    public int MaxChapters { get; set; } = 12;
    
    /// <summary>
    /// Target chapters per 30 minutes of content.
    /// </summary>
    public int ChaptersPer30Min { get; set; } = 5;
    
    /// <summary>
    /// Maximum words per chapter title.
    /// </summary>
    public int ChapterTitleMaxWords { get; set; } = 8;
    
    #endregion
    
    #region Episode Context
    
    /// <summary>
    /// Optional episode context provided by the user (guest names, topics, etc.).
    /// This is per-session and not persisted.
    /// </summary>
    public string? EpisodeContext { get; set; }
    
    /// <summary>
    /// Default podcast name (persisted for reuse).
    /// </summary>
    public string? PodcastName { get; set; }
    
    /// <summary>
    /// Default host name(s) (persisted for reuse).
    /// </summary>
    public string? HostNames { get; set; }
    
    #endregion
    
    #region Parser Settings
    
    /// <summary>
    /// Default duration to add if end time is not available (in ms).
    /// </summary>
    public long DefaultSegmentDurationMs { get; set; } = 5000;
    
    #endregion
    
    #region Prompt Settings
    
    /// <summary>
    /// Whether to prompt for episode context when loading a transcript.
    /// </summary>
    public bool PromptForContextOnLoad { get; set; } = true;
    
    #endregion
    
    /// <summary>
    /// Calculates the target number of chapters based on episode duration.
    /// </summary>
    public int CalculateTargetChapters(double durationMinutes)
    {
        var target = (int)(durationMinutes / 30.0 * ChaptersPer30Min);
        return Math.Max(MinChapters, Math.Min(MaxChapters, target));
    }
}
