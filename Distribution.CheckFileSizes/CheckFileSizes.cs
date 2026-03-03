using System.IO.Compression;
using System.Xml;

namespace Distribution.CheckFileSizes;

/// <summary>
///     Checks the file sizes of the zip files in the manifest against the actual zip files in the directory.
///     If the file sizes do not match, it will print the file path and the expected and actual file sizes.
///     This is useful for verifying that the files in the manifest are correct and have not been corrupted during the distribution process.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.CheckFileSizes.exe "path/to/directory"
///     </code>
/// </remarks>
internal class CheckFileSizes
{
    internal static void Main(string[] args)
    {
        string parentDirectory = args.Length is 1 ? args.Single() : Environment.CurrentDirectory;

        string manifestZipFile = Path.Combine(parentDirectory, "manifest.xml.zip");
        string manifestFile = Path.Combine(parentDirectory, "manifest.xml");

        ZipFile.ExtractToDirectory(manifestZipFile, parentDirectory, true);

        try
        {
            XmlDocument xml = new();

            xml.Load(manifestFile);

            XmlNodeList files = xml.GetElementsByTagName("file");

            for (int i = 0; i < files.Count; i++)
            {
                XmlNode file = files[i] ?? throw new NullReferenceException($"File Index {i} Is NULL");

                string filePath = Path.Combine(parentDirectory, file.Attributes!["path"]!.Value + ".zip");

                long manifestZipSize = long.Parse(file.Attributes!["zipsize"]!.Value);
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
