using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;

using static Windows_Spotlight.Constants.Paths;

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
                    switch (Orientation(file))
                    {
                        case 16m / 9m:
                            File.Copy(file, LANDSCAPE + fileName + ".jpg", false);
                            imageCount++;
                            break;

                        case 9m / 16m:
                            File.Copy(file, PORTRAIT + fileName + ".jpg", false);
                            imageCount++;
                            break;
                    }
                }
                catch (IOException)
                {

                }
            }
        }

        internal static void OpenFolder() 
        {
            System.Console.WriteLine($"Opening {DESTINATION}\n");
            Process.Start(DESTINATION);
        }
        
        internal static string GetVersion()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var fvi = FileVersionInfo.GetVersionInfo(assembly.Location);
            var version = fvi.FileVersion;

            return version;
        }

        internal static void Help()
        {
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\nWindows Spotlight v{GetVersion()}\n");
            Console.ResetColor();
            Console.WriteLine("Usage:\n");
            Console.WriteLine("\tWindows-Spotlight.exe <option>\n");
            Console.WriteLine("options:\n\t-O, --open-folder\tTo Open the output folder.");
            Console.WriteLine("\t-V, --version\t\tReports the version of this tool.");
            Console.WriteLine("\t-h, --help\t\tHelp Menu.\n");
            Console.WriteLine("\nProject URL: https://github.com/krishna-santosh/Windows_Spotlight\n");
        }
    }
}
