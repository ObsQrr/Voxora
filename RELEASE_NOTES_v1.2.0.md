# Voxora v1.2.0 Release Notes

**Date:** 2026-10-28  
**Branch:** `voxora`  
**Base Commit:** `028449a` (v1.1.0)

---

## 🌍 Full English UI Support
- Complete **Turkish + English** localization across all windows, dialogs, tray menu, device lists, categories, and hotkey names.
- Language selector in **Settings → General → Language** (`tr` / `en`).
- Default language remains **Turkish**.
- 209 localization keys with exact parity between TR/EN.

---

## 🔧 Bug Fixes
| # | Area | Fixed |
|---|------|-------|
| 1 | Playback | Muted sounds no longer audible at trigger moment |
| 2 | Audio Engine | Streaming file handles now released when voice finishes |
| 3 | Device Selection | ComboBox TwoWay binding race condition fixed — devices no longer reset to blank on refresh |
| 4 | Theme | Light theme now defines `Icon` / `IconSm` styles (was missing) |
| 5 | Context Menus | Sound card & category right-click menus now work (ContextMenu visual tree fix) |
| 6 | Import | ZIP zip-slip, symlink, entry count (5K), total size (2 GB), single file (512 MB) limits enforced |
| 7 | JSON | 32 MB document size guard added |
| 8 | Settings | `micDeviceId` / `monitorDeviceId` no longer silently cleared |

---

## 🛡 Security Hardening
- Import/Export: atomic export overwrite, zip-slip protection, symlink skipping, safe path resolution
- JSON: max depth 64, 32 MB document limit, strict parsing
- Audio decode: `maxBytes` DoS guard
- Device enumeration: proper COM object disposal

---

## 🎨 UX Improvements
- Volume/mute changes debounced (400 ms) to reduce disk writes
- Theme now resolved from application-level resources only — consistent across all windows
- Device display names: `Voxora In Audio` / `Voxora Out Audio` (internal MMDevice IDs preserved)
- Cable guide wired to real window; category context menu selects right-clicked category

---

## 📦 Distribution
| Package | SHA256 | Size |
|---------|--------|------|
| `Voxora-v1.2.0-win-x64.zip` (self-contained portable) | `61328C324F83CCC2585DE7CD0040AFD5932805B69A6F9613EA63C9D0FF426A7A` | 64.6 MB |
| `Voxora-Setup.exe` (Inno Setup installer) | *not built — Inno Setup unavailable on build machine* | — |

> Installer (`Voxora-Setup.exe`) can be built locally with `ISCC.exe installer/Voxora.iss` once Inno Setup 6 is installed.

---

## ✅ Verification
- **Build:** `dotnet build -c Release` — 0 warnings, 0 errors
- **Tests:** 66/66 passed (previous baseline 43/43)
- **New test coverage:** `LocalizationTests`, `DeviceNamingTests`, `SettingsLanguageTests`, `ThemeResourceTests`
- **Privacy:** No personal audio files, `library.json`, or `settings.json` in repository (only synthetic test fixtures in `tests/TestAssets/`)

---

## ⚠️ Not Verified on Real Hardware
- Installer build / silent install / Start Menu shortcuts / auto-start registry
- VB-CABLE missing scenario (auto-setup behavior)
- Windows 10 21H2 / Windows 11 24H2 DPI scaling
- Long-running stability (>1 hour)
- Global hotkey capture in full-screen DirectX/Vulkan games