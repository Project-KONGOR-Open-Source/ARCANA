using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Distribution.DeleteFromObjectStorage;

/// <summary>
///     Deletes all objects under a given key prefix (folder) from an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3.
///     Lists objects in batches, deletes them, tracks total progress as object count and percentage, retries failed deletions with exponential backoff, and logs failures to the console.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.DeleteFromObjectStorage.exe "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY" "wac/4.10.1"
///     </code>
///
///     Optional 6th argument: maximum retry attempts per deletion batch (default: 5).
/// </remarks>
internal class DeleteFromObjectStorage
{
    private const int DefaultMaxRetries = 5;
    private const int ListBatchSize = 1000;
    private static readonly TimeSpan InitialRetryDelay = TimeSpan.FromSeconds(1);

    internal static async Task<int> Main(string[] args)
    {
        if (args.Length < 5)
        {
            Console.WriteLine("USAGE: Distribution.DeleteFromObjectStorage <bucket> <serviceUrl> <accessKey> <secretKey> <keyPrefix> [maxRetries]");

            return 1;
        }

        string bucket = args[0];
        string serviceUrl = args[1];
        string accessKey = args[2];
        string secretKey = args[3];
        string keyPrefix = args[4];
        int maxRetries = args.Length >= 6 ? int.Parse(args[5]) : DefaultMaxRetries;

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

        Console.WriteLine($@"Deleting {keys.Count} Objects From Bucket ""{bucket}""");

        int deleted = 0;
        int failed = 0;

        foreach (string key in keys)
        {
            bool success = await DeleteObjectWithRetry(client, bucket, key, maxRetries);

            if (success) deleted++; else failed++;

            int total = deleted + failed;
            double percentage = (double)total / keys.Count * 100;

            Console.WriteLine($"[{total.ToString().PadLeft(keys.Count.ToString().Length)}/{keys.Count} ({percentage,5:F1}%)] {(success ? "OK" : "FAILED")}: {key}");
        }

        Console.WriteLine();
        Console.WriteLine($"Deletion Complete: {deleted} Succeeded, {failed} Failed, {keys.Count} Total");

        return failed > 0 ? 1 : 0;
    }

    private static async Task<bool> DeleteObjectWithRetry(IAmazonS3 client, string bucket, string key, int maxRetries)
    {
        TimeSpan delay = InitialRetryDelay;

        for (int attempt = 1; attempt <= maxRetries; attempt++)
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
                Console.WriteLine($@"[Attempt {attempt}/{maxRetries}] Deletion Failed For ""{key}"": {exception.Message}");

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
