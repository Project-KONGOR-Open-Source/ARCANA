namespace Distribution.DeleteFromObjectStorage;

/// <summary>
///     The command line as the tool reads it: where to delete from, how often to retry each deletion, and the exact keys to delete, if any were given.
/// </summary>
internal sealed record DeletionArguments(string Bucket, string ServiceURL, string AccessKey, string SecretKey, string KeyPrefix, int MaximumRetries, IReadOnlyList<string> ExactKeys);
