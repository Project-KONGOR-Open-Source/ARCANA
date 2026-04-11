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
    private const int DefaultMaximumRetries = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    internal static int Main(string[] arguments)
    {
        if (arguments is ["--help" or "-h"])
        {
            Console.WriteLine("Description:");
            Console.WriteLine("  Packs directories with an .s2z suffix back into S2Z archives (ZIP format).");
            Console.WriteLine("  Each source directory is deleted after the archive is successfully created.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Distribution.PackResourceFiles [directory] [maxRetries]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  [directory]   parent directory containing .s2z directories (default: current directory)");
            Console.WriteLine("  [maxRetries]  maximum retry attempts per directory (default: 5)");

            return 0;
        }

        string parentDirectory = arguments.Length >= 1 ? arguments[0] : Environment.CurrentDirectory;
        int maximumRetries = arguments.Length >= 2 ? int.Parse(arguments[1]) : DefaultMaximumRetries;

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
            string temporaryArchivePath = $"{resourceDirectory}.tmp";

            Console.WriteLine($@"Packing ""{resourceDirectory}"" → ""{archivePath}""");

            if (PackWithRetry(resourceDirectory, temporaryArchivePath, maximumRetries))
            {
                if (DeleteDirectoryWithRetry(resourceDirectory, maximumRetries))
                {
                    MoveFileWithRetry(temporaryArchivePath, archivePath, maximumRetries);

                    succeeded++;
                }

                else
                {
                    Console.WriteLine($@"Failed To Delete Directory ""{resourceDirectory}"" After {maximumRetries} Attempts; Cleaning Up Temp Archive");

                    DeleteFileWithRetry(temporaryArchivePath, maximumRetries);

                    failed++;
                }
            }

            else
            {
                Console.WriteLine($@"Failed To Pack ""{resourceDirectory}"" After {maximumRetries} Attempts");

                DeleteFileWithRetry(temporaryArchivePath, maximumRetries);

                failed++;
            }
        }

        Console.WriteLine($"{succeeded} Directories Packed Successfully; {failed} Failed");

        return failed > 0 ? 1 : 0;
    }

    /// <summary>
    ///     Creates a ZIP archive from a directory with retry and exponential backoff.
    /// </summary>
    private static bool PackWithRetry(string sourceDirectory, string temporaryArchivePath, int maximumRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maximumRetries; attempt++)
        {
            try
            {
                if (File.Exists(temporaryArchivePath))
                    File.Delete(temporaryArchivePath);

                ZipFile.CreateFromDirectory(sourceDirectory, temporaryArchivePath);

                return true;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maximumRetries} Failed For ""{sourceDirectory}"": {exception.Message}");

                if (attempt < maximumRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        return false;
    }

    /// <summary>
    ///     Deletes a directory recursively with retry and exponential backoff.
    /// </summary>
    private static bool DeleteDirectoryWithRetry(string directoryPath, int maximumRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maximumRetries; attempt++)
        {
            try
            {
                Directory.Delete(directoryPath, recursive: true);

                return true;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maximumRetries} To Delete ""{directoryPath}"" Failed: {exception.Message}");

                if (attempt < maximumRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        return false;
    }

    /// <summary>
    ///     Moves a file with retry and exponential backoff.
    /// </summary>
    private static void MoveFileWithRetry(string sourcePath, string destinationPath, int maximumRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maximumRetries; attempt++)
        {
            try
            {
                File.Move(sourcePath, destinationPath, overwrite: true);

                return;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"Attempt {attempt}/{maximumRetries} To Move ""{sourcePath}"" → ""{destinationPath}"" Failed: {exception.Message}");

                if (attempt < maximumRetries)
                    Thread.Sleep(delay);

                delay = InitialRetryDelay * (1 << attempt);
            }
        }

        Console.WriteLine($@"Failed To Move ""{sourcePath}"" → ""{destinationPath}"" After {maximumRetries} Attempts");
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
                if (File.Exists(filePath))
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
}
