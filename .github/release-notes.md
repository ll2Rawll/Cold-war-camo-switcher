Redesigned Windows x64 camo switcher with an iOS 26 inspired glass interface, rounded controls, a cleaner connect–choose–apply flow, and 121 searchable camo IDs.

- Named saved configs with local JSON storage, import/export and same-name updates.
- Favorites and remembered selection, filters and the last selected config.
- Debounced search and preference saves, double-buffered rendering and no continuous visual animation.
- Config loading prepares the selection; it does not automatically apply a camo to a game process.
- Atomic config writes, validation and recovery backups for unreadable local config files.

Download `ColdWarCamoSwitcher-win-x64.zip`, extract it, and run `ColdWarCamoSwitcher.exe`. The .NET runtime is included. Equip an available Shards camo in Cold War before scanning. Read the bundled `READ-ME-FIRST.md` for the in-game steps.

**Experimental Game Pass compatibility:** this build implements the historical Shards option replacement method. It has not been tested against a running PC Xbox/Game Pass copy of Cold War. The 121 IDs are catalog entries, not a guarantee that every camo is usable. This does not permanently unlock the account's camo menu. Online force-save and offline save-file editing remain unimplemented.

The Windows build checks cover controlled child-process memory access, switching, restoration, read-only page rejection, the build-report helper, config import/export and recovery, and saving/loading preferences across an app restart. These checks do not establish actual in-game compatibility or saved profile persistence.

The SHA-256 checksum for the ZIP is in `SHA256SUMS`.

![Desktop menu preview](https://github.com/pistolswang013/Cold-war-camo-switcher/releases/download/v0.2.0/ui-preview.png)
