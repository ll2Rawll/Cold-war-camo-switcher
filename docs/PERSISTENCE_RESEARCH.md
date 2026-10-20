# Camo persistence investigation

Status: **research only. No Game Pass force-save or permanent-unlock implementation has been verified.** The v0.2.0 release implements the menu, local configs and experimental runtime Shards replacement.

## What the available evidence establishes

| Evidence | Finding | Limit |
| --- | --- | --- |
| [Activision account documentation](https://support.activision.com/content/atvi/support/web/en/black-ops-cold-war/articles/black-ops-cold-war-in-game-account-registration.html) | Cold War progression is stored on the Activision account and follows linked platforms. | A local camo-option write does not establish an account progression update. |
| [Treyarch's April 15, 2021 patch notes](https://www.treyarch.com/game-intel/2021/04/April_15_2021_Patch_Notes) | Create-a-Class supports naming and saving up to ten Custom Mod blueprints per weapon. | This documents the game's save feature; it does not establish that a runtime-swapped or locked camo is accepted or retained on the current Game Pass build. |
| [Online MP loadout schema](https://github.com/ate47/bocw-source/blob/main/ddl/mp_loadouts.ddl) | `mploadoutweapon` contains a `uint:9 camoindex` field. Saved loadout data is distinct from the option-table byte used by this app. | The schema does not identify the current Game Pass buffer address, storage wrapper, integrity checks or save transaction. |
| [Offline MP loadout schema](https://github.com/ate47/bocw-source/blob/main/ddl/mp_loadouts_offline.ddl) | Offline loadouts have their own schema and root layout. | Online and offline offsets must not be treated as interchangeable. |
| [Persistence script](https://github.com/ate47/bocw-source/blob/main/scripts/core_common/persistence_shared.gsc) | The internal `upload_stats_soon` function eventually calls the game builtin `uploadstats(self)`. | This is a script-context function, not a documented external application API or proof that modified camo/unlock data will be accepted. |
| [Public soft-unlock implementation](https://github.com/brizzaard/soft-unlock-bocw/blob/main/dllmain.cpp) | Populates a runtime inventory representation and changes related local state. | It does not demonstrate a server-accepted permanent unlock or an authenticated save/restart test on PC Game Pass. Its fixed scan ranges and internal layouts are not verified for this target. |
| [Public trainer camo feature](https://github.com/JoakimCarlsson/ColdWarZombieTrainer/blob/master/ColdWarZombieTrainer/Features/CamoFeatures.cs) | Its `OneShotGold` method has an empty body, and the project lists that feature as TODO. | It is not an implemented unlock method to adapt. |

The public schema dump identifies the online schema with version hash `3c1663985019d37a` and an indicated header byte size of 13018; the offline schema has version hash `9782ec6bb68af34b` and size 7934. These are identifiers in that dump, **not a match to the user's executable or a verified on-disk save format**. The dumped weapon struct places its camo field at bit offset `0x348`; this is relative to that struct, not an executable address or a save-file offset.

## First candidate: the game's own Custom Mod save

The most direct experiment is to let the game save an equipped selection through Gunsmith's **Custom Mod Options**. This avoids assuming that an external stats-upload call grants ownership. **Whether the current switcher can feed a camo selection that this save accepts is unverified.**

Run the [restart test in the quick start](QUICKSTART.md#test-the-games-custom-mod-save) on a spare class and a new mod name. Start with a normally unlocked camo as a control. If that control survives a restart, repeat with one runtime-swapped camo. Restore the Shards option and close the app before restarting the game. Record the class and saved mod separately: either might be retained, reset or rejected.

A retained Custom Mod would establish a saved selection for that weapon and build. It would not establish that the camo is normally unlocked on other classes, weapons or accounts. No automatic save command, account unlock or offline save-file implementation has been added on the basis of this hypothesis.

## The next technically justified investigation

1. Identify the installed Game Pass executable by SHA-256, file version and in-game version. The release includes `Get-ColdWarBuildReport.ps1` for this read-only step.
2. Confirm the target build's loadout schema and locate its actual loadout data, separately for online and offline modes. Do not apply offsets from another storefront or build just because a pattern appears similar.
3. Compare an ordinary in-game camo change on the same weapon/class before and after the game's own save. This must establish which data changes, how the game commits it, and what integrity/version rules apply.
4. Implement only a matched format or verified in-game save path, with restoration/backup behavior and read-back checks. Calling an internal stats-upload builtin alone is insufficient evidence.
5. Test the resulting selection with the app stopped, then after a complete game restart and online profile reload. Offline save editing needs a separate offline restart/load test.

The development workspace has neither the game executable nor access to the user's running Windows game. The target build report and observed save behavior are still missing. No authenticated game service endpoint has been identified that grants permanent camo ownership.

## What counts as success

- **Runtime switch:** the camo changes while the current game process is running. This is the current experimental implementation.
- **Saved selection:** the chosen camo remains on that class/blueprint after the app and game have both closed and the profile has been loaded again. That does not, by itself, prove the camo is unlocked for other weapons/classes.
- **Permanent unlock:** the camo can also be selected normally on an eligible fresh loadout with the app absent, and that entitlement/progression state survives a fresh game/profile session.

Local app configs, inventory-cache patches, automatic reapplication and a successful memory read-back must not be reported as permanent in-game unlocks.
