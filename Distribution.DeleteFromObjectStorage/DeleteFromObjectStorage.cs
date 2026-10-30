using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Distribution.DeleteFromObjectStorage;

/// <summary>
///     Deletes all objects under a given key prefix (folder) from an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3.
///     Lists objects in batches, deletes them, tracks total progress as object count and percentage, retries failed deletions with exponential backoff, and logs failures to the console.
///     With "--exact-keys", deletes only the keys listed in a file, and only when every one of them exists under the prefix exactly as written; otherwise, it deletes nothing.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.DeleteFromObjectStorage.exe "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY" "wac/4.10.1"
///         ./Distribution.DeleteFromObjectStorage.exe "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY" "mac/" --exact-keys "keys.txt"
///     </code>
///
///     Optional 6th argument: maximum retry attempts per deletion batch (default: 5).
/// </remarks>
internal class DeleteFromObjectStorage
{
    private const int DefaultMaximumRetries = 5;
    private const int ListBatchSize = 1000;
    private const string ExactKeysOption = "--exact-keys";
    private const string Usage = "USAGE: Distribution.DeleteFromObjectStorage <bucket> <serviceUrl> <accessKey> <secretKey> <keyPrefix> [maxRetries] [--exact-keys <file>]";
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    internal static async Task<int> Main(string[] arguments)
    {
        if (arguments.Any(argument => argument is "--help" or "-h"))
        {
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  Deletes all objects under a given key prefix from an S3-compatible bucket, or only the exact keys listed in a file.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Distribution.DeleteFromObjectStorage <bucket> <serviceUrl> <accessKey> <secretKey> <keyPrefix> [maxRetries] [--exact-keys <file>]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <bucket>       S3 bucket name");
            Console.WriteLine("  <serviceUrl>   S3-compatible service URL (e.g. https://ACCOUNT_ID.r2.cloudflarestorage.com)");
            Console.WriteLine("  <accessKey>    access key ID");
            Console.WriteLine("  <secretKey>    secret access key");
            Console.WriteLine("  <keyPrefix>    key prefix (folder) to delete from (e.g. wac/4.10.1)");
            Console.WriteLine("  [maxRetries]   maximum retry attempts per deletion (default: 5)");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  --exact-keys <file>   deletes only the keys in the file, one per line, and nothing at all unless every one of them exists under the prefix exactly as written");
            Console.WriteLine();

            return 0;
        }

        string? exactKeysFile = null;
        int exactKeysIndex = Array.IndexOf(arguments, ExactKeysOption);

        if (exactKeysIndex >= 0)
        {
            if (exactKeysIndex + 1 >= arguments.Length)
            {
                Console.WriteLine(Usage);

                return 1;
            }

            exactKeysFile = arguments[exactKeysIndex + 1];
            arguments = [.. arguments[..exactKeysIndex], .. arguments[(exactKeysIndex + 2)..]];
        }

        if (arguments.Length < 5)
        {
            Console.WriteLine(Usage);

            return 1;
        }

        string bucket = arguments[0];
        string serviceUrl = arguments[1];
        string accessKey = arguments[2];
        string secretKey = arguments[3];
        string keyPrefix = arguments[4];
        int maximumRetries = arguments.Length >= 6 ? int.Parse(arguments[5]) : DefaultMaximumRetries;

        // The Requested Keys Are Read Before Anything Is Listed, So That A Missing Or Empty File Stops The Tool Before It Reaches The Bucket
        List<string>? requestedKeys = exactKeysFile is null ? null : [.. File.ReadAllLines(exactKeysFile).Where(line => line.Length > 0)];

        if (requestedKeys is { Count: 0 })
        {
            Console.WriteLine($@"No Keys Requested In ""{exactKeysFile}""");

            return 1;
        }

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

        if (requestedKeys is not null)
        {
            (IReadOnlyList<string> selectedKeys, IReadOnlyList<string> missingKeys) = KeySelection.SelectExactKeys(keys, requestedKeys);

            if (missingKeys.Count > 0)
            {
                foreach (string key in missingKeys)
                    Console.WriteLine($@"Not Found: ""{key}""");

                Console.WriteLine($@"Nothing Deleted: {missingKeys.Count} Of {requestedKeys.Count} Requested Keys Do Not Exist Under Prefix ""{keyPrefix}""");

                return 1;
            }

            keys = [.. selectedKeys];
        }

        if (keys.Count is 0)
        {
            Console.WriteLine("No Objects Found");

            return 0;
        }

        Console.WriteLine($@"Deleting {keys.Count} Objects From Bucket ""{bucket}""");

        int deleted = 0;
        int failed = 0;

        foreach (string key in keys)
        {
            bool success = await DeleteObjectWithRetry(client, bucket, key, maximumRetries);

            if (success) deleted++; else failed++;

            int total = deleted + failed;
            double percentage = (double)total / keys.Count * 100;

            Console.WriteLine($"[{total.ToString().PadLeft(keys.Count.ToString().Length)}/{keys.Count} ({percentage,5:F1}%)] {(success ? "OK" : "FAILED")}: {key}");
        }

        Console.WriteLine();
        Console.WriteLine($"Deletion Complete: {deleted} Succeeded, {failed} Failed, {keys.Count} Total");

        return failed > 0 ? 1 : 0;
    }

    private static async Task<bool> DeleteObjectWithRetry(IAmazonS3 client, string bucket, string key, int maximumRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maximumRetries; attempt++)
        {
            try
            {
                DeleteObjectRequest request = new()
                {
                    BucketName = bucket,
                    Key = key
                };

                await client.DeleteObjectAsync(request);

                return true;
            }

            catch (Exception exception)
            {
                Console.WriteLine($@"[Attempt {attempt}/{maximumRetries}] Deletion Failed For ""{key}"": {exception.Message}");

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
