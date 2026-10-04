# Contributing to OutroKit

First off, thank you for considering contributing to OutroKit! It's people like you that make this tool better for everyone.

## Code of Conduct

This project and everyone participating in it is governed by our commitment to providing a welcoming and inclusive experience for everyone. Please be respectful and constructive in all interactions.

## How Can I Contribute?

### Reporting Bugs

Before creating bug reports, please check the existing issues to avoid duplicates. When you create a bug report, include as many details as possible:

- **Use a clear and descriptive title**
- **Describe the exact steps to reproduce the problem**
- **Provide the transcript format you were using** (if applicable)
- **Include any error messages** you received
- **Specify your environment**: OS, .NET version, Copilot CLI version

### Suggesting Enhancements

Enhancement suggestions are tracked as GitHub issues. When creating an enhancement suggestion:

- **Use a clear and descriptive title**
- **Provide a detailed description** of the suggested enhancement
- **Explain why this enhancement would be useful**
- **List any alternatives you've considered**

### Pull Requests

1. **Fork the repo** and create your branch from `main`
2. **Write clear, readable code** that follows the existing style
3. **Add comments** for complex logic
4. **Test your changes** thoroughly
5. **Update documentation** if needed
6. **Write a clear PR description** explaining what and why

## Development Setup

### Prerequisites

- .NET 10.0 SDK or later
- GitHub Copilot CLI (authenticated)
- A code editor (VS Code with C# extension recommended)

### Getting Started

```bash
# Clone your fork
git clone https://github.com/your-username/outrokit.git
cd outrokit

# Build the project
dotnet build

# Run tests
dotnet test

# Run the application
dotnet run
```

The tests in `tests/` burn captions into short clips that ffmpeg generates on the spot, so most of them need an ffmpeg that includes libass. They look for `ffmpeg` on your `PATH`, then for Homebrew's `ffmpeg-full`; set `PMG_TEST_FFMPEG` to use a different one. Tests that cannot find a suitable ffmpeg are skipped locally. CI sets `PMG_TEST_REQUIRE_FFMPEG=1`, which makes them fail instead, and runs them on Linux, Windows, and both kinds of Mac.

### Project Structure

- `Models/` - Data models and settings
- `Services/` - Business logic and integrations
- `Prompts/` - AI prompt templates
- `UI/` - Console UI components
- `Program.cs` - Application entry point

### NuGet Packages

The console app is published as two tool packages, and `.github/scripts/pack-tool.sh` packs and checks both:

- **`OutroKit`** is the app, with the command `outrokit`.
- **`PodcastMetadataGenerator`** is the app's original package. A tool package cannot depend on another package, so it carries the same app under its old ID and old command, and tells people at startup to move to `OutroKit`.

Releases publish through nuget.org [Trusted Publishing](https://learn.microsoft.com/nuget/nuget-org/trusted-publishing), so there is no API key to store. The policy on nuget.org names this repository and the workflow file `release.yml`, with no environment, and has to cover both package IDs. If the repository is renamed, update the policy to match.

When the old package is no longer needed, deprecate it on nuget.org with `OutroKit` as the alternate package, and remove it from the push step in `.github/workflows/release.yml`.

## Style Guidelines

### C# Style

- Use C# 12+ features where appropriate
- Follow [Microsoft's C# coding conventions](https://docs.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use nullable reference types (`#nullable enable`)
- Prefer `var` when the type is obvious
- Use expression-bodied members for simple getters/methods
- Add XML documentation comments for public APIs

### Commit Messages

- Use the present tense ("Add feature" not "Added feature")
- Use the imperative mood ("Move cursor to..." not "Moves cursor to...")
- Keep the first line under 72 characters
- Reference issues and PRs when relevant

### Example

```csharp
/// <summary>
/// Parses a transcript file and returns structured segments.
/// </summary>
/// <param name="filePath">Path to the transcript file.</param>
/// <returns>A parsed transcript with segments.</returns>
public async Task<Transcript> ParseAsync(string filePath)
{
    // Implementation
}
```

## Adding New Features

### New Transcript Formats

1. Add format detection in `TranscriptParser.cs`
2. Create a parsing method following the existing pattern
3. Update `TranscriptFormat` enum in `Transcript.cs`
4. Add tests and update README

### New AI Features

1. Add prompt template in `PromptTemplates.cs`
2. Add generation method in `MetadataGenerator.cs`
3. Update `GenerationResult.cs` if new data fields needed
4. Add UI flow in `AppWorkflow.cs`
5. Update `OutputService.cs` for file output

## Questions?

Feel free to open an issue with the "question" label if you have any questions about contributing.

Thank you for contributing! 🎙️
