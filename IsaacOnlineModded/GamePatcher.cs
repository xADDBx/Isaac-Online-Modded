using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using GameFinder.StoreHandlers.Steam.Services;
using Microsoft.Win32;
using NexusMods.Paths;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace IsaacModInstaller {
    public static class GamePatcher {
        #region GameFinder
        public static string DetectGamePath() {
            string steamPath = GetSteamGamePath();
            if (!string.IsNullOrEmpty(steamPath))
                return steamPath;

            string gogPath = GetGOGGamePath();
            if (!string.IsNullOrEmpty(gogPath))
                return gogPath;

            string epicPath = GetEpicGamePath();
            if (!string.IsNullOrEmpty(epicPath))
                return epicPath;

            return string.Empty;
        }
        private static string GetSteamGamePath() {
            var handler = new SteamHandler(FileSystem.Shared, OperatingSystem.IsWindows() ? WindowsRegistry.Shared : null);
            var maybeGame = handler.FindOneGameById(AppId.From(250900), out _);
            if (maybeGame is SteamGame game) {
                return Path.Combine(game.Path.GetFullPath(), "isaac-ng.exe");
            }
            return "";
        }

        private static string GetGOGGamePath() {
            var handler = new GOGHandler(WindowsRegistry.Shared, FileSystem.Shared);
            var maybeGame = handler.FindOneGameById(GOGGameId.From(1205572215), out _);
            if (maybeGame is GOGGame game) {
                return Path.Combine(game.Path.GetFullPath(), "isaac-ng.exe");
            }
            return "";
        }

        private static string GetEpicGamePath() {
            var handler = new EGSHandler(WindowsRegistry.Shared, FileSystem.Shared);
            var maybeGame = handler.FindOneGameById(EGSGameId.From("dbf5337d024e457bac68f2059112da86"), out _);
            if (maybeGame is EGSGame game) {
                return Path.Combine(game.InstallLocation.GetFullPath(), "isaac-ng.exe");
            }
            return "";
        }
        #endregion
        #region Patcher
        public static bool PatchGameExecutable(string gamePath) {
            // Create a backup of the original executable
            string backupPath = gamePath + ".bak";

            if (!File.Exists(backupPath)) {
                File.Copy(gamePath, backupPath);
            }

            // Read the executable into a byte array
            byte[] exeBytes = File.ReadAllBytes(gamePath);

            // Define the byte pattern to search for
            byte[] pattern = new byte[]
            {
        0x83, 0xE8, 0x02, 0x74, 0x2A, 0x83, 0xE8, 0x01, 0x74, 0x1E, 0x83, 0xE8, 0x01, 0x74, 0x12, 0x32, 0xC0,
        0x8B, 0x4D, 0xF4, 0x64, 0x89, 0x0D, 0x00, 0x00, 0x00, 0x00
            }; 
            byte[] alreadyPatchedPattern = new byte[]
            {
        0x83, 0xE8, 0x02, 0x90, 0x90, 0x83, 0xE8, 0x02, 0x90, 0x90, 0x83, 0xE8, 0x01, 0x90, 0x90, 0x32, 0xC0,
        0x8B, 0x4D, 0xF4, 0x64, 0x89, 0x0D, 0x00, 0x00, 0x00, 0x00
            };

            // Search for the pattern in the executable
            int index = FindPattern(exeBytes, pattern);

            if (index == -1) {
                if (FindPattern(exeBytes, alreadyPatchedPattern) > -1) {
                    return false;
                } else {
                    throw new Exception("Pattern not found in the executable. Maybe this tool is outdated?");
                }
            }

            // Modify the bytes at the found index
            // For example, replace the three jump instructions (0x74, 0x2A) (0x74, 0x1E) and (0x74, 0x12) with NOPs (0x90, 0x90)
            exeBytes[index + 3] = 0x90;
            exeBytes[index + 4] = 0x90;
            exeBytes[index + 8] = 0x90;
            exeBytes[index + 9] = 0x90;
            exeBytes[index + 13] = 0x90;
            exeBytes[index + 14] = 0x90;

            // Write the modified bytes back to the executable
            File.WriteAllBytes(gamePath, exeBytes);

            return true;
        }

        private static int FindPattern(byte[] body, byte[] pattern) {
            for (int i = 0; i < body.Length - pattern.Length; i++) {
                bool found = true;
                for (int j = 0; j < pattern.Length; j++) {
                    if (body[i + j] != pattern[j]) {
                        found = false;
                        break;
                    }
                }
                if (found) {
                    return i;
                }
            }
            return -1;
        }
        #endregion
    }
}
