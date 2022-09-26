using System;

namespace Windows_Spotlight.Constants
{
    internal static class Paths
    {
        internal readonly static string SOURCE = Environment.GetEnvironmentVariable("localappdata") + @"\Packages\Microsoft.Windows.ContentDeliveryManager_cw5n1h2txyewy\LocalState\Assets\";
        internal static string DESTINATION = Environment.GetEnvironmentVariable("userprofile") + @"\Pictures\Windows_Spotlight_Images_2";
        internal static string PORTRAIT = DESTINATION + @"\Portrait\";
        internal static string LANDSCAPE = DESTINATION + @"\Landscape\";
    }
}
