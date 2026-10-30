using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;

namespace Distribution.CreateManifest;

/// <summary>
///     Generates or updates a manifest file (<c>manifest.json</c>) for a local directory, listing every file with its size, its SHA-256 hash, and whether it is a program that needs the execute permission.
///     When an existing manifest is found, hand-edited exclusion lists are preserved. Files matching <c>excludeFromTarget</c> globs are omitted from the listing.
///     The manifest carries two versions: <c>versionResources</c>, the date of the run in UTC, and <c>versionExecutable</c>, the version the distribution's binaries were built with, which is given on the command line or kept from the existing manifest.
///     The manifest file itself is always excluded from the file listing and included in <c>excludeFromSource</c>.
/// </summary>
/// <remarks>
///     Example Usage:
///
///     <code>
///         ./Distribution.CreateManifest.exe "path/to/directory" --version-executable 5.26.10.31
///     </code>
/// </remarks>
internal class CreateManifest
{
    private const string ManifestFileName = "manifest.json";
    private const string HashAlgorithmName = "SHA-256";
    private const string VersionExecutableOption = "--version-executable";
    private const string Usage = "USAGE: Distribution.CreateManifest <directory> [--version-executable 5.YY.MM.DD]";

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
            Console.WriteLine("  Generates or updates a manifest.json for a directory, listing every file with its size, its SHA-256 hash, and whether it is a program.");
            Console.WriteLine("  Records two versions: versionResources, the date of the run in UTC, and versionExecutable, the version the binaries were built with.");
            Console.WriteLine("  Preserves hand-edited exclusion lists, and the executable version, from an existing manifest.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  Distribution.CreateManifest <directory> [--version-executable 5.YY.MM.DD]");
            Console.WriteLine();
            Console.WriteLine("Arguments:");
            Console.WriteLine("  <directory>                       directory to scan and generate the manifest for");
            Console.WriteLine("  --version-executable 5.YY.MM.DD   the binaries' version; required unless the existing manifest has one");
            Console.WriteLine();

            return 0;
        }

        string? directory = null;
        string? versionExecutable = null;

        for (int index = 0; index < arguments.Length; index++)
        {
            string argument = arguments[index];

            if (argument is VersionExecutableOption && index + 1 < arguments.Length)
                versionExecutable = arguments[++index];

            else if (argument.StartsWith('-') is false && directory is null)
                directory = argument;

            else
            {
                Console.WriteLine(Usage);

                return 1;
            }
        }

        if (directory is null)
        {
            Console.WriteLine(Usage);

            return 1;
        }

        return await Run(Path.GetFullPath(directory), versionExecutable, DateTime.UtcNow, Console.Error);
    }

    /// <summary>
    ///     Writes the manifest for <paramref name="directory"/>, and returns 0 on success, or 1 after writing the reason to <paramref name="error"/>, in which case nothing is written.
    /// </summary>
    internal static async Task<int> Run(string directory, string? versionExecutable, DateTime utcNow, TextWriter error)
    {
        if (Directory.Exists(directory) is false)
        {
            await error.WriteLineAsync($@"Directory ""{directory}"" Does Not Exist");

            return 1;
        }

        string manifestPath = Path.Combine(directory, ManifestFileName);

        string? existingVersionExecutable = null;

        List<string> excludeFromSource = [ManifestFileName];
        List<string> excludeFromTarget = [];

        if (File.Exists(manifestPath))
        {
            Console.WriteLine("Loading Existing Manifest Metadata");

            try
            {
                (existingVersionExecutable, excludeFromSource, excludeFromTarget) = await LoadExistingMetadataAsync(manifestPath);
            }

            // A Manifest That Is Not JSON, Or Whose Fields Have The Wrong Type, Is Reported Rather Than Overwritten, Since Its Exclusion Lists May Hold Hand-Edited Patterns
            catch (Exception exception) when (exception is JsonException or InvalidOperationException)
            {
                await error.WriteLineAsync($@"The Existing Manifest ""{manifestPath}"" Cannot Be Read: {exception.Message}");

                return 1;
            }

            if (excludeFromSource.Contains(ManifestFileName, StringComparer.OrdinalIgnoreCase) is false)
                excludeFromSource.Insert(0, ManifestFileName);
        }

        versionExecutable ??= existingVersionExecutable;

        if (versionExecutable is null)
        {
            await error.WriteLineAsync($@"No Executable Version: Pass ""{VersionExecutableOption} 5.YY.MM.DD"" With The Version The Binaries Were Built With; Later Runs Keep It");

            return 1;
        }

        if (ManifestVersion.IsValid(versionExecutable) is false)
        {
            await error.WriteLineAsync($@"Invalid Executable Version ""{versionExecutable}"": Expected A Whole Version 5, ""5.YY.MM.DD"", Whose Date Exists");

            return 1;
        }

        string versionResources = ManifestVersion.FromDate(utcNow);

        Console.WriteLine($@"Resources Version ""{versionResources}"", Executable Version ""{versionExecutable}""");

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
                ["hash"] = hash,
                ["executable"] = ExecutableDetection.IsExecutable(fullPath)
            };

            hashed++;

            double percentage = (double)hashed / relativePaths.Count * 100;

            Console.WriteLine($"[{hashed.ToString().PadLeft(relativePaths.Count.ToString().Length)}/{relativePaths.Count} ({percentage,5:F1}%)] {relativePath}");
        }

        JsonObject manifest = new()
        {
            ["versionResources"] = versionResources,
            ["versionExecutable"] = versionExecutable,
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

    private static async Task<(string? VersionExecutable, List<string> ExcludeFromSource, List<string> ExcludeFromTarget)> LoadExistingMetadataAsync(string manifestPath)
    {
        await using FileStream stream = File.OpenRead(manifestPath);

        JsonNode? root = await JsonNode.ParseAsync(stream);

        string? versionExecutable = root?["versionExecutable"]?.GetValue<string>();

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

        return (versionExecutable, excludeFromSource, excludeFromTarget);
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
