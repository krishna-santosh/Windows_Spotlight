using System;
using System.IO;

namespace Windows_Spotlight.Constants
{
    internal static class Paths
    {
        internal static readonly string DESTINATION = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Windows_Spotlight_Images");
    }
}
