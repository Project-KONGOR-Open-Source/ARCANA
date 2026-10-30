namespace Distribution.DeleteFromObjectStorage;

/// <summary>
///     Chooses the objects that an exact-key deletion deletes: every requested key that the bucket lists exactly as written, once each, or none of them when any requested key is not listed.
/// </summary>
internal static class KeySelection
{
    internal static (IReadOnlyList<string> Keys, IReadOnlyList<string> Missing) SelectExactKeys(IReadOnlyCollection<string> listedKeys, IReadOnlyList<string> requestedKeys)
    {
        HashSet<string> listed = new(listedKeys, StringComparer.Ordinal);
        List<string> requested = [.. requestedKeys.Distinct(StringComparer.Ordinal)];

        List<string> keys = [.. requested.Where(listed.Contains)];
        List<string> missing = [.. requested.Where(key => listed.Contains(key) is false)];

        return missing.Count is 0 ? (keys, missing) : ([], missing);
    }
}
