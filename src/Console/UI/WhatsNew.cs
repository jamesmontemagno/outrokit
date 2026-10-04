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
        new(Version: null,
        [
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
