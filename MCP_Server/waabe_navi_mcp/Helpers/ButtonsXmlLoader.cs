using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using waabe_navi_mcp.Models;

namespace waabe_navi_mcp.Helpers
{
    public static class ButtonsXmlLoader
    {
        /// <summary>
        /// Loads all button definitions from the Ribbon/Buttons.xml file.
        /// - Resolves the path based on the current assembly's location inside the Navisworks bundle.
        /// - If the file does not exist, returns an empty list.
        /// - Returns a list of ButtonDefinition objects that can be used to construct ribbon buttons.
        /// - Typically invoked during Add-in startup or Ribbon initialization.
        /// </summary>
        public static List<ButtonDefinition> LoadButtonDefinitions()
        {
            var dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var versionDir = Path.GetDirectoryName(dllPath);
            var bundleDir = Directory.GetParent(versionDir).Parent.FullName;

            var xmlPath = Path.Combine(bundleDir, "Ribbon", "Buttons.xml");
            if (!File.Exists(xmlPath))
                return new List<ButtonDefinition>();

            var xdoc = XDocument.Load(xmlPath);
            return xdoc.Root.Elements("Button").Select(b => new ButtonDefinition
            {
                Id = (string)b.Element("Id"),
                Text = (string)b.Element("Text"),
                Description = (string)b.Element("Description"),
                LargeImage = (string)b.Element("LargeImage"),
                SmallImage = (string)b.Element("SmallImage"),
                Panel = (string)b.Element("Panel")
            }).ToList();
        }
    }
}
