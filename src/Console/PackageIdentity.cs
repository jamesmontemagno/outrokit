namespace PodcastMetadataGenerator.Console;

/// <summary>
/// Which NuGet package the running app came from.
/// </summary>
public static class PackageIdentity
{
    public const string PackageId = "OutroKit";

    public const string LegacyPackageId = "PodcastMetadataGenerator";

    // Packed next to the app only in the legacy package; see the project file.
    private const string LegacyMarkerFile = "renamed-package.txt";

    /// <summary>
    /// Whether the app was installed from its original package, which has been renamed.
    /// </summary>
    public static bool IsLegacyPackage { get; } =
        File.Exists(Path.Combine(AppContext.BaseDirectory, LegacyMarkerFile));
}
