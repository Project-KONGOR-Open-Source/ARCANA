using System.IO.Compression;

namespace Distribution.PackResourceFiles;

/// <summary>
///     Packs directories with an .s2z suffix back into S2Z archives (ZIP format).
///     Each source directory is deleted after the archive is successfully created. Failed operations are retried with exponential backoff.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.PackResourceFiles.exe "path/to/directory"
///     </code>
///
///     Optional 2nd argument: maximum retry attempts per directory (default: 5).
/// </remarks>
internal class PackResourceFiles
{
    private const int DefaultMaxRetries = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    internal static int Main(string[] args)
    {
        string parentDirectory = args.Length >= 1 ? args[0] : Environment.CurrentDirectory;
        int maxRetries = args.Length >= 2 ? int.Parse(args[1]) : DefaultMaxRetries;

        string[] resourceDirectories = Directory.GetDirectories(parentDirectory, "*.s2z", SearchOption.AllDirectories);

        if (resourceDirectories.Length is 0)
        {
            Console.WriteLine("No S2Z Resource Directories Found");

            return 0;
        }

        int succeeded = 0;
        int failed = 0;

        foreach (string resourceDirectory in resourceDirectories)
        {
            string archivePath = resourceDirectory;
            string tempArchivePath = $"{resourceDirectory}.tmp";

            Console.WriteLine($@"Packing ""{resourceDirectory}"" → ""{archivePath}""");

            if (PackWithRetry(resourceDirectory, tempArchivePath, maxRetries))
            {
                if (DeleteDirectoryWithRetry(resourceDirectory, maxRetries))
                {
                    MoveFileWithRetry(tempArchivePath, archivePath, maxRetries);

                    succeeded++;
                }

                else
                {
                    Console.WriteLine($@"Failed To Delete Directory ""{resourceDirectory}"" After {maxRetries} Attempts; Cleaning Up Temp Archive");

                    DeleteFileWithRetry(tempArchivePath, maxRetries);

                    failed++;
                }
            }

            else
            {
                Console.WriteLine($@"Failed To Pack ""{resourceDirectory}"" After {maxRetries} Attempts");

                DeleteFileWithRetry(tempArchivePath, maxRetries);

                failed++;
            }
        }

        Console.WriteLine($"{succeeded} Directories Packed Successfully; {failed} Failed");

        return failed > 0 ? 1 : 0;
    }

    /// <summary>
    ///     Creates a ZIP archive from a directory with retry and exponential backoff.
    /// </summary>
    private static bool PackWithRetry(string sourceDirectory, string tempArchivePath, int maxRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(tempArchivePath))
                    File.Delete(tempArchivePath);

                ZipFile.CreateFromDirectory(sourceDirectory, tempArchivePath);

                return true;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maxRetries} Failed For ""{sourceDirectory}"": {exception.Message}");

                if (attempt < maxRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        return false;
    }

    /// <summary>
    ///     Deletes a directory recursively with retry and exponential backoff.
    /// </summary>
    private static bool DeleteDirectoryWithRetry(string directoryPath, int maxRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);

                return true;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maxRetries} To Delete ""{directoryPath}"" Failed: {exception.Message}");

                if (attempt < maxRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        return false;
    }

    /// <summary>
    ///     Moves a file with retry and exponential backoff.
    /// </summary>
    private static void MoveFileWithRetry(string sourcePath, string destinationPath, int maxRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);

                return;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maxRetries} To Move ""{sourcePath}"" → ""{destinationPath}"" Failed: {exception.Message}");

                if (attempt < maxRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        Console.WriteLine($@"Failed To Move ""{sourcePath}"" → ""{destinationPath}"" After {maxRetries} Attempts");
    }

    /// <summary>
    ///     Deletes a file with retry and exponential backoff to handle transient file locks.
    /// </summary>
    private static void DeleteFileWithRetry(string filePath, int maxRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);

                return;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maxRetries} To Delete ""{filePath}"" Failed: {exception.Message}");

                if (attempt < maxRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        Console.WriteLine($@"Failed To Delete ""{filePath}"" After {maxRetries} Attempts");
    }
}
