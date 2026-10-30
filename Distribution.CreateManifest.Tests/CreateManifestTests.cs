using System.Text.Json.Nodes;

namespace Distribution.CreateManifest.Tests;

/// <summary>
///     Verifies a whole run of the tool: the two versions it records, the executable flags, the exclusion lists it keeps, and the runs it refuses.
/// </summary>
public sealed class CreateManifestTests
{
    private static readonly DateTime RunTime = new (2026, 12, 20, 12, 0, 0, DateTimeKind.Utc);

    private static string CreateTemporaryDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());

        Directory.CreateDirectory(directory);

        return directory;
    }

    private static JsonNode ReadManifest(string directory)
        => JsonNode.Parse(File.ReadAllText(Path.Combine(directory, "manifest.json")))!;

    private static List<string> ReadList(JsonNode manifest, string name)
        => [.. manifest[name]!.AsArray().Select(node => node!.GetValue<string>())];

    [Test]
    public async Task A_Manifest_Records_Both_Versions_And_Flags_Executables()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "hon.exe"), "text");
            File.WriteAllText(Path.Combine(directory, "Server-Console.sh"), "#!/usr/bin/env bash\n");
            File.WriteAllText(Path.Combine(directory, "readme.txt"), "Heroes Of Newerth\n");

            int exitCode = await CreateManifest.Run(directory, "5.26.12.24", RunTime, new StringWriter());

            await Assert.That(exitCode).IsEqualTo(0);

            JsonNode manifest = ReadManifest(directory);
            JsonObject files = manifest["files"]!.AsObject();

            await Assert.That(manifest["versionResources"]!.GetValue<string>()).IsEqualTo("5.26.12.20");
            await Assert.That(manifest["versionExecutable"]!.GetValue<string>()).IsEqualTo("5.26.12.24");
            await Assert.That(manifest.AsObject().ContainsKey("version")).IsFalse();

            await Assert.That(files["hon.exe"]!["executable"]!.GetValue<bool>()).IsTrue();
            await Assert.That(files["Server-Console.sh"]!["executable"]!.GetValue<bool>()).IsTrue();
            await Assert.That(files["readme.txt"]!["executable"]!.GetValue<bool>()).IsFalse();

            await Assert.That(files.ContainsKey("manifest.json")).IsFalse();
            await Assert.That(ReadList(manifest, "excludeFromSource")).IsEquivalentTo(["manifest.json"]);
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Leaving_Out_The_Executable_Version_Keeps_The_Existing_One()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "hon.exe"), "text");

            await Assert.That(await CreateManifest.Run(directory, "5.26.12.24", RunTime, new StringWriter())).IsEqualTo(0);

            // Hand-Edited Exclusion Lists Must Survive A Run
            JsonNode edited = ReadManifest(directory);

            edited["excludeFromSource"]!.AsArray().Add("source-pattern");
            edited["excludeFromTarget"]!.AsArray().Add("target-pattern");

            File.WriteAllText(Path.Combine(directory, "manifest.json"), edited.ToJsonString());

            await Assert.That(await CreateManifest.Run(directory, null, RunTime, new StringWriter())).IsEqualTo(0);

            JsonNode manifest = ReadManifest(directory);

            await Assert.That(manifest["versionExecutable"]!.GetValue<string>()).IsEqualTo("5.26.12.24");
            await Assert.That(ReadList(manifest, "excludeFromSource")).Contains("source-pattern");
            await Assert.That(ReadList(manifest, "excludeFromTarget")).Contains("target-pattern");
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task Leaving_Out_The_Executable_Version_With_None_Existing_Fails()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "hon.exe"), "text");

            StringWriter error = new ();

            await Assert.That(await CreateManifest.Run(directory, null, RunTime, error)).IsEqualTo(1);
            await Assert.That(File.Exists(Path.Combine(directory, "manifest.json"))).IsFalse();
            await Assert.That(error.ToString()).Contains("--version-executable");
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    [Arguments("not a manifest")]
    [Arguments("""{"versionExecutable": 5, "excludeFromSource": [], "excludeFromTarget": [], "files": {}}""")]
    public async Task An_Unreadable_Existing_Manifest_Fails_Without_Writing(string existingManifest)
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "hon.exe"), "text");
            File.WriteAllText(Path.Combine(directory, "manifest.json"), existingManifest);

            StringWriter error = new ();

            await Assert.That(await CreateManifest.Run(directory, "5.26.12.24", RunTime, error)).IsEqualTo(1);
            await Assert.That(File.ReadAllText(Path.Combine(directory, "manifest.json"))).IsEqualTo(existingManifest);
            await Assert.That(error.ToString()).Contains("manifest.json");
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task An_Invalid_Executable_Version_Fails_Without_Writing()
    {
        string directory = CreateTemporaryDirectory();

        try
        {
            File.WriteAllText(Path.Combine(directory, "hon.exe"), "text");

            await Assert.That(await CreateManifest.Run(directory, "5.26.12.24", RunTime, new StringWriter())).IsEqualTo(0);

            string before = File.ReadAllText(Path.Combine(directory, "manifest.json"));

            await Assert.That(await CreateManifest.Run(directory, "5.26.02.30", RunTime, new StringWriter())).IsEqualTo(1);
            await Assert.That(File.ReadAllText(Path.Combine(directory, "manifest.json"))).IsEqualTo(before);
        }

        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
