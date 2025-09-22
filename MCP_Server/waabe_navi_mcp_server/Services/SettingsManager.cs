using System;
using System.IO;
using System.Xml;
using waabe_navi_shared;

namespace waabe_navi_mcp_server.Services
{
    /// <summary>
    /// Datei/File: Services/SettingsManager.cs | Klasse/Class: SettingsManager
    ///   Manages server settings (e.g. port, enabled state).
    ///     Stores and loads configuration from an XML file.
    /// </summary>
    public static class SettingsManager
    {
        private static readonly string SettingsPath = GetSettingsPath();

        /// <summary>
        /// Resolves the path to the settings XML file.
        /// Creates the directory and a default file if they do not exist.
        /// </summary>
        private static string GetSettingsPath()
        {
            try
            {
                 
                var dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var dllDir = Path.GetDirectoryName(dllPath);  
                var contentsDir = Path.GetDirectoryName(dllDir);  
                var bundleDir = Path.GetDirectoryName(contentsDir);  
                var settingsDir = Path.Combine(bundleDir, "Settings");
                var settingsFile = Path.Combine(settingsDir, "waabe_settings.xml");

                 
                if (!Directory.Exists(settingsDir))
                {
                    Directory.CreateDirectory(settingsDir);
                    LogHelper.LogEvent($"Settings-Verzeichnis erstellt: {settingsDir}");
                }

                 
                if (!File.Exists(settingsFile))
                {
                    CreateDefaultSettings(settingsFile);
                }

                return settingsFile;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Settings-Pfad: {ex.Message}");
                 
                return Path.Combine(Path.GetTempPath(), "waabe_settings.xml");
            }
        }

        /// <summary>
        /// Creates a default settings XML file with initial values
        /// (port=8080, enabled=false, version, created date).
        /// </summary>
        private static void CreateDefaultSettings(string filePath)
        {
            try
            {
                var defaultXml = @"<?xml version=""1.0"" encoding=""utf-8""?>
                                    <WaabeSettings>
                                      <MCPServer>
                                        <Port>8080</Port>
                                        <LastUsed>" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + @"</LastUsed>
                                        <IsEnabled>false</IsEnabled>
                                      </MCPServer>
                                      <General>
                                        <Version>1.0.0</Version>
                                        <Created>" + DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss") + @"</Created>
                                      </General>
                                    </WaabeSettings>";

                File.WriteAllText(filePath, defaultXml);
                LogHelper.LogEvent($"Standard-Settings erstellt: {filePath}");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Erstellen der Standard-Settings: {ex.Message}");
            }
        }

        /// <summary>
        /// Loads the configured MCP server port from the settings file.
        /// Returns 8080 if the file or value is missing or invalid.
        /// </summary>
        public static int GetMCPPort()
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    LogHelper.LogEvent("Settings-Datei nicht gefunden, verwende Standard-Port 8080");
                    return 8080;
                }

                var doc = new XmlDocument();
                doc.Load(SettingsPath);

                var portNode = doc.SelectSingleNode("//MCPServer/Port");
                if (portNode != null && int.TryParse(portNode.InnerText, out int port))
                {
                    LogHelper.LogEvent($"Port aus Settings geladen: {port}");
                    return port;
                }

                LogHelper.LogEvent("Port-Node nicht gefunden, verwende Standard-Port 8080");
                return 8080;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Laden des Ports: {ex.Message}");
                return 8080;
            }
        }

        /// <summary>
        /// Saves the given MCP server port into the settings file.
        /// Creates the file if necessary and updates the "LastUsed" timestamp.
        /// </summary>
        public static void SaveMCPPort(int port)
        {
            try
            {
                XmlDocument doc;

                if (File.Exists(SettingsPath))
                {
                    doc = new XmlDocument();
                    doc.Load(SettingsPath);
                }
                else
                {
                    CreateDefaultSettings(SettingsPath);
                    doc = new XmlDocument();
                    doc.Load(SettingsPath);
                }

                var portNode = doc.SelectSingleNode("//MCPServer/Port");
                if (portNode != null)
                {
                    portNode.InnerText = port.ToString();
                }
                else
                {
                     
                    var mcpServerNode = doc.SelectSingleNode("//MCPServer");
                    if (mcpServerNode == null)
                    {
                        var rootNode = doc.DocumentElement;
                        mcpServerNode = doc.CreateElement("MCPServer");
                        rootNode.AppendChild(mcpServerNode);
                    }

                    var newPortNode = doc.CreateElement("Port");
                    newPortNode.InnerText = port.ToString();
                    mcpServerNode.AppendChild(newPortNode);
                }

                 
                var lastUsedNode = doc.SelectSingleNode("//MCPServer/LastUsed");
                if (lastUsedNode != null)
                {
                    lastUsedNode.InnerText = DateTime.Now.ToString("yyyy-MM-ddTHH:mm:ss");
                }

                doc.Save(SettingsPath);
                LogHelper.LogEvent($"Port {port} in Settings gespeichert: {SettingsPath}");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Speichern des Ports: {ex.Message}");
            }
        }

        /// <summary>
        /// Reads whether the MCP server is marked as enabled in the settings file.
        /// Returns false if the file or value is missing or invalid.
        /// </summary>
        public static bool GetMCPServerEnabled()
        {
            try
            {
                if (!File.Exists(SettingsPath)) return false;

                var doc = new XmlDocument();
                doc.Load(SettingsPath);

                var enabledNode = doc.SelectSingleNode("//MCPServer/IsEnabled");
                if (enabledNode != null && bool.TryParse(enabledNode.InnerText, out bool enabled))
                {
                    return enabled;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Laden des Server-Status: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Persists the enabled/disabled state of the MCP server in the settings file.
        /// Creates the file if it does not exist.
        /// </summary>
        public static void SaveMCPServerEnabled(bool enabled)
        {
            try
            {
                if (!File.Exists(SettingsPath))
                {
                    CreateDefaultSettings(SettingsPath);
                }

                var doc = new XmlDocument();
                doc.Load(SettingsPath);

                var enabledNode = doc.SelectSingleNode("//MCPServer/IsEnabled");
                if (enabledNode != null)
                {
                    enabledNode.InnerText = enabled.ToString().ToLower();
                }

                doc.Save(SettingsPath);
                LogHelper.LogEvent($"Server-Status {enabled} gespeichert");
            }
            catch (Exception ex)
            {
                LogHelper.LogEvent($"Fehler beim Speichern des Server-Status: {ex.Message}");
            }
        }

        /// <summary>
        /// Returns the absolute file path of the settings XML.
        /// </summary>
        public static string GetSettingsFilePath()
        {
            return SettingsPath;
        }
    }
}