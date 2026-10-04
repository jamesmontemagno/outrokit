# Product

<!-- impeccable:product-schema 1 -->

## Platform

web

## Users

Podcasters and YouTubers who have a finished episode and still need to publish it. This includes people who have never installed .NET or used a command line (confirmed by James Montemagno, 2026-10-04). They arrive with a transcript, a video, or an audio file and want titles, show notes, chapter markers, and subtitles without writing them by hand.

Secondary: developers who want to see a working GitHub Copilot SDK app in .NET.

## Product Purpose

Podcast Metadata Generator is a terminal app that reads an episode transcript and writes the publishing metadata for it: title suggestions, short/medium/long descriptions, YouTube-compatible chapters, and an SRT subtitle file. It can also start from a video or audio file by transcribing it locally first. Success is a visitor going from "I have an episode file" to saved, paste-ready metadata.

## Positioning

It runs on the user's own machine as a single command, uses their GitHub Copilot account for generation, including the free plan, and transcribes audio locally with Whisper rather than uploading it to a transcription service.

## Operating Context

- Run from a terminal: `dnx PodcastMetadataGenerator` (no install) or `dotnet tool install -g PodcastMetadataGenerator`, then `podcast-metadata-generator [file]`.
- Interactive, menu-driven UI (Spectre.Console): load a file, generate titles / descriptions / chapters / SRT, copy to clipboard, save results, settings.
- Inputs: transcripts in Zencastr, time-range, SRT, or plain text formats; video (`.mp4`, `.mov`, `.mkv`, `.avi`, `.webm`, `.m4v`, `.wmv`, `.mpeg`, `.mpg`); audio (`.mp3`, `.wav`).
- Outputs, named after the input file (per `OutputService.cs`): `<name>_titles.txt`, `<name>_description_short.txt`, `<name>_description_medium.txt`, `<name>_description_long.txt`, `<name>_chapters.txt`, `<name>.srt`, `<name>_manifest.json`. The README's output table lists older unprefixed names.
- Settings persist to `~/.podcast-metadata-generator/settings.json`; Whisper models download to `~/.podcast-metadata-generator/models`.

## Capabilities and Constraints

- Requires the .NET 10 SDK or later.
- Requires a GitHub Copilot account and sign-in, unless using BYOK. The free Copilot plan works; no paid subscription is needed (confirmed by James Montemagno, 2026-10-04). The NuGet tool only bundles a Copilot runtime for Linux x64; on macOS, Windows, and Linux on Arm the Copilot CLI must be installed and on `PATH` (or `COPILOT_CLI_PATH` set). It is also how users sign in.
- ffmpeg is required only for video/audio transcription.
- Chapters and SRT conversion require a transcript with timestamps; plain text supports titles and descriptions only.
- Nine title styles: Balanced, Descriptive, Curiosity Hook, Question, How-To / Educational, Playful, Professional, SEO Keywords, Mixed Variety.
- Defaults: 5 titles, 10 words max; descriptions of about 50 / 150 / 300 words; 3–12 chapters.
- Twelve Whisper GGML model options from Tiny (75 MiB) to Large v3 (2.9 GiB); Base is the default.
- On Linux, clipboard copy requires `xsel`.
- The Blazor web UI in this repo is a local-only demo and is not the subject of the public site.

## Brand Commitments

- Name: Podcast Metadata Generator. Package ID `PodcastMetadataGenerator`; command `podcast-metadata-generator`.
- Author: James Montemagno. MIT licensed, open source on GitHub.

## Evidence on Hand

- README.md is the source of truth for install commands, formats, outputs, and settings.
- Published on NuGet as `PodcastMetadataGenerator`; latest GitHub release v1.3.2.
- Two terminal screenshots are linked from the README (hosted on GitHub user attachments).
- Any example episode shown on the site must be made up and clearly labeled as an example (confirmed 2026-10-04). Do not present invented output as real tool output, and do not fabricate testimonials, user counts, or benchmarks.

## Product Principles

1. The visitor's episode file is the starting point; everything is explained in terms of what they hand in and what they get back.
2. Assume no terminal experience. Every command is shown whole, copyable, and explained in plain words.
3. Be honest about prerequisites up front: .NET 10, a Copilot account (the free plan works), and ffmpeg only when transcribing.
4. Show the output, since titles, descriptions, chapters, and subtitles are the reason to install.

## Accessibility & Inclusion

No product-specific standard was set. Not confirmed with the owner: the site is built to WCAG 2.2 AA as a working default.
