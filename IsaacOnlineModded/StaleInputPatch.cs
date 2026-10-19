using System;
using System.Linq;
using System.Text;

namespace IsaacModInstaller {
    // Drops input packets more than FutureFrameWindow frames ahead of the current frame. Without it, packets from
    // the end of the previous run are applied to a run restarted in an online session, which desyncs it.
    public static class StaleInputPatch {
        public const int FutureFrameWindow = 20;

        // mov eax, [manager]; mov eax, [eax + frame]; cmp esi, eax; jae accept; push eax; push esi; push msg; jmp drop
        private static readonly int[] OriginalSite = Parse(
            "A1 ?? ?? ?? ?? 8B 80 ?? ?? ?? ?? 3B F0 73 0C 50 56 68 ?? ?? ?? ?? E9 ?? ?? ?? ?? 56 8D 8B ?? ?? ?? ?? E8");
        private static readonly int[] PatchedSite = Parse(
            "A1 ?? ?? ?? ?? 8B 80 ?? ?? ?? ?? E9 ?? ?? ?? ?? 56 68 ?? ?? ?? ?? E9 ?? ?? ?? ?? 56 8D 8B ?? ?? ?? ?? E8");

        private const int JumpAt = 0x0B;
        private const int OutsideResumeAt = 0x10;
        private const int AcceptAt = 0x1B;
        private const int CaveSize = 22;
        private const int MinPaddingRun = 32;

        private readonly record struct Section(int RawStart, int RawEnd, int Rva) {
            public int RvaOf(int offset) => Rva + offset - RawStart;
        }

        public static PatchStatus GetStatus(byte[] exe) {
            Section text = TextSection(exe);
            FindUnique(exe, text, OriginalSite, out int originalCount);
            int patched = FindUnique(exe, text, PatchedSite, out int patchedCount);
            if (originalCount == 1 && patchedCount == 0)
                return FindCave(exe, text) >= 0 ? PatchStatus.NotPatched : PatchStatus.Unsupported;
            if (originalCount == 0 && patchedCount == 1 && IsOurCode(exe, text, patched))
                return PatchStatus.Patched;
            return PatchStatus.Unsupported;
        }

        public static void Apply(byte[] exe) {
            Section text = TextSection(exe);
            int site = FindUnique(exe, text, OriginalSite, out _);
            int cave = FindCave(exe, text);
            BuildCave(cave, site).CopyTo(exe, cave);
            exe[site + JumpAt] = 0xE9;
            BitConverter.TryWriteBytes(exe.AsSpan(site + JumpAt + 1), cave - (site + JumpAt + 5));
        }

        private static byte[] BuildCave(int cave, int site) {
            byte[] code = {
                0x3B, 0xF0,                          // cmp esi, eax
                0x72, 0x0C,                          // jb  outside
                0x8D, 0x50, (byte)FutureFrameWindow, // lea edx, [eax + window]
                0x3B, 0xF2,                          // cmp esi, edx
                0x77, 0x05,                          // ja  outside
                0xE9, 0, 0, 0, 0,                    // jmp site + AcceptAt
                0x50,                                // outside: push eax
                0xE9, 0, 0, 0, 0,                    // jmp site + OutsideResumeAt
            };
            BitConverter.TryWriteBytes(code.AsSpan(12), site + AcceptAt - (cave + 16));
            BitConverter.TryWriteBytes(code.AsSpan(18), site + OutsideResumeAt - (cave + 22));
            return code;
        }

        private static bool IsOurCode(byte[] exe, Section text, int site) {
            int cave = site + JumpAt + 5 + BitConverter.ToInt32(exe, site + JumpAt + 1);
            return cave >= text.RawStart && cave + CaveSize <= text.RawEnd
                && exe.AsSpan(cave, CaveSize).SequenceEqual(BuildCave(cave, site));
        }

        private static int FindCave(byte[] exe, Section text) {
            int i = text.RawStart;
            while (i < text.RawEnd) {
                if (exe[i] != 0xCC) {
                    i++;
                    continue;
                }
                int runStart = i;
                while (i < text.RawEnd && exe[i] == 0xCC)
                    i++;
                if (i - runStart < MinPaddingRun)
                    continue;
                int cave = runStart + 1;
                while (text.RvaOf(cave) % 16 != 0)
                    cave++;
                if (cave + CaveSize < i)
                    return cave;
            }
            return -1;
        }

        private static Section TextSection(byte[] exe) {
            if (exe.Length < 0x40)
                throw new InvalidOperationException("Not a PE executable.");
            int pe = BitConverter.ToInt32(exe, 0x3C);
            if (pe <= 0 || pe + 24 > exe.Length || BitConverter.ToUInt32(exe, pe) != 0x4550)
                throw new InvalidOperationException("Not a PE executable.");
            int sectionCount = BitConverter.ToUInt16(exe, pe + 6);
            int table = pe + 24 + BitConverter.ToUInt16(exe, pe + 20);
            for (int i = 0; i < sectionCount; i++) {
                int header = table + i * 40;
                if (Encoding.ASCII.GetString(exe, header, 8).TrimEnd('\0') != ".text")
                    continue;
                int virtualSize = BitConverter.ToInt32(exe, header + 8);
                int rva = BitConverter.ToInt32(exe, header + 12);
                int rawSize = BitConverter.ToInt32(exe, header + 16);
                int rawStart = BitConverter.ToInt32(exe, header + 20);
                return new Section(rawStart, Math.Min(rawStart + Math.Min(rawSize, virtualSize), exe.Length), rva);
            }
            throw new InvalidOperationException("The executable has no .text section.");
        }

        private static int FindUnique(byte[] exe, Section text, int[] pattern, out int count) {
            int hit = -1;
            count = 0;
            for (int i = text.RawStart; i <= text.RawEnd - pattern.Length; i++) {
                int j = 0;
                while (j < pattern.Length && (pattern[j] < 0 || exe[i + j] == pattern[j]))
                    j++;
                if (j < pattern.Length)
                    continue;
                if (++count > 1)
                    return -1;
                hit = i;
            }
            return hit;
        }

        private static int[] Parse(string signature) =>
            signature.Split(' ').Select(token => token == "??" ? -1 : Convert.ToInt32(token, 16)).ToArray();
    }
}
