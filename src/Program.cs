using System;

using static Windows_Spotlight.ProgramHelpers;

namespace Windows_Spotlight
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            bool open = false;
            bool executeProgram = true;

            if (args.Length > 0)
            {
                switch (args[0])
                {
                    case "-V":
                    case "--version":
                        Console.WriteLine($"\nWindows Spotlight v{GetVersion()}\n");
                        executeProgram = false;
                        break;

                    case "-O":
                    case "--open-folder":
                        open = true;
                        break;

                    case "-h":
                    case "--help":
                    default:
                        Help();
                        executeProgram = false;
                        break;

                }
            }

            if (executeProgram)
            {
                CreateDirectories();
                CopyFiles();

                if (GetImageCount() > 0)
                {
                    Console.WriteLine($"Yay! Got {GetImageCount()} new images.\n");
                }
                else
                {
                    Console.WriteLine("Nothing new yet, see ya later!\n");
                }

                if (open)
                {
                    OpenFolder();
                }
            }
        }
    }
}
