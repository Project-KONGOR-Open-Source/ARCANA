string[] directories = Directory.GetDirectories(Directory.GetCurrentDirectory()).OrderByDescending(path => new Version(new DirectoryInfo(path).Name)).ToArray();

for (int i = 0; i < directories.Length - 1; i++)
{
    string[] files = Directory.GetFiles(directories[i]);

    foreach (string file in files)
        File.Copy(file, Path.Combine(directories[i + 1], Path.GetFileName(file)), true);

    string currentVersion = new DirectoryInfo(directories[i]).Name;
    string previousVersion = new DirectoryInfo(directories[i + 1]).Name;

    Console.WriteLine($@"{files.Length} Files From Version ""{currentVersion}"" Applied On Top Of Version ""{previousVersion}""");

    Directory.Delete(directories[i], true);
}

string first = directories.Last();
string latest = directories.First();

Directory.Move(first, latest);

Console.WriteLine($"{directories.Length} Versions Consolidated");
