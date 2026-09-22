# RSDW Save Converter

RSDW Save Converter is a portable Windows utility for importing RuneScape: Dragonwilds Steam world and character saves into the Xbox Game Pass save container.

The app runs locally, makes no network requests, and creates a complete backup before changing a Game Pass profile.

## Download

Each GitHub release provides:

- `RSDWSaveConverter-v*-win-x64.exe`: the standalone portable application.
- `RSDWSaveConverter-v*-win-x64.zip`: the application plus README, changelog, and third-party notices.
- `RSDWSaveConverter-v*-win-x64.sha256`: SHA-256 hashes for both downloads.

The executable is not currently code-signed, so Windows SmartScreen may show an unknown-publisher warning. Verify the SHA-256 hash against the release manifest before running it.

## Supported Saves

| Source | Game Pass destination | Conversion |
| --- | --- | --- |
| Steam world `.sav` | Existing `Qxav` world slot | Adds the Game Pass header and zlib wrapper |
| Game Pass world `.xav` | Existing `Qxav` world slot | Validates and reimports the wrapped world |
| Steam character `.json` | Existing `Qjson` character slot | Preserves the JSON bytes exactly |

Version `0.1.0` replaces an existing destination slot. Create a disposable world or character in the Game Pass version first if you do not want to replace an existing save.

## Use

1. Close RuneScape: Dragonwilds.
2. Open `RSDWSaveConverter.exe`.
3. Drop in a world `.sav`/`.xav` or character `.json`.
4. Select the detected Game Pass profile, or choose its WGS folder manually.
5. Select a destination of the matching type.
6. Confirm **Import World** or **Import Character**.
7. Launch the game and choose the local save if Xbox presents a synchronization conflict.

The automatic profile search checks:

```text
%LOCALAPPDATA%\Packages\JagexLimited.Dominion_srxstwq7wczqa\SystemAppData\wgs
```

## Safety

- The game must be closed before an import can begin.
- A ZIP of the complete WGS profile is written before any save data changes.
- New payload and table files are written before `containers.index` is replaced.
- The updated payload is read back and compared with the source; a failed verification restores the original index.
- The destination's in-game `Qbak` backup slot is not changed.

Backups are stored under:

```text
%LOCALAPPDATA%\RSDWSaveConverter\Backups
```

Keep the backup until the imported save has loaded, saved, and synchronized successfully. Character files may contain account or gameplay information; do not attach them to public bug reports.

## Known Limitations

- Windows x64 only.
- A matching Game Pass destination slot must already exist.
- The executable is unsigned.
- Future game updates may require save-format updates.

## Build

Requirements:

- Windows 10 or later
- .NET 8 SDK

Run the release script from PowerShell:

```powershell
.\scripts\publish.ps1
```

It runs the test suite and creates the versioned EXE, ZIP, and SHA-256 manifest under `artifacts\`.

For individual development commands:

```powershell
dotnet restore RSDWSaveConverter.sln
dotnet test RSDWSaveConverter.sln --configuration Release
dotnet format RSDWSaveConverter.sln --verify-no-changes --no-restore
```

Optional local integration fixtures can be selected with `RSDW_SAVE_SAMPLE_ROOT`. Those fixtures are never included in release assets.

## Format Notes

Game Pass world payloads use a 12-byte little-endian header followed by zlib data:

```text
uint32 12
uint32 65536
uint32 uncompressed_save_length
byte[] zlib_compressed_SAVE_data
```

Game Pass character payloads use the same WGS index/container structure, but their `Data` blob is plain UTF-8 JSON. WGS payloads are stored behind GUID-named files described by `containers.index` and `container.N`.

See [CHANGELOG.md](CHANGELOG.md), [RELEASE_CHECKLIST.md](RELEASE_CHECKLIST.md), and [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) for release history and attribution.

RSDW Save Converter is a community utility and is not affiliated with or endorsed by Jagex or Microsoft.
