# 🎙️ Podcast Metadata Generator

Generate podcast metadata (titles, descriptions, chapters, SRT subtitles) from transcripts using AI powered by the GitHub Copilot SDK.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)
![GitHub Copilot](https://img.shields.io/badge/GitHub%20Copilot-SDK-000?logo=github)

**New here?** The [step-by-step install and usage guide](https://outrokit.com/) at **outrokit.com** walks through everything, starting from opening a terminal.

<img width="896" height="378" alt="Screenshot 2026-02-13 at 11 29 28 AM" src="https://github.com/user-attachments/assets/2dfef3a4-6323-4b18-b1b0-2e17ba2fddef" />


<img width="1145" height="682" alt="Screenshot 2026-02-13 at 11 29 09 AM" src="https://github.com/user-attachments/assets/4fe2fe1c-b390-42c0-b730-8aa5e1f03662" />

## ✨ Features

- **🎯 Title Generation** - Get multiple creative title suggestions for your episode
- **🎨 Title Styles** - Pick a style (balanced, descriptive, curiosity hook, question, how-to, playful, professional, SEO, or mixed) each time you generate titles
- **📝 Description Generation** - Create short, medium, and long descriptions optimized for different platforms
- **📑 Chapter Generation** - Auto-generate YouTube-compatible chapter markers with timestamps
- **🎬 SRT Conversion** - Convert transcripts to valid SRT subtitle format
- **🔄 Multiple Transcript Formats** - Support for Zencastr, time-range, SRT formats, and plain text
- **🎧 Video & Audio Transcription** - Start from a video or audio file (`.mp3`, `.wav`) and generate an SRT transcript locally with ffmpeg and Whisper.net
- **🔥 Burn Captions into Video** - Draw an `.srt`, `.vtt`, `.ass`, or `.ssa` captions file into the picture of a video with ffmpeg
- **📂 File Browser** - Built-in file browser or drag-and-drop support
- **⚡ Streaming Responses** - Watch AI responses generate in real-time
- **📋 Copy to Clipboard** - Copy titles, descriptions, chapters, or everything at once; finished output is printed unwrapped so it also selects cleanly in the terminal
- **🤖 Model Selection** - Choose from multiple AI models (GPT-5, Claude, Gemini)
- **⚙️ Configurable Settings** - Customize generation parameters and save preferences
- **✨ What's New** - See the latest features from the main menu

## 📋 Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- A GitHub Copilot account and authentication, unless using BYOK. The free Copilot plan works
- [GitHub Copilot CLI](https://docs.github.com/en/copilot/how-tos/set-up/install-copilot-cli) installed, when running the NuGet tool on macOS, Windows, or Linux on Arm (see below)
- [ffmpeg](https://ffmpeg.org/download.html) available on `PATH`, or its executable path configured in Settings (only needed to transcribe video or audio files and to burn captions into video)
  - Burning captions needs an ffmpeg built with libass, which provides the `subtitles` filter. Homebrew's standard `ffmpeg` formula leaves it out; `brew install ffmpeg-full` includes it

The app talks to Copilot through a Copilot runtime, and whether you need to install one depends on how you run the app:

- **NuGet tool (`dnx` or `dotnet tool install`)** - The package only bundles the runtime for Linux x64. On macOS, Windows, and Linux on Arm, install the Copilot CLI and make sure `copilot` is on your `PATH`, or set `COPILOT_CLI_PATH` to the executable.
- **From source** - The `GitHub.Copilot.SDK` package bundles a runtime for the machine you build on, so the Copilot CLI is optional. It is still useful for signing in interactively.

If a Copilot CLI is found on your `PATH`, the app uses it in preference to the bundled runtime.

### Installing GitHub Copilot CLI

**macOS and Linux (Homebrew):**
```bash
brew install copilot-cli
```

**Windows (WinGet):**
```powershell
winget install GitHub.Copilot
```

**All platforms (npm, requires Node.js 22+):**
```bash
npm install -g @github/copilot
```

**macOS and Linux (install script):**
```bash
curl -fsSL https://gh.io/copilot-install | bash
```

Then authenticate:
```bash
copilot
# Type /login and follow the prompts
```

## 🚀 Installation

### Just run it with .NET 10+ (Recommended)

```bash
dnx PodcastMetadataGenerator
```

### As a .NET Tool

```bash
dotnet tool install -g PodcastMetadataGenerator
```

### From Source

```bash
git clone https://github.com/jamesmontemagno/podcast-metadata-generator.git
cd podcast-metadata-generator
dotnet build PodcastMetadataGenerator.sln
```

## 📚 Additional Docs

- [Using GitHub Copilot SDK built-in APIs in .NET](docs/copilot-sdk-dotnet-built-in-apis.md)

## 📖 Usage

### Console App - Interactive Mode

```bash
# If installed as a tool:
podcast-metadata-generator

# Or from source:
cd src/Console
dotnet run
```

### Console App - With a Transcript File

```bash
podcast-metadata-generator /path/to/transcript.txt

# Or from source:
dotnet run -- /path/to/transcript.txt
```

### Console App - With a Video or Audio File

Open **Settings → Transcription Settings**, choose a Whisper GGML model, then download and initialize it. After setup, select **Load Transcript, Video, or Audio → Video or audio file**, or pass the file on the command line:

```bash
podcast-metadata-generator /path/to/episode.mp4
podcast-metadata-generator /path/to/episode.mp3
```

Supported video formats are `.mp4`, `.mov`, `.mkv`, `.avi`, `.webm`, `.m4v`, `.wmv`, `.mpeg`, and `.mpg`. Supported audio formats are `.mp3` and `.wav`.

The app checks the file for an audio track with ffmpeg, asks where to save the `.srt`, transcribes the audio locally, and loads that SRT into the existing metadata flow. Model downloads and transcription can take a while; press **Ctrl+C** to cancel either one and return to the menu.

Models are downloaded from the Whisper.net Hugging Face repository into `~/.podcast-metadata-generator/models`. Smaller models are faster and use less memory; larger models generally improve accuracy. English (`.en`) variants only transcribe English, while the other models are multilingual.

### Console App - Burn Captions into a Video

Select **Burn Captions into Video** from the main menu, then choose a video and a captions file (`.srt`, `.vtt`, `.ass`, or `.ssa`). No transcript needs to be loaded, and an SRT made by the app from the same video works as the captions file.

The app asks you to confirm the caption text size (Small, Medium, or Large) and position (Bottom or Top), then where to save the result. The default is `<video name>-captioned` next to the original, and the original is never overwritten. Captions are white with a dark outline and are sized to the video, so they look the same at 720p and 4K and stay proportionate on portrait video. An `.ass` or `.ssa` file keeps its own fonts, sizes, and positions.

Burning captions re-encodes the picture as H.264, so it takes a while on long videos. The result is saved as `.mp4`, `.mov`, `.mkv`, or `.m4v`; a source in another format is saved as `.mp4`. Audio is copied unchanged when the format stays the same and converted to AAC otherwise. Press **Ctrl+C** to cancel and return to the menu; a cancelled or failed burn saves nothing and leaves an existing file as it was.

This feature needs an ffmpeg that includes libass. If yours does not, the app says so and offers to switch to another one. With Homebrew:

```bash
brew install ffmpeg-full
# It is installed beside the standard ffmpeg, not on PATH. Use this as the ffmpeg path:
echo "$(brew --prefix ffmpeg-full)/bin/ffmpeg"
```

## 🌐 Blazor Demo (Local Only)

A web UI demo is included for local development and presentations.

> ⚠️ **Local Use Only**: The Blazor app uses your local Copilot authentication and is not designed for deployment or multi-user access.

### Running the Blazor Demo

```bash
# From repository root
cd src/Blazor
dotnet run
```

Then open https://localhost:5001 in your browser.

### Features
- Drag-and-drop file upload
- Real-time streaming AI output
- Tabbed results view
- Settings persistence via localStorage

### Title Styles

| Style | What you get |
|-------|--------------|
| Balanced | Engaging, descriptive, and SEO-friendly |
| Descriptive | Clear and straightforward, no hype |
| Curiosity Hook | Teases a hook that makes people want to listen |
| Question | Phrased as a question the episode answers |
| How-To / Educational | Outcome-focused: how-to, guide, lessons learned |
| Playful | Witty, with wordplay or humor |
| Professional | Polished and authoritative |
| SEO Keywords | Leads with the terms people search for |
| Mixed Variety | A different style for each title |

Set the default under **Settings → Generation Settings → Title Settings** (console) or on the **Settings** page (Blazor). The default is preselected whenever you generate titles, and you can choose a different style for that run.

### Copying Results (Console)

When a generation finishes, the console offers a **Copy to clipboard** menu for the selected title, all titles, each description, chapters, or everything. The same menu is available from the main menu once you have results. On Linux, clipboard access requires `xsel`.

## 🌍 Website

The guide at [outrokit.com](https://outrokit.com/) lives in [`site/`](site/) as plain HTML, CSS, and JavaScript with no build step. Pushing changes under `site/` to `main` deploys it through the [Pages workflow](.github/workflows/pages.yml).

To preview it locally:

```bash
cd site
python3 -m http.server 4173
```

Then open http://localhost:4173.

The site is served at outrokit.com through the custom domain in the repository's Pages settings.

The image shown when the link is shared is `site/og.png`. It is a 1200×630 screenshot of [`design/og-image.html`](design/og-image.html), which uses the site's fonts and colors. To regenerate it after an edit, with Chrome or Edge:

```bash
"/Applications/Google Chrome.app/Contents/MacOS/Google Chrome" --headless --hide-scrollbars \
  --force-device-scale-factor=1 --window-size=1200,630 \
  --screenshot="$PWD/site/og.png" "file://$PWD/design/og-image.html"
```

The browser can keep running after the file is written; stop it with Ctrl+C.

## 🎯 Supported Transcript Formats

### Zencastr Format
```
00:00.00 Speaker 1: Hello and welcome to the show.
00:15.50 Speaker 2: Thanks for having me!
```

### Time-Range Format
```
00:00:00 - 00:00:15
Hello and welcome to the show.

00:00:15 - 00:00:30
Thanks for having me!
```

### SRT Format
```
1
00:00:00,000 --> 00:00:15,000
Hello and welcome to the show.

2
00:00:15,000 --> 00:00:30,000
Thanks for having me!
```

### Plain Text
Any text file without timestamps will be processed as plain text. Note: Chapter generation and SRT conversion require timestamps.

## 📁 Output Files

When you save results, the following files are generated:

| File | Description |
|------|-------------|
| `titles.txt` | List of generated title suggestions |
| `description-short.txt` | Short description (~50 words) |
| `description-medium.txt` | Medium description (~150 words) |
| `description-long.txt` | Long description (~300 words) |
| `chapters.txt` | YouTube-compatible chapter markers |
| `subtitles.srt` | SRT subtitle file |
| `manifest.json` | JSON manifest with all metadata |

## ⚙️ Configuration

Access settings from the main menu to configure:

### General Settings
- **AI Model** - Select from available Copilot models (dynamically fetched from CLI)
- **ffmpeg Path** - Executable path or command used to check video and audio files, extract 16 kHz mono audio, and burn captions into video
- **Whisper GGML Model** - Select, download, and initialize a local Whisper.net model for transcription
- **Caption Size and Position** - Default text size (default: Medium) and position (default: Bottom) for captions burned into video. You are asked to confirm or change them each time you burn captions
- **Output Directory** - Default location for saved files
- **Podcast Name** - Your podcast name (used in prompts for better context)
- **Host Names** - Host names (used in prompts)
- **Episode Context** - Add guest names, topics, or other context to improve generation

### Generation Settings
- **Title Count** - Number of title suggestions to generate (default: 5)
- **Title Max Words** - Maximum words per title (default: 10)
- **Title Style** - Default style for generated titles (default: Balanced). You are asked to confirm or change it each time you generate titles or all metadata
- **Description Lengths** - Word counts for short/medium/long descriptions (default: 50/150/300)
- **Chapter Range** - Min/max chapters to generate (default: 3-12)
- **Chapters per 30 min** - Target density of chapters (default: 5)
- **Chapter Title Words** - Max words per chapter title (default: 8)

Settings are automatically saved to `~/.podcast-metadata-generator/settings.json`.

## 🏗️ Project Structure

```
podcast-metadata-generator/
├── PodcastMetadataGenerator.sln     # Solution file
├── src/
│   ├── Core/                        # Shared class library
│   │   ├── Models/
│   │   │   ├── AppSettings.cs       # Configuration and generation settings
│   │   │   ├── CaptionStyle.cs      # Caption size and position options
│   │   │   ├── GenerationResult.cs  # Results container
│   │   │   ├── Manifest.cs          # JSON manifest structure
│   │   │   ├── TitleStyle.cs        # Title style options and prompt guidance
│   │   │   ├── Transcript.cs        # Transcript model
│   │   │   └── TranscriptSegment.cs # Segment model
│   │   ├── Services/
│   │   │   ├── CaptionBurnService.cs   # Burns captions into video with ffmpeg
│   │   │   ├── CopilotAuthService.cs   # CLI authentication checks
│   │   │   ├── FfmpegRunner.cs         # Shared ffmpeg process launcher
│   │   │   ├── MetadataGenerator.cs    # AI generation via Copilot SDK
│   │   │   ├── OutputService.cs        # File output handling
│   │   │   ├── SettingsService.cs      # Settings persistence
│   │   │   ├── SrtConverter.cs         # SRT format conversion
│   │   │   └── TranscriptParser.cs     # Multi-format transcript parsing
│   │   └── Prompts/
│   │       └── PromptTemplates.cs      # AI prompt templates
│   ├── Console/                     # Console application
│   │   ├── UI/
│   │   │   ├── AppWorkflow.cs          # Main application workflow
│   │   │   ├── ConsoleUI.cs            # Spectre.Console UI helpers
│   │   │   └── WhatsNew.cs             # Entries for the What's New screen
│   │   └── Program.cs                  # Console entry point
│   └── Blazor/                      # Blazor Server demo (local only)
│       ├── Components/
│       │   ├── Layout/                 # MainLayout, NavMenu
│       │   └── Pages/                  # Home, Generate, Settings
│       ├── wwwroot/
│       │   └── css/app.css
│       └── Program.cs                  # Blazor entry point
├── tests/                           # xUnit tests, run on Linux, Windows, and macOS in CI
└── data/                            # Sample transcripts
```

## 🤝 Contributing

Contributions are welcome! Please see [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add some amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

## 📄 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- [GitHub Copilot SDK](https://github.com/github/copilot-sdk) for AI integration
- [Spectre.Console](https://spectreconsole.net/) for the beautiful terminal UI
- Inspired by [jamesmontemagno/app-podcast-assistant](https://github.com/jamesmontemagno/app-podcast-assistant)
