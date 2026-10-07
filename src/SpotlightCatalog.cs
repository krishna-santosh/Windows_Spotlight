using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Web.Script.Serialization;
using Microsoft.Win32;

namespace Windows_Spotlight
{
    internal enum ImageSource { LockScreen, Desktop, All }

    internal sealed class ImageInfo
    {
        internal string Name;
        internal string Description;
        internal string Copyright;
        internal string LearnMore;
    }

    internal sealed class SpotlightImage
    {
        internal string Path;
        internal string Source;
    }

    internal sealed class SpotlightCatalog
    {
        private readonly string packageRoot;
        private readonly bool includeDesktopRegistry;
        private readonly Dictionary<string, ImageInfo> byPath = new Dictionary<string, ImageInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, ImageInfo> byHash = new Dictionary<string, ImageInfo>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> desktopPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> defaultPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        internal SpotlightCatalog(string localAppData, bool includeDesktopRegistry = true)
        {
            packageRoot = System.IO.Path.Combine(localAppData, "Packages");
            this.includeDesktopRegistry = includeDesktopRegistry;
        }

        internal IEnumerable<SpotlightImage> Discover(ImageSource source)
        {
            string state = System.IO.Path.Combine(packageRoot, "Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy", "LocalState");
            foreach (string file in Files(System.IO.Path.Combine(state, "TargetedContentCache"))) ReadMetadata(file);
            foreach (string file in Files(System.IO.Path.Combine(state, "ContentManagementSDK", "Creatives", "338387"))) ReadMetadata(file);

            if (source != ImageSource.Desktop)
                foreach (string file in Files(System.IO.Path.Combine(state, "Assets"), false))
                    yield return new SpotlightImage { Path = file, Source = "lockscreen" };

            if (source == ImageSource.LockScreen) yield break;

            foreach (string file in Files(System.IO.Path.Combine(state, "ContentManagementSDK", "Creatives", "88000326"))) ReadMetadata(file);
            if (includeDesktopRegistry) ReadDesktopRegistry();
            string iris = System.IO.Path.Combine(packageRoot, "MicrosoftWindows.Client.CBS_cw5n1h2txyewy", "LocalCache", "Microsoft", "IrisService");
            foreach (string file in Files(iris))
            {
                if (string.Equals(System.IO.Path.GetExtension(file), ".jpg", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(System.IO.Path.GetExtension(file), ".jpeg", StringComparison.OrdinalIgnoreCase))
                    desktopPaths.Add(file);
            }

            var available = desktopPaths.Where(File.Exists).ToList();
            // Windows has bundled defaults even when Desktop Spotlight has no downloaded cache.
            if (available.Count == 0) available.AddRange(defaultPaths.Where(File.Exists));
            foreach (string file in available)
                yield return new SpotlightImage { Path = file, Source = "desktop" };
        }

        internal ImageInfo FindInfo(string path, string hash)
        {
            ImageInfo info;
            return byPath.TryGetValue(path, out info) || byHash.TryGetValue(hash, out info) ? info : null;
        }

        private void ReadMetadata(string path)
        {
            try { ParseMetadata(File.ReadAllText(path)); }
            catch (Exception ex) when (IsReadError(ex)) { }
        }

        internal void ParseMetadata(string json, bool isDefault = false)
        {
            if (string.IsNullOrWhiteSpace(json)) return;
            try
            {
                var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024, RecursionLimit = 100 };
                Visit(serializer.DeserializeObject(json.TrimStart('\uFEFF')), isDefault, 0);
            }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { }
        }

        private void Visit(object value, bool isDefault, int depth)
        {
            if (depth > 30) return;
            var node = value as Dictionary<string, object>;
            if (node != null)
            {
                string name = Text(node, "name");
                if (name == "LockScreen" || name == "DesktopSpotlight") ReadContent(node, name == "DesktopSpotlight", isDefault);
                else if (node.ContainsKey("landscapeImage") && (node.ContainsKey("iconHoverText") || node.ContainsKey("copyright"))) ReadDesktopCreative(node, isDefault);
                foreach (var pair in node)
                {
                    if (pair.Key == "item" && pair.Value is string)
                    {
                        try
                        {
                            var serializer = new JavaScriptSerializer { MaxJsonLength = 4 * 1024 * 1024 };
                            Visit(serializer.DeserializeObject((string)pair.Value), isDefault, depth + 1);
                        }
                        catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException) { }
                    }
                    else Visit(pair.Value, isDefault, depth + 1);
                }
            }
            else if (value is IEnumerable && !(value is string))
                foreach (object item in (IEnumerable)value) Visit(item, isDefault, depth + 1);
        }

