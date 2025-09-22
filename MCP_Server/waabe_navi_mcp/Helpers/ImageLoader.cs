using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace waabe_navi_mcp.Helpers
{
    /// <summary>
    /// Utility class for loading images from the Ribbon/Icons directory of the bundle.
    /// - Provides ImageSource objects for use in Ribbon buttons and other UI elements.
    /// </summary>
    public static class ImageLoader
    {
        /// <summary>
        /// Loads an image from the "Ribbon/Icons" folder of the Navisworks Add-in bundle.
        /// - Input: fileName (e.g., "myIcon.png").
        /// - Returns an ImageSource that can be bound to WPF controls.
        /// - If the file is missing or loading fails, returns null.
        /// - Typically used during Ribbon UI initialization to load button icons.
        /// </summary>
        public static ImageSource LoadImage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return null;

            var dllPath = System.Reflection.Assembly.GetExecutingAssembly().Location;
            var versionDir = Path.GetDirectoryName(dllPath);
            var bundleDir = Directory.GetParent(versionDir).Parent.FullName;

            var imgPath = Path.Combine(bundleDir, "Ribbon", "Icons", fileName);
            if (!File.Exists(imgPath))
                return null;

            try
            {
                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new System.Uri(imgPath, System.UriKind.Absolute);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();
                return bitmap;
            }
            catch
            {
                return null;
            }
        }
    }
}
