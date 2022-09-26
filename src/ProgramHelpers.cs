using System.IO;
using System.Drawing;
using static Windows_Spotlight.Constants.Paths;
using System.Diagnostics;

namespace Windows_Spotlight
{
    internal static class ProgramHelpers
    {

        private static int imageCount = 0;

        internal static int GetImageCount() => imageCount;

        internal static void CreateDirectories()
        {
            Directory.CreateDirectory(DESTINATION);
            Directory.CreateDirectory(PORTRAIT);
            Directory.CreateDirectory(LANDSCAPE);
        }

        private static decimal Orientation(string file)
        {
            Image img = Image.FromFile(file);
            decimal width = img.Width;
            decimal height = img.Height;

            return width / height;
        }

        internal static void CopyFiles()
        {
            foreach (string file in Directory.GetFiles(SOURCE))
            {
                string fileName = Path.GetFileName(file);

                try
                {
                    if (Orientation(file) == 16m / 9m)
                    {
                        File.Copy(file, LANDSCAPE + fileName + ".jpg", false);
                        imageCount++;
                    }

                    if (Orientation(file) == 9m / 16m)
                    {
                        File.Copy(file, PORTRAIT + fileName + ".jpg", false);
                        imageCount++;
                    }
                }
                catch (IOException)
                {

                }
            }
        }

        internal static void OpenFolder() => Process.Start(DESTINATION);
    
    }
}
