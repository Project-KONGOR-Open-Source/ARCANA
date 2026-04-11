using System.IO.Compression;
using System.Xml;

namespace Archive.CheckFileSizes;

/// <summary>
///     Checks the file sizes of the zip files in the manifest against the actual zip files in the directory.
///     If the file sizes do not match, it will print the file path and the expected and actual file sizes.
///     This is useful for verifying that the files in the manifest are correct and have not been corrupted during the archive unpacking process.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Archive.CheckFileSizes.exe "path/to/directory"
///     </code>
/// </remarks>
internal class CheckFileSizes
{
    internal static void Main(string[] arguments)
    {
        if (arguments is ["--help" or "-h"])
        {
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  Checks file sizes of zip files against a manifest to verify archive integrity.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Archive.CheckFileSizes [directory]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  [directory]  directory containing manifest.xml.zip and zip files (default: current directory)");
            Console.WriteLine();

            return;
        }

        string parentDirectory = arguments.Length is 1 ? arguments.Single() : Environment.CurrentDirectory;

        string manifestZipFile = Path.Combine(parentDirectory, "manifest.xml.zip");
        string manifestFile = Path.Combine(parentDirectory, "manifest.xml");

        ZipFile.ExtractToDirectory(manifestZipFile, parentDirectory, true);

        try
        {
            XmlDocument xmlDocument = new();

            xmlDocument.Load(manifestFile);

            XmlNodeList files = xmlDocument.GetElementsByTagName("file");

            for (int index = 0; index < files.Count; index++)
            {
                XmlNode file = files[index] ?? throw new NullReferenceException($"File At Index {index} Is NULL");

                XmlAttributeCollection attributes = file.Attributes
                    ?? throw new InvalidOperationException($"File At Index {index} Has No Attributes");

                string path = attributes["path"]?.Value
                    ?? throw new InvalidOperationException($@"File At Index {index} Is Missing ""path"" Attribute");

                string zipSize = attributes["zipsize"]?.Value
                    ?? throw new InvalidOperationException($@"File At Index {index} Is Missing ""zipsize"" Attribute");

                string filePath = Path.Combine(parentDirectory, path + ".zip");

                long manifestZipSize = long.Parse(zipSize);
                long fileZipSize = new FileInfo(filePath).Length;

                if (fileZipSize.Equals(manifestZipSize).Equals(false))
                {
                    Console.WriteLine($"File Path: {filePath}");
                    Console.WriteLine($"Manifest Zip Size {manifestZipSize} Does Not Match File Zip Size {fileZipSize}");
                }
            }

            Console.WriteLine($"{files.Count} Files Processed");
        }

        finally
        {
            File.Delete(manifestFile);
        }
    }
}
