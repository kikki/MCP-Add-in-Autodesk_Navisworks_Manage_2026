using Autodesk.Windows;
using System;
using System.IO;
using System.Linq;
using System.Windows.Media.Imaging;
using System.Xml.Linq;
using waabe_navi_mcp.Commands;
using waabe_navi_shared;

namespace waabe_navi_mcp.Ribbon
{
    /// <summary>
    /// Utility class for dynamically creating Ribbon buttons from an external XML configuration (Buttons.xml).
    /// - Reads button definitions (text, id, icons, handler, panel).
    /// - Ensures that the Ribbon tab contains the required panels.
    /// - Creates RibbonButton instances and binds them to ICommand handlers via ButtonHandlerFactory.
    /// </summary>
    public class ButtonXmlLoader
    {
        // Path to the Buttons.xml file relative to the Add-in base directory
        private readonly string _xmlPath = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, @"..\..\Ribbon\Buttons.xml");

        /// <summary>
        /// Reads the Buttons.xml file and adds buttons to the given RibbonTab.
        /// - If the file is missing, logs an event and does nothing.
        /// - Creates panels if they do not already exist.
        /// - For each <Button> element, creates a RibbonButton with text, id, icons, and handler.
        /// </summary>
        /// <param name="tab">The RibbonTab to which buttons should be added.</param>
        public void AddButtonsFromXml(RibbonTab tab)
        {
            if (!File.Exists(_xmlPath))
            {
                LogHelper.LogEvent($"Buttons.xml not found: {_xmlPath}");
                return;
            }

            var xml = XDocument.Load(_xmlPath);

            foreach (var buttonElem in xml.Descendants("Button"))
            {
                // Panel handling
                string panelTitle = buttonElem.Attribute("Panel")?.Value ?? "MCP";
                var panel = tab.Panels.FirstOrDefault(p => p.Source.Title == panelTitle);

                if (panel == null)
                {
                    var panelSource = new RibbonPanelSource { Title = panelTitle };
                    panel = new RibbonPanel { Source = panelSource };
                    tab.Panels.Add(panel);
                }

                // Button creation
                var btn = new RibbonButton
                {
                    Text = buttonElem.Attribute("Text")?.Value ?? "Button",
                    ShowText = true,
                    Id = buttonElem.Attribute("Id")?.Value,
                    Size = RibbonItemSize.Large,
                    LargeImage = LoadPngImage(buttonElem.Attribute("Icon32")?.Value),
                    Image = LoadPngImage(buttonElem.Attribute("Icon16")?.Value),
                };

                // Assign handler (via ButtonHandlerFactory)
                string handler = buttonElem.Attribute("Handler")?.Value ?? "MCPButtonHandler";
                btn.CommandHandler = ButtonHandlerFactory.GetHandler(handler);

                panel.Source.Items.Add(btn);
            }
        }

        /// <summary>
        /// Loads a PNG image from a relative path and returns it as a BitmapImage.
        /// - Returns null if the path is empty or the file does not exist.
        /// - Used to provide 16x16 or 32x32 icons for Ribbon buttons.
        /// </summary>
        /// <param name="relPath">Relative path to the icon file.</param>
        /// <returns>BitmapImage if successfully loaded, otherwise null.</returns>
        private BitmapImage LoadPngImage(string relPath)
        {
            if (string.IsNullOrEmpty(relPath))
                return null;

            string absPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\", relPath);
            if (!File.Exists(absPath))
                return null;

            var bitmap = new BitmapImage();
            bitmap.BeginInit();
            bitmap.UriSource = new Uri(absPath, UriKind.Absolute);
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.EndInit();
            return bitmap;
        }
    }
}
