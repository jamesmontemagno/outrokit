using PodcastMetadataGenerator.Core.Models;
using Whisper.net;
using Whisper.net.Ggml;

namespace PodcastMetadataGenerator.Core.Services;

public class WhisperModelService
{
    private const int DownloadBufferSize = 81920;

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
        IProgress<long>? downloadProgress = null,
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
                    DownloadBufferSize,
                    useAsync: true))
                {
                    var buffer = new byte[DownloadBufferSize];
                    long downloadedBytes = 0;
                    int bytesRead;
                    while ((bytesRead = await modelStream.ReadAsync(buffer, cancellationToken)) > 0)
                    {
                        await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                        downloadedBytes += bytesRead;
                        downloadProgress?.Report(downloadedBytes);
                    }

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

        try
        {
            Initialize(modelPath);
        }
        catch (WhisperModelLoadException ex)
        {
            // Not deleted automatically: a load can also fail for lack of memory, and the file may be a valid multi-GB download.
            throw new InvalidOperationException(
                $"The {model.DisplayName} model at '{modelPath}' could not be loaded. It may be corrupt or too large " +
                "for this machine. Delete the file to download it again, or choose a smaller model.",
                ex);
        }

        settings.WhisperModelPath = modelPath;
        return modelPath;
    }

    /// <summary>
    /// Loads the model to confirm it is a usable GGML file.
    /// </summary>
    /// <exception cref="WhisperModelLoadException">The file is not a loadable Whisper model.</exception>
    public void Initialize(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("The Whisper GGML model was not found.", modelPath);
        }

        using var factory = WhisperFactory.FromPath(modelPath);
        // FromPath does not report a failed load; CreateBuilder is what throws.
        factory.CreateBuilder();
    }
}
