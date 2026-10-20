# Cold War Camo Switcher

**Experimental Windows x64 build. Game Pass compatibility has not been verified in Cold War.**

This app changes the running game's Shards camo option using a historical Cold War memory signature. It is not a permanent unlock-all tool. The catalog has 121 IDs; the game may not accept every option on every weapon or build.

## Try it in the game

1. Extract the ZIP into a folder and open `ColdWarCamoSwitcher.exe`. The .NET runtime is included.
2. Start your PC Xbox/Game Pass copy of Cold War. Equip **Shards** on the weapon you want to customize, and leave the camo selection menu open. Shards needs to be available normally on that weapon.
3. In the app, select the actual game process and press **Connect**. Common Cold War process names appear first. If the Xbox edition uses a different executable name, choose it from the list.
4. After exactly one matching Shards option is found, choose a camo and press **Apply selected camo**.
5. Back in the game, move to another camo and reselect the Shards option to refresh the selection. Check the weapon preview and the actual weapon in-game. A verified memory write alone does not prove the game accepted the camo.
6. Press **Restore original Shards** when finished. Disconnecting or closing the app also attempts restoration. If the game has exited or data changed, restoration may be unavailable; restarting the game discards the runtime patch.

Changing the replacement can affect other weapons using that menu entry. This app does not provide per-loadout targeting. No claim is made that a selected camo persists after leaving the game or that it becomes unlocked in the normal camo menu.

## If it does not work

- **No signature:** re-open the camo menu with Shards equipped and retry once. A different executable layout or a data table outside the main module requires a new compatibility implementation. The app does not write when no match is found.
- **Multiple signatures:** the app refuses to guess which one is correct.
- **Access denied / non-writable data:** the app stops. It does not change process protections or bypass a protection system.
- **Data changed / read-back failed:** restart the game before reconnecting. The app blocks further writes on that connection.
- **Camo does not appear:** the game may not load or accept that ID. Restore Shards. A cosmetic table edit does not grant account entitlements or blueprint assets.

Open **Activity → Save diagnostic report** to save a report. You can also run the included `Get-ColdWarBuildReport.ps1` from PowerShell to select the executable and record its SHA-256 and file version. Share reports in the development conversation after reviewing them. Do not post game executables, raw process dumps, account credentials or save files to the repository.

## Save your setup

Choose a camo, enter a name under **Saved configs**, and press **Save**. Saving the same name updates that config. Select a saved name and press **Load** to prepare it; use **Apply selected camo** separately when connected. Config loading never automatically connects to a game or writes its memory.

Use **☆** to favorite a camo and **Favorites** to filter the library. The app remembers favorites, your last camo selection, search, collection filter and selected config across restarts. **Import** merges a JSON config library by name and adds its favorites. **Export** saves your entire config library and favorites to a portable JSON file. **Delete** removes a selected config after confirmation.

Configs live at `%LOCALAPPDATA%\ColdWarCamoSwitcher\configs.json`. Up to 100 named configs are supported. Unreadable local files are preserved as `.unreadable-….bak` files in that folder before defaults are used. Export a backup before moving to another PC.

Keyboard shortcuts: **Ctrl+F** searches, **Ctrl+S** saves the named config, **F5** refreshes processes, and **Esc** cancels a scan. Menu preference saves are batched briefly while you interact with controls and flushed on normal closure.

## Persistence and offline saves

Online force-save and offline save-file editing remain unimplemented. These require the exact target build, the relevant save layout/integrity checks, and an actual in-game restart test. The app's session switcher is usable code, but it has not been validated against your running Game Pass game.
