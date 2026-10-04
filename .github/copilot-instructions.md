# Copilot Instructions

## Project Guidelines
- When the Copilot SDK raises a permission request, the app should ask the user to approve or deny it and return that decision instead of auto-denying.

## What's New
- The console app's "✨ What's New" screen is driven by `src/Console/UI/WhatsNew.cs`. Whenever a change adds or noticeably changes something a user of the app would care about (a new feature, a changed default or behavior, a notable fix), add an entry for it in the same change.
- Add entries to the unreleased section at the top of `WhatsNew.Releases` (the one with `Version: null`), most important first. If the first section already has a version, add a new `Version: null` section above it. Never edit the version yourself; the release prompt stamps it.
- Write for users, not developers: a short title, then one or two sentences on what they can now do and where to find it in the app. Leave out refactors, internal cleanup, and dependency updates with no visible effect.
