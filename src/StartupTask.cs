using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Principal;
using System.Xml.Linq;

namespace Windows_Spotlight
{
    internal sealed class StartupStatus
    {
        internal bool Enabled;
        internal bool Running;
        internal DateTime LastRun;
        internal DateTime NextRun;
        internal uint LastResult;
        internal string Xml;
    }

    internal interface IStartupScheduler
    {
        StartupStatus Get(string name);
        void Register(string name, string xml, string userId);
        bool Delete(string name);
    }

    internal sealed class WindowsTaskScheduler : IStartupScheduler
    {
        private const int FileNotFound = unchecked((int)0x80070002);

        private static T WithFolder<T>(Func<dynamic, T> action)
        {
            object service = null;
            object folder = null;
            try
            {
                service = Activator.CreateInstance(Type.GetTypeFromProgID("Schedule.Service", true));
                ((dynamic)service).Connect();
                folder = ((dynamic)service).GetFolder(@"\");
                return action(folder);
            }
            finally { Release(folder); Release(service); }
        }

        internal static void Release(object value)
        {
            if (value != null && Marshal.IsComObject(value)) Marshal.FinalReleaseComObject(value);
        }

        public StartupStatus Get(string name)
        {
            return WithFolder<StartupStatus>(folder =>
            {
                object task = null;
                try
                {
                    task = folder.GetTask(name);
                    dynamic registered = task;
                    return new StartupStatus
                    {
                        Enabled = registered.Enabled,
                        Running = registered.State == 4,
                        LastRun = registered.LastRunTime,
                        NextRun = registered.NextRunTime,
                        LastResult = unchecked((uint)(int)registered.LastTaskResult),
                        Xml = registered.Xml
                    };
                }
                catch (Exception ex) when (ex.HResult == FileNotFound) { return null; }
                finally { Release(task); }
            });
        }

        public void Register(string name, string xml, string userId)
        {
            WithFolder(folder =>
            {
                // TASK_CREATE_OR_UPDATE, TASK_LOGON_INTERACTIVE_TOKEN: current user, no password.
                object task = folder.RegisterTask(name, xml, 6, userId, null, 3, null);
                Release(task);
                return true;
            });
        }

        public bool Delete(string name)
        {
            return WithFolder(folder =>
            {
                try { folder.DeleteTask(name, 0); return true; }
                catch (Exception ex) when (ex.HResult == FileNotFound) { return false; }
            });
        }
    }

    internal sealed class StartupManager
    {
        internal static readonly string StoragePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Windows-Spotlight");
        internal static readonly string LogPath = Path.Combine(StoragePath, "startup.log");
        private readonly IStartupScheduler scheduler;
        private readonly string storage;
        private readonly string userId;
        internal readonly string TaskName;
        internal string WorkerPath { get { return Path.Combine(storage, "Windows-Spotlight.Background.exe"); } }

        internal StartupManager(IStartupScheduler scheduler, string storage = null, string userId = null, string taskName = null)
        {
            this.scheduler = scheduler;
            this.storage = storage ?? StoragePath;
            this.userId = userId ?? WindowsIdentity.GetCurrent().User.Value;
            TaskName = taskName ?? "Windows-Spotlight-" + this.userId;
        }

        internal int Execute(string command, ImageSource source)
        {
            switch (command)
            {
                case "enable":
                    using (var gate = new ExportGate())
                    {
                        if (!gate.Acquired) throw new InvalidOperationException("An image export is running. Try enabling startup again shortly.");
                        InstallWorker();
                        scheduler.Register(TaskName, BuildXml(userId, WorkerPath, source), userId);
                    }
                    Console.WriteLine("Startup enabled for the current user.");
                    Console.WriteLine("Source: {0}", ProgramHelpers.SourceLabel(source));
                    Console.WriteLine("Schedule: One minute after sign-in, then hourly while signed in.");
                    Console.WriteLine("Output: {0}", Constants.Paths.DESTINATION);
                    Console.WriteLine("Background log: {0}", Path.Combine(storage, "startup.log"));
                    Console.WriteLine("Run --startup status to check the last run. Run --startup disable to turn it off.");
                    return 0;
                case "disable":
                    Console.WriteLine(scheduler.Delete(TaskName) ? "Startup disabled. The startup task was removed." : "Startup is already disabled.");
                    Console.WriteLine("Saved images are kept. A background export already in progress may finish.");
                    return 0;
                case "status": return WriteStatus(scheduler.Get(TaskName));
                default: throw new InvalidOperationException("Unknown startup command.");
            }
        }

        internal void InstallWorker()
        {
            byte[] worker = WorkerBytes();
            Directory.CreateDirectory(storage);
            WriteIfChanged(WorkerPath, worker);
            // The worker uses the same runtime configuration as the portable CLI.
            string config = Assembly.GetExecutingAssembly().Location + ".config";
            if (File.Exists(config)) WriteIfChanged(WorkerPath + ".config", File.ReadAllBytes(config));
            else if (File.Exists(WorkerPath + ".config")) File.Delete(WorkerPath + ".config");
        }

        internal static byte[] WorkerBytes()
        {
            using (Stream resource = Assembly.GetExecutingAssembly().GetManifestResourceStream("Windows_Spotlight.Background.exe"))
            {
                if (resource == null) throw new InvalidOperationException("The background app is missing from this build. Use the portable Windows-Spotlight.exe to enable startup.");
                using (var buffer = new MemoryStream()) { resource.CopyTo(buffer); return buffer.ToArray(); }
            }
        }

