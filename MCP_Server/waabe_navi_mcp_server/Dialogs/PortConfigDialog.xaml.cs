using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using waabe_navi_mcp_server.Services;
using waabe_navi_shared;

namespace waabe_navi_mcpserver.Dialogs
{
    public partial class PortConfigDialog : Window
    {
        public int SelectedPort { get; private set; }
        public bool IsConfirmed { get; private set; }

        public PortConfigDialog()
        {
            InitializeComponent();
            LoadCurrentSettings();
        }

        public PortConfigDialog(int currentPort) : this()
        {
            PortTextBox.Text = currentPort.ToString();
        }

        private void LoadCurrentSettings()
        {
            try
            {
                var currentPort = SettingsManager.GetMCPPort();
                PortTextBox.Text = currentPort.ToString();
                LogHelper.LogEvent($"Port dialog loaded with port: {currentPort}");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error loading settings in dialog: {ex.Message}");
                PortTextBox.Text = "8080"; // Fallback
            }
        }

        private void PortTextBox_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            // Nur Zahlen erlauben
            var regex = new Regex("[^0-9]+");
            e.Handled = regex.IsMatch(e.Text);
        }

        private bool ValidatePort()
        {
            ValidationMessage.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(PortTextBox.Text))
            {
                ShowValidationError("Port must not be empty.");
                return false;
            }

            if (!int.TryParse(PortTextBox.Text, out int port))
            {
                ShowValidationError("Port must be a valid number.");
                return false;
            }

            if (port < 1024 || port > 65535)
            {
                ShowValidationError("Port must be between 1024 and 65535.");
                return false;
            }

            // Prüfe, ob Port bereits verwendet wird (optional)
            if (IsPortInUse(port))
            {
                ShowValidationError($"Port {port} is already in use. Please select a different port.");
                return false;
            }

            SelectedPort = port;
            return true;
        }

        private bool IsPortInUse(int port)
        {
            try
            {
                // Einfache Prüfung ob Port bereits gebunden ist
                var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, port);
                {
                    listener.Start();
                    listener.Stop();
                    return false; // Port ist frei
                }
            }
            catch
            {
                return true; // Port ist belegt
            }
        }

        private void ShowValidationError(string message)
        {
            ValidationMessage.Text = message;
            ValidationMessage.Visibility = Visibility.Visible;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (ValidatePort())
            {
                try
                {
                    // Speichere Port in Settings
                    SettingsManager.SaveMCPPort(SelectedPort);

                    IsConfirmed = true;
                    LogHelper.LogEvent($"Port dialog confirmed with port: {SelectedPort}");

                    DialogResult = true;
                    Close();
                }
                catch (Exception ex)
                {
                    LogHelper.LogEvent($"Error saving port: {ex.Message}");
                    ShowValidationError($"Error saving: {ex.Message}");
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            IsConfirmed = false;
            LogHelper.LogEvent("Port dialog aborted");

            DialogResult = false;
            Close();
        }

        // Öffentliche statische Methode für einfache Verwendung
        public static bool ShowDialog(out int selectedPort)
        {
            selectedPort = 0;

            try
            {
                var dialog = new PortConfigDialog();
                var result = dialog.ShowDialog();

                if (result == true && dialog.IsConfirmed)
                {
                    selectedPort = dialog.SelectedPort;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error displaying the port dialog: {ex.Message}");
                System.Windows.Forms.MessageBox.Show(
                    $"Error opening the configuration dialog: {ex.Message}",
                    "WAABE MCP Server",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }

        // Überladung mit aktuellem Port
        public static bool ShowDialog(int currentPort, out int selectedPort)
        {
            selectedPort = 0;

            try
            {
                var dialog = new PortConfigDialog(currentPort);
                var result = dialog.ShowDialog();

                if (result == true && dialog.IsConfirmed)
                {
                    selectedPort = dialog.SelectedPort;
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Error displaying the port dialog: {ex.Message}");
                System.Windows.Forms.MessageBox.Show(
                    $"Error opening the configuration dialog: {ex.Message}",
                    "WAABE MCP Server",
                    System.Windows.Forms.MessageBoxButtons.OK,
                    System.Windows.Forms.MessageBoxIcon.Error);
                return false;
            }
        }
    }
}