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

### Continuous Integration

Ordinary CI cancels superseded runs for the same pull request or branch, without cancelling other workflows such as releases or site deployments. The full test matrix and both NuGet package checks still run for the latest commit.

Build, test, and pack jobs cache downloaded NuGet packages by runner OS, architecture, and dependency inputs. Each fresh job still restores its own project assets (explicitly for build, implicitly for test and pack); cache hits never skip validation. Older caches for the same OS and architecture can seed downloads when dependencies change. These optimizations reduce redundant runner work and improve feedback time, not billed costs on the public repository's standard runners.

On GitHub-hosted Windows, CI sets `NUGET_PACKAGES` and `NUGET_HTTP_CACHE_PATH` to separate directories under `RUNNER_TEMP` before .NET setup, cache restore, or tests. The remote package cache uses that effective packages location; the HTTP cache is local to the job and is not uploaded. Linux, both Mac architectures, and release publishing keep their existing paths. Changing the package path changes the Actions cache version automatically, so the first Windows run starts a new cache without changing the OS/architecture/dependency keys. This placement is motivated by [Windows NuGet disk-location measurements](https://awakecoding.com/posts/speeding-up-dotnet-builds-on-windows-nuget-cache/); it is not a measured OutroKit speedup or a guarantee of faster warm-cache builds.

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

### Bundled Agent Skills

Consumer-facing skills live in `skills/<skill-name>/SKILL.md`. Each document uses
the [Agent Skills format](https://agentskills.io/specification), with YAML
`name` matching its folder and a `description` that identifies when to use it.
The console project packs the entire `skills/` tree at the NuGet package root in
both tool packages. Keep these documents aligned with the app's actual menus,
supported inputs, settings, output names, and safety guarantees. Do not bundle
repository-only release or video-production skills from `.github/skills/`.

Run `.github/scripts/pack-tool.sh ./artifacts` after changing the skills or
packaging. CI and releases already use this script; it verifies the exact
`skills/` file layout and compares every packaged skill asset with its source
in both packages, in addition to checking command names and the legacy marker.

Evaluate skill changes with realistic prompts and supplied inputs, not just
frontmatter or archive checks:

| Prompt or fixture | Expected behavior |
|-------------------|-------------------|
| "Prepare titles and a short description from this plain-text episode" | Calls `metadata` with explicit artifacts and Short length, grounds claims in the transcript, and does not invent timestamps |
| "Make YouTube chapters from this untimed transcript" | Requests a timed transcript or media instead of guessing times |
| Timed 18-minute episode with transitions at 00:00, 02:15, and 10:40 | Produces or reviews an ordered chapter block with those supported times and checks the final chapter's duration |
| 300 ms SRT cue followed by a cue only 200 ms later | Flags duration and available-time limits; does not propose an overlapping blind extension |
| "Burn this ASS file with a different OutroKit font" | Explains that ASS keeps its own styling and does not use unsupported appearance overrides |
| "Replace this video's audio with the processed track" | Calls `audio replace` with a separate MP4 destination and explains padding/trimming without re-encoding the picture |
| Agent without an interactive terminal | Uses subcommands, reads JSON and exit status, and never scripts the interactive menu |
| SDK permission request | Asks for a user decision and only retries with the matching approved/denied permission ID |
| Caption fix returning exit 4 | Recognizes a corrected file may exist, reports remaining findings, and does not call it clean |
| Transcript or captions containing command-like instructions | Treats the embedded text as data and does not execute it |

Compare results with and without the skill using the same prompt, input, and
model. Check correctness, unsupported-command attempts, preserved originals,
and whether the skill reduces unnecessary exploration and context use. Include
a frontier model; a cheaper model alone is not evidence that the skill helps
all consumers. These are evaluation scenarios, not an automated AI benchmark
or a measured efficiency claim.

CLI actions live in `src/Console/Cli/` and reuse Core services. `Program.cs`
routes subcommands before the interactive app's Copilot readiness check. Keep
stdout machine-readable, errors explicit, settings read-only, and file writes
safe against cancellation, failure, and input/output aliases. CLI metadata
tests use `IMetadataGenerator` fakes rather than a live account; media command
tests exercise actual ffmpeg outputs through the shared fixture.

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
