using System;
using System.Diagnostics;
using System.Reflection;
using static Windows_Spotlight.Constants.Paths;

namespace Windows_Spotlight
{
    internal static class ProgramHelpers
    {
        internal static void OpenFolder()
        {
            Console.WriteLine("Opening {0}", DESTINATION);
            Process.Start(new ProcessStartInfo(DESTINATION) { UseShellExecute = true });
        }

        internal static string GetVersion()
        {
            return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        }

        internal static void Help()
        {
            Console.WriteLine("Windows Spotlight v{0}\n", GetVersion());
            Console.WriteLine("Usage: Windows-Spotlight.exe [--source lockscreen|desktop|all] [--open-folder]\n");
            Console.WriteLine("  --source           Image source; defaults to lockscreen.");
            Console.WriteLine("  -O, --open-folder  Open the output folder after exporting.");
            Console.WriteLine("  -V, --version      Report the tool version.");
            Console.WriteLine("  -h, --help         Show this help.\n");
            Console.WriteLine("Project URL: https://github.com/krishna-santosh/Windows_Spotlight");
        }
    }
}
