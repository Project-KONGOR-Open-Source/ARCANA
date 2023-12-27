using System.Xml;

namespace Distribution.CheckFileSizes;

internal class CheckFileSizes
{
    internal static void Main(string[] args)
    {
        XmlDocument xml = new();
        xml.Load("manifest.xml");

        XmlNodeList files = xml.GetElementsByTagName("file");

        string parentDirectory = args.Length is 1 ? args.Single() : Environment.CurrentDirectory;

        string rootPath = Path.Combine(parentDirectory);

        for (int i = 0; i < files.Count; i++)
        {
            XmlNode file = files[i] ?? throw new NullReferenceException($"File Index {i} Is NULL");

            string filePath = Path.Combine(rootPath, file.Attributes!["path"]!.Value + ".zip");

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
}
