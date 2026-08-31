using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using GameFinder.StoreHandlers.Steam.Services;
using NexusMods.Paths;
using System;
using System.IO;

namespace IsaacModInstaller {
    public enum PatchStatus {
        NotPatched,
        PartiallyPatched,
        Patched,
        Unsupported,
    }

    public static class GamePatcher {
        private static readonly byte[] CoopOriginal = Convert.FromHexString(
            "83E802742A83E801741E83E801741232C08B4DF464890D00000000");
        private static readonly byte[] CoopPatched = Convert.FromHexString(
            "83E802909083E801909083E801909032C08B4DF464890D00000000");
        private static readonly byte[] AnalyticsOriginal = Convert.FromHexString(
            "558BEC83EC10535657FF15");
        private static readonly byte[] AnalyticsPatched = Convert.FromHexString(
            "C38BEC83EC10535657FF15");

        private static readonly byte[] CoopCharactersToggleOriginal = Convert.FromHexString(
            "8A4F0984C9510F94C033D284C98847090FB647080F44D303D08D04528D0CC54041B200E81F9B0500");
        private static readonly byte[] CoopCharactersTogglePatched = Convert.FromHexString(
            "807709010FB647083C117604B001EB180FB657096BD21203D06BCA185181C14041B200E81F9B0500");
        private static readonly byte[] CoopCharactersCycleOriginal = Convert.FromHexString(
            "8A7F0833F6807F0900B9120000008ADF0F45F10F1F0002D8885F0880FBFF7508C6470811B311EB0B80FB127206C647080032DB0FB6C303C6518D04408D0CC54041B200E87E9A050084C075078B45083ADF75C3817DF8E90300007452");
        private static readonly byte[] CoopCharactersCyclePatched = Convert.FromHexString(
            "8A7F088ADFBE8898C7008B0E2B4EFCC1F90383C1128A450802D83AD97209C0F8078AD822D902D8885F0880FB1273200FB657096BD2120FB6CB03CA6BC9188D8C0EB8A8EAFF51E87B9A050084C074BB807DF8E9745990909090909090");
        private static readonly byte[] CoopCharactersRelocationOriginal = Convert.FromHexString(
            "B33149327332");
        private static readonly byte[] CoopCharactersRelocationPatched = Convert.FromHexString(
            "B33110327332");
        private static readonly byte[] CoopCharactersAvailabilityOriginal = Convert.FromHexString(
            "0FB6C1518D04C08D04428D04408D0CC54041B200E847D1FFFFC3CCCCCCCCCCCC");
        private static readonly byte[] CoopCharactersAvailabilityPatched = Convert.FromHexString(
            "83FA127203B001C30FB6C16BC01203C26BC8185181C14041B200E841D1FFFFC3");
        private static readonly byte[] CoopCharactersAvailabilityRelocationOriginal = Convert.FromHexString(
            "073B1E3B323B803B9C3BB43BCD3B");
        private static readonly byte[] CoopCharactersAvailabilityRelocationPatched = Convert.FromHexString(
            "073B1E3B323B863B9C3BB43BCD3B");

        public static string DetectGamePath() {
            string steamPath = GetSteamGamePath();
            if (!string.IsNullOrEmpty(steamPath))
                return steamPath;

            string gogPath = GetGOGGamePath();
            if (!string.IsNullOrEmpty(gogPath))
                return gogPath;

            string epicPath = GetEpicGamePath();
            return string.IsNullOrEmpty(epicPath) ? string.Empty : epicPath;
        }

        public static PatchStatus GetCoopPatchStatus(string gamePath) =>
            GetPatchStatus(File.ReadAllBytes(gamePath), CoopOriginal, CoopPatched);

        public static PatchStatus GetCoopCharactersPatchStatus(string gamePath) {
            byte[] exeBytes = File.ReadAllBytes(gamePath);
            return GetCoopCharactersPatchStatus(exeBytes);
        }

        public static bool PatchGameExecutable(string gamePath) =>
            PatchFile(gamePath, CoopOriginal, CoopPatched);

        public static bool PatchGameExecutableAnalytics(string gamePath) =>
            PatchFile(gamePath, AnalyticsOriginal, AnalyticsPatched);

        public static bool PatchGameExecutableCoopCharacters(string gamePath) {
            byte[] exeBytes = File.ReadAllBytes(gamePath);
            if (GetPatchStatus(exeBytes, CoopOriginal, CoopPatched) != PatchStatus.Patched)
                throw new InvalidOperationException("The co-op mods patch must be applied first.");

            PatchStatus status = GetCoopCharactersPatchStatus(exeBytes);
            if (status == PatchStatus.Unsupported)
                throw new InvalidOperationException("The co-op character patch does not support this game version.");
            if (status == PatchStatus.Patched)
                return false;

            ApplyPatch(exeBytes, CoopCharactersToggleOriginal, CoopCharactersTogglePatched);
            ApplyPatch(exeBytes, CoopCharactersCycleOriginal, CoopCharactersCyclePatched);
            ApplyPatch(exeBytes, CoopCharactersRelocationOriginal, CoopCharactersRelocationPatched);
            ApplyPatch(exeBytes, CoopCharactersAvailabilityOriginal, CoopCharactersAvailabilityPatched);
            ApplyPatch(exeBytes, CoopCharactersAvailabilityRelocationOriginal,
                CoopCharactersAvailabilityRelocationPatched);
            if (GetCoopCharactersPatchStatus(exeBytes) != PatchStatus.Patched)
                throw new InvalidOperationException("The co-op character patch could not be verified.");

            WritePatchedFile(gamePath, exeBytes);
            return true;
        }

