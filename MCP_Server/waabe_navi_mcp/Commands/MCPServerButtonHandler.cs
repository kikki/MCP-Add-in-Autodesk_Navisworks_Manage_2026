using System;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using waabe_navi_shared;
using waabe_navi_mcp.Services;

namespace waabe_navi_mcp.Commands
{
    /// <summary>
    /// Command handler for the MCP Server button in the Navisworks Add-in.
    /// - Implements ICommand to start or stop the embedded MCP server.
    /// - Uses ServiceRegistry to resolve the server service dynamically via reflection.
    /// - Provides UI feedback through message boxes and logs detailed events.
    /// </summary>
    public class MCPServerButtonHandler : ICommand
    {
        /// <summary>
        /// Required by the ICommand interface.
        /// - Not actively used; CanExecute always returns true.
        /// </summary>
        public event EventHandler CanExecuteChanged { add { } remove { } }

        /// <summary>
        /// Determines if the button can be executed.
        /// - Always returns true, so the button is always enabled.
        /// </summary>
        public bool CanExecute(object parameter) => true;

        /// <summary>
        /// Executes when the MCP Server button is clicked.
        /// - Checks if the MCP server service is registered.
        /// - If missing, tries to create and register it dynamically via reflection.
        /// - Depending on the service state, starts or stops the server.
        /// - Displays errors or status information via MessageBox.
        /// </summary>
        public void Execute(object parameter)
        {
            try
            {
                LogHelper.LogEvent("=== MCP SERVER BUTTON GEKLICKT ===");

                ServiceRegistry.LogRegisteredServices();

                 
                var mcpService = ServiceRegistry.GetService("waabe_navi_mcp_server");
                if (mcpService == null)
                {
                    LogHelper.LogEvent("❌ MCP Server Service nicht gefunden");
                     
                    try
                    {
                        LogHelper.LogEvent("🔧 Versuche Service direkt zu erstellen...");

                        // Direkte Service-Erstellung als Fallback
                        var mcpServerServiceType = Type.GetType("waabe_navi_mcp_server.Plugins.MCPServerService, waabe_navi_mcp_server");
                        if (mcpServerServiceType != null)
                        {
                            var directService = Activator.CreateInstance(mcpServerServiceType);
                            ServiceRegistry.Register((IWaabeService)directService);

                            LogHelper.LogEvent("✅ Service direkt erstellt und registriert");
                            mcpService = ServiceRegistry.GetService("waabe_navi_mcp_server");
                        }
                        else
                        {
                            LogHelper.LogEvent("❌ MCPServerService Type nicht gefunden");
                        }
                    }
                    catch (Exception fallbackEx)
                    {
                        LogHelper.LogEvent($"❌ Fallback Service-Erstellung fehlgeschlagen: {fallbackEx.Message}");
                    }

                    if (mcpService == null)
                    {
                        MessageBox.Show("MCP Server Service nicht verfügbar.\n\nDas waabe_navi_mcp_server Plugin wird nicht geladen!\n\nPrüfen Sie:\n- Build der waabe_navi_mcp_server.dll\n- Plugin-Registrierung in PackageContents.xml",
                                      "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        return;
                    }


                    //return;
                }

                LogHelper.LogEvent("✅ MCP Server Service gefunden");

                 
                var serverServiceType = mcpService.GetType();
                var isServerRunningProperty = serverServiceType.GetProperty("IsServerRunning");
                var currentPortProperty = serverServiceType.GetProperty("CurrentPort");

                if (isServerRunningProperty == null)
                {
                    LogHelper.LogEvent("❌ IsServerRunning Property nicht gefunden");
                    MessageBox.Show("MCP Server Service-Fehler: IsServerRunning Property fehlt.",
                                  "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var isServerRunning = (bool)isServerRunningProperty.GetValue(mcpService);
                LogHelper.LogEvent($"Server-Status: {(isServerRunning ? "Läuft" : "Gestoppt")}");

                if (isServerRunning)
                {
                     
                    HandleMCPServerStop(mcpService, serverServiceType, currentPortProperty);
                }
                else
                {
                     
                    HandleMCPServerStart(mcpService, serverServiceType);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"❌ Fehler im MCPServerButtonHandler: {ex.Message}");
                MessageBox.Show($"Fehler beim MCP Server: {ex.Message}", "WAABE MCP Server",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles starting the MCP server.
        /// - Prompts the user for a port (via ShowPortConfigDialog).
        /// - Invokes StartServerAsync on the service using reflection.
        /// - Waits asynchronously for completion and updates UI/logs accordingly.
        /// </summary>
        private void HandleMCPServerStart(object mcpService, Type serverServiceType)
        {
            try
            {
                LogHelper.LogEvent("Starte MCP Server...");

                 
                var portResult = ShowPortConfigDialog();
                if (portResult == null)
                {
                    LogHelper.LogEvent("Port-Konfiguration abgebrochen");
                    return;
                }

                int selectedPort = portResult.Value;
                LogHelper.LogEvent($"Port konfiguriert: {selectedPort}");

                 
                var startServerMethod = serverServiceType.GetMethod("StartServerAsync");
                if (startServerMethod == null)
                {
                    LogHelper.LogEvent("❌ StartServerAsync Methode nicht gefunden");
                    MessageBox.Show("MCP Server Service-Fehler: StartServerAsync Methode fehlt.",
                                  "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                 
                var startTask = (Task<bool>)startServerMethod.Invoke(mcpService, new object[] { selectedPort });

                 
                Task.Run(async () =>
                {
                    try
                    {
                        var success = await startTask;

                         
                        InvokeOnUIThread(() =>
                        {
                            HandleServerStartResult(success, selectedPort, mcpService, serverServiceType);
                        });
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogEvent($"Async Server-Start Fehler: {ex.Message}");

                        InvokeOnUIThread(() =>
                        {
                            MessageBox.Show($"Fehler beim Starten des Servers: {ex.Message}",
                                          "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Server-Start: {ex.Message}");
                MessageBox.Show($"Fehler beim Starten: {ex.Message}", "WAABE MCP Server",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles the result of a server start attempt.
        /// - Displays success or failure messages to the user.
        /// - If successful, updates ServerStatusManager and logs the server URL/port.
        /// - If failed, prompts again for a different port.
        /// </summary>
        private void HandleServerStartResult(bool success, int port, object mcpService, Type serverServiceType)
        {
            try
            {
                if (success)
                {
                     
                    var getServerUrlMethod = serverServiceType.GetMethod("GetServerUrl");
                    var serverUrl = "http://localhost:" + port + "/";  

                    if (getServerUrlMethod != null)
                    {
                        try
                        {
                            var urlResult = getServerUrlMethod.Invoke(mcpService, null);
                            if (urlResult != null)
                            {
                                serverUrl = urlResult.ToString();
                            }
                        }
                        catch (Exception ex)
                        {
                            LogHelper.LogEvent($"Fehler beim Abrufen der Server-URL: {ex.Message}");
                        }
                    }

                    waabe_navi_mcp.Services.ServerStatusManager.SetServerStarted(port);

                    MessageBox.Show($"✅ MCP Server erfolgreich gestartet!\n\nPort: {port}\nURL: {serverUrl}\n\nSie können jetzt API-Anfragen an den Server senden.",
                                  "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Information);

                    LogHelper.LogEvent($"✅ MCP Server erfolgreich gestartet auf Port {port}");
                }
                else
                {
                    MessageBox.Show($"❌ MCP Server konnte nicht gestartet werden.\n\nMögliche Ursachen:\n- Port {port} ist bereits belegt\n- Firewall blockiert den Port\n- Unzureichende Berechtigungen\n\nBitte versuchen Sie einen anderen Port.",
                                  "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);

                    LogHelper.LogEvent($"❌ MCP Server-Start auf Port {port} fehlgeschlagen");

                    // Port-Dialog erneut anzeigen bei Fehler
                    HandleMCPServerStart(mcpService, serverServiceType);
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Verarbeiten des Server-Start-Ergebnisses: {ex.Message}");
                MessageBox.Show($"Fehler beim Verarbeiten des Ergebnisses: {ex.Message}",
                              "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Handles stopping the MCP server.
        /// - Reads the current port (if available).
        /// - Invokes StopServerAsync on the service using reflection.
        /// - Updates ServerStatusManager and shows confirmation in the UI.
        /// </summary>
        private void HandleMCPServerStop(object mcpService, Type serverServiceType, System.Reflection.PropertyInfo currentPortProperty)
        {
            try
            {
                LogHelper.LogEvent("Stoppe MCP Server...");

                var currentPort = 0;
                if (currentPortProperty != null)
                {
                    try
                    {
                        currentPort = (int)(currentPortProperty.GetValue(mcpService) ?? 0);
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogEvent($"Fehler beim Abrufen des aktuellen Ports: {ex.Message}");
                    }
                }

                 
                var stopServerMethod = serverServiceType.GetMethod("StopServerAsync");
                if (stopServerMethod == null)
                {
                    LogHelper.LogEvent("❌ StopServerAsync Methode nicht gefunden");
                    MessageBox.Show("MCP Server Service-Fehler: StopServerAsync Methode fehlt.",
                                  "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                 
                var stopTask = (Task)stopServerMethod.Invoke(mcpService, null);

                Task.Run(async () =>
                {
                    try
                    {
                        await stopTask;

                         
                        InvokeOnUIThread(() =>
                        {
                            waabe_navi_mcp.Services.ServerStatusManager.SetServerStopped();


                            MessageBox.Show($"✅ MCP Server gestoppt.\n\nPort {currentPort} wurde gespeichert und ist jetzt wieder verfügbar.\n\nDer Server kann jederzeit neu gestartet werden.",
                                          "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Information);

                            LogHelper.LogEvent($"✅ MCP Server auf Port {currentPort} erfolgreich gestoppt");
                        });
                    }
                    catch (Exception ex)
                    {
                        LogHelper.LogEvent($"Async Server-Stop Fehler: {ex.Message}");

                        InvokeOnUIThread(() =>
                        {
                            MessageBox.Show($"Fehler beim Stoppen des Servers: {ex.Message}",
                                          "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        });
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Server-Stop: {ex.Message}");
                MessageBox.Show($"Fehler beim Stoppen: {ex.Message}", "WAABE MCP Server",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        /// <summary>
        /// Debug helper for logging MCP service settings state.
        /// - Invokes the "LogSettingsDebug" method on the service if available.
        /// </summary>
        private void LogSettingsDebug(object mcpService)
        {
            try
            {
                var debugMethod = mcpService.GetType().GetMethod("LogSettingsDebug");
                debugMethod?.Invoke(mcpService, null);
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Settings-Debug: {ex.Message}");
            }
        }

        /// <summary>
        /// Utility method for safely invoking actions on the UI thread.
        /// - Uses Form.ActiveForm.Invoke if required.
        /// - Falls back to direct execution if no UI thread is detected.
        /// </summary>
        private void InvokeOnUIThread(Action action)
        {
            try
            {
                // Prüfe, ob wir im UI-Thread sind
                if (Control.FromHandle(IntPtr.Zero) != null)
                {
                    // Verwende Control.Invoke falls verfügbar
                    var form = Form.ActiveForm;
                    if (form != null && form.InvokeRequired)
                    {
                        form.Invoke(action);
                    }
                    else
                    {
                        action();
                    }
                }
                else
                {
                    // Fallback: Direkt ausführen
                    action();
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim UI-Thread-Invoke: {ex.Message}");
                // Fallback: Direkt ausführen
                try
                {
                    action();
                }
                catch (Exception directEx)
                {
                    LogHelper.LogEvent($"Fehler beim direkten Action-Aufruf: {directEx.Message}");
                }
            }
        }

        /// <summary>
        /// Shows a dialog to configure the port for the MCP server.
        /// - Prefills with the current or saved port.
        /// - On success, saves the chosen port to settings.
        /// - Fallback: InputBox or default port if dialog fails.
        /// </summary>
        private int? ShowPortConfigDialog()
        {
            try
            {
                // Lade aktuellen Port aus Settings (falls verfügbar)
                var currentPort = GetCurrentPortFromSettings();

                var portDialog = new PortInputDialog(currentPort);
                var result = portDialog.ShowDialog();

                if (result == DialogResult.OK)
                {
                    SavePortToSettings(portDialog.SelectedPort);
                    return portDialog.SelectedPort;
                }

                return null;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Port-Dialog: {ex.Message}");

                // Fallback zu einfachem InputBox
                try
                {
                    var portText = Microsoft.VisualBasic.Interaction.InputBox(
                        "Bitte geben Sie den Port für den MCP Server ein (1024-65535):",
                        "MCP Server Port",
                        "8080");

                    if (int.TryParse(portText, out int port) && port >= 1024 && port <= 65535)
                    {
                        SavePortToSettings(port);
                        return port;
                    }
                }
                catch
                {
                    // Fallback wenn VisualBasic nicht verfügbar
                    MessageBox.Show("Port-Dialog nicht verfügbar. Verwende Standard-Port 8080.",
                                  "WAABE MCP Server", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    SavePortToSettings(8080);
                    return 8080;
                }

                return null;
            }
        }

        /// <summary>
        /// Retrieves the currently saved port from the service settings.
        /// - Falls back to default port 8080 if unavailable or errors occur.
        /// </summary>
        private int GetCurrentPortFromSettings()
        {
            try
            {
                // Verwende MCP Server Service für Settings-Zugriff
                var mcpService = ServiceRegistry.GetService("waabe_navi_mcp_server");
                if (mcpService != null)
                {
                    var getPortMethod = mcpService.GetType().GetMethod("GetPortFromSettings");
                    if (getPortMethod != null)
                    {
                        var port = (int)getPortMethod.Invoke(mcpService, null);
                        LogHelper.LogEvent($"Port aus Settings über Service geladen: {port}");
                        return port;
                    }
                }

                // Fallback: Standard-Port
                LogHelper.LogEvent("Settings-Service nicht verfügbar, verwende Standard-Port 8080");
                return 8080;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Laden des aktuellen Ports: {ex.Message}");
                return 8080;
            }
        }

        /// <summary>
        /// Persists the selected port to the MCP server settings.
        /// - Uses reflection on the server service to call SavePortToSettings.
        /// - Logs debug output if available.
        /// </summary>
        private void SavePortToSettings(int port)
        {
            try
            {
                // Verwende MCP Server Service für Settings-Zugriff
                var mcpService = ServiceRegistry.GetService("waabe_navi_mcp_server");
                if (mcpService != null)
                {
                    var savePortMethod = mcpService.GetType().GetMethod("SavePortToSettings");
                    if (savePortMethod != null)
                    {
                        savePortMethod.Invoke(mcpService, new object[] { port });
                        LogHelper.LogEvent($"Port {port} über Service in Settings gespeichert");

                        // Debug: Settings-Status ausgeben
                        LogSettingsDebug(mcpService);
                        return;
                    }
                }

                LogHelper.LogEvent($"❌ Settings-Service nicht verfügbar - Port {port} konnte nicht gespeichert werden");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Speichern des Ports: {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Simple Windows Forms dialog for entering a port number.
    /// - Provides validation (numeric only, range 1024–65535).
    /// - Displays additional settings info if available from the service.
    /// - Used by MCPServerButtonHandler as the primary UI for port configuration.
    /// </summary> 
    public class PortInputDialog : Form
    {
        public int SelectedPort { get; private set; }

        private TextBox portTextBox;
        private Label validationLabel;
        private Label settingsInfoLabel;

        /// <summary>
        /// Constructor that initializes the dialog with the current port.
        /// </summary>
        public PortInputDialog(int currentPort = 8080)
        {
            InitializeComponent();
            portTextBox.Text = currentPort.ToString();
            SelectedPort = currentPort;

            ShowSettingsInfo();
        }

        /// <summary>
        /// Builds and configures the Windows Forms controls for the dialog.
        /// </summary>
        private void InitializeComponent()
        {
            this.Text = "MCP Server Port Konfiguration";
            this.Size = new System.Drawing.Size(400, 200);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;

            var lblPrompt = new Label
            {
                Text = "Bitte geben Sie den Port für den MCP Server ein:",
                Location = new System.Drawing.Point(20, 20),
                Size = new System.Drawing.Size(350, 20)
            };

            var lblPort = new Label
            {
                Text = "Port:",
                Location = new System.Drawing.Point(20, 50),
                Size = new System.Drawing.Size(40, 20)
            };

            portTextBox = new TextBox
            {
                Location = new System.Drawing.Point(70, 50),
                Size = new System.Drawing.Size(100, 20)
            };
            portTextBox.KeyPress += PortTextBox_KeyPress;

            var lblRange = new Label
            {
                Text = "(1024-65535)",
                Location = new System.Drawing.Point(180, 50),
                Size = new System.Drawing.Size(100, 20),
                ForeColor = System.Drawing.Color.Gray
            };

            validationLabel = new Label
            {
                Location = new System.Drawing.Point(20, 80),
                Size = new System.Drawing.Size(350, 20),
                ForeColor = System.Drawing.Color.Red,
                Visible = false
            };

            settingsInfoLabel = new Label
            {
                Location = new System.Drawing.Point(20, 110),
                Size = new System.Drawing.Size(400, 60),
                ForeColor = System.Drawing.Color.Blue,
                Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular),
                Text = "Lade Settings-Information..."
            };

            var btnOK = new Button
            {
                Text = "OK",
                Location = new System.Drawing.Point(220, 120),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.OK
            };
            btnOK.Click += BtnOK_Click;

            var btnCancel = new Button
            {
                Text = "Abbrechen",
                Location = new System.Drawing.Point(300, 120),
                Size = new System.Drawing.Size(75, 23),
                DialogResult = DialogResult.Cancel
            };

            this.Controls.AddRange(new Control[] { lblPrompt, lblPort, portTextBox, lblRange, validationLabel, btnOK, btnCancel });
        }

        /// <summary>
        /// Loads settings information from the service and displays it in the dialog.
        /// - Shows the saved port and settings file path if available.
        /// </summary>
        private void ShowSettingsInfo()
        {
            try
            {
                var mcpService = waabe_navi_shared.ServiceRegistry.GetService("waabe_navi_mcp_server");
                if (mcpService != null)
                {
                    var getPortMethod = mcpService.GetType().GetMethod("GetPortFromSettings");
                    var getPathMethod = mcpService.GetType().GetMethod("GetSettingsFilePath");

                    if (getPortMethod != null && getPathMethod != null)
                    {
                        var savedPort = (int)getPortMethod.Invoke(mcpService, null);
                        var settingsPath = getPathMethod.Invoke(mcpService, null)?.ToString() ?? "Unbekannt";

                        settingsInfoLabel.Text = $"Gespeicherter Port: {savedPort}\nSettings-Datei: {System.IO.Path.GetFileName(settingsPath)}";

                        // Debug-Ausgabe
                        var debugMethod = mcpService.GetType().GetMethod("LogSettingsDebug");
                        debugMethod?.Invoke(mcpService, null);
                    }
                    else
                    {
                        settingsInfoLabel.Text = "Settings-Service verfügbar, aber Methoden nicht gefunden.";
                    }
                }
                else
                {
                    settingsInfoLabel.Text = "Settings-Service nicht verfügbar.";
                }
            }
            catch (Exception ex)
            {
                settingsInfoLabel.Text = $"Fehler beim Laden der Settings: {ex.Message}";
            }
        }

        /// <summary>
        /// Ensures only numeric input is allowed in the port textbox.
        /// </summary>
        private void PortTextBox_KeyPress(object sender, KeyPressEventArgs e)
        {
            // Nur Zahlen und Backspace erlauben
            if (!char.IsDigit(e.KeyChar) && e.KeyChar != (char)Keys.Back)
            {
                e.Handled = true;
            }
        }

        /// <summary>
        /// Handles the OK button click.
        /// - Validates the entered port and closes the dialog if valid.
        /// </summary>
        private void BtnOK_Click(object sender, EventArgs e)
        {
            if (ValidatePort())
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
        }


        /// <summary>
        /// (Not used) Attempts to save the port directly via the service.
        /// - Left as a fallback method but not actively invoked.
        /// </summary>
        private void SavePortViaService()
        {
            try
            {
                var mcpService = waabe_navi_shared.ServiceRegistry.GetService("waabe_navi_mcp_server");
                if (mcpService != null)
                {
                    var savePortMethod = mcpService.GetType().GetMethod("SavePortToSettings");
                    savePortMethod?.Invoke(mcpService, new object[] { SelectedPort });

                    waabe_navi_shared.LogHelper.LogEvent($"Port {SelectedPort} über Dialog-Service gespeichert");
                }
            }
            catch (Exception ex)
            {
                waabe_navi_shared.LogHelper.LogEvent($"Fehler beim Speichern über Dialog-Service: {ex.Message}");
            }
        }

        /// <summary>
        /// Validates the entered port.
        /// - Must be numeric and within the allowed range (1024–65535).
        /// - Updates the SelectedPort property on success.
        /// </summary>
        private bool ValidatePort()
        {
            validationLabel.Visible = false;

            if (string.IsNullOrWhiteSpace(portTextBox.Text))
            {
                ShowValidationError("Port darf nicht leer sein.");
                return false;
            }

            if (!int.TryParse(portTextBox.Text, out int port))
            {
                ShowValidationError("Port muss eine gültige Zahl sein.");
                return false;
            }

            if (port < 1024 || port > 65535)
            {
                ShowValidationError("Port muss zwischen 1024 und 65535 liegen.");
                return false;
            }

            SelectedPort = port;
            return true;
        }

        /// <summary>
        /// Displays a validation error message within the dialog.
        /// </summary>
        private void ShowValidationError(string message)
        {
            validationLabel.Text = message;
            validationLabel.Visible = true;
        }
    }
}