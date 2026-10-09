using System.Text.Json;
using PodcastMetadataGenerator.Core.Models;

namespace PodcastMetadataGenerator.Core.Services;

public enum OutputArtifact
{
    Titles,
    ShortDescription,
    MediumDescription,
    LongDescription,
    Chapters,
    Srt,
    Manifest
}

/// <summary>
/// Handles output file generation (descriptions, chapters, manifest, SRT).
/// </summary>
public class OutputService
{
    private readonly SrtConverter _srtConverter;
    
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };
    
    public OutputService(SrtConverter srtConverter)
    {
        _srtConverter = srtConverter;
    }
    
    /// <summary>
    /// Saves all generated metadata to the specified output directory.
    /// Returns the list of files created.
    /// </summary>
    public async Task<List<string>> SaveAllAsync(
        string outputDirectory, 
        Transcript transcript, 
        GenerationResult result,
        AppSettings settings) =>
        await SaveSelectedAsync(
            outputDirectory,
            transcript,
            result,
            settings,
            Enum.GetValues<OutputArtifact>());

    /// <summary>
    /// Saves only the selected metadata artifacts to the specified output directory.
    /// Returns the list of files created.
    /// </summary>
    public async Task<List<string>> SaveSelectedAsync(
        string outputDirectory,
        Transcript transcript,
        GenerationResult result,
        AppSettings settings,
        IReadOnlyCollection<OutputArtifact> artifacts)
    {
        var createdFiles = new List<string>();
        var selectedArtifacts = artifacts.ToHashSet();
        if (selectedArtifacts.Count == 0)
        {
            return createdFiles;
        }

        // Ensure output directory exists
        Directory.CreateDirectory(outputDirectory);
        
        var baseName = Path.GetFileNameWithoutExtension(transcript.FilePath);
        
        // Save titles
        if (selectedArtifacts.Contains(OutputArtifact.Titles))
        {
            var titlesPath = Path.Combine(outputDirectory, $"{baseName}_titles.txt");
            await File.WriteAllTextAsync(titlesPath, string.Join(Environment.NewLine, result.Titles));
            createdFiles.Add(titlesPath);
        }
        
        // Save descriptions
        foreach (var length in Enum.GetValues<DescriptionLength>())
        {
            var artifact = length switch
            {
                DescriptionLength.Short => OutputArtifact.ShortDescription,
                DescriptionLength.Medium => OutputArtifact.MediumDescription,
                DescriptionLength.Long => OutputArtifact.LongDescription,
                _ => throw new ArgumentOutOfRangeException(nameof(length), length, null)
            };
            if (!selectedArtifacts.Contains(artifact)
                || !result.Descriptions.TryGetValue(length, out var description))
            {
                continue;
            }

            var descPath = Path.Combine(outputDirectory, $"{baseName}_description_{length.ToString().ToLower()}.txt");
            await File.WriteAllTextAsync(descPath, description);
            createdFiles.Add(descPath);
        }
        
        // Save chapters (as YouTube format and raw list)
        if (selectedArtifacts.Contains(OutputArtifact.Chapters) && result.Chapters.Count > 0)
        {
            var chaptersPath = Path.Combine(outputDirectory, $"{baseName}_chapters.txt");
            var chaptersContent = _srtConverter.FormatChaptersForYouTube(result.Chapters);
            await File.WriteAllTextAsync(chaptersPath, chaptersContent);
            createdFiles.Add(chaptersPath);
        }
        
        // Convert and save SRT if we have timestamps
        var includeSrt = selectedArtifacts.Contains(OutputArtifact.Srt) && transcript.HasTimestamps;
        string? srtFileName = null;
        if (includeSrt)
        {
            srtFileName = $"{baseName}.srt";
            var srtPath = Path.Combine(outputDirectory, srtFileName);
            if (CaptionBurnService.IsSameFile(srtPath, transcript.FilePath))
            {
                srtFileName = $"{baseName}_subtitles.srt";
                srtPath = Path.Combine(outputDirectory, srtFileName);
            }

            var srtResult = _srtConverter.ConvertToSrt(transcript);
            await File.WriteAllTextAsync(srtPath, srtResult.Content);
            createdFiles.Add(srtPath);
            
            result.SrtContent = srtResult.Content;
            result.SrtValidationErrors = srtResult.Errors;
        }
        
        // Save manifest
        if (selectedArtifacts.Contains(OutputArtifact.Manifest))
        {
            var manifestPath = Path.Combine(outputDirectory, $"{baseName}_manifest.json");
            var manifest = CreateManifest(transcript, result, settings, srtFileName);
            var manifestJson = JsonSerializer.Serialize(manifest, JsonOptions);
            await File.WriteAllTextAsync(manifestPath, manifestJson);
            createdFiles.Add(manifestPath);
        }
        
        return createdFiles;
    }
    
    /// <summary>
    /// Gets all output content as a dictionary (for in-memory/clipboard use).
    /// </summary>
    public Dictionary<string, string> GetAllContent(
        Transcript transcript,
        GenerationResult result,
        AppSettings settings)
    {
        var content = new Dictionary<string, string>();
        
        // Titles
        content["titles"] = string.Join(Environment.NewLine, result.Titles);
        
        // Descriptions
        foreach (var (length, description) in result.Descriptions)
        {
            content[$"description_{length.ToString().ToLower()}"] = description;
        }
        
        // Chapters
        if (result.Chapters.Count > 0)
        {
            content["chapters"] = _srtConverter.FormatChaptersForYouTube(result.Chapters);
        }
        
        // SRT
        if (transcript.HasTimestamps)
        {
            var srtResult = _srtConverter.ConvertToSrt(transcript);
            content["srt"] = srtResult.Content;
        }
        
        // Manifest
        var srtFileName = transcript.HasTimestamps
            ? Path.GetFileNameWithoutExtension(transcript.FilePath) + ".srt"
            : null;
        var manifest = CreateManifest(transcript, result, settings, srtFileName);
        content["manifest"] = JsonSerializer.Serialize(manifest, JsonOptions);
        
        return content;
    }
    
    /// <summary>
    /// Formats all generated metadata as one plain-text block (for clipboard use).
    /// </summary>
    public string FormatCombinedText(GenerationResult result)
    {
        var sections = new List<string>();
        
        if (!string.IsNullOrWhiteSpace(result.SelectedTitle))
        {
            sections.Add($"TITLE{Environment.NewLine}{result.SelectedTitle}");
        }
        else if (result.Titles.Count > 0)
        {
            sections.Add($"TITLES{Environment.NewLine}{string.Join(Environment.NewLine, result.Titles)}");
        }
        
        foreach (var length in Enum.GetValues<DescriptionLength>())
        {
            if (result.Descriptions.TryGetValue(length, out var description))
            {
                sections.Add($"{length.ToString().ToUpperInvariant()} DESCRIPTION{Environment.NewLine}{description}");
            }
        }
        
        if (result.Chapters.Count > 0)
        {
            sections.Add($"CHAPTERS{Environment.NewLine}{_srtConverter.FormatChaptersForYouTube(result.Chapters)}");
        }
        
        return string.Join(Environment.NewLine + Environment.NewLine, sections);
    }
    
    private static Manifest CreateManifest(
        Transcript transcript, 
        GenerationResult result, 
        AppSettings settings,
        string? srtFileName)
    {
        return new Manifest
        {
            TranscriptPath = transcript.FilePath,
            GeneratedAt = DateTime.UtcNow,
            Model = settings.Model,
            EpisodeContext = settings.EpisodeContext,
            Titles = result.Titles,
            TitleStyle = result.Titles.Count > 0 ? result.TitleStyle : null,
            SelectedTitle = result.SelectedTitle,
            Descriptions = new ManifestDescriptions
            {
                Short = result.Descriptions.GetValueOrDefault(DescriptionLength.Short),
                Medium = result.Descriptions.GetValueOrDefault(DescriptionLength.Medium),
                Long = result.Descriptions.GetValueOrDefault(DescriptionLength.Long)
            },
            Chapters = result.Chapters.Select(c => new ManifestChapter
            {
                Timestamp = c.Timestamp,
                Title = c.Title,
                Summary = c.Summary
            }).ToList(),
            SrtPath = srtFileName,
            DurationSeconds = transcript.DurationSeconds
        };
    }
}
