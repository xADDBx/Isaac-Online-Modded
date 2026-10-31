using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using System;
using System.IO;
using System.Linq;

namespace IsaacModInstaller {
    public partial class MainWindow : Window {
        private static readonly string[] Kinds = ["good", "warn", "bad", "muted"];

        public MainWindow() {
            InitializeComponent();
            txtGamePath.Text = GameLocator.FindGamePath();
            UpdateDiagnostics();
            if (string.IsNullOrEmpty(txtGamePath.Text))
                ShowMessage("Couldn't find the game. Select isaac-ng.exe with Browse.", "warn");
        }

        private async void Browse_Click(object? sender, RoutedEventArgs e) {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions {
                Title = "Select The Binding of Isaac Executable",
                FileTypeFilter = [new FilePickerFileType("Game Executable (isaac-ng.exe)") { Patterns = ["isaac-ng.exe"] }],
            });
            if (files.Count > 0 && files[0].TryGetLocalPath() is string path)
                txtGamePath.Text = path;
        }

        private void Coop_Click(object? sender, RoutedEventArgs e) =>
            Patch("Coop mods", path => GamePatcher.PatchGameExecutable(path) | GamePatcher.PatchGameExecutableAnalytics(path));

        private void RestartDesync_Click(object? sender, RoutedEventArgs e) =>
            Patch("Restart desync fix", GamePatcher.PatchGameExecutableStaleInput);

        private void CoopCharacters_Click(object? sender, RoutedEventArgs e) =>
            Patch("Coop characters", GamePatcher.PatchGameExecutableCoopCharacters);

        private void Eid_Click(object? sender, RoutedEventArgs e) =>
            Patch("External Item Descriptions", path => {
                string eidPath = FindEidPath(path)
                    ?? throw new InvalidOperationException("External Item Descriptions was not found in the game's mods directory.");
                if (GamePatcher.GetMapInputPatchStatus(path) == PatchStatus.Unsupported)
                    throw new InvalidOperationException("The online map button fix does not support this game version.");
                return EIDPatcher.Patch(eidPath) | GamePatcher.PatchGameExecutableMapInput(path);
            });

        private void AntibirthMusic_Click(object? sender, RoutedEventArgs e) =>
            Patch("Antibirth Music+++", path => AntibirthMusicPatcher.Patch(FindAntibirthMusicPath(path)
                ?? throw new InvalidOperationException("Antibirth Music+++ was not found in the game's mods directory.")));

        private void GamePath_TextChanged(object? sender, TextChangedEventArgs e) {
            txtMessage.IsVisible = false;
            UpdateDiagnostics();
        }

        private void Patch(string name, Func<string, bool> patch) {
            try {
                string gamePath = txtGamePath.Text ?? "";
                if (!File.Exists(gamePath))
                    throw new InvalidOperationException("Invalid game path. Please select the correct executable.");
                bool modified = patch(gamePath);
                ShowMessage(modified ? $"{name} patched successfully." : $"{name} is already patched.", modified ? "good" : "warn");
            } catch (Exception ex) {
                ShowMessage(ex.Message, "bad");
            }
            UpdateDiagnostics();
        }

        private void UpdateDiagnostics() {
            string? gamePath = File.Exists(txtGamePath.Text) ? txtGamePath.Text : null;
            PatchStatus? coop = GetStatus(gamePath, path =>
                Combine(GamePatcher.GetCoopPatchStatus(path), GamePatcher.GetAnalyticsPatchStatus(path)));
            ShowStatus(statusCoop, btnCoop, coop);
            ShowStatus(statusRestartDesync, btnRestartDesync, GetStatus(gamePath, GamePatcher.GetStaleInputPatchStatus));
            ShowStatus(statusCoopCharacters, btnCoopCharacters,
                GetStatus(gamePath, GamePatcher.GetCoopCharactersPatchStatus), coop == PatchStatus.Patched);

            string? eidPath = gamePath == null ? null : FindEidPath(gamePath);
            ShowStatus(statusEid, btnEid, GetStatus(eidPath, path =>
                Combine(EIDPatcher.GetPatchStatus(path), GamePatcher.GetMapInputPatchStatus(gamePath!))));
            string? musicPath = gamePath == null ? null : FindAntibirthMusicPath(gamePath);
            ShowStatus(statusAntibirthMusic, btnAntibirthMusic, GetStatus(musicPath, AntibirthMusicPatcher.GetPatchStatus));

            if (gamePath != null && eidPath == null)
                SetText(statusEid, "Not installed");
            if (gamePath != null && musicPath == null)
                SetText(statusAntibirthMusic, "Not installed");
        }

        private static PatchStatus? GetStatus(string? path, Func<string, PatchStatus> getStatus) {
            if (path == null)
                return null;
            try {
                return getStatus(path);
            } catch {
                return null;
            }
        }

        // The coop fix includes the analytics patch and the EID fix the game's online map button. Only both parts
        // together count as patched, so the button applies whatever is missing.
        private static PatchStatus Combine(PatchStatus first, PatchStatus second) {
            if (first == PatchStatus.Unsupported || second == PatchStatus.Unsupported)
                return PatchStatus.Unsupported;
            return first == PatchStatus.Patched && second == PatchStatus.Patched ? PatchStatus.Patched : PatchStatus.NotPatched;
        }

        private static void ShowStatus(Border pill, Button button, PatchStatus? status, bool canPatch = true) {
            SetText(pill, status switch {
                PatchStatus.Patched => "Patched",
                PatchStatus.NotPatched => "Not patched",
                PatchStatus.PartiallyPatched => "Partially patched",
                PatchStatus.Unsupported => "Unsupported",
                _ => "Unavailable",
            });
            SetKind(pill, status switch {
                PatchStatus.Patched => "good",
                PatchStatus.NotPatched or PatchStatus.PartiallyPatched => "warn",
                _ => "muted",
            });
            button.IsEnabled = canPatch && status is PatchStatus.NotPatched or PatchStatus.PartiallyPatched;
        }

        private static string? FindEidPath(string gamePath) =>
            FindModPath(gamePath, path => {
                string name = Path.GetFileName(path);
                return name.Contains("external", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("item", StringComparison.OrdinalIgnoreCase)
                    && name.Contains("descriptions", StringComparison.OrdinalIgnoreCase)
                    && File.Exists(Path.Combine(path, "features", "eid_api.lua"));
            });

        private static string? FindAntibirthMusicPath(string gamePath) =>
            FindModPath(gamePath, path => Path.GetFileName(path).Contains("antibirth", StringComparison.OrdinalIgnoreCase)
                && AntibirthMusicPatcher.IsAntibirthMusic(path));

        private static string? FindModPath(string gamePath, Func<string, bool> match) {
            string? gameDirectory = Path.GetDirectoryName(gamePath);
            if (gameDirectory == null)
                return null;

            string modsPath = Path.Combine(gameDirectory, "mods");
            if (!Directory.Exists(modsPath))
                return null;

            return Directory.EnumerateDirectories(modsPath).FirstOrDefault(match);
        }

        private void ShowMessage(string message, string kind) {
            txtMessage.Text = message;
            txtMessage.IsVisible = true;
            SetKind(txtMessage, kind);
        }

        private static void SetText(Border pill, string text) =>
            ((TextBlock)pill.Child!).Text = text;

        private static void SetKind(StyledElement element, string kind) {
            element.Classes.RemoveAll(Kinds);
            element.Classes.Add(kind);
        }
    }
}
