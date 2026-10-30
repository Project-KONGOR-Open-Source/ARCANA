namespace Distribution.CreateManifest.Tests;

/// <summary>
///     Verifies the manifest's version format: version 5, then a two-digit year, month, and day that together make a real date.
/// </summary>
public sealed class ManifestVersionTests
{
    [Test]
    public async Task The_Resources_Version_Is_The_Date()
    {
        await Assert.That(ManifestVersion.FromDate(new DateTime(2026, 1, 5, 23, 30, 0, DateTimeKind.Utc))).IsEqualTo("5.26.01.05");
    }

    [Test]
    [Arguments("5.26.10.31")]
    [Arguments("5.28.02.29")]
    public async Task A_Whole_Version_5_With_A_Real_Date_Is_Valid(string version)
    {
        await Assert.That(ManifestVersion.IsValid(version)).IsTrue();
    }

    [Test]
    [Arguments("5.26.02.30")]
    [Arguments("5.27.02.29")]
    [Arguments("5.26.13.01")]
    [Arguments("5.26.1.5")]
    [Arguments("4.10.1.0")]
    [Arguments("5.26.10.31.0")]
    [Arguments(" 5.26.10.31")]
    [Arguments("")]
    [Arguments("5.٢٦.10.31")]
    public async Task An_Invalid_Version_Is_Rejected(string version)
    {
        await Assert.That(ManifestVersion.IsValid(version)).IsFalse();
    }
}