        private static bool PatchFile(string path, byte[] original, byte[] patched) {
            byte[] bytes = File.ReadAllBytes(path);
            bool modified = ApplyPatch(bytes, original, patched);
            if (modified)
                WritePatchedFile(path, bytes);
            return modified;
        }

        private static bool ApplyPatch(byte[] body, byte[] original, byte[] patched) {
            if (original.Length != patched.Length)
                throw new InvalidOperationException("Patch regions must have equal lengths.");

            PatchStatus status = GetPatchStatus(body, original, patched);
            if (status == PatchStatus.NotPatched) {
                int index = FindPattern(body, original);
                patched.CopyTo(body, index);
                return true;
            }
            if (status == PatchStatus.Patched)
                return false;
            throw new InvalidOperationException("Patch pattern is missing or ambiguous. The game version is not supported.");
        }

        private static PatchStatus GetPatchStatus(byte[] body, byte[] original, byte[] patched) {
            int originalCount = CountPattern(body, original);
            int patchedCount = CountPattern(body, patched);
            if (originalCount == 0 && patchedCount == 1)
                return PatchStatus.Patched;
            if (originalCount == 1 && patchedCount == 0)
                return PatchStatus.NotPatched;
            return PatchStatus.Unsupported;
        }

        private static void WritePatchedFile(string path, byte[] bytes) {
            string directory = Path.GetDirectoryName(path)
                ?? throw new InvalidOperationException("The target file has no parent directory.");
            string temporaryPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
            try {
                File.WriteAllBytes(temporaryPath, bytes);
                File.Move(temporaryPath, path, true);
            } finally {
                if (File.Exists(temporaryPath))
                    File.Delete(temporaryPath);
            }
        }

        private static int FindPattern(byte[] body, byte[] pattern) {
            for (int i = 0; i <= body.Length - pattern.Length; i++) {
                int j = 0;
                while (j < pattern.Length && body[i + j] == pattern[j])
                    j++;
                if (j == pattern.Length)
                    return i;
            }
            return -1;
        }

        private static int CountPattern(byte[] body, byte[] pattern) {
            int count = 0;
            for (int i = 0; i <= body.Length - pattern.Length; i++) {
                int j = 0;
                while (j < pattern.Length && body[i + j] == pattern[j])
                    j++;
                if (j != pattern.Length)
                    continue;

                count++;
                if (count > 1)
                    return count;
                i += pattern.Length - 1;
            }
            return count;
        }

        private static PatchStatus GetCoopCharactersPatchStatus(byte[] exeBytes) {
            PatchStatus toggle = GetPatchStatus(exeBytes, CoopCharactersToggleOriginal, CoopCharactersTogglePatched);
            PatchStatus cycle = GetPatchStatus(exeBytes, CoopCharactersCycleOriginal, CoopCharactersCyclePatched);
            PatchStatus relocation = GetPatchStatus(exeBytes, CoopCharactersRelocationOriginal,
                CoopCharactersRelocationPatched);
            PatchStatus availability = GetPatchStatus(exeBytes, CoopCharactersAvailabilityOriginal,
                CoopCharactersAvailabilityPatched);
            PatchStatus availabilityRelocation = GetPatchStatus(exeBytes,
                CoopCharactersAvailabilityRelocationOriginal, CoopCharactersAvailabilityRelocationPatched);
            if (toggle == PatchStatus.Patched && cycle == PatchStatus.Patched && relocation == PatchStatus.Patched &&
                availability == PatchStatus.Patched && availabilityRelocation == PatchStatus.Patched)
                return PatchStatus.Patched;
            if (toggle == PatchStatus.NotPatched && cycle == PatchStatus.NotPatched &&
                relocation == PatchStatus.NotPatched && availability == PatchStatus.NotPatched &&
                availabilityRelocation == PatchStatus.NotPatched)
                return PatchStatus.NotPatched;
            return PatchStatus.Unsupported;
        }

        private static string GetSteamGamePath() {
            var handler = new SteamHandler(FileSystem.Shared, OperatingSystem.IsWindows() ? WindowsRegistry.Shared : null);
            var maybeGame = handler.FindOneGameById(AppId.From(250900), out _);
            return maybeGame is SteamGame game ? Path.Combine(game.Path.GetFullPath(), "isaac-ng.exe") : string.Empty;
        }

        private static string GetGOGGamePath() {
            var handler = new GOGHandler(WindowsRegistry.Shared, FileSystem.Shared);
            var maybeGame = handler.FindOneGameById(GOGGameId.From(1205572215), out _);
            return maybeGame is GOGGame game ? Path.Combine(game.Path.GetFullPath(), "isaac-ng.exe") : string.Empty;
        }

        private static string GetEpicGamePath() {
            var handler = new EGSHandler(WindowsRegistry.Shared, FileSystem.Shared);
            var maybeGame = handler.FindOneGameById(EGSGameId.From("dbf5337d024e457bac68f2059112da86"), out _);
            return maybeGame is EGSGame game ? Path.Combine(game.InstallLocation.GetFullPath(), "isaac-ng.exe") : string.Empty;
        }
    }
}
