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

        private void PatchButton_Click(object sender, RoutedEventArgs e) {
            string gamePath = txtGamePath.Text;

            if (!File.Exists(gamePath)) {
                MessageBox.Show("Invalid game path. Please select the correct executable.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try {
                bool success = GamePatcher.PatchGameExecutable(gamePath);

                if (success) {
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
