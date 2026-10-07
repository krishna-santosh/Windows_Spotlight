using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Windows_Spotlight
{
    // Insert or update an APP1 XMP packet; JPEG coding tables and compressed pixels stay byte-for-byte intact.
    internal static class JpegMetadata
    {
        internal static readonly XNamespace Spotlight = "https://github.com/krishna-santosh/Windows_Spotlight/ns/1.0/";
        private static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
        private static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
        private static readonly byte[] XmpHeader = Encoding.ASCII.GetBytes("http://ns.adobe.com/xap/1.0/\0");

        private sealed class Segment
        {
            internal int Start;
            internal int End;
            internal int Payload;
            internal bool IsXmp;
        }

        internal static string Hash(byte[] bytes)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
        }

        internal static bool IsJpeg(byte[] bytes) { return bytes.Length >= 4 && bytes[0] == 0xff && bytes[1] == 0xd8; }

        internal static string ReadOriginalHash(byte[] jpeg)
        {
            if (!IsJpeg(jpeg)) return null;
            foreach (Segment segment in Segments(jpeg).Where(s => s.IsXmp))
            {
                XDocument document = ReadPacket(jpeg, segment);
                if (document == null) continue;
                string hash = document.Descendants(Spotlight + "OriginalSha256").Select(e => e.Value).FirstOrDefault()
                    ?? document.Descendants().Attributes(Spotlight + "OriginalSha256").Select(a => a.Value).FirstOrDefault();
                if (hash != null && hash.Length == 64 && hash.All(Uri.IsHexDigit)) return hash.ToLowerInvariant();
            }
            return null;
        }

        internal static byte[] Embed(byte[] jpeg, string originalHash, ImageInfo info, string source, string assetName)
        {
            var segments = Segments(jpeg);
            Segment existing = segments.FirstOrDefault(s => s.IsXmp);
            XDocument document = existing == null ? null : ReadPacket(jpeg, existing);
            if (existing != null && document == null) throw new InvalidDataException("The existing XMP packet is invalid; preserving the original file.");
            if (document == null)
                document = new XDocument(new XElement(XName.Get("xmpmeta", "adobe:ns:meta/"),
                    new XAttribute(XNamespace.Xmlns + "x", "adobe:ns:meta/"), new XElement(Rdf + "RDF")));
            XElement rdf = document.Descendants(Rdf + "RDF").FirstOrDefault();
            if (rdf == null) throw new InvalidDataException("The existing XMP packet has no RDF container.");
            var description = new XElement(Rdf + "Description", new XAttribute(Rdf + "about", ""),
                new XAttribute(XNamespace.Xmlns + "spotlight", Spotlight.NamespaceName),
                new XAttribute(XNamespace.Xmlns + "dc", Dc.NamespaceName));
            rdf.Add(description);
            Set(document, description, Spotlight + "OriginalSha256", originalHash);
            Set(document, description, Spotlight + "Source", source);
            Set(document, description, Spotlight + "AssetName", assetName);
            if (info != null)
            {
                SetAlternative(document, description, Dc + "title", info.Name);
                SetAlternative(document, description, Dc + "description", info.Description);
                SetAlternative(document, description, Dc + "rights", info.Copyright);
                Set(document, description, Spotlight + "LearnMore", info.LearnMore);
            }
            byte[] xml = Encoding.UTF8.GetBytes(document.ToString(SaveOptions.DisableFormatting));
            int length = 2 + XmpHeader.Length + xml.Length;
            if (length > ushort.MaxValue) throw new InvalidDataException("Image metadata exceeds the JPEG XMP packet limit.");
            using (var output = new MemoryStream())
            {
                // Keep JFIF/EXIF headers in their original position. Replace existing XMP in-place when present.
                int insertion = existing == null ? segments.Last().Start : existing.Start;
                output.Write(jpeg, 0, insertion);
                output.WriteByte(0xff);
                output.WriteByte(0xe1);
                output.WriteByte((byte)(length >> 8));
                output.WriteByte((byte)length);
                output.Write(XmpHeader, 0, XmpHeader.Length);
                output.Write(xml, 0, xml.Length);
                int rest = existing == null ? insertion : existing.End;
                output.Write(jpeg, rest, jpeg.Length - rest);
                return output.ToArray();
            }
        }

        private static void Set(XDocument document, XElement description, XName name, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            document.Descendants(name).Remove();
            document.Descendants().Attributes(name).Remove();
            description.Add(new XElement(name, Clean(value)));
        }

        private static void SetAlternative(XDocument document, XElement description, XName name, string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            document.Descendants(name).Remove();
            document.Descendants().Attributes(name).Remove();
            description.Add(new XElement(name, new XElement(Rdf + "Alt",
                new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), Clean(value)))));
        }

        private static string Clean(string value)
        {
            var result = new StringBuilder();
            for (int i = 0; i < value.Length && result.Length < 8000; i++)
            {
                char c = value[i];
                if (XmlConvert.IsXmlChar(c)) result.Append(c);
                else if (char.IsHighSurrogate(c) && i + 1 < value.Length && char.IsLowSurrogate(value[i + 1]))
                { result.Append(c); result.Append(value[++i]); }
            }
            return result.ToString();
        }

        private static XDocument ReadPacket(byte[] jpeg, Segment segment)
        {
            try
            {
                int start = segment.Payload + XmpHeader.Length;
                using (var stream = new MemoryStream(jpeg, start, segment.End - start))
                using (var reader = XmlReader.Create(stream, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null }))
                    return XDocument.Load(reader);
            }
            catch (XmlException) { return null; }
        }

        private static List<Segment> Segments(byte[] jpeg)
        {
            if (!IsJpeg(jpeg)) throw new InvalidDataException("Not a JPEG image.");
            var result = new List<Segment>();
            int offset = 2;
            while (offset < jpeg.Length)
            {
                int start = offset;
                if (jpeg[offset++] != 0xff) throw new InvalidDataException("Invalid JPEG marker.");
                while (offset < jpeg.Length && jpeg[offset] == 0xff) offset++;
                if (offset >= jpeg.Length) break;
                byte marker = jpeg[offset++];
                if (marker == 0xda || marker == 0xd9)
                {
                    result.Add(new Segment { Start = start, End = jpeg.Length });
                    return result;
                }
                if (marker == 0x01 || (marker >= 0xd0 && marker <= 0xd7))
                { result.Add(new Segment { Start = start, End = offset }); continue; }
                if (offset + 2 > jpeg.Length) break;
                int length = (jpeg[offset] << 8) | jpeg[offset + 1];
                if (length < 2 || length > jpeg.Length - offset) break;
                int payload = offset + 2;
                int end = offset + length;
                bool xmp = marker == 0xe1 && end - payload >= XmpHeader.Length;
                if (xmp)
                    for (int i = 0; i < XmpHeader.Length; i++)
                        if (jpeg[payload + i] != XmpHeader[i]) { xmp = false; break; }
                result.Add(new Segment { Start = start, End = end, Payload = payload, IsXmp = xmp });
                offset = end;
            }
            throw new InvalidDataException("Truncated JPEG headers.");
        }
    }
}
