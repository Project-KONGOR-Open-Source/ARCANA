namespace Archive.ConsolidateVersions;

/// <summary>
///     Consolidates multiple versions of a project into a single version by moving files from each version on top of the previous version, starting from the oldest version to the latest version.
///     After all versions have been consolidated, the oldest version is renamed to the latest version.
///     This process helps to maintain a clean and organized directory structure while preserving the history of changes across different versions.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Archive.ConsolidateVersions.exe "path/to/directory"
///     </code>
/// </remarks>
internal class ConsolidateVersions
{
    internal static void Main(string[] args)
    {
        string parentDirectory = args.Length is 1 ? args.Single() : Environment.CurrentDirectory;

        string[] directories = Directory.GetDirectories(parentDirectory).OrderByDescending(path => new Version(new DirectoryInfo(path).Name)).ToArray();

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

                string destinationDirectory = Path.GetDirectoryName(destinationFile) ?? throw new NullReferenceException($@"Directory Name For File ""{destinationFile}"" Is NULL");

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
    }
}
