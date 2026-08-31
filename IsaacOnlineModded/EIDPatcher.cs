using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace IsaacModInstaller {
    public static class EIDPatcher {
        private const string Anchor = "return listUpdatedForPlayers -- dont evaluate when bad data is present";
        private const string Marker = "-- IsaacOnlineModded: avoid invalid player access after the run ends";
        private static readonly string[] Guard = [
            Marker,
            "local stage = Game():GetLevel():GetStage()",
            "if stage == nil or stage >= 13 or stage < 1 then",
            "return listUpdatedForPlayers",
            "end",
        ];

        public static PatchStatus GetPatchStatus(string eidPath) {
            LuaDocument document = ReadDocument(GetApiPath(eidPath));
            int anchorIndex = FindUniqueAnchor(document.Lines);
            if (anchorIndex < 0)
                return PatchStatus.Unsupported;
            return HasStageGuard(document.Lines, anchorIndex) ? PatchStatus.Patched : PatchStatus.NotPatched;
        }

        public static bool Patch(string eidPath) {
            string path = GetApiPath(eidPath);
            LuaDocument document = ReadDocument(path);
            int anchorIndex = FindUniqueAnchor(document.Lines);
            if (anchorIndex < 0)
                throw new InvalidOperationException("The installed EID version is missing a unique patch location.");
            if (HasStageGuard(document.Lines, anchorIndex))
                return false;

            int blockEnd = -1;
            for (int i = anchorIndex + 1; i < Math.Min(document.Lines.Count, anchorIndex + 4); i++) {
                if (document.Lines[i].Trim() == "end") {
                    blockEnd = i;
                    break;
                }
            }
            if (blockEnd < 0)
                throw new InvalidOperationException("Could not locate the EID player validation block.");

            document.Lines.InsertRange(blockEnd + 1, [
                "",
                "\t\t" + Guard[0],
                "\t\t" + Guard[1],
                "\t\t" + Guard[2],
                "\t\t\t" + Guard[3],
                "\t\t" + Guard[4],
            ]);
            WriteDocument(path, document);
            return true;
        }

        private static bool HasStageGuard(IReadOnlyList<string> lines, int anchorIndex) {
            int end = Math.Min(lines.Count, anchorIndex + 14);
            for (int i = anchorIndex + 1; i < end; i++) {
                if (MatchesBlock(lines, i, Guard))
                    return true;

                string line = lines[i].Trim();
                if (line == "local stage = Game():GetLevel():GetStage()" && MatchesLegacyStageGuard(lines, i))
                    return true;
                if (line.Contains("Game():GetLevel():GetStage() >= LevelStage.Home", StringComparison.Ordinal)
                    && MatchesReturnAndEnd(lines, i + 1))
                    return true;
            }
            return false;
        }

        private static bool MatchesBlock(IReadOnlyList<string> lines, int index, IReadOnlyList<string> block) {
            if (index + block.Count > lines.Count)
                return false;
            for (int i = 0; i < block.Count; i++) {
                if (lines[index + i].Trim() != block[i])
                    return false;
            }
            return true;
        }

        private static bool MatchesLegacyStageGuard(IReadOnlyList<string> lines, int index) {
            if (index + 7 > lines.Count)
                return false;
            return lines[index + 1].Trim() == "if stage == nil then"
                && lines[index + 2].Trim().StartsWith("return listUpdatedForPlayers", StringComparison.Ordinal)
                && lines[index + 3].Trim() == "end"
                && lines[index + 4].Contains("stage >= 13", StringComparison.Ordinal)
                && lines[index + 4].Contains("stage < 1", StringComparison.Ordinal)
                && MatchesReturnAndEnd(lines, index + 5);
        }

        private static bool MatchesReturnAndEnd(IReadOnlyList<string> lines, int index) =>
            index + 1 < lines.Count
            && lines[index].Trim().StartsWith("return listUpdatedForPlayers", StringComparison.Ordinal)
            && lines[index + 1].Trim() == "end";

        private static int FindUniqueAnchor(IReadOnlyList<string> lines) {
            int index = -1;
            for (int i = 0; i < lines.Count; i++) {
                if (!lines[i].Contains(Anchor, StringComparison.Ordinal))
                    continue;
                if (index >= 0)
                    return -1;
                index = i;
            }
            return index;
        }

        private static LuaDocument ReadDocument(string path) {
            byte[] bytes = File.ReadAllBytes(path);
            bool hasBom = bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF;
            string text = new UTF8Encoding(false, true).GetString(bytes, hasBom ? 3 : 0, bytes.Length - (hasBom ? 3 : 0));
            string newLine = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
            var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
            return new LuaDocument(lines, newLine, hasBom);
        }

        private static void WriteDocument(string path, LuaDocument document) {
            string text = string.Join(document.NewLine, document.Lines);
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("The EID file has no parent directory.");
            string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try {
                File.WriteAllText(temporaryPath, text, new UTF8Encoding(document.HasBom));
                File.Move(temporaryPath, path, true);
            } finally {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static string GetApiPath(string eidPath) =>
            Path.Combine(eidPath, "features", "eid_api.lua");

        private sealed record LuaDocument(List<string> Lines, string NewLine, bool HasBom);
    }
}
