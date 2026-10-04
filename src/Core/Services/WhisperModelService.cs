using PodcastMetadataGenerator.Core.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace PodcastMetadataGenerator.Core.Services;

public class WhisperModelService
{
    public string ModelsDirectory { get; }

    public WhisperModelService(string? modelsDirectory = null)
    {
        ModelsDirectory = modelsDirectory
            ?? Path.Combine(SettingsService.GetDefaultConfigDirectory(), "models");
    }

    public string GetModelPath(string? modelId)
    {
        var model = WhisperModelCatalog.Get(modelId);
        return Path.Combine(ModelsDirectory, WhisperModelCatalog.GetFileName(model));
    }

    public string? GetInstalledModelPath(AppSettings settings)
    {
        var configuredPath = settings.WhisperModelPath;
        if (!string.IsNullOrWhiteSpace(configuredPath) && File.Exists(configuredPath))
        {
            return configuredPath;
        }

        var defaultPath = GetModelPath(settings.WhisperModel);
        return File.Exists(defaultPath) ? defaultPath : null;
    }

    public async Task<string> DownloadAndInitializeAsync(
        AppSettings settings,
        CancellationToken cancellationToken = default)
    {
        var model = WhisperModelCatalog.Get(settings.WhisperModel);
        Directory.CreateDirectory(ModelsDirectory);

        var modelPath = GetModelPath(model.Id);
        if (!File.Exists(modelPath))
        {
            var temporaryPath = modelPath + ".download";
            Exception? downloadException = null;
            try
            {
                await using var modelStream = await WhisperGgmlDownloader.Default.GetGgmlModelAsync(
                    model.GgmlType,
                    cancellationToken: cancellationToken);
                await using (var fileStream = new FileStream(
                    temporaryPath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    81920,
                    useAsync: true))
                {
                    await modelStream.CopyToAsync(fileStream, cancellationToken);
                    await fileStream.FlushAsync(cancellationToken);
                }

                File.Move(temporaryPath, modelPath, overwrite: true);
            }
            catch (Exception ex)
            {
                downloadException = ex;
                throw;
            }
            finally
            {
                TemporaryFileCleanup.Delete(temporaryPath, downloadException);
            }
        }

        Initialize(modelPath);
        settings.WhisperModelPath = modelPath;
        return modelPath;
    }

    public void Initialize(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("The Whisper GGML model was not found.", modelPath);
        }

        using var factory = WhisperFactory.FromPath(modelPath);
    }
}
