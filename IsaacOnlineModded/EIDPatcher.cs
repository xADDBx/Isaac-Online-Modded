using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IsaacModInstaller {
    public static class EIDPatcher {
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
            bool isFirstContained = lines.Any(line => line.Contains("if EID.isOnlineMultiplayer and Game():GetLevel():GetStage() >= LevelStage.Home then"));
            bool eidApiModified = false;
            var lineList = lines.ToList();
            if (isFirstContained) {
                int firstLine = lineList.IndexOf(lines.First(line => line.Contains("if EID.isOnlineMultiplayer and Game():GetLevel():GetStage() >= LevelStage.Home then")));
                eidApiModified = lineList[firstLine + 1].Contains("return listUpdatedForPlayers -- Calling player:HasCollectible can cause a crash after beating The Beast in R+ Coop") && lineList[firstLine + 2].Contains("end");
            }
            if (!eidApiModified) {
                int threeBeforePatch = lineList.IndexOf(lines.First(line => line.Contains("return listUpdatedForPlayers -- dont evaluate when bad data is present")));
                lineList.Insert(threeBeforePatch + 2, "\t\t");
                lineList.Insert(threeBeforePatch + 3, "\t\tif EID.isOnlineMultiplayer and Game():GetLevel():GetStage() >= LevelStage.Home then");
                lineList.Insert(threeBeforePatch + 4, "\t\t\treturn listUpdatedForPlayers -- Calling player:HasCollectible can cause a crash after beating The Beast in R+ Coop");
                lineList.Insert(threeBeforePatch + 5, "\t\tend");
                File.WriteAllLines(eidAPIPath, lineList);
            }
            Console.WriteLine(eidApiModified.ToString());
            Console.WriteLine(mainLuaModified.ToString());
            return !(eidApiModified && mainLuaModified);
        }
    }
}
