using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Distribution.UploadToObjectStorage;

/// <summary>
///     Uploads all files from a local directory (recursively) to an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3.
///     Tracks total progress as file count and percentage, retries failed uploads with exponential backoff (delay doubles after each failure), and logs failures to the console.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.UploadToObjectStorage.exe "path/to/directory" "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY"
///     </code>
///
///     Optional 6th argument: maximum retry attempts per file (default: 5).
///     Optional 7th argument: key prefix to prepend to all uploaded object keys.
/// </remarks>
internal class UploadToObjectStorage
{
    private const int DefaultMaxRetries = 5;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    internal static async Task<int> Main(string[] args)
    {
        if (args.Length < 5)
        {
            Console.WriteLine("USAGE: Distribution.UploadToObjectStorage <directory> <bucket> <serviceUrl> <accessKey> <secretKey> [maxRetries] [keyPrefix]");

            return 1;
        }

        string directory = args[0];
        string bucket = args[1];
        string serviceUrl = args[2];
        string accessKey = args[3];
        string secretKey = args[4];
        int maxRetries = args.Length >= 6 ? int.Parse(args[5]) : DefaultMaxRetries;
        string keyPrefix = args.Length >= 7 ? args[6] : string.Empty;

        if (Directory.Exists(directory) is false)
        {
            Console.WriteLine($@"Directory ""{directory}"" Does Not Exist");

            return 1;
        }

        BasicAWSCredentials credentials = new (accessKey, secretKey);

        using AmazonS3Client client = new (credentials, new AmazonS3Config
        {
            ServiceURL = serviceUrl
        });

        string[] files = Directory.GetFiles(directory, "*", SearchOption.AllDirectories);

        if (files.Length is 0)
        {
            Console.WriteLine("No Files Found");

            return 0;
        }

        Console.WriteLine($@"Uploading {files.Length} Files To Bucket ""{bucket}""");

        int uploaded = 0;
        int failed = 0;

        foreach (string file in files)
        {
            string relativePath = Path.GetRelativePath(directory, file).Replace('\\', '/');
            string key = string.IsNullOrEmpty(keyPrefix) ? relativePath : $"{keyPrefix.TrimEnd('/')}/{relativePath}";

            bool success = await UploadFileWithRetry(client, bucket, key, file, maxRetries);

            if (success) uploaded++; else failed++;

            int total = uploaded + failed;
            double percentage = (double)total / files.Length * 100;

            Console.WriteLine($"[{total.ToString().PadLeft(files.Length.ToString().Length)}/{files.Length} ({percentage,5:F1}%)] {(success ? "OK" : "FAILED")}: {relativePath}");
        }

        Console.WriteLine();
        Console.WriteLine($"Upload Complete: {uploaded} Succeeded, {failed} Failed, {files.Length} Total");

        return failed > 0 ? 1 : 0;
    }

    private static async Task<bool> UploadFileWithRetry(IAmazonS3 client, string bucket, string key, string filePath, int maxRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
        {
            try
            {
                PutObjectRequest request = new ()
                {
                    BucketName = bucket,
                    Key = key,
                    FilePath = filePath,
                    DisablePayloadSigning = true,
                    DisableDefaultChecksumValidation = true
                };

                await client.PutObjectAsync(request);

                return true;
            }

            catch (Exception ex)
            {
                Console.WriteLine($@"[Attempt {attempt}/{maxRetries}] Upload Failed For ""{key}"": {ex.Message}");

                if (attempt < maxRetries)
                {
                    Console.WriteLine($"Retrying In {delay.TotalSeconds:F0}s ...");

                    await Task.Delay(delay);

                    delay = InitialRetryDelay * (1 << attempt);
                }
            }
        }

        return false;
    }
}
