namespace PodcastMetadataGenerator.Core.Services;

internal static class TemporaryFileCleanup
{
    public static void Delete(string path, Exception? primaryException)
    {
        try
        {
            File.Delete(path);
        }
        catch when (primaryException is not null)
        {
            // Cleanup must not replace the operation failure that triggered it.
        }
    }
}
