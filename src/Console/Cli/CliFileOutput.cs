using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Console.Cli;

internal static class CliFileOutput
{
    public static string Validate(string outputPath, bool overwrite, params string[] inputs)
    {
        var fullPath = Path.GetFullPath(outputPath);
        if (inputs.Any(input => CaptionBurnService.IsSameFile(input, fullPath)))
            throw new ArgumentException("The output path cannot overwrite an input file, even with --overwrite.");
        if (Directory.Exists(fullPath))
            throw new ArgumentException($"The output path is a directory: {fullPath}");
        if (File.Exists(fullPath) && !overwrite)
            throw new IOException($"The output already exists: {fullPath}. Choose another path or explicitly use --overwrite.");
        return fullPath;
    }

    public static async Task<string> WriteAsync(
        string outputPath, bool overwrite, string[] inputs,
        Func<string, Task> write, CancellationToken cancellationToken)
    {
        var fullPath = Validate(outputPath, overwrite, inputs);
        var directory = Path.GetDirectoryName(fullPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = Path.Combine(directory,
            $".{Path.GetFileNameWithoutExtension(fullPath)}.{Guid.NewGuid():N}.partial{Path.GetExtension(fullPath)}");
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            await write(temporaryPath);
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(temporaryPath) || new FileInfo(temporaryPath).Length == 0)
                throw new IOException("The operation did not write an output file.");
            File.Move(temporaryPath, fullPath, overwrite);
            return fullPath;
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }
}
