# Tests for the restart desync fix

Both tests need your own unpatched `isaac-ng.exe` (game files can't be distributed, so these
don't run in CI). Neither modifies the game: they work on an in-memory copy and an optional
patched copy you choose the path for.

## 1. Patch check (C#)

Runs `IsaacOnlineModded/StaleInputPatch.cs` against the real executable and checks that the
status is detected correctly, that patching twice is refused, that only the 5-byte jump and
the 22 bytes of new code change, that the new code goes into int3 padding, and that no
changed byte overlaps an address the loader relocates (which would break under ASLR).

```
dotnet run --project Tests/StaleInputPatchCheck -- "<game folder>\isaac-ng.exe" "%TEMP%\isaac-ng.patched.exe"
```

## 2. Emulation (Python)

Executes the input-frame check of the original and the patched executable in the Unicorn CPU
emulator with crafted (packet frame, current frame) pairs, including the patched copy
relocated to a different base address. The original accepts input for frames far ahead of
the current one (the bug); the patched one drops frames more than 20 ahead and nothing else.

```
pip install pefile unicorn
python Tests/emulate_stale_input_patch.py "<game folder>\isaac-ng.exe" "%TEMP%\isaac-ng.patched.exe"
```
