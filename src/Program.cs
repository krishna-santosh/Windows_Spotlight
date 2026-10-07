using System;
using static Windows_Spotlight.ProgramHelpers;

namespace Windows_Spotlight
{
    internal class Program
    {
        private static int Main(string[] args)
        {
            bool open = false;
            ImageSource source = ImageSource.LockScreen;
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-V":
                    case "--version":
                        Console.WriteLine("Windows Spotlight v{0}", GetVersion());
                        return 0;
                    case "-O":
                    case "--open-folder":
                        open = true;
                        break;
                    case "-h":
                    case "--help":
                        Help();
                        return 0;
                    case "--source":
                        if (++i >= args.Length || !TrySource(args[i], out source))
                        {
                            Console.Error.WriteLine("--source requires lockscreen, desktop, or all.");
                            return 1;
                        }
                        break;
                    default:
                        Console.Error.WriteLine("Unknown option: {0}", args[i]);
                        Help();
                        return 1;
                }
            }
            try
            {
                var catalog = new SpotlightCatalog(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
                ExportResult result = ImageExporter.Export(catalog, source, Constants.Paths.DESTINATION);
                Console.WriteLine(result.Saved > 0 ? $"Got {result.Saved} new images." : "No new images found.");
                if (result.Duplicates > 0) Console.WriteLine("Skipped {0} existing images.", result.Duplicates);
                if (result.Failed > 0) Console.Error.WriteLine("Could not export {0} images.", result.Failed);
                if (open) OpenFolder();
                return result.Failed == 0 ? 0 : 1;
            }
            catch (Exception ex) when (SpotlightCatalog.IsReadError(ex) || ex is System.ComponentModel.Win32Exception)
            {
                Console.Error.WriteLine(ex.Message);
                return 1;
            }
        }

        private static bool TrySource(string value, out ImageSource source)
        {
            source = ImageSource.LockScreen;
            switch (value.ToLowerInvariant())
            {
                case "lockscreen": return true;
                case "desktop": source = ImageSource.Desktop; return true;
                case "all": source = ImageSource.All; return true;
                default: return false;
            }
        }
    }
}
