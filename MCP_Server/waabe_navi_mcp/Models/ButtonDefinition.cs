namespace waabe_navi_mcp.Models
{
    /// <summary>
    /// Data model representing a Ribbon button definition.
    /// - Typically created by ButtonsXmlLoader when parsing the Buttons.xml file.
    /// - Provides all metadata required to render a button in the Navisworks Ribbon UI.
    /// </summary>
    public class ButtonDefinition
    {
        /// <summary>
        /// Unique identifier of the button.
        /// - Used internally to map button actions (e.g., to command handlers).
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// Display text shown on the Ribbon button.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Description or tooltip text shown when hovering over the button.
        /// </summary>
        public string Description { get; set; }

        /// <summary>
        /// Path to the large image icon (typically 32x32).
        /// - Used when the button is displayed in large mode on the Ribbon.
        /// </summary>
        public string LargeImage { get; set; }

        /// <summary>
        /// Path to the small image icon (typically 16x16).
        /// - Used when the button is displayed in compact mode.
        /// </summary>
        public string SmallImage { get; set; }

        /// <summary>
        /// The Ribbon panel to which this button belongs.
        /// - Used for grouping buttons in the UI.
        /// </summary>
        public string Panel { get; set; }
    }
}
