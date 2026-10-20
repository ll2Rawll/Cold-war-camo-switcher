# Cold War Camo Switcher

A Windows desktop camo menu targeting **Call of Duty: Black Ops Cold War on PC Xbox / Game Pass**, with an iOS 26 inspired glass interface and named saved configs. Browse and search **121 camo IDs**: 70 base Multiplayer/Zombies camos, 6 mastery camos, 9 Pack-a-Punch variants, and 36 CDL variants.

**Experimental: Game Pass compatibility has not been tested in the actual game.** The app implements a historical Shards option replacement method. It does not permanently unlock camos, provide an online force-save operation, or edit offline save files. Some IDs may require assets or validation that this method cannot provide.

## Download and use

Download the Windows x64 ZIP from [Releases](https://github.com/pistolswang013/Cold-war-camo-switcher/releases), extract it, and open `ColdWarCamoSwitcher.exe`. The release includes the .NET runtime; no SDK installation is needed.

Equip **Shards** on your weapon and open the game's camo menu. Select the running game process in the app, click **Connect**, choose a camo, and click **Apply selected camo**. Reselect Shards in the game to refresh the option. Use **Restore original Shards** afterward. See the [step-by-step instructions](docs/QUICKSTART.md) for limits and troubleshooting.

If the signature is missing or ambiguous, the app stops without writing. A successful write message means that a single byte was changed and read back; it does not prove that the game accepted or saved the camo.

## Implemented behavior

- Light glass-style panels, rounded controls, clear connection status, keyboard shortcuts and a quieter Activity view. The visual style is inspired by [Apple's Liquid Glass design](https://www.apple.com/newsroom/2025/06/apple-introduces-a-delightful-and-elegant-new-software-design/), implemented with Windows drawing controls.
- Named saved configs, JSON import/export, favorites and restored menu preferences. Configs are stored at `%LOCALAPPDATA%\ColdWarCamoSwitcher\configs.json`; loading prepares the UI and never automatically writes to a game process.
- Searchable desktop catalog with collection filters and process selection. Search is debounced for 160 ms, preference writes for 600 ms; the grid and drawn controls are double-buffered and there is no continuous decorative animation.
- Cancellable scan of readable, non-executable data pages inside the selected process's main executable module. There are no fixed executable offsets.
- Exactly one Shards signature match is required. Incomplete reads fail discovery rather than skipping bytes and assuming uniqueness.
- The full captured 20-byte context is checked before each change. A single camo byte is written into existing writable data, then the result and surrounding bytes are read back.
- Original-option restoration, with an attempt on disconnect or normal app closure. Failed verification invalidates the connection and blocks further writes.
- Exportable diagnostics and a read-only executable build-report helper. Reports are saved locally; the app has no network client, telemetry, injection code, driver, or protection bypass.

Only the Shards menu option is patched. This is a shared menu entry, not a per-weapon or per-loadout editor. The app cannot restore after a crash or game exit; restarting the game discards its runtime patch. The sparse historical signature is a compatibility heuristic, not a proven identification of a camo object on every executable.

## Validation

The test console covers catalog IDs, overlapping patterns, all chunk boundary positions, adjacent regions versus gaps, ambiguous or missing matches, incomplete reads, cancellation, process exit, changed data, exact one-byte writes, failed read-back, restoration and disposal. Config checks cover restart persistence, same-name updates, validation without overwriting good files, corrupt-file recovery, import merging, size and count limits, and temporary-file cleanup after a failed replacement.

On Windows it also allocates a fixture in a separate child process and exercises real `OpenProcess`, `VirtualQueryEx`, `ReadProcessMemory` and `WriteProcessMemory` calls. The child independently confirms writes and restoration. A read-only fixture page must reject writes without changing its protection. A UI smoke check exercises search, category filters, favorites, saving/loading a config and restoring settings in a second app instance, then captures a preview. Smoke checks use a temporary config folder.

Those checks verify the implementation against controlled fixtures. **They do not verify Game Pass compatibility, actual in-game camo availability, online persistence, or an offline save format.**

## Build from source

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0). Run the app on Windows 10/11 x64.

```powershell
dotnet run --project tests/CamoSwitcher.Tests -c Release
dotnet publish src/CamoSwitcher.App -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o artifacts/windows
```

The core checks also run on Linux; native Windows checks are explicitly skipped there. The [Windows workflow](.github/workflows/build.yml) runs the checks, verifies the UI and packages the app.

For the standalone build report helper:

```powershell
& .\tools\Get-ColdWarBuildReport.ps1
& .\tests\Test-BuildReport.ps1
```

## Compatibility research and remaining save work

To investigate a missing Game Pass signature, provide the build report and app diagnostics in the development conversation. The selected executable's full hash is more reliable than a version string. Offline save editing additionally needs relevant save copies from before and after one normal camo change, with their layout and integrity rules identified. Online persistence requires a verified build-specific method and an in-game save/restart test.

## Data and method references

- Camo IDs and asset keys: [public Cold War camooptions dump](https://github.com/ate47/bocw-source/blob/main/gamedata/weapons/common/camooptions.csv), blob `0ec48ba6239d9ca19af30c39f8935258aa9c9d38`. Labels are for browsing; CDL suffixes MS/PC/SY are retained as variants.
- Historical Shards replacement signature and mastery/PAP mappings: [CW-PAP-Tool](https://github.com/whatIsT/CW-PAP-Tool/blob/main/CWHelper/MemClass/Main.cs) and [its ID list](https://github.com/whatIsT/CW-PAP-Tool/blob/main/CWHelper/MemClass/Camos.cs).

This implementation is newly written. Referenced projects have not been bundled or copied. It is an unofficial fan project, not affiliated with Activision, Treyarch, Microsoft or Xbox.
