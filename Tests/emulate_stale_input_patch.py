"""Executes the input-frame check of the original and the patched isaac-ng.exe in a CPU
emulator and compares where the code goes for crafted (packet frame, current frame) pairs.

  accept - normal path (duplicate check, checksum, store the input)
  drop   - the game's "outside of match scope" log + drop path

Original: drops only frames behind the current frame, so stale old-run inputs are accepted.
Patched:  also drops frames more than 20 ahead. The patched copy is also run after relocating
it to a different base address, the way ASLR loads the game.

Usage: python emulate_stale_input_patch.py <original isaac-ng.exe> <patched copy>
       (create the patched copy with StaleInputPatchCheck)
Needs: pip install pefile unicorn
"""
import re
import struct
import sys

import pefile
from unicorn import UC_ARCH_X86, UC_HOOK_CODE, UC_MODE_32, Uc
from unicorn.x86_const import UC_X86_REG_EBX, UC_X86_REG_ESI, UC_X86_REG_ESP

ORIGINAL_SITE = "A1 ?? ?? ?? ?? 8B 80 ?? ?? ?? ?? 3B F0 73 0C 50 56 68 ?? ?? ?? ?? E9 ?? ?? ?? ?? 56 8D 8B ?? ?? ?? ?? E8"
PATCHED_SITE = "A1 ?? ?? ?? ?? 8B 80 ?? ?? ?? ?? E9 ?? ?? ?? ?? 56 68 ?? ?? ?? ?? E9 ?? ?? ?? ?? 56 8D 8B ?? ?? ?? ?? E8"
FAKE_MANAGER = 0x7000_0000
STACK = 0x6000_0000
RELOCATED_BASE = 0x0135_0000

CASES = [
    # description, packet frame, current frame, original exit, patched exit
    ("old frame (already simulated)", 5, 10, "drop", "drop"),
    ("current frame", 10, 10, "accept", "accept"),
    ("normal lead (input delay 3)", 13, 10, "accept", "accept"),
    ("largest legitimate lead (5 + 5 + 1)", 21, 10, "accept", "accept"),
    ("window edge (current + 20)", 30, 10, "accept", "accept"),
    ("current + 21", 31, 10, "accept", "drop"),
    ("restart: old-run frame 347, new run at 0", 347, 0, "accept", "drop"),
    ("restart: old-run frame 2826, new run at 3", 2826, 3, "accept", "drop"),
    ("first frame of a run", 0, 0, "accept", "accept"),
]


class Image:
    def __init__(self, path, base=None):
        self.pe = pefile.PE(path)
        if base is not None:
            self.pe.relocate_image(base)  # applies the base relocations and updates ImageBase
        self.base = self.pe.OPTIONAL_HEADER.ImageBase
        img = bytearray(self.pe.get_memory_mapped_image())
        size = (self.pe.OPTIONAL_HEADER.SizeOfImage + 0xFFF) & ~0xFFF
        img.extend(b"\0" * (size - len(img)))
        self.img = img

    def u32(self, va):
        return struct.unpack_from("<I", self.img, va - self.base)[0]

    def s32(self, va):
        return struct.unpack_from("<i", self.img, va - self.base)[0]

    def find_unique(self, sig):
        text = next(s for s in self.pe.sections if s.Name.rstrip(b"\0") == b".text")
        data = bytes(self.img[text.VirtualAddress:text.VirtualAddress + text.Misc_VirtualSize])
        rx = b"".join(b"." if t == "??" else re.escape(bytes([int(t, 16)])) for t in sig.split())
        hits = [m.start() for m in re.finditer(rx, data, re.S)]
        assert len(hits) == 1, f"signature found {len(hits)} times"
        return self.base + text.VirtualAddress + hits[0]


def run_cases(image, site, label, column):
    accept = site + 0x1B
    drop = site + 0x1B + image.s32(site + 0x17)
    fmt = image.u32(site + 0x12)
    mgr_ptr = image.u32(site + 1)
    frame_off = image.u32(site + 7)

    uc = Uc(UC_ARCH_X86, UC_MODE_32)
    uc.mem_map(image.base, len(image.img))
    uc.mem_write(image.base, bytes(image.img))
    uc.mem_map(FAKE_MANAGER, (frame_off + 4 + 0xFFF) & ~0xFFF)
    uc.mem_map(STACK - 0x10000, 0x20000)
    uc.mem_write(mgr_ptr, struct.pack("<I", FAKE_MANAGER))
    exits = {accept: "accept", drop: "drop"}
    hit = {}

    def on_code(emu, address, size, _):
        if address in exits:
            hit["exit"] = exits[address]
            emu.emu_stop()

    uc.hook_add(UC_HOOK_CODE, on_code)
    failures = 0
    print(f"== {label}: site 0x{site:08X}, base 0x{image.base:08X}")
    for desc, frame, current, *expected in CASES:
        uc.mem_write(FAKE_MANAGER + frame_off, struct.pack("<I", current))
        uc.reg_write(UC_X86_REG_ESI, frame)
        uc.reg_write(UC_X86_REG_EBX, 0x12345678)
        uc.reg_write(UC_X86_REG_ESP, STACK)
        hit.clear()
        uc.emu_start(site, 0, count=64)
        got = hit.get("exit", "none")
        ok = got == expected[column]
        if got == "drop":  # the game's logger gets (fmt, frame, current) on the stack
            ok &= struct.unpack("<III", uc.mem_read(STACK - 12, 12)) == (fmt, frame, current)
        failures += not ok
        print(f"  [{'PASS' if ok else 'FAIL'}] {desc:42s} frame={frame:<5d} current={current:<3d} -> {got}")
    return failures


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 2
    original = Image(sys.argv[1])
    patched = Image(sys.argv[2])
    relocated = Image(sys.argv[2], RELOCATED_BASE)
    failures = run_cases(original, original.find_unique(ORIGINAL_SITE), "original exe", 0)
    failures += run_cases(patched, patched.find_unique(PATCHED_SITE), "patched exe", 1)
    failures += run_cases(relocated, relocated.find_unique(PATCHED_SITE), "patched exe, relocated", 1)
    print("ALL PASSED" if not failures else f"{failures} FAILURE(S)")
    return 1 if failures else 0


if __name__ == "__main__":
    sys.exit(main())
