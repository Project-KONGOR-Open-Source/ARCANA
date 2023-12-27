string[] directories = Directory.GetDirectories(Directory.GetCurrentDirectory()).OrderByDescending(path => new Version(new DirectoryInfo(path).Name)).ToArray();

string first = directories.Last();
string latest = directories.First();

for (int i = 0; i < directories.Length - 1; i++)
{
    string[] files = Directory.GetFiles(directories[i], "*.*", SearchOption.AllDirectories);

    string currentVersion = new DirectoryInfo(directories[i]).Name;
    string previousVersion = new DirectoryInfo(directories[i + 1]).Name;

    foreach (string file in files)
    {
        string destinationFile = file.Replace(currentVersion, previousVersion);

        string destinationDirectory = Path.GetDirectoryName(destinationFile);

        if (Directory.Exists(destinationDirectory) is false)
            Directory.CreateDirectory(destinationDirectory);

        File.Move(file, destinationFile, true);
    }

    string[] leftovers = Directory.GetFiles(directories[i], "*.*", SearchOption.AllDirectories);

    if (leftovers.Any())
        Console.WriteLine($@"[ERROR] {leftovers.Length} Leftover Files In Version ""{currentVersion}""");

    else
    {
        Directory.Delete(directories[i], true);

        Console.WriteLine($@"{files.Length} Files From Version ""{currentVersion}"" Applied On Top Of Version ""{previousVersion}""");
    }
}

Directory.Move(first, latest);

Console.WriteLine($"{directories.Length} Versions Consolidated");
