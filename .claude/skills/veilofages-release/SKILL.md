---
name: veilofages-release
description: Prepare and publish a new Veil of Ages release. Use when cutting a new version, tagging a release, updating version numbers, or consolidating the changelog. Triggers on tasks like "prepare a release", "bump the version", "tag v0.2.0", "cut a release".
---

# Veil of Ages Release Process

## Pre-Release Checklist

1. **Ensure CI is green** on main: check https://github.com/azrazalea/Veil-of-Ages/actions
2. **Build locally**: `dotnet build` — must pass with 0 warnings, 0 errors
3. **Verify no uncommitted changes**: `git status`
4. **Test in-game**: Run the game and verify core gameplay works

## Version Bump

Update the version string in all locations:

1. `Veil of Ages.csproj` — `<Version>X.Y.Z</Version>`
2. `export_presets.cfg` — `application/file_version` and `application/product_version` (format: `"X.Y.Z.0"`)
3. `README.md` — version badge: `![Version: X.Y.Z](https://img.shields.io/badge/Version-X.Y.Z-green)`

Veil of Ages uses [Semantic Versioning](https://semver.org/).

## Consolidate Changelog

In `CHANGELOG.md`:

1. Move all entries from `## [Unreleased]` into a new `## [X.Y.Z] - YYYY-MM-DD` section
2. Leave `## [Unreleased]` empty (but keep the heading)
3. Review entries — consolidate, reword for clarity, remove internal-only changes
4. Ensure categories follow Keep a Changelog: Added, Changed, Deprecated, Removed, Fixed, Security

The release workflow extracts this section for the GitHub Release body.

## Commit, Tag, Push

```bash
git add -A
git commit -m "Release vX.Y.Z"
git push
git tag vX.Y.Z
git push origin vX.Y.Z
```

## Wiki Submodule

If wiki pages were updated since the last release:

```bash
cd wiki
git add -A && git commit -m "Update wiki for vX.Y.Z release"
git push
cd ..
git add wiki && git commit -m "Update wiki submodule for vX.Y.Z" && git push
```

## What Happens Automatically

The GitHub Actions workflow (`.github/workflows/build.yml`) triggers on `v*` tags and:

1. Builds Godot exports for Windows, Linux, and macOS
2. Copies JSON resources and documentation (CHANGELOG, README, LICENSE) into build artifacts
3. Creates platform archives (`.zip` for Windows/macOS, `.tar.gz` for Linux)
4. Extracts the tagged version's section from CHANGELOG.md for the release body
5. Creates a GitHub Release with platform archives and auto-generated commit notes

## Post-Release

- Verify the release at https://github.com/azrazalea/Veil-of-Ages/releases
- Download and spot-check at least one platform archive
- If something went wrong: delete the tag (`git tag -d vX.Y.Z && git push origin :refs/tags/vX.Y.Z`), fix, and re-tag
