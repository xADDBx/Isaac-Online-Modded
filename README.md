# Isaac Online Modded

This tool allows you to patch the game binary of **The Binding of Isaac** to enable mods in online co-op play without the need to disable them beforehand. 

This tool also allows you to modify the External Item Descriptions and Antibirth Music+++ mods in a way to enable using them in coop.

The **Restart desync fix** fixes a bug in the game's online netcode that desyncs runs restarted inside an online session. It doesn't need the other patches and helps unmodded games too; apply it on every PC, at least on the host's.

**Note:** You will need to run the patch again each time the game updates.

## Download

Get the latest release: `IsaacOnlineModded.exe` for Windows, `IsaacOnlineModded-Linux` for Linux (including the Steam Deck in desktop mode). Neither needs .NET installed.

On Linux, make the file executable first (`chmod +x IsaacOnlineModded-Linux`, or the file's properties in your file manager). The tool finds the game in Steam (native, Flatpak or Snap) and in Wine or Bottles prefixes; otherwise select `isaac-ng.exe` with **Browse**.

## Warning

While this patch allows starting online co-op with any mod installed, doing so can quickly cause desync issues. It often helps if all players have the same mods installed. ***Additionally, some mods may work even if others don't have them installed, but they might require special handling.***

*For further information, see the specific subdirectories (e.g., for External Item Descriptions).*

## Common Desync Issues

- Desync at the start of a run with `Save checksum mismatch` in `log.txt`: the players don't have exactly the same mods enabled.
- Desync right after a mod's restart hotkey or a console command: these skip the online restart vote, so each player starts a different run.
- Character stuck after picking up an item: a mod replaces the player animations with a file that has no `Pickup_Online` animation (e.g. Garry's Mod Death Animation).

## Disclaimer

This tool is intended for **friends-only co-op play**. While it probably works in other scenarios, those are not explicitly accounted for. **I do not condone ruining other players' experiences in online matches.**

---

Please use this tool responsibly and enjoy your enhanced co-op experience!
