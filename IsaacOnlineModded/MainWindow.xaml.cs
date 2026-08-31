using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace IsaacModInstaller {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            string gamePath = GamePatcher.DetectGamePath();
            if (string.IsNullOrEmpty(gamePath)) {
                ShowStatus("Game path not detected. Please browse manually.", Brushes.Red);
                UpdateDiagnostics();
                return;
            }

            txtGamePath.Text = gamePath;
            ShowStatus("Game path detected automatically.", Brushes.Green);
            UpdateDiagnostics();
        }

        private void BrowseButton_Click(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog {
                Filter = "Game Executable (isaac-ng.exe)|isaac-ng.exe",
                Title = "Select The Binding of Isaac Executable",
            };

            if (dialog.ShowDialog() == true) {
                txtGamePath.Text = dialog.FileName;
                ShowStatus("Game path selected.", Brushes.Green);
            }
        }

        private void PatchButton_Click(object sender, RoutedEventArgs e) {
            string gamePath = txtGamePath.Text;
            if (!File.Exists(gamePath)) {
                ShowError("Invalid game path. Please select the correct executable.");
                return;
            }

            try {
                bool modified = GamePatcher.PatchGameExecutable(gamePath);
                modified |= GamePatcher.PatchGameExecutableAnalytics(gamePath);
                ShowStatus(modified ? "Game patched successfully." : "Game is already patched.",
                    modified ? Brushes.Green : Brushes.DarkOrange);
            } catch (Exception ex) {
                ShowError(ex.Message);
            } finally {
                UpdateDiagnostics();
            }
        }

        private void CoopCharactersButton_Click(object sender, RoutedEventArgs e) {
            string gamePath = txtGamePath.Text;
            if (!File.Exists(gamePath)) {
                ShowError("Invalid game path. Please select the correct executable.");
                return;
            }

            try {
                bool modified = GamePatcher.PatchGameExecutableCoopCharacters(gamePath);
                ShowStatus(modified ? "Coop characters patched successfully." : "Coop characters are already patched.",
                    modified ? Brushes.Green : Brushes.DarkOrange);
            } catch (Exception ex) {
                ShowError(ex.Message);
            } finally {
                UpdateDiagnostics();
            }
        }

        private void EIDButton_Click(object sender, RoutedEventArgs e) {
            string gamePath = txtGamePath.Text;
            if (!File.Exists(gamePath)) {
                ShowError("Invalid game path. Please select the correct executable.");
                return;
            }

            string? eidPath = FindEidPath(gamePath);
            if (eidPath == null) {
                ShowError("External Item Descriptions was not found in the game's mods directory.");
                return;
            }

            try {
                bool modified = EIDPatcher.Patch(eidPath);
                ShowStatus(modified ? "EID patched successfully." : "EID is already patched.",
                    modified ? Brushes.Green : Brushes.DarkOrange);
            } catch (Exception ex) {
                ShowError(ex.Message);
            } finally {
                UpdateDiagnostics();
            }
        }

        private void GamePath_TextChanged(object sender, TextChangedEventArgs e) {
            if (IsInitialized)
                UpdateDiagnostics();
        }

        private void UpdateDiagnostics() {
            string gamePath = txtGamePath.Text;
            if (!File.Exists(gamePath)) {
                SetUnavailable(txtCoopPatchStatus);
                SetUnavailable(txtCoopCharactersPatchStatus);
                SetUnavailable(txtEidPatchStatus);
                btnPatchCoopCharacters.IsEnabled = false;
                return;
            }

            try {
                PatchStatus coopStatus = GamePatcher.GetCoopPatchStatus(gamePath);
                PatchStatus charactersStatus = GamePatcher.GetCoopCharactersPatchStatus(gamePath);
                SetPatchStatus(txtCoopPatchStatus, coopStatus);
                SetPatchStatus(txtCoopCharactersPatchStatus, charactersStatus);
                btnPatchCoopCharacters.IsEnabled = coopStatus == PatchStatus.Patched
                    && charactersStatus != PatchStatus.Unsupported;
            } catch {
                SetUnavailable(txtCoopPatchStatus);
                SetUnavailable(txtCoopCharactersPatchStatus);
                btnPatchCoopCharacters.IsEnabled = false;
            }

            string? eidPath = FindEidPath(gamePath);
            if (eidPath == null)
                SetUnavailable(txtEidPatchStatus, "Not installed");
            else {
                try {
                    SetPatchStatus(txtEidPatchStatus, EIDPatcher.GetPatchStatus(eidPath));
                } catch {
                    SetUnavailable(txtEidPatchStatus);
                }
            }
        }

        private static string? FindEidPath(string gamePath) {
            string? gameDirectory = Path.GetDirectoryName(gamePath);
            if (gameDirectory == null)
                return null;

            string modsPath = Path.Combine(gameDirectory, "mods");
            if (!Directory.Exists(modsPath))
                return null;

            return Directory.EnumerateDirectories(modsPath)
                .FirstOrDefault(path => {
                    string name = Path.GetFileName(path);
                    return name.Contains("external", StringComparison.OrdinalIgnoreCase)
                        && name.Contains("item", StringComparison.OrdinalIgnoreCase)
                        && name.Contains("descriptions", StringComparison.OrdinalIgnoreCase)
                        && File.Exists(Path.Combine(path, "features", "eid_api.lua"));
                });
        }

        private static void SetPatchStatus(TextBlock target, PatchStatus status) {
            target.Text = status switch {
                PatchStatus.Patched => "Yes",
                PatchStatus.NotPatched => "No",
                PatchStatus.PartiallyPatched => "Partial",
                _ => "Unsupported",
            };
            target.Foreground = status switch {
                PatchStatus.Patched => Brushes.Green,
                PatchStatus.NotPatched => Brushes.DarkOrange,
                PatchStatus.PartiallyPatched => Brushes.DarkOrange,
                _ => Brushes.Gray,
            };
        }

        private static void SetUnavailable(TextBlock target, string text = "Unavailable") {
            target.Text = text;
            target.Foreground = Brushes.Gray;
        }

        private void ShowError(string message) {
            ShowStatus(message, Brushes.Red);
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowStatus(string message, Brush color) {
            txtStatus.Text = message;
            txtStatus.Foreground = color;
        }
    }
}
