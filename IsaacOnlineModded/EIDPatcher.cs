using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsaacModInstaller {
    public static class EIDPatcher {
        private static List<string[]> oldPatches = [
            [
                "if EID.isOnlineMultiplayer and Game():GetLevel():GetStage() >= LevelStage.Home then",
                "return listUpdatedForPlayers -- Calling player:HasCollectible can cause a crash after beating The Beast in R+ Coop",
                "end",
                ""
                ]
            ];
        public static bool Patch(string EIDPath) {
            var mainLuaPath = Path.Combine(EIDPath, "main.lua");
            string[] lines = File.ReadAllLines(mainLuaPath);
            bool mainLuaModified = lines.Any(line => line.Contains("EID.isMultiplayer = true")) && lines.Any(line => line.Contains("EID.isOnlineMultiplayer = true"));
            if (!mainLuaModified) {
                for (int i = 0; i < lines.Length; i++) {
                    if (lines[i].Contains("EID.isMultiplayer = false -- Used to color P1's highlight/outline indicators (single player just uses white)")) {
                        lines[i] = lines[i].Replace("EID.isMultiplayer = false", "EID.isMultiplayer = true");
                    }

                    if (lines[i].Contains("EID.isOnlineMultiplayer = false -- Set to true to disable code functions that might cause desyncs")) {
                        lines[i] = lines[i].Replace("EID.isOnlineMultiplayer = false", "EID.isOnlineMultiplayer = true");
                    }
                }
                File.WriteAllLines(mainLuaPath, lines);
            }
            var eidAPIPath = Path.Combine(EIDPath, "features", "eid_api.lua");
            lines = File.ReadAllLines(eidAPIPath);
            var lineList = lines.ToList();
            lineList = RemoveOldPatches(lineList);
            bool isFirstContained = lineList.Any(line => line.Contains("local stage = Game():GetLevel():GetStage()"));
            bool eidApiModified = false;
            if (isFirstContained) {
                int firstLine = lineList.IndexOf(lines.First(line => line.Contains("local stage = Game():GetLevel():GetStage()")));
                eidApiModified = lineList[firstLine + 1].Contains("if stage == nil then") 
                              && lineList[firstLine + 2].Contains("return listUpdatedForPlayers")
                              && lineList[firstLine + 3].Contains("end")
                              && lineList[firstLine + 4].Contains("if EID.isOnlineMultiplayer and (stage >= 13 or stage < 1) then")
                              && lineList[firstLine + 5].Contains("return listUpdatedForPlayers")
                              && lineList[firstLine + 6].Contains("end");
            }
            if (!eidApiModified) {
                int threeBeforePatch = lineList.IndexOf(lines.First(line => line.Contains("return listUpdatedForPlayers -- dont evaluate when bad data is present")));
                lineList.Insert(threeBeforePatch + 2, "\t\t");
                lineList.Insert(threeBeforePatch + 3, "\t\tlocal stage = Game():GetLevel():GetStage()");
                lineList.Insert(threeBeforePatch + 4, "\t\tif stage == nil then");
                lineList.Insert(threeBeforePatch + 5, "\t\t\treturn listUpdatedForPlayers");
                lineList.Insert(threeBeforePatch + 6, "\t\tend");
                lineList.Insert(threeBeforePatch + 7, "\t\tif EID.isOnlineMultiplayer and (stage >= 13 or stage < 1) then");
                lineList.Insert(threeBeforePatch + 8, "\t\t\treturn listUpdatedForPlayers -- Calling player:HasCollectible can cause a crash after beating The Beast in R+ Coop");
                lineList.Insert(threeBeforePatch + 9, "\t\tend");
                File.WriteAllLines(eidAPIPath, lineList);
            }
            return !(eidApiModified && mainLuaModified);
        }

        public static List<string> RemoveOldPatches(List<string> lines) {
            foreach (var oldPatch in oldPatches) {
                var candidates = lines.Where(l => l.Contains(oldPatch[0]));
                foreach (var candidate in candidates) {
                    int startIndex = lines.IndexOf(candidate);

                    if (startIndex == -1)
                        continue;

                    if (startIndex + oldPatch.Length > lines.Count)
                        continue;

                    bool allMatch = true;
                    for (int i = 1; i < oldPatch.Length; i++) {
                        if (!lines[startIndex + i].Contains(oldPatch[i])) {
                            allMatch = false;
                            break;
                        }
                    }
                    if (allMatch) {
                        lines.RemoveRange(startIndex, oldPatch.Length);
                        break;
                    }
                }
            }
            return lines;
        }
    }
}
