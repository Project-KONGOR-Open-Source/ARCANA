using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Distribution.DownloadFromObjectStorage;

/// <summary>
///     Downloads all objects under a given key prefix (folder) from an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3 to a local directory.
///     Lists objects in batches, downloads them preserving the relative directory structure, tracks total progress as file count and percentage, retries failed downloads with exponential backoff (delay doubles after each failure), and logs failures to the console.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.DownloadFromObjectStorage.exe "path/to/directory" "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY" "wac/4.10.1"
///     </code>
///
///     Optional 7th argument: maximum retry attempts per file (default: 5).
/// </remarks>
internal class DownloadFromObjectStorage
{
    private const int DefaultMaximumRetries = 5;
    private const int ListBatchSize = 1000;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    internal static async Task<int> Main(string[] arguments)
    {
        if (arguments.Any(argument => argument is "--help" or "-h"))
        {
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  Downloads all objects under a given key prefix from an S3-compatible bucket to a local directory.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Distribution.DownloadFromObjectStorage <directory> <bucket> <serviceUrl> <accessKey> <secretKey> <keyPrefix> [maxRetries]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <directory>    local directory to download files into");
            Console.WriteLine("  <bucket>       S3 bucket name");
            Console.WriteLine("  <serviceUrl>   S3-compatible service URL (e.g. https://ACCOUNT_ID.r2.cloudflarestorage.com)");
            Console.WriteLine("  <accessKey>    access key ID");
            Console.WriteLine("  <secretKey>    secret access key");
            Console.WriteLine("  <keyPrefix>    key prefix (folder) to download from (e.g. wac/4.10.1)");
            Console.WriteLine("  [maxRetries]   maximum retry attempts per file (default: 5)");
            Console.WriteLine();

            return 0;
        }

        if (arguments.Length < 6)
        {
            Console.WriteLine("USAGE: Distribution.DownloadFromObjectStorage <directory> <bucket> <serviceUrl> <accessKey> <secretKey> <keyPrefix> [maxRetries]");

            return 1;
        }

        string directory = arguments[0];
        string bucket = arguments[1];
        string serviceUrl = arguments[2];
        string accessKey = arguments[3];
        string secretKey = arguments[4];
        string keyPrefix = arguments[5];
        int maximumRetries = arguments.Length >= 7 ? int.Parse(arguments[6]) : DefaultMaximumRetries;

        Directory.CreateDirectory(directory);

        BasicAWSCredentials credentials = new(accessKey, secretKey);

        using AmazonS3Client client = new(credentials, new AmazonS3Config
        {
            ServiceURL = serviceUrl
        });

        Console.WriteLine($@"Listing Objects In Bucket ""{bucket}"" Under Prefix ""{keyPrefix}""");

        List<string> keys = [];
        string? continuationToken = null;

        do
        {
            ListObjectsV2Request listRequest = new()
            {
                BucketName = bucket,
                Prefix = keyPrefix,
                MaxKeys = ListBatchSize,
                ContinuationToken = continuationToken
            };

            ListObjectsV2Response listResponse = await client.ListObjectsV2Async(listRequest);

            foreach (S3Object s3Object in listResponse.S3Objects)
                keys.Add(s3Object.Key);

            continuationToken = listResponse.IsTruncated ? listResponse.NextContinuationToken : null;
        }

        while (continuationToken is not null);

        if (keys.Count is 0)
        {
            Console.WriteLine("No Objects Found");

            return 0;
        }

        Console.WriteLine($@"Downloading {keys.Count} Objects From Bucket ""{bucket}"" To ""{directory}""");

        int downloaded = 0;
        int failed = 0;

        foreach (string key in keys)
        {
            string relativePath = key.StartsWith(keyPrefix) ? key[keyPrefix.Length..].TrimStart('/') : key;
            string localPath = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));

            bool success = await DownloadFileWithRetry(client, bucket, key, localPath, maximumRetries);

            if (success) downloaded++; else failed++;

            int total = downloaded + failed;
            double percentage = (double)total / keys.Count * 100;

            Console.WriteLine($"[{total.ToString().PadLeft(keys.Count.ToString().Length)}/{keys.Count} ({percentage,5:F1}%)] {(success ? "OK" : "FAILED")}: {relativePath}");
        }

        Console.WriteLine();
        Console.WriteLine($"Download Complete: {downloaded} Succeeded, {failed} Failed, {keys.Count} Total");

        return failed > 0 ? 1 : 0;
    }

    private static async Task<bool> DownloadFileWithRetry(IAmazonS3 client, string bucket, string key, string localPath, int maximumRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maximumRetries; attempt++)
        {
            try
            {
                string? parentDirectory = Path.GetDirectoryName(localPath);

                if (parentDirectory is not null)
                    Directory.CreateDirectory(parentDirectory);

                GetObjectRequest request = new()
                {
                    BucketName = bucket,
                    Key = key
                };

                using GetObjectResponse response = await client.GetObjectAsync(request);

                await using FileStream fileStream = File.Create(localPath);

                await response.ResponseStream.CopyToAsync(fileStream);

                return true;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"[Attempt {attempt}/{maximumRetries}] Download Failed For ""{key}"": {exception.Message}");

                if (attempt < maximumRetries)
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
