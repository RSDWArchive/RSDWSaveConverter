# Release Checklist

Use this checklist for every public release.

## Source

- [ ] Confirm the app version in `src/RSDWSaveConverter.App/RSDWSaveConverter.App.csproj`.
- [ ] Add the release date and changes to `CHANGELOG.md`.
- [ ] Confirm no save files, WGS captures, user paths, secrets, or build artifacts are tracked.
- [ ] Decide whether the source release requires a project license; no project license is currently declared.

## Automated Verification

- [ ] Run `dotnet restore RSDWSaveConverter.sln`.
- [ ] Run `dotnet format RSDWSaveConverter.sln --verify-no-changes --no-restore`.
- [ ] Run `dotnet test RSDWSaveConverter.sln --configuration Release --no-restore`.
- [ ] Run `dotnet list RSDWSaveConverter.sln package --vulnerable --include-transitive`.
- [ ] Run `.\scripts\publish.ps1` and confirm the EXE, ZIP, and SHA-256 manifest are produced.
- [ ] Open the ZIP and confirm it contains only the executable and release documentation.

## In-Game Smoke Tests

- [ ] Import a Steam `.sav` into a disposable Game Pass world and load it in-game.
- [ ] Import a character `.json` into a disposable Game Pass character and load it in-game.
- [ ] Confirm the generated WGS backup ZIP opens successfully.
- [ ] Confirm the destination `Qbak` payload remains unchanged.
- [ ] Confirm imports are blocked while Dragonwilds is running.
- [ ] Save once in-game and complete an Xbox cloud synchronization cycle.

## Publish

- [ ] Consider code signing; unsigned releases may trigger Windows SmartScreen.
- [ ] Commit and push the release-ready source.
- [ ] Create and push a tag matching the project version, such as `v0.1.0`.
- [ ] Confirm the GitHub Actions build succeeds and publishes exactly three release assets.
- [ ] Download the public assets and verify their hashes against the `.sha256` manifest.
- [ ] Keep the previous release available until the new build has passed a clean-machine smoke test.
