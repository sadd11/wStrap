using System.Runtime.InteropServices;

namespace Bloxstrap.Utility
{
    /// <summary>
    /// Captures the Roblox window content into a PNG file, occluded windows included.
    /// </summary>
    public static class GameScreenshot
    {
        private const uint PW_RENDERFULLCONTENT = 2;

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [DllImport("user32.dll")]
        private static extern bool GetWindowRect(IntPtr hwnd, out RECT rect);

        [DllImport("user32.dll")]
        private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

        /// <summary>
        /// Captures the game window. Returns the saved file path, or null when capture failed.
        /// When <paramref name="saveToFile"/> is false the screenshot is not written to disk;
        /// the caller must copy <paramref name="result"/> to the clipboard itself.
        /// </summary>
        public static (string? Path, System.Drawing.Bitmap? Bitmap) Capture(IntPtr hwnd, bool saveToFile = true)
        {
            const string LOG_IDENT = "GameScreenshot::Capture";

            if (hwnd == IntPtr.Zero || !GetWindowRect(hwnd, out RECT rect))
                return (null, null);

            int width = rect.Right - rect.Left;
            int height = rect.Bottom - rect.Top;

            if (width <= 0 || height <= 0)
                return (null, null);

            using var bmp = new System.Drawing.Bitmap(width, height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (var graphics = System.Drawing.Graphics.FromImage(bmp))
            {
                IntPtr hdc = graphics.GetHdc();

                try
                {
                    if (!PrintWindow(hwnd, hdc, PW_RENDERFULLCONTENT))
                    {
                        App.Logger.WriteLine(LOG_IDENT, "PrintWindow failed");
                        return (null, null);
                    }
                }
                finally
                {
                    graphics.ReleaseHdc(hdc);
                }
            }

            if (!saveToFile)
                return (null, new System.Drawing.Bitmap(bmp));

            string directory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "wStrap");
            Directory.CreateDirectory(directory);

            string path = Path.Combine(directory, $"wStrap_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
            bmp.Save(path, System.Drawing.Imaging.ImageFormat.Png);

            App.Logger.WriteLine(LOG_IDENT, $"Screenshot saved to {path}");

            return (path, null);
        }
    }
}
