using System.IO.Compression;
using System.Text.RegularExpressions;

namespace Distribution.UnpackResourceFiles;

/// <summary>
///     Unpacks S2Z resource archives (ZIP format) into directories with an .s2z suffix.
///     Each archive is deleted after successful extraction. Failed operations are retried with exponential backoff.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.UnpackResourceFiles.exe "path/to/directory"
///     </code>
///
///     Optional 2nd argument: maximum retry attempts per archive (default: 5).
/// </remarks>
internal partial class UnpackResourceFiles
{
    private const int DefaultMaximumRetries = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    internal static int Main(string[] arguments)
    {
        string parentDirectory = arguments.Length >= 1 ? arguments[0] : Environment.CurrentDirectory;
        int maximumRetries = arguments.Length >= 2 ? int.Parse(arguments[1]) : DefaultMaximumRetries;

        string[] archives = Directory.GetFiles(parentDirectory, "*.s2z", SearchOption.AllDirectories);

        if (archives.Length is 0)
        {
            Console.WriteLine("No S2Z Archives Found");

            return 0;
        }

        Dictionary<string, string> outputDirectoryByArchive = BuildOutputDirectoryMap(archives);

        int succeeded = 0;
        int failed = 0;

        foreach (string archive in archives)
        {
            string outputDirectory = outputDirectoryByArchive[archive];

            Console.WriteLine($@"Unpacking ""{archive}"" → ""{outputDirectory}""");

            if (ExtractWithRetry(archive, outputDirectory, maximumRetries))
            {
                DeleteFileWithRetry(archive, maximumRetries);

                succeeded++;
            }

            else
            {
                Console.WriteLine($@"Failed To Unpack ""{archive}"" After {maximumRetries} Attempts");

                failed++;
            }
        }

        Console.WriteLine($"{succeeded} Archives Unpacked Successfully; {failed} Failed");

        return failed > 0 ? 1 : 0;
    }

    /// <summary>
    ///     Builds a mapping from each archive path to its output directory path, applying a missing-index rule.
    ///     Archives that belong to a split set and have no trailing digit get index 1 appended.
    /// </summary>
    private static Dictionary<string, string> BuildOutputDirectoryMap(string[] archives)
    {
        Dictionary<string, string> map = new(StringComparer.OrdinalIgnoreCase);

        IEnumerable<IGrouping<string, string>> archivesByDirectory = archives.GroupBy(Path.GetDirectoryName, StringComparer.OrdinalIgnoreCase)!;

        foreach (IGrouping<string, string> group in archivesByDirectory)
        {
            string directory = group.Key!;

            Dictionary<string, List<string>> splitGroups = new(StringComparer.OrdinalIgnoreCase);

            foreach (string archive in group)
            {
                string fileName = Path.GetFileNameWithoutExtension(archive);
                string baseName = TrailingDigitsPattern().Replace(fileName, string.Empty);

                if (splitGroups.TryGetValue(baseName, out List<string>? members) is false)
                {
                    members = [];
                    splitGroups[baseName] = members;
                }

                members.Add(archive);
            }

            foreach (string archive in group)
            {
                string fileName = Path.GetFileNameWithoutExtension(archive);
                string baseName = TrailingDigitsPattern().Replace(fileName, string.Empty);
                bool hasTrailingDigit = fileName.Length > baseName.Length;
                bool isSplitSet = splitGroups[baseName].Count > 1;

                string outputName = isSplitSet && hasTrailingDigit is false
                    ? $"{baseName}1.s2z"
                    : $"{fileName}.s2z";

                map[archive] = Path.Combine(directory, outputName);
            }
        }

        return map;
    }

    /// <summary>
    ///     Extracts a ZIP archive to the specified directory with retry and exponential backoff.
    /// </summary>
    private static bool ExtractWithRetry(string archivePath, string outputDirectory, int maximumRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maximumRetries; attempt++)
        {
            try
            {
                Directory.CreateDirectory(outputDirectory);

                ZipFile.ExtractToDirectory(archivePath, outputDirectory, overwriteFiles: true);

                return true;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maximumRetries} Failed For ""{archivePath}"": {exception.Message}");

                if (attempt < maximumRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        return false;
    }

    /// <summary>
    ///     Deletes a file with retry and exponential backoff to handle transient file locks.
    /// </summary>
    private static void DeleteFileWithRetry(string filePath, int maximumRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maximumRetries; attempt++)
        {
            try
            {
                File.Delete(filePath);

                return;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maximumRetries} To Delete ""{filePath}"" Failed: {exception.Message}");

                if (attempt < maximumRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        Console.WriteLine($@"Failed To Delete ""{filePath}"" After {maximumRetries} Attempts");
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex TrailingDigitsPattern();
}
