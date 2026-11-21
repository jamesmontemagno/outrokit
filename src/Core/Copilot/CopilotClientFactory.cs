using Microsoft.Copilot;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace PodcastMetadataGenerator.Core.Copilot;

/// <summary>
/// Creates Copilot SDK clients.
/// </summary>
public static class CopilotClientFactory
{
    /// <summary>
    /// Name this app identifies itself with, on both the client connection and each session.
    /// </summary>
    public const string ClientName = "outrokit";

    /// <summary>
    /// Whether a Copilot CLI was found on this machine. The app does not bundle one, so it cannot
    /// reach Copilot without it.
    /// </summary>
    public static bool IsCliInstalled => !string.IsNullOrWhiteSpace(ResolveCliPath());

    /// <summary>
    /// Creates a CopilotClient that runs the installed Copilot CLI as a child process: the one named
    /// by COPILOT_CLI_PATH, or else the one on PATH.
    /// </summary>
    /// <remarks>
    /// The SDK defaults to hosting a bundled runtime in this process, but the app doesn't bundle one,
    /// so it always spawns the installed CLI over stdio.
    /// </remarks>
    public static CopilotClient CreateClient()
    {
        var cliPath = ResolveCliPath();

        return new CopilotClient(new CopilotClientOptions
        {
            Mode = CopilotClientMode.CodingAgent,
            Runtime = RuntimeConnection.ForSpawnedStdio(path: string.IsNullOrWhiteSpace(cliPath) ? null : cliPath),
            ClientInfo = new CopilotClientInfo { ApplicationName = ClientName }
        });
    }

    /// <summary>
    /// Shown when the installed Copilot CLI is too old for the SDK protocol this app uses.
    /// </summary>
    public const string CliOutdatedMessage =
        "Your Copilot CLI is too old for this version of OutroKit. Update it with 'copilot update' " +
        "(or the package manager you installed it with), then try again.";

    /// <summary>
    /// Shown when the installed Copilot CLI is newer than the SDK protocol this app supports.
    /// </summary>
    public const string AppOutdatedMessage =
        "This version of OutroKit is too old for your Copilot CLI. Update OutroKit with " +
        "'dotnet tool update -g OutroKit', then try again.";

    private static readonly Regex ProtocolMismatch = new(
        @"protocol version mismatch: SDK supports versions \d+-(?<max>\d+), but server (?:reports version (?<server>\d+)|does not report)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>
    /// Explains a startup failure caused by the installed Copilot CLI and this app speaking different
    /// SDK protocol versions, or returns null for any other failure.
    /// </summary>
    public static string? DescribeVersionMismatch(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var match = ProtocolMismatch.Match(current.Message);
            if (!match.Success)
            {
                continue;
            }

            var cliIsNewer = match.Groups["server"].Success
                && int.Parse(match.Groups["server"].Value, CultureInfo.InvariantCulture)
                    > int.Parse(match.Groups["max"].Value, CultureInfo.InvariantCulture);
            return cliIsNewer ? AppOutdatedMessage : CliOutdatedMessage;
        }

        return null;
    }

    private static string ResolveCliPath()
    {
        var explicitPath = TryGetValidatedPath(Environment.GetEnvironmentVariable("COPILOT_CLI_PATH"));
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            return explicitPath;
        }

        var legacyPath = TryGetValidatedPath(Environment.GetEnvironmentVariable("GITHUB_COPILOT_CLI_PATH"));
        if (!string.IsNullOrWhiteSpace(legacyPath))
        {
            return legacyPath;
        }

        var pathCandidate = FindExecutableOnPath();
        if (!string.IsNullOrWhiteSpace(pathCandidate))
        {
            return pathCandidate;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            var winGetLinkPath = TryGetValidatedPath(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "WinGet",
                "Links",
                "copilot.exe"));

            if (!string.IsNullOrWhiteSpace(winGetLinkPath))
            {
                return winGetLinkPath;
            }
        }

        return string.Empty;
    }

    private static string? TryGetValidatedPath(string? candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return null;
        }

        var trimmed = candidate.Trim();
        return File.Exists(trimmed) ? trimmed : null;
    }

    private static string? FindExecutableOnPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        var pathExts = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? GetPathExtensions()
            : [string.Empty];

        foreach (var dir in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (var ext in pathExts)
            {
                var candidate = TryGetValidatedPath(Path.Combine(dir, $"copilot{ext}"));
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }
        }

        return null;
    }

    private static string[] GetPathExtensions()
    {
        var pathExt = Environment.GetEnvironmentVariable("PATHEXT");
        if (string.IsNullOrWhiteSpace(pathExt))
        {
            return [".exe", ".cmd", ".bat"];
        }

        return pathExt
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(ext => ext.StartsWith('.') ? ext : $".{ext}")
            .ToArray();
    }
}
