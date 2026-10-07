using System;

namespace Windows_Spotlight
{
    internal sealed class CliOptions
    {
        internal bool Help;
        internal bool Version;
        internal bool OpenFolder;
        internal bool Background;
        internal string Startup;
        internal ImageSource Source = ImageSource.LockScreen;

        internal static bool TryParse(string[] args, out CliOptions options, out string error)
        {
            options = new CliOptions();
            error = null;
            bool sourceSpecified = false;
            foreach (string arg in args)
                if (arg == "--help" || arg == "-h") { options.Help = true; return true; }
            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "-V": case "--version": options.Version = true; break;
                    case "-O": case "--open-folder": options.OpenFolder = true; break;
                    case "--background": options.Background = true; break;
                    case "--source":
                        if (sourceSpecified) { error = "Specify --source only once."; return false; }
                        if (++i >= args.Length || !TrySource(args[i], out options.Source))
                        { error = "--source requires lockscreen, desktop, or all."; return false; }
                        sourceSpecified = true;
                        break;
                    case "--startup":
                        if (options.Startup != null) { error = "Specify --startup only once."; return false; }
                        if (++i >= args.Length || (args[i] != "enable" && args[i] != "disable" && args[i] != "status"))
                        { error = "--startup requires enable, disable, or status."; return false; }
                        options.Startup = args[i];
                        break;
                    default: error = "Unknown option: " + args[i]; return false;
                }
            }
            if (options.Startup != null && (options.OpenFolder || options.Background || options.Version))
            { error = "--startup cannot be combined with --open-folder, --background, or --version."; return false; }
            if (sourceSpecified && options.Startup != null && options.Startup != "enable")
            { error = "Use --source with --startup enable to configure automatic exports."; return false; }
            if (options.Background && options.OpenFolder)
            { error = "Background exports cannot open the output folder."; return false; }
            if (!sourceSpecified && (options.Startup == "enable" || options.Background)) options.Source = ImageSource.All;
            return true;
        }

        internal static bool TrySource(string value, out ImageSource source)
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
