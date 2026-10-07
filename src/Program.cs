using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using static Windows_Spotlight.ProgramHelpers;

namespace Windows_Spotlight
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            CliOptions options;
            string error;
            if (!CliOptions.TryParse(args, out options, out error))
            {
                Console.Error.WriteLine("Error: {0}", error);
                Console.Error.WriteLine("Run Windows-Spotlight.exe --help for usage and examples.");
                return 1;
            }
            if (options.Help) { Help(); return 0; }
            if (options.Version) { Console.WriteLine("Windows Spotlight v{0}", GetVersion()); return 0; }
            try
            {
                if (options.Startup != null)
                    return new StartupManager(new WindowsTaskScheduler()).Execute(options.Startup, options.Source);
                using (var gate = new ExportGate())
                {
                    if (!gate.Acquired)
                    {
                        Console.WriteLine("An image export is already running. Try again shortly.");
                        return 0;
                    }
                    if (options.Background)
                        return BackgroundLog.Run(StartupManager.LogPath, () => Export(options));
                    return Export(options);
                }
            }
            catch (Exception ex) when (IsOperationalError(ex))
            {
                Console.Error.WriteLine("Error: {0}", ex.Message);
                if (options.Startup != null)
                    Console.Error.WriteLine("Could not manage the startup task. Check Task Scheduler availability and your account's permissions, then try again.");
                return 1;
            }
        }

        private static int Export(CliOptions options)
        {
            Console.WriteLine("Source: {0}", SourceLabel(options.Source));
            Console.WriteLine("Output: {0}", Constants.Paths.DESTINATION);
            var catalog = new SpotlightCatalog(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
            ExportResult result = ImageExporter.Export(catalog, options.Source, Constants.Paths.DESTINATION);
            WriteExportResult(result, Console.Out, Console.Error);
            if (options.OpenFolder) OpenFolder();
            return result.Failed == 0 ? 0 : 1;
        }

        internal static bool IsOperationalError(Exception ex)
        {
            return SpotlightCatalog.IsReadError(ex) || ex is Win32Exception || ex is COMException
                || ex is InvalidOperationException || ex is NotSupportedException || ex is System.Xml.XmlException;
        }
    }
}
