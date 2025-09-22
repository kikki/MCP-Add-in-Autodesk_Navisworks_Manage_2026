using System;
using System.Windows.Input;
using waabe_navi_shared;

namespace waabe_navi_mcp.Commands
{
    /// <summary>
    /// Command handler for the MCP button in the Navisworks Add-in.
    /// - Implements the ICommand interface so it can be bound to UI elements (Ribbon buttons).
    /// - Displays a message box when executed and writes a log entry.
    /// </summary>
    public class MCPButtonHandler : ICommand
    {
        /// <summary>
        /// Required by the ICommand interface.
        /// - This event is triggered whenever the CanExecute state changes.
        /// - Not actively used here, since CanExecute always returns true.
        /// </summary>
        public event EventHandler CanExecuteChanged { add { } remove { } }

        /// <summary>
        /// Determines whether the command can be executed.
        /// - Always returns true in this implementation, so the button is always enabled.
        /// - Called by the WPF/WinForms command infrastructure before executing.
        /// </summary>
        public bool CanExecute(object parameter) => true;

        /// <summary>
        /// Executes the command when the MCP button is clicked.
        /// - Shows a message box to the user.
        /// - Logs the click event using LogHelper.
        /// - Invoked automatically by the ICommand framework when the bound button is pressed.
        /// </summary>
        public void Execute(object parameter)
        {
            System.Windows.Forms.MessageBox.Show(
                "MCP – Navisworks Manage 2026\n\n" +
                "This application was created in September 2025.\n\n" +
                "Purpose: Effort estimation and implementation of an MCP interface " +
                "in an environment that was hardly documented until then.\n\n" +
                "Only the Navisworks 2026 version was considered, " +
                "other versions were not taken into account.\n\n" +
                "The project is published on \n\n" +
                " github.com/kikki/MCP-Add-in-Autodesk_Navisworks_Manage_2026 \n\n" +
                "@n.rube@waabe.de"
            );
            LogHelper.LogEvent("MCP-Button wurde geklickt.");
        }
    }
}