        private void ReadContent(Dictionary<string, object> node, bool desktop, bool isDefault)
        {
            var properties = Map(node, "properties");
            var info = new ImageInfo();
            var captions = new List<string>();
            object items;
            if (node.TryGetValue("items", out items) && items is IEnumerable)
            {
                foreach (object item in (IEnumerable)items)
                {
                    var itemMap = item as Dictionary<string, object>;
                    var p = Map(itemMap, "properties");
                    string template = WrappedText(p, "template");
                    if (template == "infoHotspot")
                    {
                        info.Name = WrappedText(p, "description");
                        info.Copyright = WrappedText(p, "copyright");
                        info.LearnMore = Text(Map(Map(p, "onClick"), "parameters"), "uri");
                    }
                    else if (template == "desktopIcon")
                    {
                        string hover = WrappedText(p, "hoverText");
                        info.Name = FirstLine(hover);
                        if (hover != null) info.Copyright = hover.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).FirstOrDefault();
                        info.LearnMore = Text(Map(Map(p, "onClick"), "parameters"), "uri");
                    }
                    string caption = WrappedText(p, "title");
                    if (!string.IsNullOrWhiteSpace(caption) && caption != "Learn about this picture") captions.Add(caption);
                }
            }
            info.Description = string.Join(Environment.NewLine, captions);
            Register(Map(properties, "landscapeImage"), info, desktop, isDefault);
            Register(Map(properties, "portraitImage"), info, desktop, isDefault);
        }

        private void ReadDesktopCreative(Dictionary<string, object> node, bool isDefault)
        {
            var info = new ImageInfo
            {
                Name = FirstLine(Text(node, "iconHoverText")) ?? Text(node, "title"),
                Description = Text(node, "description"),
                Copyright = Text(node, "copyright"),
                LearnMore = Text(node, "ctaUri")
            };
            Register(Map(node, "landscapeImage"), info, true, isDefault);
            Register(Map(node, "portraitImage"), info, true, isDefault);
        }

        private void Register(Dictionary<string, object> image, ImageInfo info, bool desktop, bool isDefault)
        {
            if (image == null) return;
            string path = Text(image, "image") ?? Text(image, "asset");
            if (!string.IsNullOrEmpty(path) && System.IO.Path.IsPathRooted(path))
            {
                byPath[path] = info;
                if (desktop) (isDefault ? defaultPaths : desktopPaths).Add(path);
            }
            string hash = Text(image, "sha256");
            if (!string.IsNullOrEmpty(hash))
            {
                try
                {
                    byte[] bytes = Convert.FromBase64String(hash);
                    if (bytes.Length == 32) byHash[BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant()] = info;
                }
                catch (FormatException) { }
            }
        }

        private void ReadDesktopRegistry()
        {
            try
            {
                using (RegistryKey root = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\DesktopSpotlight"))
                    ReadRegistry(root);
            }
            catch (Exception ex) when (IsReadError(ex)) { }
        }

        private void ReadRegistry(RegistryKey key)
        {
            if (key == null) return;
            foreach (string name in key.GetValueNames())
            {
                string value = key.GetValue(name) as string;
                if (string.IsNullOrEmpty(value)) continue;
                string trimmed = value.TrimStart();
                if (trimmed.StartsWith("{") || trimmed.StartsWith("[")) ParseMetadata(value, name == "DefaultCreatives");
                else if ((name.IndexOf("landscape", StringComparison.OrdinalIgnoreCase) >= 0
                    || name.IndexOf("portrait", StringComparison.OrdinalIgnoreCase) >= 0) && File.Exists(value)) desktopPaths.Add(value);
            }
            foreach (string name in key.GetSubKeyNames())
                using (RegistryKey child = key.OpenSubKey(name)) ReadRegistry(child);
        }

        internal static IEnumerable<string> Files(string directory, bool recursive = true)
        {
            string[] files;
            string[] children;
            try
            {
                if (!Directory.Exists(directory)) yield break;
                files = Directory.GetFiles(directory);
                children = recursive ? Directory.GetDirectories(directory) : new string[0];
            }
            catch (Exception ex) when (IsReadError(ex)) { yield break; }
            foreach (string file in files) yield return file;
            foreach (string child in children)
                foreach (string file in Files(child)) yield return file;
        }

        internal static bool IsReadError(Exception ex)
        {
            return ex is IOException || ex is UnauthorizedAccessException || ex is SecurityException;
        }

        private static Dictionary<string, object> Map(Dictionary<string, object> node, string key)
        {
            object value;
            return node != null && node.TryGetValue(key, out value) ? value as Dictionary<string, object> : null;
        }

        private static string Text(Dictionary<string, object> node, string key)
        {
            object value;
            return node != null && node.TryGetValue(key, out value) ? value as string : null;
        }

        private static string WrappedText(Dictionary<string, object> node, string key) { return Text(Map(node, key), "text"); }
        private static string FirstLine(string value) { return string.IsNullOrWhiteSpace(value) ? null : value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0]; }
    }
}
