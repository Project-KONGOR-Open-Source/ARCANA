namespace Distribution.DeleteFromObjectStorage.Tests;

/// <summary>
///     Verifies how the command line is read: each "--exact-key" option names one key, and anything that would make the tool fall back to deleting a whole prefix by mistake is refused.
/// </summary>
public sealed class DeletionArgumentTests
{
    private static readonly string[] RequiredArguments = ["bucket", "url", "access", "secret", "mac/"];

    [Test]
    public async Task Every_Exact_Key_Option_Adds_Its_Key_And_Leaves_The_Other_Arguments_In_Order()
    {
        bool valid = DeleteFromObjectStorage.TryReadArguments([.. RequiredArguments, "--exact-key", "mac/game/resources0.s2z", "3", "--exact-key", "mac/maps/with space.s2z"], out DeletionArguments? deletion);

        await Assert.That(valid).IsTrue();
        await Assert.That(deletion!.KeyPrefix).IsEqualTo("mac/");
        await Assert.That(deletion.MaximumRetries).IsEqualTo(3);
        await Assert.That(string.Join('|', deletion.ExactKeys)).IsEqualTo("mac/game/resources0.s2z|mac/maps/with space.s2z");
    }

    [Test]
    public async Task Without_An_Exact_Key_Option_No_Key_Is_Named_And_The_Default_Retries_Apply()
    {
        bool valid = DeleteFromObjectStorage.TryReadArguments([.. RequiredArguments], out DeletionArguments? deletion);

        await Assert.That(valid).IsTrue();
        await Assert.That(string.Join('|', deletion!.Bucket, deletion.ServiceURL, deletion.AccessKey, deletion.SecretKey, deletion.KeyPrefix)).IsEqualTo("bucket|url|access|secret|mac/");
        await Assert.That(deletion.MaximumRetries).IsEqualTo(5);
        await Assert.That(deletion.ExactKeys).IsEmpty();
    }

    [Test]
    public async Task An_Exact_Key_Option_At_The_End_Without_A_Key_Is_Refused()
    {
        await Assert.That(DeleteFromObjectStorage.TryReadArguments([.. RequiredArguments, "--exact-key"], out _)).IsFalse();
    }

    [Test]
    public async Task An_Exact_Key_Option_With_An_Empty_Key_Is_Refused()
    {
        await Assert.That(DeleteFromObjectStorage.TryReadArguments([.. RequiredArguments, "--exact-key", ""], out _)).IsFalse();
    }

    [Test]
    public async Task A_Misspelt_Option_Is_Refused()
    {
        await Assert.That(DeleteFromObjectStorage.TryReadArguments([.. RequiredArguments, "5", "--exact-ky", "mac/game/resources0.s2z"], out _)).IsFalse();
    }

    [Test]
    public async Task A_Key_Given_Without_Its_Option_Is_Refused()
    {
        await Assert.That(DeleteFromObjectStorage.TryReadArguments([.. RequiredArguments, "5", "mac/game/resources0.s2z"], out _)).IsFalse();
    }

    [Test]
    [Arguments("abc")]
    [Arguments("0")]
    [Arguments("-1")]
    [Arguments("")]
    public async Task A_Retry_Count_That_Is_Not_A_Positive_Number_Is_Refused(string retries)
    {
        await Assert.That(DeleteFromObjectStorage.TryReadArguments([.. RequiredArguments, retries], out _)).IsFalse();
    }

    [Test]
    public async Task Fewer_Than_Five_Arguments_Are_Refused()
    {
        await Assert.That(DeleteFromObjectStorage.TryReadArguments(["bucket", "url", "access", "secret", "--exact-key", "mac/game/resources0.s2z"], out _)).IsFalse();
    }
}
