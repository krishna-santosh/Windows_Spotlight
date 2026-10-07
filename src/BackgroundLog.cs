using System;
using System.IO;
using System.Security.Principal;
using System.Text;
using System.Threading;

namespace Windows_Spotlight
{
    internal sealed class ExportGate : IDisposable
    {
        private readonly Mutex mutex;
        internal bool Acquired { get; private set; }

        internal ExportGate(string name = null)
        {
            mutex = new Mutex(false, name ?? @"Global\Windows-Spotlight-Export-" + WindowsIdentity.GetCurrent().User.Value);
            try { Acquired = mutex.WaitOne(0); }
            catch (AbandonedMutexException) { Acquired = true; }
        }

        public void Dispose()
        {
            if (Acquired) mutex.ReleaseMutex();
            mutex.Dispose();
        }
    }

    internal static class BackgroundLog
    {
        internal const long MaxBytes = 1024 * 1024;

        // Called under ExportGate: keep both console streams and log rotation serialized.
        internal static int Run(string path, Func<int> export)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            if (File.Exists(path) && new FileInfo(path).Length >= MaxBytes)
            {
                if (File.Exists(path + ".1")) File.Delete(path + ".1");
                File.Move(path, path + ".1");
            }
            using (var stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var log = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true })
            {
                TextWriter output = Console.Out;
                TextWriter errors = Console.Error;
                try
                {
                    Console.SetOut(log);
                    Console.SetError(log);
                    log.WriteLine("[{0:O}] Background export started (UTC).", DateTime.UtcNow);
                    int result;
                    try { result = export(); }
                    catch (Exception ex) { log.WriteLine("Error: {0}", ex.Message); result = 1; }
                    log.WriteLine("[{0:O}] Background export finished. Exit code: {1}.", DateTime.UtcNow, result);
                    return result;
                }
                finally { Console.SetOut(output); Console.SetError(errors); }
            }
        }
    }
}
