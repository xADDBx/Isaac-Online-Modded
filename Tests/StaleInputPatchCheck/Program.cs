// Checks StaleInputPatch against a real isaac-ng.exe without modifying it.
//
// Usage: StaleInputPatchCheck <path\to\isaac-ng.exe> [path\for\patched-copy.exe]
//
// The optional patched copy is the input for emulate_stale_input_patch.py.

using IsaacModInstaller;

if (args.Length < 1) {
    Console.WriteLine("Usage: StaleInputPatchCheck <path to isaac-ng.exe> [output path for the patched copy]");
    return 2;
}

int failures = 0;
void Check(bool ok, string what) {
    Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {what}");
    if (!ok)
        failures++;
}

byte[] original = File.ReadAllBytes(args[0]);
PatchStatus before = StaleInputPatch.GetStatus(original);
Check(before == PatchStatus.NotPatched, $"unpatched exe reports NotPatched (got {before})");
if (before != PatchStatus.NotPatched) {
    Console.WriteLine("Run this against an unpatched isaac-ng.exe (verify game files in Steam first).");
    return 1;
}

byte[] patched = (byte[])original.Clone();
StaleInputPatch.Apply(patched);
Check(StaleInputPatch.GetStatus(patched) == PatchStatus.Patched, "patched exe reports Patched");
bool refused = false;
try {
    StaleInputPatch.Apply(patched);
} catch (InvalidOperationException) {
    refused = true;
}
Check(refused, "applying the patch twice is refused");

int[] changed = Enumerable.Range(0, original.Length).Where(i => original[i] != patched[i]).ToArray();
// The jump replaces "cmp esi, eax / jae +0C / push eax" (3B F0 73 0C 50).
int jump = changed.FirstOrDefault(i => patched[i] == 0xE9 && i + 5 <= original.Length
    && original.AsSpan(i, 5).SequenceEqual(new byte[] { 0x3B, 0xF0, 0x73, 0x0C, 0x50 }), -1);
Check(jump >= 0, $"frame comparison replaced by a jmp (file offset 0x{jump:X})");
if (jump < 0)
    return 1;
int site = jump - 0x0B;
int cave = jump + 5 + BitConverter.ToInt32(patched, jump + 1);
Console.WriteLine($"  site at file offset 0x{site:X}, new code at 0x{cave:X}");
Check(changed.All(i => (i >= jump && i < jump + 5) || (i >= cave && i < cave + 22)),
    $"only the 5-byte jump and the 22-byte new code changed ({changed.Length} bytes differ)");
Check(original.AsSpan(cave, 22).ToArray().All(b => b == 0xCC), "new code was placed in int3 padding");

// No changed byte may lie inside a dword that the loader relocates, or ASLR would corrupt it.
var pe = new PeInfo(original);
HashSet<int> relocated = pe.RelocatedRvas();
int[] hit = changed.Select(pe.RvaOfOffset).Where(rva => relocated.Contains(rva)).ToArray();
Check(relocated.Count > 1000, $"relocation table parsed ({relocated.Count} relocated bytes)");
Check(hit.Length == 0, "no changed byte overlaps a relocated address");

if (args.Length > 1) {
    File.WriteAllBytes(args[1], patched);
    Console.WriteLine($"  patched copy written to {args[1]}");
}

Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILURE(S)");
return failures == 0 ? 0 : 1;

sealed class PeInfo {
    private readonly byte[] exe;
    private readonly List<(int Rva, int VirtualSize, int RawStart, int RawSize)> sections = new();
    private readonly int relocRva, relocSize;

    public PeInfo(byte[] exe) {
        this.exe = exe;
        int pe = BitConverter.ToInt32(exe, 0x3C);
        int count = BitConverter.ToUInt16(exe, pe + 6);
        int optional = pe + 24;
        int table = optional + BitConverter.ToUInt16(exe, pe + 20);
        relocRva = BitConverter.ToInt32(exe, optional + 96 + 5 * 8);   // PE32: data directory 5
        relocSize = BitConverter.ToInt32(exe, optional + 96 + 5 * 8 + 4);
        for (int i = 0; i < count; i++) {
            int h = table + i * 40;
            sections.Add((BitConverter.ToInt32(exe, h + 12), BitConverter.ToInt32(exe, h + 8),
                BitConverter.ToInt32(exe, h + 20), BitConverter.ToInt32(exe, h + 16)));
        }
    }

    public int RvaOfOffset(int offset) {
        foreach (var s in sections)
            if (offset >= s.RawStart && offset < s.RawStart + s.RawSize)
                return s.Rva + offset - s.RawStart;
        return -1;
    }

    private int OffsetOfRva(int rva) {
        foreach (var s in sections)
            if (rva >= s.Rva && rva < s.Rva + Math.Max(s.VirtualSize, s.RawSize))
                return s.RawStart + rva - s.Rva;
        throw new InvalidDataException($"RVA 0x{rva:X} is not in any section");
    }

    // Every byte RVA covered by an IMAGE_REL_BASED_HIGHLOW fixup.
    public HashSet<int> RelocatedRvas() {
        var result = new HashSet<int>();
        int at = OffsetOfRva(relocRva), end = at + relocSize;
        while (at + 8 <= end) {
            int page = BitConverter.ToInt32(exe, at);
            int blockSize = BitConverter.ToInt32(exe, at + 4);
            if (blockSize < 8)
                break;
            for (int e = at + 8; e + 2 <= at + blockSize; e += 2) {
                int entry = BitConverter.ToUInt16(exe, e);
                if (entry >> 12 == 3)
                    for (int b = 0; b < 4; b++)
                        result.Add(page + (entry & 0xFFF) + b);
            }
            at += blockSize;
        }
        return result;
    }
}
