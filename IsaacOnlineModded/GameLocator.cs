using GameFinder.RegistryUtils;
using GameFinder.StoreHandlers.EGS;
using GameFinder.StoreHandlers.GOG;
using GameFinder.StoreHandlers.Steam;
using GameFinder.StoreHandlers.Steam.Models.ValueTypes;
using GameFinder.Wine;
using GameFinder.Wine.Bottles;
using NexusMods.Paths;
using System;
using System.Collections.Generic;
using System.IO;

namespace IsaacModInstaller {
    // Windows: Steam, GOG Galaxy and Epic. Linux: Steam (native, Flatpak and Snap, which covers Proton)
    // and Steam, GOG Galaxy or Epic installed in a Wine or Bottles prefix.
    public static class GameLocator {
        private static readonly AppId SteamId = AppId.From(250900);
        private static readonly GOGGameId GogId = GOGGameId.From(1205572215);
        private static readonly EGSGameId EpicId = EGSGameId.From("dbf5337d024e457bac68f2059112da86");

        public static string FindGamePath() {
            foreach (Func<AbsolutePath?> find in GetFinders()) {
                try {
                    if (find() is not AbsolutePath directory)
                        continue;
                    string path = Path.GetFullPath(Path.Combine(directory.GetFullPath(), "isaac-ng.exe"));
                    if (File.Exists(path))
                        return path;
                } catch {
                    
                }
            }
            return string.Empty;
        }

        private static IEnumerable<Func<AbsolutePath?>> GetFinders() {
            IFileSystem fileSystem = FileSystem.Shared;
            if (OperatingSystem.IsWindows()) {
                IRegistry registry = WindowsRegistry.Shared;
                yield return () => FindInStores(fileSystem, registry);
                yield break;
            }

            yield return () => FindInStores(fileSystem, null);
            foreach (var result in new DefaultWinePrefixManager(fileSystem).FindPrefixes()) {
                if (result.TryGetPrefix(out var prefix))
                    yield return () => FindInPrefix(fileSystem, prefix);
            }
            foreach (var result in new BottlesWinePrefixManager(fileSystem).FindPrefixes()) {
                if (result.TryGetPrefix(out var prefix))
                    yield return () => FindInPrefix(fileSystem, prefix);
            }
        }

        private static AbsolutePath? FindInPrefix(IFileSystem fileSystem, AWinePrefix prefix) {
            IFileSystem overlay = prefix.CreateOverlayFileSystem(fileSystem);
            return FindInStores(overlay, prefix.CreateRegistry(overlay));
        }

        private static AbsolutePath? FindInStores(IFileSystem fileSystem, IRegistry? registry) {
            if (new SteamHandler(fileSystem, registry).FindOneGameById(SteamId, out _) is SteamGame steam)
                return steam.Path;
            if (registry == null)
                return null;
            if (new GOGHandler(registry, fileSystem).FindOneGameById(GogId, out _) is GOGGame gog)
                return gog.Path;
            return new EGSHandler(registry, fileSystem).FindOneGameById(EpicId, out _) is EGSGame epic ? epic.InstallLocation : null;
        }
    }
}
