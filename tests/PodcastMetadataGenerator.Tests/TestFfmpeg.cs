using System.Diagnostics;
using System.Runtime.InteropServices;
using PodcastMetadataGenerator.Core.Models;
using PodcastMetadataGenerator.Core.Services;

namespace PodcastMetadataGenerator.Tests;

/// <summary>
/// Finds the ffmpeg builds the tests run against.
/// </summary>
/// <remarks>
/// Set PMG_TEST_FFMPEG to choose the ffmpeg used for burning captions. Otherwise the tests try
/// "ffmpeg" on PATH and then Homebrew's ffmpeg-full. Tests that need ffmpeg are skipped when none
/// is found, unless PMG_TEST_REQUIRE_FFMPEG is 1, as it is in CI, where they fail instead.
/// </remarks>
internal static class TestFfmpeg
{
    public const string PathVariable = "PMG_TEST_FFMPEG";
    public const string RequireVariable = "PMG_TEST_REQUIRE_FFMPEG";

    private static readonly Lazy<(string? WithCaptions, string? WithoutCaptions)> Found = new(Discover);

    public static bool IsRequired => Environment.GetEnvironmentVariable(RequireVariable) == "1";

    /// <summary>An ffmpeg that has the subtitles filter, or null.</summary>
    public static string? WithCaptionSupport => Found.Value.WithCaptions;

    /// <summary>An ffmpeg that runs but lacks the subtitles filter, or null.</summary>
    public static string? WithoutCaptionSupport => Found.Value.WithoutCaptions;

    public const string MissingMessage =
        "No ffmpeg with the subtitles filter was found. Install one, or set PMG_TEST_FFMPEG to its path.";

    private static (string?, string?) Discover()
    {
        string?[] candidates =
        [
            Environment.GetEnvironmentVariable(PathVariable),
            "ffmpeg",
            CaptionBurnService.GetHomebrewFullFfmpegPath()
        ];

        string? withCaptions = null;
        string? withoutCaptions = null;
        foreach (var candidate in candidates)
        {
            if (string.IsNullOrWhiteSpace(candidate))
            {
                continue;
            }

            try
            {
                // Run off the caller's context: this is reached from attribute constructors.
                var supported = Task.Run(() =>
                    new CaptionBurnService(new AppSettings { FfmpegPath = candidate }).SupportsCaptionBurnAsync())
                    .GetAwaiter().GetResult();
                if (supported)
                {
                    withCaptions ??= candidate;
                }
                else
                {
                    withoutCaptions ??= candidate;
                }
            }
            catch (FfmpegNotFoundException)
            {
            }
        }

        return (withCaptions, withoutCaptions);
    }

    /// <summary>
    /// Runs ffmpeg for test setup and inspection, failing with its error output.
    /// </summary>
    public static async Task RunAsync(string ffmpegPath, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = ffmpegPath,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true
        };
        string[] quiet = ["-nostdin", "-hide_banner", "-loglevel", "error", "-y"];
        foreach (var argument in quiet.Concat(arguments))
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException($"Could not start {ffmpegPath}.");
        var output = process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await output;
        await process.WaitForExitAsync();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(
                $"ffmpeg {string.Join(' ', arguments)} exited with {process.ExitCode}: {error.Trim()}");
        }
    }
}

/// <summary>
/// A test that burns captions, so it needs an ffmpeg with the subtitles filter.
/// </summary>
public sealed class CaptionFfmpegFactAttribute : FactAttribute
{
    public CaptionFfmpegFactAttribute()
    {
        if (TestFfmpeg.WithCaptionSupport is null && !TestFfmpeg.IsRequired)
        {
            Skip = TestFfmpeg.MissingMessage;
        }
    }
}

/// <inheritdoc cref="CaptionFfmpegFactAttribute"/>
public sealed class CaptionFfmpegTheoryAttribute : TheoryAttribute
{
    public CaptionFfmpegTheoryAttribute()
    {
        if (TestFfmpeg.WithCaptionSupport is null && !TestFfmpeg.IsRequired)
        {
            Skip = TestFfmpeg.MissingMessage;
        }
    }
}

/// <summary>
/// A test that needs an ffmpeg built without the subtitles filter, such as Homebrew's standard formula.
/// </summary>
public sealed class FfmpegWithoutCaptionsFactAttribute : FactAttribute
{
    public FfmpegWithoutCaptionsFactAttribute()
    {
        // Only macOS CI installs such a build on purpose, so only there is its absence a failure.
        var expected = TestFfmpeg.IsRequired && RuntimeInformation.IsOSPlatform(OSPlatform.OSX);
        if (TestFfmpeg.WithoutCaptionSupport is null && !expected)
        {
            Skip = "No ffmpeg without the subtitles filter was found on PATH.";
        }
    }
}

/// <summary>
/// A test of the Homebrew ffmpeg-full install the app suggests on macOS.
/// </summary>
public sealed class HomebrewFullFfmpegFactAttribute : FactAttribute
{
    public HomebrewFullFfmpegFactAttribute()
    {
        var path = CaptionBurnService.GetHomebrewFullFfmpegPath();
        if (path is null)
        {
            Skip = "Homebrew's ffmpeg-full is a macOS install.";
        }
        else if (!File.Exists(path) && !TestFfmpeg.IsRequired)
        {
            Skip = "Homebrew's ffmpeg-full is not installed.";
        }
    }
}
