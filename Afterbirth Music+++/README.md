### Antibirth music+++
The mod itself uses the base game RNG once to initialize it's own RNG which causes a desync.
If ***everyone has the mod installed***, this mod works without changes.

You can either replaced your Antibirth music+++ main.lua with the one here or manually change the following line in the mod's main.lua:
```
randomRNG:SetSeed((Random() // 2) + 1, RECOMMENDED_SHIFT_IDX)
```
=>
```
randomRNG:SetSeed((os.time() // 2) + 1, RECOMMENDED_SHIFT_IDX)
```
