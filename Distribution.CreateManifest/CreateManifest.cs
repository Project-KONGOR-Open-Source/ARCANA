using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Distribution.CreateManifest;

/// <summary>
///     Generates or updates a manifest file (<c>manifest.json</c>) for a local directory, listing every file with its size and SHA-256 hash.
///     When an existing manifest is found, hand-edited exclusion lists are preserved. Files matching <c>excludeFromTarget</c> globs are omitted from the listing.
///     The manifest file itself is always excluded from the file listing and included in <c>excludeFromSource</c>.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.CreateManifest.exe "path/to/directory"
///     </code>
/// </remarks>
internal class CreateManifest
{
    private const string ManifestFileName = "manifest.json";
    private const string ManifestVersion = "1.0.0";
    private const string HashAlgorithmName = "SHA-256";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        IndentSize = 4,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    internal static async Task<int> Main(string[] arguments)
    {
        if (arguments.Any(argument => argument is "--help" or "-h"))
        {
            Console.WriteLine();
            Console.WriteLine("Description:");
            Console.WriteLine("  Generates or updates a manifest.json for a directory, listing every file with its size and SHA-256 hash.");
            Console.WriteLine("  Preserves hand-edited exclusion lists from an existing manifest.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Distribution.CreateManifest <directory>");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <directory>  directory to scan and generate the manifest for");
            Console.WriteLine();

            return 0;
        }

        if (arguments.Length < 1)
        {
            Console.WriteLine("USAGE: Distribution.CreateManifest <directory>");

            return 1;
        }

        string directory = Path.GetFullPath(arguments[0]);

        if (Directory.Exists(directory) is false)
        {
            Console.WriteLine($@"Directory ""{directory}"" Does Not Exist");

            return 1;
        }

        string manifestPath = Path.Combine(directory, ManifestFileName);

        List<string> excludeFromSource = [ManifestFileName];
        List<string> excludeFromTarget = [];

        if (File.Exists(manifestPath))
        {
            Console.WriteLine("Loading Existing Manifest Exclusions");

            (excludeFromSource, excludeFromTarget) = await LoadExclusionsAsync(manifestPath);

            if (excludeFromSource.Contains(ManifestFileName, StringComparer.OrdinalIgnoreCase) is false)
                excludeFromSource.Insert(0, ManifestFileName);
        }

        Console.WriteLine($@"Scanning Directory ""{directory}""");

        List<string> relativePaths = GetMatchingFiles(directory, excludeFromTarget);

        if (relativePaths.Count is 0)
        {
            Console.WriteLine("No Files Found");

            return 0;
        }

        Console.WriteLine($"Hashing {relativePaths.Count} Files");

        JsonObject filesObject = [];

        int hashed = 0;

        foreach (string relativePath in relativePaths)
        {
            string fullPath = Path.Combine(directory, relativePath.Replace('/', Path.DirectorySeparatorChar));
            long size = new FileInfo(fullPath).Length;
            string hash = await ComputeHashAsync(fullPath);

            filesObject[relativePath] = new JsonObject
            {
                ["size"] = size,
                ["hash"] = hash
            };

            hashed++;

            double percentage = (double)hashed / relativePaths.Count * 100;

            Console.WriteLine($"[{hashed.ToString().PadLeft(relativePaths.Count.ToString().Length)}/{relativePaths.Count} ({percentage,5:F1}%)] {relativePath}");
        }

        JsonObject manifest = new()
        {
            ["version"] = ManifestVersion,
            ["hashAlgorithm"] = HashAlgorithmName,
            ["excludeFromSource"] = new JsonArray(excludeFromSource.Select(pattern => (JsonNode)pattern).ToArray()),
            ["excludeFromTarget"] = new JsonArray(excludeFromTarget.Select(pattern => (JsonNode)pattern).ToArray()),
            ["files"] = filesObject
        };

        string json = manifest.ToJsonString(SerializerOptions);

        await File.WriteAllTextAsync(manifestPath, json + "\n");

        Console.WriteLine();
        Console.WriteLine($@"Manifest Written To ""{manifestPath}"" With {relativePaths.Count} Files");

        return 0;
    }

    private static async Task<(List<string> ExcludeFromSource, List<string> ExcludeFromTarget)> LoadExclusionsAsync(string manifestPath)
    {
        await using FileStream stream = File.OpenRead(manifestPath);

        JsonNode? root = await JsonNode.ParseAsync(stream);

        List<string> excludeFromSource = root?["excludeFromSource"]?
            .AsArray()
            .OfType<JsonNode>()
            .Select(node => node.GetValue<string>())
            .ToList() ?? [];

        List<string> excludeFromTarget = root?["excludeFromTarget"]?
            .AsArray()
            .OfType<JsonNode>()
            .Select(node => node.GetValue<string>())
            .ToList() ?? [];

        return (excludeFromSource, excludeFromTarget);
    }

    private static List<string> GetMatchingFiles(string directory, List<string> excludePatterns)
    {
        Matcher matcher = new(StringComparison.Ordinal);

        matcher.AddInclude("**/*");
        matcher.AddExclude(ManifestFileName);

        foreach (string pattern in excludePatterns)
            matcher.AddExclude(pattern);

        PatternMatchingResult result = matcher.Execute(new DirectoryInfoWrapper(new DirectoryInfo(directory)));

        return [.. result.Files.Select(match => match.Path).Order(StringComparer.Ordinal)];
    }

    private static async Task<string> ComputeHashAsync(string filePath)
    {
        await using FileStream stream = File.OpenRead(filePath);

        byte[] hashBytes = await SHA256.HashDataAsync(stream);

        return Convert.ToHexStringLower(hashBytes);
    }
}
