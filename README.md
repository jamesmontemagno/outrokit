# 🎙️ Podcast Metadata Generator

Generate podcast metadata (titles, descriptions, chapters, SRT subtitles) from transcripts using AI powered by the GitHub Copilot SDK.

![.NET](https://img.shields.io/badge/.NET-10.0-512BD4?logo=dotnet)
![License](https://img.shields.io/badge/license-MIT-green)
![GitHub Copilot](https://img.shields.io/badge/GitHub%20Copilot-SDK-000?logo=github)

<img width="896" height="378" alt="Screenshot 2026-02-13 at 11 29 28 AM" src="https://github.com/user-attachments/assets/2dfef3a4-6323-4b18-b1b0-2e17ba2fddef" />


<img width="1145" height="682" alt="Screenshot 2026-02-13 at 11 29 09 AM" src="https://github.com/user-attachments/assets/4fe2fe1c-b390-42c0-b730-8aa5e1f03662" />

## ✨ Features

- **🎯 Title Generation** - Get multiple creative title suggestions for your episode
- **🎨 Title Styles** - Pick a style (balanced, descriptive, curiosity hook, question, how-to, playful, professional, SEO, or mixed) each time you generate titles
- **📝 Description Generation** - Create short, medium, and long descriptions optimized for different platforms
- **📑 Chapter Generation** - Auto-generate YouTube-compatible chapter markers with timestamps
- **🎬 SRT Conversion** - Convert transcripts to valid SRT subtitle format
- **🔄 Multiple Transcript Formats** - Support for Zencastr, time-range, SRT formats, and plain text
- **📂 File Browser** - Built-in file browser or drag-and-drop support
- **⚡ Streaming Responses** - Watch AI responses generate in real-time
- **📋 Copy to Clipboard** - Copy titles, descriptions, chapters, or everything at once; finished output is printed unwrapped so it also selects cleanly in the terminal
- **🤖 Model Selection** - Choose from multiple AI models (GPT-5, Claude, Gemini)
- **⚙️ Configurable Settings** - Customize generation parameters and save preferences

## 📋 Prerequisites

- [.NET 10.0 SDK](https://dotnet.microsoft.com/download) or later
- A GitHub Copilot subscription and authentication, unless using BYOK

The `GitHub.Copilot.SDK` package bundles a compatible Copilot runtime. Installing the Copilot CLI separately is optional, but useful when you want to sign in interactively or use a local CLI override.

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

Settings are automatically saved to `~/.config/podcast-metadata-generator/settings.json`.

## 🏗️ Project Structure

```
podcast-metadata-generator/
├── PodcastMetadataGenerator.sln     # Solution file
├── src/
│   ├── Core/                        # Shared class library
│   │   ├── Models/
│   │   │   ├── AppSettings.cs       # Configuration and generation settings
│   │   │   ├── GenerationResult.cs  # Results container
│   │   │   ├── Manifest.cs          # JSON manifest structure
│   │   │   ├── TitleStyle.cs        # Title style options and prompt guidance
│   │   │   ├── Transcript.cs        # Transcript model
│   │   │   └── TranscriptSegment.cs # Segment model
│   │   ├── Services/
│   │   │   ├── CopilotAuthService.cs   # CLI authentication checks
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
│   │   │   └── ConsoleUI.cs            # Spectre.Console UI helpers
│   │   └── Program.cs                  # Console entry point
│   └── Blazor/                      # Blazor Server demo (local only)
│       ├── Components/
│       │   ├── Layout/                 # MainLayout, NavMenu
│       │   └── Pages/                  # Home, Generate, Settings
│       ├── wwwroot/
│       │   └── css/app.css
│       └── Program.cs                  # Blazor entry point
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
