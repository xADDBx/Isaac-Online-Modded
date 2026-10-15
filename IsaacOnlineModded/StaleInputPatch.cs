using System;
using System.Linq;
using System.Text;

namespace IsaacModInstaller {
    /// <summary>
    /// Fixes the desync that happens a few seconds into a run that was restarted inside an
    /// online session.
    ///
    /// NetInputDevice::ProcessInputMessage only drops input packets for frames *behind* the
    /// current frame, and packets carry no run id. When the host restarts, the other player's
    /// inputs for the last frames of the old run (F+1..F+inputDelay) are still in flight. They
    /// arrive after the reset, get stored as frames F+1.. of the new run, and the real inputs
    /// for those frames are later discarded as "Dropping input packet for frame we already
    /// have". The host then simulates the stale input and the other player the real one.
    ///
    /// A legitimate packet is never more than ownDelay + remoteDelay + 1 = 11 frames ahead
    /// (the game clamps OnlineInputDelay to 1..5), so packets more than FutureFrameWindow
    /// frames ahead are now dropped through the game's own "outside of match scope" path
    /// (logged with Received Frame &gt; Current Frame).
    ///
    /// The patch replaces 5 bytes of the frame comparison with a jump to 22 bytes of new code
    /// placed in int3 padding of .text. Neither place holds relocated operands and the new
    /// code only uses relative jumps, so it works at any load address.
    /// </summary>
    public static class StaleInputPatch {
        public const int FutureFrameWindow = 20;

        // +00 A1 imm32          mov  eax, [g_Manager]
        // +05 8B 80 imm32       mov  eax, [eax + currentFrame]
        // +0B 3B F0             cmp  esi, eax                ; esi = packet frame
        // +0D 73 0C             jae  +1B                     ; frame >= current: accept
        // +0F 50 56 68 imm32    push eax / push esi / push "...outside of match scope..."
        // +16 E9 rel32          jmp  LogAndDrop
        // +1B 56 8D 8B ...      "already have" check, checksum, insert into the input queue
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
            if (GetStatus(exe) != PatchStatus.NotPatched)
                throw new InvalidOperationException("The restart desync fix cannot be applied to this executable.");

            Section text = TextSection(exe);
            int site = FindUnique(exe, text, OriginalSite, out _);
            int cave = FindCave(exe, text);
            BuildCave(cave, site).CopyTo(exe, cave);
            exe[site + JumpAt] = 0xE9;
            BitConverter.TryWriteBytes(exe.AsSpan(site + JumpAt + 1), cave - (site + JumpAt + 5));
        }

        // Offsets are file offsets; site and cave are in the same section, so their distance
        // is the same in memory and rel32 jumps can be computed from them directly.
        private static byte[] BuildCave(int cave, int site) {
            byte[] code = {
                0x3B, 0xF0,                          // cmp  esi, eax
                0x72, 0x0C,                          // jb   outside             ; frame < current
                0x8D, 0x50, (byte)FutureFrameWindow, // lea  edx, [eax + window] ; edx is free here
                0x3B, 0xF2,                          // cmp  esi, edx
                0x77, 0x05,                          // ja   outside             ; frame > current + window
                0xE9, 0, 0, 0, 0,                    // jmp  site+1B             ; accept
                0x50,                                // outside: push eax        ; the overwritten instruction
                0xE9, 0, 0, 0, 0,                    // jmp  site+10             ; push esi / push fmt / log + drop
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

        // First run of int3 padding long enough for the code, 16-byte aligned in memory.
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