        private static void WriteIfChanged(string path, byte[] contents)
        {
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(contents)) return;
            string temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                File.WriteAllBytes(temporary, contents);
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private int WriteStatus(StartupStatus status)
        {
            if (status == null)
            {
                Console.WriteLine("Startup: Disabled");
                Console.WriteLine("Run Windows-Spotlight.exe --startup enable to enable automatic exports.");
                return 0;
            }
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            XDocument task = XDocument.Parse(status.Xml);
            XElement action = task.Descendants(ns + "Exec").FirstOrDefault();
            CliOptions options;
            string error;
            string arguments = action == null ? "" : (string)action.Element(ns + "Arguments") ?? "";
            string executable = action == null ? "" : (string)action.Element(ns + "Command") ?? "";
            bool valid = CliOptions.TryParse(arguments.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries), out options, out error)
                && options.Background && options.Startup == null && !options.Help && !options.Version;
            Console.WriteLine("Startup: {0}", status.Enabled ? "Enabled" : "Disabled (task exists)");
            Console.WriteLine("Task: {0}", TaskName);
            Console.WriteLine("Source: {0}", valid ? ProgramHelpers.SourceLabel(options.Source) : "Unknown; run --startup enable to repair");
            XElement triggers = task.Root.Element(ns + "Triggers");
            XElement logon = triggers?.Element(ns + "LogonTrigger");
            bool standardSchedule = triggers?.Elements().Count() == 1
                && logon?.Element(ns + "Delay")?.Value == "PT1M"
                && logon?.Element(ns + "Repetition")?.Element(ns + "Interval")?.Value == "PT1H";
            Console.WriteLine("Schedule: {0}", standardSchedule ? "One minute after sign-in, then hourly while signed in." : "Custom; see Task Scheduler.");
            Console.WriteLine("State: {0}", status.Running ? "Running" : "Idle");
            Console.WriteLine("Last run: {0}", FormatTime(status.LastRun));
            Console.WriteLine("Last result: {0}", status.LastRun.Year < 2000 ? "Not run yet" : ResultLabel(status.LastResult));
            Console.WriteLine("Next run: {0}", status.Enabled ? FormatTime(status.NextRun) : "Not scheduled");
            Console.WriteLine("Background app: {0}", executable);
            if (!File.Exists(executable)) Console.WriteLine("Action needed: Background app is missing. Run --startup enable to repair.");
            else if (!File.ReadAllBytes(executable).SequenceEqual(WorkerBytes()))
                Console.WriteLine("Action needed: Background app differs from this version. Run --startup enable to refresh it.");
            Console.WriteLine("Background log: {0}", Path.Combine(storage, "startup.log"));
            return 0;
        }

        private static string FormatTime(DateTime value)
        {
            return value.Year < 2000 ? "Not scheduled / not run yet" : value.ToString("yyyy-MM-dd HH:mm:ss") + " (local time)";
        }

        private static string ResultLabel(uint value)
        {
            if (value == 0) return "Success";
            if (value == 1) return "Failed; see the background log";
            if (value == 0x41301) return "Running";
            return "Task Scheduler code 0x" + value.ToString("X8") + "; check Task Scheduler and the background log";
        }

        internal static string BuildXml(string userId, string executable, ImageSource source)
        {
            XNamespace ns = "http://schemas.microsoft.com/windows/2004/02/mit/task";
            return new XDocument(new XElement(ns + "Task", new XAttribute("version", "1.2"),
                new XElement(ns + "RegistrationInfo", new XElement(ns + "Description", "Save cached Windows Spotlight images after sign-in and hourly.")),
                new XElement(ns + "Triggers", new XElement(ns + "LogonTrigger",
                    new XElement(ns + "Repetition", new XElement(ns + "Interval", "PT1H"), new XElement(ns + "StopAtDurationEnd", false)),
                    new XElement(ns + "Enabled", true), new XElement(ns + "UserId", userId), new XElement(ns + "Delay", "PT1M"))),
                new XElement(ns + "Principals", new XElement(ns + "Principal", new XAttribute("id", "CurrentUser"),
                    new XElement(ns + "UserId", userId), new XElement(ns + "LogonType", "InteractiveToken"), new XElement(ns + "RunLevel", "LeastPrivilege"))),
                new XElement(ns + "Settings",
                    new XElement(ns + "MultipleInstancesPolicy", "IgnoreNew"),
                    new XElement(ns + "DisallowStartIfOnBatteries", false), new XElement(ns + "StopIfGoingOnBatteries", false),
                    new XElement(ns + "StartWhenAvailable", true), new XElement(ns + "RunOnlyIfNetworkAvailable", false),
                    new XElement(ns + "Enabled", true), new XElement(ns + "Hidden", false),
                    new XElement(ns + "WakeToRun", false), new XElement(ns + "ExecutionTimeLimit", "PT15M")),
                new XElement(ns + "Actions", new XAttribute("Context", "CurrentUser"), new XElement(ns + "Exec",
                    new XElement(ns + "Command", executable),
                    new XElement(ns + "Arguments", "--background --source " + source.ToString().ToLowerInvariant()),
                    new XElement(ns + "WorkingDirectory", Path.GetDirectoryName(executable)))))).ToString();
        }
    }
}
