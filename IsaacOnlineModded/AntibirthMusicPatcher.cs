using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace IsaacModInstaller {
    // Random() advances the game's synced RNG, so players without the mod desync.
    public static class AntibirthMusicPatcher {
        private const string Marker = "-- IsaacOnlineModded: Random() advances the synced RNG";
        private const string FixedSeed = "randomRNG:SetSeed((Isaac.GetTime() // 2) + 1, RECOMMENDED_SHIFT_IDX)";
        private static readonly string[] OldSeeds = [
            "randomRNG:SetSeed((Random() // 2) + 1, RECOMMENDED_SHIFT_IDX)",
            // Old replacement file from this repo
            "randomRNG:SetSeed((Game():GetFrameCount() // 2) + 1, RECOMMENDED_SHIFT_IDX)",
        ];
        private static readonly string[] UnusedSeed = [
            "local seed = Random()",
            "if seed < 1 or seed > 4294967295 then",
            "seed = 256",
            "end",
        ];

        public static bool IsAntibirthMusic(string modPath) {
            string path = GetMainPath(modPath);
            return File.Exists(path) && File.ReadAllText(path).Contains("RegisterMod(\"Antibirth Music+++\"", StringComparison.Ordinal);
        }

        public static PatchStatus GetPatchStatus(string modPath) {
            List<string> lines = LuaDocument.Read(GetMainPath(modPath)).Lines;
            if (!CallsRandom(lines))
                return PatchStatus.Patched;
            Apply(lines);
            return CallsRandom(lines) ? PatchStatus.Unsupported : PatchStatus.NotPatched;
        }

        public static bool Patch(string modPath) {
            string path = GetMainPath(modPath);
            LuaDocument document = LuaDocument.Read(path);
            if (!CallsRandom(document.Lines))
                return false;
            Apply(document.Lines);
            if (CallsRandom(document.Lines))
                throw new InvalidOperationException("The installed Antibirth Music+++ version calls Random() in an unknown place.");
            document.Write(path);
            return true;
        }

        private static void Apply(List<string> lines) {
            for (int i = 0; i < lines.Count; i++) {
                string indent = lines[i][..^lines[i].TrimStart().Length];
                if (OldSeeds.Contains(lines[i].Trim())) {
                    lines[i] = indent + FixedSeed;
                    lines.Insert(i++, indent + Marker);
                } else if (lines.Skip(i).Take(UnusedSeed.Length).Select(line => line.Trim()).SequenceEqual(UnusedSeed)) {
                    lines.RemoveRange(i, UnusedSeed.Length);
                    lines.Insert(i, indent + Marker);
                }
            }
        }

        private static bool CallsRandom(List<string> lines) =>
            lines.Any(line => line.Split("--")[0].Contains("Random()", StringComparison.Ordinal));

        private static string GetMainPath(string modPath) =>
            Path.Combine(modPath, "main.lua");
    }
}
