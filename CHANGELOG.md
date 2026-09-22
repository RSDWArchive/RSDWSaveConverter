# Changelog

All notable changes to RSDW Save Converter are documented here.

## 0.1.0 - 2026-09-22

### Added

- Portable, self-contained Windows x64 application.
- Steam `.sav` and Game Pass `.xav` world import.
- Steam character `.json` import with byte-for-byte preservation.
- Automatic Dragonwilds WGS profile discovery and manual profile selection.
- Existing world and character destination previews.
- Complete pre-import WGS ZIP backups.
- Transactional index replacement, rollback, and post-import verification.
- Versioned release EXE/ZIP assets and SHA-256 manifest.

### Known Limitations

- Imports replace an existing destination slot; creating a brand-new WGS slot is not yet supported.
- The executable is not code-signed.
