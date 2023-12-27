using System.IO.Compression;
using System.Xml;

string[] files = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.zip", SearchOption.AllDirectories);

foreach (string file in files)
{
    await Task.Run(() => ZipFile.ExtractToDirectory(file, Directory.GetParent(file)?.ToString() ?? throw new NullReferenceException($@"The Parent Of File ""{file}"" Is NULL"), true));
    await Task.Run(() => File.Delete(file));
}

XmlDocument xml = new();
xml.Load("manifest.xml");

string version = xml.DocumentElement!.GetAttribute("version");

using (ZipArchive archive = ZipFile.Open($"{version}.manifest.xml.zip", ZipArchiveMode.Create))
    archive.CreateEntryFromFile("manifest.xml", "manifest.xml");

File.Delete("manifest.xml");

Console.WriteLine($"{files.Length} ZIP Files Processed");

string[] resources = Directory.GetDirectories(Directory.GetCurrentDirectory(), "*.s2z", SearchOption.AllDirectories);

foreach (string resource in resources)
{
    await Task.Run(() => ZipFile.CreateFromDirectory(resource, $"{resource}.temp"));
    await Task.Run(() => Directory.Delete(resource, true));
    await Task.Run(() => File.Move($"{resource}.temp", resource));
}

string[] temps = Directory.GetFiles(Directory.GetCurrentDirectory(), "*.temp", SearchOption.AllDirectories);

if (temps.Any())
{
    Console.WriteLine(@"Orphaned Temp Resource Files Found; Rename The "".temp"" Extension To "".s2z"" Manually");

    foreach (string temp in temps)
        Console.WriteLine($"Orphaned Temp Resource File: {temp}");
}

Console.WriteLine($"{resources.Length} Resource Files Processed");
