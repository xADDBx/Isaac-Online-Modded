# Isaac Online Modded

This tool allows you to patch the game binary of **The Binding of Isaac** to enable mods in online co-op play without the need to disable them beforehand. 

This tool also allows you to modify the External Item Descriptions mod in a way to enable using it in coop.

**Patch Game** also applies a fix for a bug in the game's online netcode that desyncs runs restarted inside an online session (see [Restart desync fix](#restart-desync-fix)). It helps unmodded games too; apply it on every PC, at least on the host's.

**Note:** You will need to run the patch again each time the game updates.

## Warning

While this patch allows starting online co-op with any mod installed, doing so can quickly cause desync issues. It often helps if all players have the same mods installed. ***Additionally, some mods may work even if others don't have them installed, but they might require special handling.***

*For further information, see the specific subdirectories (e.g., for External Item Descriptions).*

## Troubleshooting

The game writes what you need to diagnose a desync to `Documents\My Games\Binding of Isaac Repentance+\`:

- `log.txt` — the log of the current game session.
- `online_logs\desyncs\<date>__<name>\desync_diff.txt` — which entities differed between the players and how. `desync_framestate.txt` holds the full frame on your PC; compare it with the other player's folder for the same desync.

| Symptom | Likely cause | Fix |
|---|---|---|
| Desync a few seconds or minutes after restarting a run | The restart netcode bug above. `log.txt` shows `Dropping input packet for frame we already have` just before the desync, and the desync frame is slightly past the frame number at which the previous run ended. | **Patch Game** on every PC, at least the host's. |
| Desync at frame 0 with `Save checksum mismatch` in `log.txt` | The players run different mods: different enabled mods, mods turned on for one player only (`EnableMods` in `options.ini`), or different mod versions. Mods that add items change the save data that both games compare when a run starts. | Use exactly the same enabled mods. A Steam Workshop collection helps, but subscribing to one doesn't disable your other mods — turn those off in the Mods menu and restart the game. |
| Desync at frame 0, the players' entities have different seeds, often right after a mod's restart hotkey | A mod restarted the run with a console command (e.g. Ctrl+R in Restart Until Special Room). Console restarts skip the online restart vote, and each PC picks its own seed. | Don't use console commands or mod restart hotkeys online. |
| Desync right after opening a mod's settings menu | Mod Config Menu opened during a run. | Only open it in the main menu (see `Mod Config Menu Pure/`). |
| Online only: the character keeps holding up a picked-up item and can't act until it gets hit or changes rooms | Online, the game plays the `Pickup_Online` player animation. Mods that replace `gfx/001.000_player.anm2` with a file made before Repentance+ don't have it, so the pickup never finishes (`[warn] no animation named Pickup_Online` in `log.txt`). Example: Garry's Mod Death Animation. | Disable that mod, or add a `Pickup_Online` animation to its anm2. |
| Missing sound effects and a huge `log.txt` full of `Sound N has no samples` | A mod replaces the whole `sounds.xml` with a copy from an older game version (e.g. Pre-Repentance Brimstone Sounds), so newer sounds lose their audio. | Disable that mod. |
| The whole PC freezes for seconds while you play online (mouse still moves, sound stops) | Windows Search indexing `Documents\My Games`, where the game rewrites `log.txt` many times a second and copies it into `online_logs`. On a slow or nearly full system drive this can stall every program that writes to that drive. | Exclude `Documents\My Games` from indexing: Win+R → `control srchadmin.dll` → **Modify**. |

Mods are risky online, even when everyone has them, if they:

- spawn entities or change game state from render callbacks (`MC_POST_RENDER`, `MC_POST_*_RENDER`) — e.g. Planetarium Chance (always desyncs when only one player has it) and Animated Items;
- run console commands (`Isaac.ExecuteCommand`) or restart the run;
- read the local keyboard or controller (`Input.*`) to change the game rather than their own menu;
- base gameplay on per-player settings or save data (Mod Config Menu options, `LoadModData`).

## Disclaimer

This tool is intended for **friends-only co-op play**. While it probably works in other scenarios, those are not explicitly accounted for. **I do not condone ruining other players' experiences in online matches.**

---

Please use this tool responsibly and enjoy your enhanced co-op experience!
