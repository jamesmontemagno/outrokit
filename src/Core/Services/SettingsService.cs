using System.Text.Json;
using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

/// <summary>
/// Handles loading and saving application settings.
/// </summary>
public class SettingsService
{
    private readonly string _settingsPath;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    
    public SettingsService(string? settingsPath = null)
    {
        _settingsPath = settingsPath ?? GetDefaultSettingsPath();
    }
    
    /// <summary>
    /// Gets the default settings file path in the user's home directory.
    /// </summary>
    public static string GetDefaultSettingsPath()
    {
        return Path.Combine(GetDefaultConfigDirectory(), "settings.json");
    }

    public static string GetDefaultConfigDirectory() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        ".podcast-metadata-generator");
    
    /// <summary>
    /// Loads settings from disk, returning defaults if not found.
    /// </summary>
    public async Task<AppSettings> LoadAsync()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return Normalize(new AppSettings());
            
            var json = await File.ReadAllTextAsync(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            return Normalize(settings);
        }
        catch
        {
            // Return defaults on any error
            return Normalize(new AppSettings());
        }
    }
    
    /// <summary>
    /// Loads settings synchronously.
    /// </summary>
    public AppSettings Load()
    {
        try
        {
            if (!File.Exists(_settingsPath))
                return Normalize(new AppSettings());
            
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            return Normalize(settings);
        }
        catch
        {
            return Normalize(new AppSettings());
        }
    }
    
    /// <summary>
    /// Saves settings to disk.
    /// </summary>
    public async Task SaveAsync(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        var json = JsonSerializer.Serialize(CreatePersistedCopy(settings), JsonOptions);
        await File.WriteAllTextAsync(_settingsPath, json);
    }
    
    /// <summary>
    /// Saves settings synchronously.
    /// </summary>
    public void Save(AppSettings settings)
    {
        var directory = Path.GetDirectoryName(_settingsPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }
        
        var json = JsonSerializer.Serialize(CreatePersistedCopy(settings), JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static AppSettings CreatePersistedCopy(AppSettings settings) => new()
    {
        Model = settings.Model,
        FfmpegPath = settings.FfmpegPath,
        WhisperModel = settings.WhisperModel,
        WhisperModelPath = settings.WhisperModelPath,
        CaptionSize = settings.CaptionSize,
        CaptionPosition = settings.CaptionPosition,
        OutputDirectory = settings.OutputDirectory,
        TitleCount = settings.TitleCount,
        TitleMaxWords = settings.TitleMaxWords,
        TitleStyle = settings.TitleStyle,
        ShortDescriptionWords = settings.ShortDescriptionWords,
        MediumDescriptionWords = settings.MediumDescriptionWords,
        LongDescriptionWords = settings.LongDescriptionWords,
        MinChapters = settings.MinChapters,
        MaxChapters = settings.MaxChapters,
        ChaptersPer30Min = settings.ChaptersPer30Min,
        ChapterTitleMaxWords = settings.ChapterTitleMaxWords,
        PodcastName = settings.PodcastName,
        HostNames = settings.HostNames,
        DefaultSegmentDurationMs = settings.DefaultSegmentDurationMs,
        PromptForContextOnLoad = settings.PromptForContextOnLoad
        // EpisodeContext is per-episode and intentionally not saved
    };

    private static AppSettings Normalize(AppSettings settings)
    {
        settings.Model = string.IsNullOrWhiteSpace(settings.Model)
            ? AvailableModels.PreferredDefaultModelId
            : settings.Model.Trim();
        settings.FfmpegPath = string.IsNullOrWhiteSpace(settings.FfmpegPath)
            ? "ffmpeg"
            : settings.FfmpegPath.Trim();
        settings.WhisperModel = WhisperModelCatalog.TryGet(settings.WhisperModel, out var model)
            ? model.Id
            : WhisperModelCatalog.Default.Id;
        settings.WhisperModelPath = string.IsNullOrWhiteSpace(settings.WhisperModelPath)
            ? null
            : settings.WhisperModelPath.Trim();

        if (!Enum.IsDefined(settings.TitleStyle))
        {
            settings.TitleStyle = TitleStyles.Default;
        }

        if (!Enum.IsDefined(settings.CaptionSize))
        {
            settings.CaptionSize = CaptionStyles.DefaultSize;
        }

        if (!Enum.IsDefined(settings.CaptionPosition))
        {
            settings.CaptionPosition = CaptionStyles.DefaultPosition;
        }

        return settings;
    }
}
