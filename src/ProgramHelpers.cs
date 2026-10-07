using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using static Windows_Spotlight.Constants.Paths;

namespace Windows_Spotlight
{
    internal static class ProgramHelpers
    {
        internal static void OpenFolder()
        {
            Console.WriteLine("Opening output folder: {0}", DESTINATION);
            Process.Start(new ProcessStartInfo(DESTINATION) { UseShellExecute = true });
        }

        internal static string GetVersion()
        {
            return FileVersionInfo.GetVersionInfo(Assembly.GetExecutingAssembly().Location).FileVersion;
        }

        internal static void Help()
        {
            Console.WriteLine("Windows Spotlight v{0}\n", GetVersion());
            Console.WriteLine("Save cached Windows Spotlight images to your Pictures folder.\n");
            Console.WriteLine("Usage:");
            Console.WriteLine("  Windows-Spotlight.exe [--source lockscreen|desktop|all] [--open-folder]");
            Console.WriteLine("  Windows-Spotlight.exe --startup enable [--source lockscreen|desktop|all]");
            Console.WriteLine("  Windows-Spotlight.exe --startup disable|status\n");
            Console.WriteLine("Options:");
            Console.WriteLine("  --source <source>  lockscreen, desktop, or all.");
            Console.WriteLine("                     Defaults: lockscreen for manual runs; all for startup.");
            Console.WriteLine("  --startup enable   Enable automatic exports for the current user.");
            Console.WriteLine("  --startup disable  Remove the startup task; keep saved images.");
            Console.WriteLine("  --startup status   Show configuration and the last background run.");
            Console.WriteLine("  -O, --open-folder  Open the output folder after a manual export.");
            Console.WriteLine("  -V, --version      Show the application version.");
            Console.WriteLine("  -h, --help         Show this help.\n");
            Console.WriteLine("Examples:");
            Console.WriteLine("  Windows-Spotlight.exe --source all --open-folder");
            Console.WriteLine("  Windows-Spotlight.exe --startup enable");
            Console.WriteLine("  Windows-Spotlight.exe --startup enable --source desktop");
            Console.WriteLine("  Windows-Spotlight.exe --startup status\n");
            Console.WriteLine("Startup is opt-in. Exports run without a window one minute after sign-in,");
            Console.WriteLine("then hourly while you are signed in. No administrator privileges required.");
            Console.WriteLine("After upgrading, run --startup enable again to refresh the background app.\n");
            Console.WriteLine("Output: {0}", DESTINATION);
            Console.WriteLine("Background log: {0}", StartupManager.LogPath);
            Console.WriteLine("Existing images are skipped. Windows must cache images before they can be saved.");
            Console.WriteLine("This tool does not download images or enable Windows Spotlight.");
            Console.WriteLine("Exit codes: 0 = success (including no new images); 1 = error.\n");
            Console.WriteLine("Project URL: https://github.com/krishna-santosh/Windows_Spotlight");
        }

        internal static string SourceLabel(ImageSource source)
        {
            return source == ImageSource.All ? "Lock screen and desktop" : source == ImageSource.Desktop ? "Desktop" : "Lock screen";
        }

        internal static void WriteExportResult(ExportResult result, TextWriter output, TextWriter errors)
        {
            output.WriteLine(result.Saved == 0 ? (result.Failed > 0 ? "No images were saved." : "No new images found.")
                : string.Format("Saved {0} new {1}.", result.Saved, result.Saved == 1 ? "image" : "images"));
            if (result.Duplicates > 0)
                output.WriteLine("Skipped {0} {1} already saved.", result.Duplicates, result.Duplicates == 1 ? "image" : "images");
            if (result.Failed > 0)
                errors.WriteLine("Error: Could not save {0} {1}. See the errors above and try again.", result.Failed, result.Failed == 1 ? "image" : "images");
            if (result.Saved == 0 && result.Duplicates == 0 && result.Failed == 0)
                output.WriteLine("No eligible cached images were found. Enable Spotlight in Windows Settings and try again after Windows has cached images.");
        }
    }
}
