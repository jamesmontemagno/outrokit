namespace PodcastMetadataGenerator.Console.UI;

/// <summary>
/// A single user-facing change shown on the What's New screen.
/// </summary>
public sealed record WhatsNewItem(string Icon, string Title, string Description);

/// <summary>
/// The changes that shipped together. <paramref name="Version"/> is null until the release is tagged.
/// </summary>
public sealed record WhatsNewRelease(string? Version, IReadOnlyList<WhatsNewItem> Items)
{
    public string Heading => Version is null ? "Latest" : $"v{Version}";
}

/// <summary>
/// The content of the What's New screen, newest release first.
/// </summary>
/// <remarks>
/// Add user-facing changes to the unreleased entry (Version: null) at the top, creating it
/// if the first entry already has a version. The release prompt stamps the version.
/// </remarks>
public static class WhatsNew
{
    public static IReadOnlyList<WhatsNewRelease> Releases { get; } =
    [
        new(Version: "1.6.0",
        [
            new("✅", "Caption quality checks",
                "Before burning captions into a video, OutroKit checks timing, overlaps, duration, line length, line count, and reading speed. It can safely fix common SRT and WebVTT issues in a temporary copy, so your original captions stay unchanged."),
            new("🎨", "Four caption appearances",
                "Choose Classic Outline, Contrast Panel, Bold Impact, or Clean Shadow each time you burn captions into a video. Set your preferred appearance under Settings → Caption Settings.")
        ]),
        new(Version: "1.5.0",
        [
            new("📼", "Now called OutroKit",
                "Podcast Metadata Generator is now OutroKit, with a new look and a home at outrokit.com. It works the same for podcasts and videos alike. The package has a new name too: start it with dnx OutroKit, or run dotnet tool install -g OutroKit and then outrokit. The old package still works and shows how to switch, and your settings carry over."),
            new("📦", "A much smaller download",
                "OutroKit no longer carries its own copy of the Copilot runtime, so the download is about 11 MB, down from more than 100 MB. It uses the GitHub Copilot CLI installed on your computer, which every system now needs. If the app cannot find it, it shows how to install it."),
            new("🔥", "Burn captions into video",
                "Pick a video and a captions file (.srt, .vtt, .ass, or .ssa) from Burn Captions into Video on the main menu and get a new video with the captions drawn into the picture. You confirm the text size and position each time; set your defaults under Settings → Caption Settings. This needs an ffmpeg that includes libass, such as Homebrew's ffmpeg-full, and the app tells you if yours does not. For captions in another format, or that need more work first, try captionstack.app."),
            new("🤖", "Change Model no longer crashes",
                "Settings → Change Model used to close the app when Copilot offered two models with the same name, such as Auto. The list now opens, and models that share a name show their id so you can tell them apart.")
        ]),
        new(Version: "1.4.0",
        [
            new("🎧", "Transcribe video and audio files",
                "Load a video (.mp4, .mov, .mkv, and more) or audio file (.mp3, .wav) and get an SRT transcript made locally with ffmpeg and Whisper, ready for titles, descriptions, and chapters. Choose a model under Settings → Transcription Settings. Ctrl+C cancels a transcription or model download and returns to the menu."),
            new("🎨", "Title styles",
                "Choose a style each time you generate titles: Balanced, Descriptive, Curiosity Hook, Question, How-To, Playful, Professional, SEO Keywords, or Mixed Variety. Set your default under Settings → Generation Settings → Title Settings."),
            new("📋", "Copy to clipboard",
                "Copy the selected title, all titles, a description, chapters, or everything when a generation finishes, or any time from the main menu. Results are also printed unwrapped so they select cleanly in the terminal."),
            new("🔐", "Permission prompts",
                "When Copilot asks for permission during a generation, you choose whether to approve or deny it instead of the request failing."),
            new("🤖", "Copilot SDK 1.0 and a new default model",
                "Runs on the generally available GitHub Copilot SDK (1.0.16) and uses gpt-6-luna when no model has been chosen. Change it under Settings → Change Model.")
        ])
    ];
}
