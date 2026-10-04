namespace PodcastMetadataGenerator.Core.Services;

internal static class TemporaryFileCleanup
{
    // A process that was just stopped can hold its files for a moment longer, most often on
    // Windows, where ffmpeg may also be running behind a launcher that exits first.
    private static readonly TimeSpan RetryFor = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan RetryEvery = TimeSpan.FromMilliseconds(100);

    public static Task DeleteAsync(string path, Exception? primaryException) =>
        RunAsync(() => File.Delete(path), primaryException);

    public static Task DeleteDirectoryAsync(string path, Exception? primaryException) =>
        RunAsync(
            () =>
            {
                if (Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            },
            primaryException);

    private static async Task RunAsync(Action delete, Exception? primaryException)
    {
        var giveUpAt = DateTime.UtcNow + RetryFor;
        while (true)
        {
            try
            {
                delete();
                return;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                if (DateTime.UtcNow < giveUpAt)
                {
                    await Task.Delay(RetryEvery);
                    continue;
                }

                if (primaryException is null)
                {
                    throw;
                }

                // Cleanup must not replace the operation failure that triggered it.
                return;
            }
        }
    }
}
