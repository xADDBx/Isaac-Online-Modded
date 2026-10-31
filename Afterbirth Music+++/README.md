### Antibirth music+++
This mod ***Needs adjustments to properly work*** unless ***everyone has it installed***.

The mod calls the game's `Random()`, which advances the RNG that online co-op keeps in sync between players. It does this on every new room, so players without the mod desync right at the start of the run.

To make this mod work you can:

- Use the patcher tool (**Patch Antibirth Music+++**). Only the players who have the mod need it.
- Replace the mod's `main.lua` with the one here (made for mod version 1.18).
- Change the mod's `main.lua` yourself:
  1. Near the top, replace
     ```lua
     randomRNG:SetSeed((Random() // 2) + 1, RECOMMENDED_SHIFT_IDX)
     ```
     with
     ```lua
     randomRNG:SetSeed((Isaac.GetTime() // 2) + 1, RECOMMENDED_SHIFT_IDX)
     ```
  2. In `updateRandomAltMusic`, delete these unused lines:
     ```lua
     local seed = Random()
     if seed < 1 or seed > 4294967295 then
       seed = 256
     end
     ```
