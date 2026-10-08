---
name: create-and-tag-release
description: Create and publish a new release for this repo with a v-prefixed semver tag
disable-model-invocation: true
argument-hint: "version (e.g. 1.2.3)"
---
Create and tag a new release for this repository using the provided version argument.

Requirements:
- Interpret the argument as a semver version without `v` (for example `1.2.3`).
- Use `v<version>` as the git tag (for example `v1.2.3`).
- Never delete or rewrite existing tags.
- If the target tag already exists locally or remotely, stop and report it.

Workflow:
1. Validate input format is semver (`X.Y.Z` with optional prerelease/build metadata).
2. Check git status and branch; require `main` unless explicitly told otherwise.
3. In `src/Console/UI/WhatsNew.cs`, if the first entry of `WhatsNew.Releases` has `Version: null`, set it to the release version (without `v`) so the What's New screen names the release. Check the commits since the previous tag and add any user-facing change that is missing from that entry.
4. If there are relevant uncommitted changes for the release, commit them with a clear message. If no changes, do not create an empty commit.
5. Push `main` to `origin`.
6. Create an annotated tag `v<version>` with message `Release v<version>`.
7. Push the tag to `origin`.
8. Verify the tag exists on origin and show the exact commit SHA it points to.
9. Confirm that the release workflow should trigger from the `v*` tag.

Output format:
- `Version:` the requested version and final tag.
- `Commit:` the commit SHA tagged.
- `Actions:` bullet list of commands executed.
- `Result:` success/failure with a short reason.
- `Next:` if successful, include one line about checking GitHub Actions run status.

If any step fails, stop immediately, report the failing command and stderr, and suggest the safest recovery step.
