using System.IO.Compression;

namespace Archive.UnpackFiles;

/// <summary>
///     Unpacks the zip files in the directory and deletes the zip files after unpacking.
///     If the "bundleResourceFiles" flag is set to true, it will also bundle resource files into individual zip files.
///     This is useful for preparing the files for distribution to end users from a consolidated archive.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Archive.UnpackFiles.exe "path/to/directory" false
///     </code>
/// </remarks>
internal class UnpackFiles
{
    internal static void Main(string[] arguments)
    {
        if (arguments is ["--help" or "-h"])
        {
            Console.WriteLine("Description:");
            Console.WriteLine("  Unpacks zip files in a directory and deletes them after unpacking.");
            Console.WriteLine("  Optionally bundles resource files into individual zip archives.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Archive.UnpackFiles [directory] [bundleResourceFiles]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  [directory]            directory containing zip files (default: current directory)");
            Console.WriteLine("  [bundleResourceFiles]  true/false — bundle .s2z resource files into zip archives (default: false)");

            return;
        }

        string parentDirectory = arguments.Length is 1 ? arguments.Single() : arguments.Length is 2 ? arguments.First() : Environment.CurrentDirectory;

        bool bundleResourceFiles = arguments.Length is 2 ? bool.Parse(arguments.Last()) : false;

        string[] files = Directory.GetFiles(parentDirectory, "*.zip", SearchOption.AllDirectories);

        foreach (string file in files)
        {
            string fileParentDirectory = Directory.GetParent(file)?.ToString() ?? throw new NullReferenceException($@"The Parent Of File ""{file}"" Is NULL");

            ZipFile.ExtractToDirectory(file, fileParentDirectory, true);
            File.Delete(file);
        }

        Console.WriteLine($"{files.Length} ZIP Files Processed");

        string[] resources = Directory.GetDirectories(parentDirectory, "*.s2z", SearchOption.AllDirectories);

        if (bundleResourceFiles)
        {
            foreach (string resource in resources)
            {
                ZipFile.CreateFromDirectory(resource, $"{resource}.temp");
                Directory.Delete(resource, true);
                File.Move($"{resource}.temp", resource);
            }

            string[] temporaryFiles = Directory.GetFiles(parentDirectory, "*.temp", SearchOption.AllDirectories);

            if (temporaryFiles.Any())
            {
                Console.WriteLine(@"Orphaned Temp Resource Files Found; Rename The "".temp"" Extension To "".s2z"" Manually");

                foreach (string temporaryFile in temporaryFiles)
                    Console.WriteLine($"Orphaned Temp Resource File: {temporaryFile}");
            }
        }

        else
        {
            foreach (string resource in resources)
                Directory.Move(resource, Path.ChangeExtension(resource, null));
        }

        Console.WriteLine($"{resources.Length} Resource Files Processed");
    }
}
