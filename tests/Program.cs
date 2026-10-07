using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
using System.Xml.Linq;
using Windows_Spotlight;

internal static class Tests
{
    private static int checks;
    private static readonly JavaScriptSerializer Json = new JavaScriptSerializer();

    private static int Main(string[] args)
    {
        string root = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures", Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            Run(root);
            if (args.Contains("--system")) CheckSystem(Path.Combine(root, "system"));
            Console.WriteLine("PASS: {0} checks", checks);
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
        finally
        {
            // Only remove this run's fixtures, confined to the test executable's output directory.
            string allowed = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "fixtures")) + Path.DirectorySeparatorChar;
            if (root.StartsWith(allowed, StringComparison.OrdinalIgnoreCase)) Directory.Delete(root, true);
        }
    }

    private static void Run(string root)
    {
        string local = Path.Combine(root, "local");
        string state = Path.Combine(local, "Packages", "Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy", "LocalState");
        string assets = Path.Combine(state, "Assets");
        string targetCache = Path.Combine(state, "TargetedContentCache", "v3", "338387");
        string rawCache = Path.Combine(state, "ContentManagementSDK", "Creatives", "338387");
        Directory.CreateDirectory(assets);
        Directory.CreateDirectory(targetCache);
        Directory.CreateDirectory(rawCache);
        byte[] landscape = MakeJpeg(1600, 900, Color.SteelBlue);
        byte[] portrait = MakeJpeg(900, 1600, Color.SteelBlue);
        byte[] other = MakeJpeg(1600, 900, Color.DarkGreen);
        string first = Path.Combine(assets, "random-first");
        string second = Path.Combine(assets, "random-portrait");
        string third = Path.Combine(assets, "random-second");
        File.WriteAllBytes(first, landscape);
        File.WriteAllBytes(second, portrait);
        File.WriteAllBytes(third, other);
        File.WriteAllBytes(Path.Combine(assets, "tiny"), MakeJpeg(32, 32, Color.Red));
        File.WriteAllText(Path.Combine(assets, "not-an-image"), "not a jpeg");
        File.WriteAllText(Path.Combine(assets, "broken-jpeg"), "\ufffd");
        File.WriteAllText(Path.Combine(targetCache, "current"), Content(first, second, landscape, portrait, "Bay: Côte / 海", "© Test Photographer / Agency"));
        File.WriteAllText(Path.Combine(targetCache, "bad-json"), "{broken");
        // The older batch provides remote URLs rather than paths. Match its metadata by the content hash.
        var raw = new { batchrsp = new { items = new[] { new { item = Json.Serialize(new { ad = Json.DeserializeObject(Content("https://example.test/image.jpg", null, other, null, "Bay: Côte / 海", "© Other Photographer")) }) } } } };
        File.WriteAllText(Path.Combine(rawCache, "older"), Json.Serialize(raw));

        string destination = Path.Combine(root, "exports");
        var catalog = new SpotlightCatalog(local, includeDesktopRegistry: false);
        var initial = ImageExporter.Export(catalog, ImageSource.LockScreen, destination);
        Check(initial.Saved == 3 && initial.Failed == 0, "export landscape, portrait and older cached image");
        string[] output = Directory.GetFiles(destination, "*.jpg", SearchOption.AllDirectories);
        Check(output.Length == 3, "only wallpaper images are exported");
        Check(output.All(p => !Path.GetFileName(p).Contains("random")), "names come from subject metadata");
        Check(output.Count(p => p.Contains(" - ")) == 1, "different photos with the same title get a collision suffix");
        foreach (string path in output)
        {
            byte[] exported = File.ReadAllBytes(path);
            string hash = JpegMetadata.ReadOriginalHash(exported);
            byte[] original = new[] { landscape, portrait, other }.Single(b => JpegMetadata.Hash(b) == hash);
            Check(JpegMetadata.Hash(RemoveXmp(exported)) == hash, "all original JPEG bytes survive metadata insertion");
            ComparePixels(original, exported);
            XDocument packet = Packet(exported);
            XNamespace dc = "http://purl.org/dc/elements/1.1/";
            Check(packet.Descendants(dc + "title").Single().Value == "Bay: Côte / 海", "Unicode title preserved in XMP");
            Check(packet.Descendants(dc + "rights").Any(), "copyright credit embedded");
            Check(packet.Descendants(dc + "description").Single().Value.Contains("Caption & details"), "caption embedded and XML escaped");
        }
        Check(!Directory.GetFiles(destination, "*", SearchOption.AllDirectories).Any(p => !p.EndsWith(".jpg")), "no index, sidecars or temporary files");
        File.Move(output[0], Path.Combine(Path.GetDirectoryName(output[0]), "user-renamed.jpg"));
        var repeat = ImageExporter.Export(new SpotlightCatalog(local, includeDesktopRegistry: false), ImageSource.LockScreen, destination);
        Check(repeat.Saved == 0 && repeat.Duplicates == 3, "repeat run skips duplicates after a rename");
        File.Copy(first, Path.Combine(assets, "different-source-id"));
        var alternate = ImageExporter.Export(new SpotlightCatalog(local, includeDesktopRegistry: false), ImageSource.LockScreen, destination);
        Check(alternate.Saved == 0 && alternate.Duplicates == 4, "new random IDs do not cause duplicates");

        string legacy = Path.Combine(root, "legacy");
        Directory.CreateDirectory(legacy);
        File.WriteAllBytes(Path.Combine(legacy, "old-random-name.jpg"), landscape);
        var migration = ImageExporter.Export(new SpotlightCatalog(local, includeDesktopRegistry: false), ImageSource.LockScreen, legacy);
        Check(migration.Saved == 2 && migration.Duplicates == 2, "old unmodified exports are recognized without modifying them");
        Check(File.ReadAllBytes(Path.Combine(legacy, "old-random-name.jpg")).SequenceEqual(landscape), "existing exports untouched");

        // Two numeric IrisService directories, including a desktop aspect ratio that is not 16:9.
        string iris = Path.Combine(local, "Packages", "MicrosoftWindows.Client.CBS_cw5n1h2txyewy", "LocalCache", "Microsoft", "IrisService");
        Directory.CreateDirectory(Path.Combine(iris, "111"));
        Directory.CreateDirectory(Path.Combine(iris, "999", "nested"));
        File.WriteAllBytes(Path.Combine(iris, "111", "same.jpg"), landscape);
        byte[] desktop = MakeJpeg(1200, 800, Color.Goldenrod);
        string desktopPath = Path.Combine(iris, "999", "nested", "desktop.JPG");
        File.WriteAllBytes(desktopPath, desktop);
        File.WriteAllBytes(Path.Combine(iris, "999", "thumb.jpg"), MakeJpeg(320, 200, Color.Red));
        var allCatalog = new SpotlightCatalog(local, includeDesktopRegistry: false);
        allCatalog.ParseMetadata(Json.Serialize(new { ad = new { landscapeImage = new { asset = desktopPath }, iconHoverText = "Desktop Place\r\n© Desktop Artist", description = "Desktop description", copyright = "© Desktop Artist", ctaUri = "https://example.test/desktop" } }));
        var all = ImageExporter.Export(allCatalog, ImageSource.All, destination);
        Check(all.Saved == 1 && all.Duplicates == 5, "all sources share duplicate detection and discover nested desktop directories");
        string desktopOutput = Path.Combine(destination, "Landscape", "Desktop Place.jpg");
        Check(File.Exists(desktopOutput), "desktop registry-shaped metadata names the image");
        Check(JpegMetadata.ReadOriginalHash(File.ReadAllBytes(desktopOutput)) == JpegMetadata.Hash(desktop), "desktop hash embedded");
        var desktopOnly = ImageExporter.Export(new SpotlightCatalog(local, includeDesktopRegistry: false), ImageSource.Desktop, Path.Combine(root, "desktop-only"));
        Check(desktopOnly.Saved == 2 && desktopOnly.Failed == 0, "desktop-only selection excludes lockscreen images");
        string deleted = Directory.GetFiles(destination, "*.jpg", SearchOption.AllDirectories).First(p => JpegMetadata.ReadOriginalHash(File.ReadAllBytes(p)) == JpegMetadata.Hash(portrait));
        File.Delete(deleted);
        Check(ImageExporter.Export(new SpotlightCatalog(local, includeDesktopRegistry: false), ImageSource.LockScreen, destination).Saved == 1, "deleted images can be exported again");
        Check(ImageExporter.Export(new SpotlightCatalog(Path.Combine(root, "missing"), includeDesktopRegistry: false), ImageSource.LockScreen, Path.Combine(root, "empty")).Saved == 0, "missing caches tolerated");
        Check(ImageExporter.Export(new SpotlightCatalog(Path.Combine(root, "missing"), includeDesktopRegistry: false), ImageSource.Desktop, Path.Combine(root, "empty-desktop")).Saved == 0, "fixture desktop discovery excludes the user's registry");

        string bundledPath = Path.Combine(root, "bundled.jpg");
        File.WriteAllBytes(bundledPath, landscape);
        var fallbackCatalog = new SpotlightCatalog(Path.Combine(root, "missing"), includeDesktopRegistry: false);
        fallbackCatalog.ParseMetadata(Json.Serialize(new { landscapeImage = new { asset = bundledPath }, iconHoverText = "Bundled Place" }), isDefault: true);
        var fallback = ImageExporter.Export(fallbackCatalog, ImageSource.Desktop, Path.Combine(root, "fallback"));
        Check(fallback.Saved == 1 && fallback.Failed == 0, "desktop defaults exported when downloaded caches are missing");
        fallbackCatalog.ParseMetadata(Json.Serialize(new { landscapeImage = new { asset = desktopPath }, iconHoverText = "Downloaded Place" }));
        string preferredOutput = Path.Combine(root, "prefer-downloaded");
        var preferred = ImageExporter.Export(fallbackCatalog, ImageSource.Desktop, preferredOutput);
        Check(preferred.Saved == 1 && preferred.Failed == 0
            && JpegMetadata.ReadOriginalHash(File.ReadAllBytes(Directory.GetFiles(preferredOutput, "*.jpg", SearchOption.AllDirectories).Single())) == JpegMetadata.Hash(desktop),
            "downloaded desktop images take precedence over bundled defaults");

        string hashValue = JpegMetadata.Hash(landscape);
        Check(ImageExporter.SafeName("CON", hashValue) == "_CON", "Windows reserved device names escaped");
        Check(ImageExporter.SafeName("LPT¹.txt", hashValue) == "_LPT¹.txt", "superscript device names escaped");
        Check(ImageExporter.SafeName("  ...  ", hashValue) == hashValue, "empty sanitized name falls back to hash");
        Check(ImageExporter.SafeName("a/b:c? . ", hashValue) == "a_b_c_", "unsafe characters and trailing spaces/dots sanitized");
        byte[] initialMetadata = JpegMetadata.Embed(landscape, hashValue, new ImageInfo { Name = "Before", Copyright = "© Artist" }, "lockscreen", "asset");
        XDocument originalPacket = Packet(initialMetadata);
        originalPacket.Descendants(XName.Get("Description", "http://www.w3.org/1999/02/22-rdf-syntax-ns#")).First().Add(new XElement(XName.Get("Keep", "urn:test"), "other metadata"));
        byte[] withForeign = ReplacePacket(initialMetadata, originalPacket);
        byte[] updated = JpegMetadata.Embed(withForeign, hashValue, new ImageInfo { Name = "After" }, "desktop", "asset");
        Check(Packet(updated).Descendants(XName.Get("Keep", "urn:test")).Single().Value == "other metadata", "foreign XMP properties preserved");
        Check(Packet(updated).Descendants(XName.Get("title", "http://purl.org/dc/elements/1.1/")).Single().Value == "After", "existing XMP title updated without duplicates");
        Check(RemoveXmp(updated).SequenceEqual(landscape), "replacing existing XMP preserves original JPEG bytes");

        byte[] unknown = MakeJpeg(1600, 900, Color.Purple);
        File.WriteAllBytes(Path.Combine(assets, "unknown-subject"), unknown);
        var noMetadata = ImageExporter.Export(new SpotlightCatalog(local, includeDesktopRegistry: false), ImageSource.LockScreen, destination);
        Check(noMetadata.Saved == 1 && File.Exists(Path.Combine(destination, "Landscape", JpegMetadata.Hash(unknown) + ".jpg")), "missing metadata still exports with a stable fallback name");
        Check(ImageExporter.Export(new SpotlightCatalog(local, includeDesktopRegistry: false), ImageSource.LockScreen, destination).Saved == 0, "unnamed images also avoid duplicates");
        var malformed = XmpRange(initialMetadata);
        byte[] invalidXmp = (byte[])initialMetadata.Clone();
        invalidXmp[malformed.Item2] = (byte)'!';
        bool rejected = false;
        try { JpegMetadata.Embed(invalidXmp, hashValue, null, "lockscreen", "asset"); }
        catch (InvalidDataException) { rejected = true; }
        Check(rejected, "invalid existing XMP is not silently overwritten");
        // A JPEG application segment such as EXIF must survive insertion unchanged.
        byte[] exifSegment = new byte[] { 0xff, 0xe1, 0, 10, (byte)'E', (byte)'x', (byte)'i', (byte)'f', 0, 0, 1, 2 };
        byte[] withExif = landscape.Take(2).Concat(exifSegment).Concat(landscape.Skip(2)).ToArray();
        byte[] exifExport = JpegMetadata.Embed(withExif, JpegMetadata.Hash(withExif), null, "lockscreen", "asset");
        Check(RemoveXmp(exifExport).SequenceEqual(withExif), "EXIF bytes preserved");

        CheckCli("--help", 0, "--source");
        CheckCli("--source desktop --help", 0, "--source");
        CheckCli("-O --source all --help", 0, "--source");
        CheckCli("--version", 0, "Windows Spotlight v2.0.0.0");
        CheckCli("--source", 1, "requires");
        CheckCli("--source bogus", 1, "requires");
        CheckCli("--bogus", 1, "Unknown option");
    }

    private static void CheckSystem(string destination)
    {
        var catalog = new SpotlightCatalog(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData));
        var first = ImageExporter.Export(catalog, ImageSource.All, destination);
        Check(first.Saved > 0 && first.Failed == 0, string.Format("real Windows caches export successfully into test workspace (saved: {0}, failed: {1}); --system requires accessible cached Spotlight images", first.Saved, first.Failed));
        var second = ImageExporter.Export(new SpotlightCatalog(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)), ImageSource.All, destination);
        Check(second.Saved == 0 && second.Failed == 0, "real Windows caches do not create duplicates on second run");
        string[] files = Directory.GetFiles(destination, "*.jpg", SearchOption.AllDirectories);
        Check(files.Select(p => JpegMetadata.ReadOriginalHash(File.ReadAllBytes(p))).Distinct().Count() == files.Length, "real export has unique embedded original hashes");
        Console.WriteLine("System smoke test: {0} images, {1} repeat duplicates, named examples: {2}", first.Saved, second.Duplicates,
            string.Join(", ", files.Where(p => Path.GetFileNameWithoutExtension(p).Length != 64).Take(4).Select(Path.GetFileName)));
    }

    private static void CheckCli(string arguments, int exit, string text)
    {
        string executable = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Windows-Spotlight.exe");
        using (var process = Process.Start(new ProcessStartInfo(executable, arguments) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true }))
        {
            string output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();
            Check(process.ExitCode == exit && output.Contains(text), "CLI " + arguments);
        }
    }

    private static string Content(string landscape, string portrait, byte[] landBytes, byte[] portBytes, string name, string copyright)
    {
        return Json.Serialize(new
        {
            name = "LockScreen",
            properties = new { landscapeImage = Image(landscape, landBytes), portraitImage = Image(portrait, portBytes) },
            items = new object[]
            {
                new { properties = new { template = new { text = "infoHotspot" }, description = new { text = name }, copyright = new { text = copyright }, onClick = new { parameters = new { uri = "https://example.test/learn" } } } },
                new { properties = new { template = new { text = "basicHotspot" }, title = new { text = "Caption & details <here>" } } }
            }
        });
    }

    private static object Image(string path, byte[] bytes)
    {
        return new { image = path, sha256 = bytes == null ? null : Convert.ToBase64String(HexBytes(JpegMetadata.Hash(bytes))) };
    }

    private static byte[] HexBytes(string hex) { return Enumerable.Range(0, hex.Length / 2).Select(i => Convert.ToByte(hex.Substring(i * 2, 2), 16)).ToArray(); }
    private static byte[] MakeJpeg(int width, int height, Color color)
    {
        using (var bitmap = new Bitmap(width, height))
        using (var graphics = Graphics.FromImage(bitmap))
        using (var stream = new MemoryStream())
        { graphics.Clear(color); bitmap.Save(stream, ImageFormat.Jpeg); return stream.ToArray(); }
    }

    private static void ComparePixels(byte[] before, byte[] after)
    {
        using (var a = new MemoryStream(before))
        using (var b = new MemoryStream(after))
        using (var original = new Bitmap(a))
        using (var exported = new Bitmap(b))
        {
            Check(original.Width == exported.Width && original.Height == exported.Height, "image dimensions preserved");
            Check(original.GetPixel(0, 0) == exported.GetPixel(0, 0) && original.GetPixel(original.Width / 2, original.Height / 2) == exported.GetPixel(exported.Width / 2, exported.Height / 2), "decoded image pixels unchanged");
        }
    }

    private static Tuple<int, int, int> XmpRange(byte[] bytes)
    {
        byte[] signature = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");
        for (int i = 2; i + 4 + signature.Length <= bytes.Length; i++)
        {
            if (bytes[i] == 0xff && bytes[i + 1] == 0xe1 && bytes.Skip(i + 4).Take(signature.Length).SequenceEqual(signature))
            { int end = i + 2 + (bytes[i + 2] << 8) + bytes[i + 3]; return Tuple.Create(i, i + 4 + signature.Length, end); }
        }
        throw new Exception("XMP not found");
    }

    private static byte[] RemoveXmp(byte[] bytes)
    {
        var range = XmpRange(bytes);
        return bytes.Take(range.Item1).Concat(bytes.Skip(range.Item3)).ToArray();
    }

    private static XDocument Packet(byte[] bytes)
    {
        var range = XmpRange(bytes);
        return XDocument.Parse(Encoding.UTF8.GetString(bytes, range.Item2, range.Item3 - range.Item2));
    }

    private static byte[] ReplacePacket(byte[] bytes, XDocument packet)
    {
        var range = XmpRange(bytes);
        byte[] header = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");
        byte[] xml = Encoding.UTF8.GetBytes(packet.ToString(SaveOptions.DisableFormatting));
        int size = header.Length + xml.Length + 2;
        return bytes.Take(range.Item1).Concat(new byte[] { 0xff, 0xe1, (byte)(size >> 8), (byte)size }).Concat(header).Concat(xml).Concat(bytes.Skip(range.Item3)).ToArray();
    }

    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception("FAIL: " + message);
        checks++;
    }
}
