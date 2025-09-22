using Autodesk.Navisworks.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace waabe_navi_shared
{
    /// <summary>
    /// Provides logging functionality for WAABE Navisworks add-ins.
    /// - Writes structured log entries into a Markdown file (<c>waabe_navi_log.md</c>).
    /// - Automatically resolves the correct bundle path or falls back to AppData / Documents.
    /// - Supports different log levels (Info, Warning, Error, Success, Debug).
    /// - Adds startup and shutdown banners for projects.
    /// </summary>
    public static class LogHelper
    {
        private static readonly string LogPath = GetBundleLogPath();

        /// <summary>
        /// Resolves the bundle-specific path where the log file should be written.
        /// Falls back to AppData or Documents if the bundle path cannot be determined.
        /// </summary>
        private static string GetBundleLogPath()
        {
            try
            {
                 
                var dllPath = Assembly.GetExecutingAssembly().Location;
                var dllDir = Path.GetDirectoryName(dllPath);  
                var contentsDir = Path.GetDirectoryName(dllDir);  
                var bundleDir = Path.GetDirectoryName(contentsDir);  

                if (bundleDir != null && bundleDir.EndsWith(".bundle"))
                {
                    var logPath = Path.Combine(bundleDir, "waabe_navi_log.md");
                    return logPath;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler bei Bundle-Pfad-Ermittlung: {ex.Message}");
            }

             
            try
            {
                var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var bundlePath = Path.Combine(appData, "Autodesk", "ApplicationPlugins", "waabe_navi_mcp.bundle");

                if (Directory.Exists(bundlePath))
                {
                    return Path.Combine(bundlePath, "waabe_navi_log.md");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Fehler bei APPDATA-Fallback: {ex.Message}");
            }

             
            var documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return Path.Combine(documentsPath, "waabe_navi_log.md");
        }

        /// <summary>
        /// Logs a message with automatic project detection based on the calling assembly.
        /// </summary> 
        public static void LogEvent(string msg)
        {
            LogEvent(msg, GetCallingProject());
        }

        /// <summary>
        /// Logs a message with an explicit project name.
        /// </summary> 
        public static void LogEvent(string msg, string projectName)
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            var projectPrefix = string.IsNullOrEmpty(projectName) ? "" : $"[{projectName}] ";
            var line = $"| {timestamp} | {projectPrefix}{msg} |";

            try
            {
                 
                if (!File.Exists(LogPath))
                {
                    CreateLogHeader();
                }

                File.AppendAllLines(LogPath, new[] { line });

                 
                System.Diagnostics.Debug.WriteLine($"[WAABE-{projectName}] {timestamp}: {msg}");

            }
            catch (Exception ex)
            {
                HandleLogError(ex, timestamp, msg, projectName);
            }
        }

        /// <summary>
        /// Logs a message with a specified log level (Info, Warning, Error, Success, Debug).
        /// </summary> 
        public static void LogEvent(string msg, string projectName, LogLevel level)
        {
            string levelPrefix = "";
            switch (level)
            {
                case LogLevel.Info:
                    levelPrefix = "ℹ️";
                    break;
                case LogLevel.Warning:
                    levelPrefix = "⚠️";
                    break;
                case LogLevel.Error:
                    levelPrefix = "❌";
                    break;
                case LogLevel.Success:
                    levelPrefix = "✅";
                    break;
                case LogLevel.Debug:
                    levelPrefix = "🔍";
                    break;
                default:
                    levelPrefix = "";
                    break;
            }

            var formattedMsg = $"{levelPrefix} {msg}";
            LogEvent(formattedMsg, projectName);
        }

        /// <summary>
        /// Logs an informational message.
        /// </summary> 
        public static void LogInfo(string msg, string projectName = null)
            => LogEvent(msg, projectName ?? GetCallingProject(), LogLevel.Info);

        /// <summary>
        /// Logs a warning message.
        /// </summary>
        public static void LogWarning(string msg, string projectName = null)
            => LogEvent(msg, projectName ?? GetCallingProject(), LogLevel.Warning);

        /// <summary>
        /// Logs an error message.
        /// </summary>
        public static void LogError(string msg, string projectName = null)
            => LogEvent(msg, projectName ?? GetCallingProject(), LogLevel.Error);

        /// <summary>
        /// Logs a success message.
        /// </summary>
        public static void LogSuccess(string msg, string projectName = null)
            => LogEvent(msg, projectName ?? GetCallingProject(), LogLevel.Success);

        /// <summary>
        /// Logs a debug message.
        /// </summary>
        public static void LogDebug(string msg, string projectName = null)
            => LogEvent(msg, projectName ?? GetCallingProject(), LogLevel.Debug);

        /// <summary>
        /// Writes a startup banner for a project, including version and DLL path.
        /// </summary> 
        public static void LogProjectStartup(string projectName, string version = "1.0.0")
        {
            LogEvent("", "");  
            LogEvent($"=== {projectName.ToUpper()} GESTARTET ===", projectName, LogLevel.Success);
            LogEvent($"Version: {version}", projectName);
            LogEvent($"DLL-Pfad: {Assembly.GetCallingAssembly().Location}", projectName);
            LogEvent($"Gestartet um: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", projectName);
        }

        /// <summary>
        /// Writes a shutdown banner for a project.
        /// </summary> 
        public static void LogProjectShutdown(string projectName)
        {
            LogEvent($"=== {projectName.ToUpper()} BEENDET ===", projectName, LogLevel.Info);
            LogEvent($"Beendet um: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", projectName);
            LogEvent("", "");  
        }


        /// <summary>
        /// Gets the default project name from the calling assembly (e.g., MCP, CHAT, SERVER).
        /// </summary>
        private static string GetCallingProject()
        {
            try
            {
                var callingAssembly = Assembly.GetCallingAssembly();
                var assemblyName = callingAssembly.GetName().Name;

                 
                switch (assemblyName)
                {
                    case "waabe_navi_mcp":
                        return "MCP";
                    case "waabe_navi_chatfenster":
                        return "CHAT";
                    case "waabe_navi_mcpserver":
                        return "SERVER";
                    case "waabe_navi_shared":
                        return "SHARED";
                    default:
                        return assemblyName ?? "UNKNOWN";
                }
            }
            catch
            {
                return "UNKNOWN";
            }
        }

        /// <summary>
        /// Creates the initial Markdown log header if the log file does not exist.
        /// </summary>
        private static void CreateLogHeader()
        {
            var header = new[]
            {
                "# WAABE Navisworks AddIn Log",
                $"**Erstellt:** {DateTime.Now:yyyy-MM-dd HH:mm:ss}",
                $"**LogPath:** {LogPath}",
                $"**Shared-DLL:** {Assembly.GetExecutingAssembly().Location}",
                "",
                "## Projekt-Abkürzungen:",
                "- **[MCP]** = waabe_navi_mcp (Hauptprojekt)",
                "- **[CHAT]** = waabe_navi_chatfenster",
                "- **[SERVER]** = waabe_navi_mcpserver",
                "- **[SHARED]** = waabe_navi_shared",
                "",
                "| Zeitstempel | Nachricht |",
                "|-------------|-----------|"
            };
            File.WriteAllLines(LogPath, header);
        }

        /// <summary>
        /// Handles logging errors by writing to the Debug console and optionally a fallback file.
        /// </summary>
        private static void HandleLogError(Exception ex, string timestamp, string msg, string projectName)
        {
             
            System.Diagnostics.Debug.WriteLine($"[WAABE-ERROR] LogHelper Fehler: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"[WAABE-ERROR] Versuchter Pfad: {LogPath}");
            System.Diagnostics.Debug.WriteLine($"[WAABE-FALLBACK] {timestamp}: [{projectName}] {msg}");

             
            try
            {
                var tempLog = Path.Combine(Path.GetTempPath(), "waabe_navi_fallback_log.md");
                var fallbackLine = $"| {timestamp} | [{projectName}] {msg} |\n";
                File.AppendAllText(tempLog, fallbackLine);
            }
            catch
            {
                 
            }
        }


        /// <summary>
        /// Returns the current log file path being used.
        /// </summary>
        public static string GetCurrentLogPath()
        {
            return LogPath;
        }

        /// <summary>
        /// Logs a system-wide startup event, including Navisworks version if available.
        /// </summary>
        public static void LogStartup()
        {
            LogProjectStartup("WAABE-SYSTEM");

            try
            {
                LogEvent($"Navisworks Version: {Autodesk.Navisworks.Api.Application.Version}", "SYSTEM");
            }
            catch
            {
                LogEvent("Navisworks Version: Nicht verfügbar", "SYSTEM");
            }
        }

        /// <summary>
        /// Logs detailed debug information about the bundle and log file paths.
        /// </summary>
        public static void LogPathDebug()
        {
            try
            {
                var dllPath = Assembly.GetExecutingAssembly().Location;
                LogDebug($"DLL Pfad: {dllPath}");

                var dllDir = Path.GetDirectoryName(dllPath);
                LogDebug($"DLL Dir: {dllDir}");

                var contentsDir = Path.GetDirectoryName(dllDir);
                LogDebug($"Contents Dir: {contentsDir}");

                var bundleDir = Path.GetDirectoryName(contentsDir);
                LogDebug($"Bundle Dir: {bundleDir}");

                LogDebug($"Finaler Log-Pfad: {LogPath}");
                LogDebug($"Bundle-Dir existiert: {Directory.Exists(bundleDir)}");
            }
            catch (Exception ex)
            {
                LogError($"DEBUG FEHLER: {ex.Message}");
            }
        }

        


    }

    /// <summary>
    /// Defines the severity levels used in <see cref="LogHelper"/>.
    /// </summary> 
    public enum LogLevel
    {
        Info,
        Warning,
        Error,
        Success,
        Debug
    }


}