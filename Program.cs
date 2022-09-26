using System;
using static Windows_Spotlight.ProgramHelpers;

namespace Windows_Spotlight
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            CreateDirectories();
            CopyFiles();
            Console.WriteLine($"Done! Copied {GetImageCount()} images.");
            //OpenFolder();
        }
    }
}
