using System.IO;
using System.Windows;

namespace IsaacModInstaller {
    public partial class MainWindow : Window {
        public MainWindow() {
            InitializeComponent();

            // Attempt to detect the game path
            string gamePath = GamePatcher.DetectGamePath();
            if (!string.IsNullOrEmpty(gamePath)) {
                txtGamePath.Text = gamePath;
                lblStatus.Content = "Game path detected automatically.";
                lblStatus.Foreground = System.Windows.Media.Brushes.Green;
            } else {
                lblStatus.Content = "Game path not detected. Please browse manually.";
                lblStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
        private void BrowseButton_Click(object sender, RoutedEventArgs e) {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Filter = "Game Executable (isaac-ng.exe)|isaac-ng.exe";
            dialog.Title = "Select The Binding of Isaac Executable";

            if (dialog.ShowDialog() == true) {
                txtGamePath.Text = dialog.FileName;
                lblStatus.Content = "Game path selected.";
                lblStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
        }
        private void EIDButton_Click(object sender, RoutedEventArgs e) {
            string gamePath = txtGamePath.Text;

            if (!File.Exists(gamePath)) {
                MessageBox.Show("Invalid game path. Please select the correct executable.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            var modsPath = Path.Combine(Path.GetDirectoryName(gamePath)!, "mods");
            if (!Directory.Exists(modsPath)) {
                MessageBox.Show($"No directory found at {modsPath}. Please make sure the mod is installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            var eidPath = Directory.GetDirectories(modsPath).FirstOrDefault(d => d.Contains("external", StringComparison.InvariantCultureIgnoreCase) && d.Contains("item", StringComparison.InvariantCultureIgnoreCase) && d.Contains("descriptions", StringComparison.InvariantCultureIgnoreCase));
            if (eidPath == default) {
                MessageBox.Show($"External Item Descriptions not found in mod dir {modsPath}. Please make sure the mod is installed.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            try {
                bool success = EIDPatcher.Patch(eidPath!);

                if (success) {
                    lblStatus.Content = "EID patched successfully!";
                    lblStatus.Foreground = System.Windows.Media.Brushes.Green;
                } else {
                    lblStatus.Content = "EID already patched.";
                    lblStatus.Foreground = System.Windows.Media.Brushes.Orange;
                }
            } catch (Exception ex) {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        private void PatchButton_Click(object sender, RoutedEventArgs e) {
            string gamePath = txtGamePath.Text;

            if (!File.Exists(gamePath)) {
                MessageBox.Show("Invalid game path. Please select the correct executable.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try {
                bool success = GamePatcher.PatchGameExecutable(gamePath);
                bool success2 = GamePatcher.PatchGameExecutableAnalytics(gamePath);

                if (success || success2) {
                    lblStatus.Content = "Game patched successfully!";
                    lblStatus.Foreground = System.Windows.Media.Brushes.Green;
                } else {
                    lblStatus.Content = "Game already patched.";
                    lblStatus.Foreground = System.Windows.Media.Brushes.Orange;
                }
            } catch (Exception ex) {
                MessageBox.Show($"An error occurred: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
