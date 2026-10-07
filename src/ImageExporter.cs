using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;

namespace Windows_Spotlight
{
    internal sealed class ExportResult
    {
        internal int Saved;
        internal int Duplicates;
        internal int Failed;
    }

    internal static class ImageExporter
    {
        internal static ExportResult Export(SpotlightCatalog catalog, ImageSource source, string destination)
        {
            var result = new ExportResult();
            Directory.CreateDirectory(destination);
            var known = ExistingHashes(destination);
            foreach (SpotlightImage candidate in catalog.Discover(source))
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(candidate.Path);
                    if (!JpegMetadata.IsJpeg(bytes)) continue;
                    bool portrait;
                    using (var stream = new MemoryStream(bytes))
                    using (Image image = Image.FromStream(stream))
                    {
                        long width = image.Width;
                        long height = image.Height;
                        if (candidate.Source == "lockscreen" && width * 9 != height * 16 && width * 16 != height * 9) continue;
                        if (candidate.Source == "desktop" && (Math.Max(width, height) < 1000 || Math.Min(width, height) < 600)) continue;
                        portrait = height > width;
                    }
                    string hash = JpegMetadata.Hash(bytes);
                    if (known.Contains(hash)) { result.Duplicates++; continue; }
                    ImageInfo info = catalog.FindInfo(candidate.Path, hash);
                    string name = SafeName(info == null ? null : info.Name, hash);
                    string folder = Path.Combine(destination, portrait ? "Portrait" : "Landscape");
                    Directory.CreateDirectory(folder);
                    string target = UniquePath(folder, name, hash);
                    byte[] export = JpegMetadata.Embed(bytes, hash, info, candidate.Source, Path.GetFileName(candidate.Path));
                    WriteAtomically(target, export);
                    known.Add(hash);
                    result.Saved++;
                }
                catch (Exception ex) when (SpotlightCatalog.IsReadError(ex) || ex is ArgumentException || ex is System.Runtime.InteropServices.ExternalException)
                {
                    result.Failed++;
                    Console.Error.WriteLine("Could not export {0}: {1}", Path.GetFileName(candidate.Path), ex.Message);
                }
            }
            return result;
        }

        private static HashSet<string> ExistingHashes(string destination)
        {
            var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string file in SpotlightCatalog.Files(destination))
            {
                string extension = Path.GetExtension(file);
                if (!string.Equals(extension, ".jpg", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(extension, ".jpeg", StringComparison.OrdinalIgnoreCase)) continue;
                try
                {
                    byte[] bytes = File.ReadAllBytes(file);
                    if (!JpegMetadata.IsJpeg(bytes)) continue;
                    // Older exports are unchanged copies, so their file hash is the original source hash.
                    hashes.Add(JpegMetadata.ReadOriginalHash(bytes) ?? JpegMetadata.Hash(bytes));
                }
                catch (Exception ex) when (SpotlightCatalog.IsReadError(ex))
                {
                    Console.Error.WriteLine("Could not check existing image {0}: {1}", Path.GetFileName(file), ex.Message);
                }
            }
            return hashes;
        }

        internal static string SafeName(string name, string hash)
        {
            if (string.IsNullOrWhiteSpace(name)) return hash;
            var cleaned = new StringBuilder();
            foreach (char c in name)
                cleaned.Append(c < 32 || Path.GetInvalidFileNameChars().Contains(c) ? '_' : c);
            string value = cleaned.ToString().Trim().TrimEnd(' ', '.');
            if (value.Length > 100)
            {
                value = value.Substring(0, 100);
                if (char.IsHighSurrogate(value[value.Length - 1])) value = value.Substring(0, value.Length - 1);
                value = value.TrimEnd(' ', '.');
            }
            if (value.Length == 0) return hash;
            string stem = value.Split('.')[0].TrimEnd().ToUpperInvariant();
            if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$"
                || (stem.Length == 4 && (stem.StartsWith("COM") || stem.StartsWith("LPT"))
                    && "123456789¹²³".Contains(stem[3]))) value = "_" + value;
            return value;
        }

        private static string UniquePath(string folder, string name, string hash)
        {
            string path = Path.Combine(folder, name + ".jpg");
            if (!File.Exists(path)) return path;
            string suffix = name + " - " + hash.Substring(0, 12);
            path = Path.Combine(folder, suffix + ".jpg");
            int count = 2;
            while (File.Exists(path)) path = Path.Combine(folder, suffix + " - " + count++ + ".jpg");
            return path;
        }

        private static void WriteAtomically(string target, byte[] bytes)
        {
            string temporary = Path.Combine(Path.GetDirectoryName(target), Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write)) stream.Write(bytes, 0, bytes.Length);
                File.Move(temporary, target);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
