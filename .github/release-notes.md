Adds a configurable silent-aim preferences dialog to the glass-style menu.

**Configuration only: in-game silent aim is not implemented.** The activation checkbox stays disabled. There is no target tracking, shot redirection, injection or aim-related game-memory writing in this release.

- Settings for Multiplayer + Zombies, FOV, hit chance, distance, target bone, activation preference, and visible-target filtering.
- Preferences remembered across app restarts and included in named config Save/Load and JSON import/export.
- Existing config libraries retain their camos and gain default aim preferences.
- Input validation rejects invalid ranges, unknown modes and null settings without replacing a valid config file.
- Reset defaults and Cancel keep unsaved changes separate from saved preferences.

Download `ColdWarCamoSwitcher-win-x64.zip`, extract it, and open `ColdWarCamoSwitcher.exe`. The .NET runtime is included. Use **Silent aim settings** in the top-right of the menu to configure preferences. The bundled `READ-ME-FIRST.md` explains installation and usage.

The existing camo switcher remains experimental on PC Xbox / Game Pass. Permanent unlocks, online force-save and offline save-file editing remain unimplemented. Actual gameplay integration still requires verification against the installed game build.

Validation covers 25 core/config checks, native Windows memory fixtures, legacy config loading, aim setting import/export and validation, and Windows UI checks for editing, reset, unavailable activation, layout and restart persistence. These checks do not establish in-game aim functionality or camo persistence.

The ZIP checksum is included in `SHA256SUMS`.

![Aim preferences, with in-game activation unavailable](https://github.com/ll2Rawll/Cold-war-camo-switcher/releases/download/v0.3.0/ui-preview-silent-aim.png)
