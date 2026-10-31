using System.Globalization;

using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;

namespace Distribution.ListObjectStorage;

/// <summary>
///     Lists every object under a given key prefix (folder) in an S3-compatible object storage bucket such as Cloudflare R2 or AWS S3, without changing anything.
///     Prints one line per object, its key and its size in bytes separated by a tab, sorted ordinally by key, and nothing else, so that the output can be compared and read by other tools.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.ListObjectStorage.exe "bucket-name" "https://ACCOUNT_ID.r2.cloudflarestorage.com" "ACCESS_KEY_ID" "SECRET_ACCESS_KEY" "las/game/shadercache/"
///     </code>
/// </remarks>
internal class ListObjectStorage
{
    private const int ListBatchSize = 1000;

    internal static async Task<int> Main(string[] arguments)
    {
        if (arguments.Any(argument => argument is "--help" or "-h"))
        {
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  Lists all objects under a given key prefix in an S3-compatible bucket, one key and size per line, without changing anything.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Distribution.ListObjectStorage <bucket> <serviceUrl> <accessKey> <secretKey> <keyPrefix>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <bucket>       S3 bucket name");
            Console.WriteLine("  <serviceUrl>   S3-compatible service URL (e.g. https://ACCOUNT_ID.r2.cloudflarestorage.com)");
            Console.WriteLine("  <accessKey>    access key ID");
            Console.WriteLine("  <secretKey>    secret access key");
            Console.WriteLine("  <keyPrefix>    key prefix (folder) to list (e.g. wac/game/), or an empty string for the whole bucket");
            Console.WriteLine();

            return 0;
        }

        if (arguments.Length < 5)
        {
            Console.Error.WriteLine("USAGE: Distribution.ListObjectStorage <bucket> <serviceUrl> <accessKey> <secretKey> <keyPrefix>");

            return 1;
        }

        string bucket = arguments[0];
        string serviceUrl = arguments[1];
        string accessKey = arguments[2];
        string secretKey = arguments[3];
        string keyPrefix = arguments[4];

        BasicAWSCredentials credentials = new(accessKey, secretKey);

        using AmazonS3Client client = new(credentials, new AmazonS3Config
        {
            ServiceURL = serviceUrl
        });

        List<S3Object> objects = [];
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

            objects.AddRange(listResponse.S3Objects);

            continuationToken = listResponse.IsTruncated ? listResponse.NextContinuationToken : null;
        }

        while (continuationToken is not null);

        objects.Sort((first, second) => string.CompareOrdinal(first.Key, second.Key));

        foreach (S3Object s3Object in objects)
            Console.WriteLine(string.Create(CultureInfo.InvariantCulture, $"{s3Object.Key}\t{s3Object.Size}"));

        return 0;
    }
}
