using System.IO.Compression;

namespace Distribution.UnpackFiles;

/// <summary>
///     Unpacks the zip files in the directory and deletes the zip files after unpacking. 
///     If the "bundleResourceFiles" flag is set to true, it will also bundle resource files into individual zip files.
///     This is useful for preparing the files for distribution to end users.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.UnpackFiles.exe "path/to/directory" false
///     </code>
/// </remarks>
internal class UnpackFiles
{
    internal static void Main(string[] args)
    {
        string parentDirectory = args.Length is 1 ? args.Single() : args.Length is 2 ? args.First() : Environment.CurrentDirectory;

        bool bundleResourceFiles = args.Length is 2 ? bool.Parse(args.Last()) : false;

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

            string[] temps = Directory.GetFiles(parentDirectory, "*.temp", SearchOption.AllDirectories);

            if (temps.Any())
            {
                Console.WriteLine(@"Orphaned Temp Resource Files Found; Rename The "".temp"" Extension To "".s2z"" Manually");

                foreach (string temp in temps)
                    Console.WriteLine($"Orphaned Temp Resource File: {temp}");
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
