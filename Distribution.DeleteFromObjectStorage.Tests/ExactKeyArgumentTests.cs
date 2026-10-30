namespace Distribution.DeleteFromObjectStorage.Tests;

/// <summary>
///     Verifies that each "--exact-key" option names one key on the command line, and that the remaining arguments keep their positions.
/// </summary>
public sealed class ExactKeyArgumentTests
{
    [Test]
    public async Task Every_Exact_Key_Option_Adds_Its_Key_And_Leaves_The_Other_Arguments_In_Order()
    {
        bool valid = DeleteFromObjectStorage.TrySplitExactKeys(["bucket", "url", "access", "secret", "mac/", "--exact-key", "mac/game/resources0.s2z", "3", "--exact-key", "mac/maps/with space.s2z"], out string[] positionalArguments, out List<string> exactKeys);

        await Assert.That(valid).IsTrue();
        await Assert.That(string.Join('|', positionalArguments)).IsEqualTo("bucket|url|access|secret|mac/|3");
        await Assert.That(string.Join('|', exactKeys)).IsEqualTo("mac/game/resources0.s2z|mac/maps/with space.s2z");
    }

    [Test]
    public async Task Without_An_Exact_Key_Option_No_Key_Is_Named()
    {
        bool valid = DeleteFromObjectStorage.TrySplitExactKeys(["bucket", "url", "access", "secret", "wac/4.10.1"], out string[] positionalArguments, out List<string> exactKeys);

        await Assert.That(valid).IsTrue();
        await Assert.That(string.Join('|', positionalArguments)).IsEqualTo("bucket|url|access|secret|wac/4.10.1");
        await Assert.That(exactKeys).IsEmpty();
    }

    [Test]
    public async Task An_Exact_Key_Option_At_The_End_Without_A_Key_Is_Refused()
    {
        bool valid = DeleteFromObjectStorage.TrySplitExactKeys(["bucket", "url", "access", "secret", "mac/", "--exact-key"], out _, out _);

        await Assert.That(valid).IsFalse();
    }

    [Test]
    public async Task An_Exact_Key_Option_With_An_Empty_Key_Is_Refused()
    {
        bool valid = DeleteFromObjectStorage.TrySplitExactKeys(["bucket", "url", "access", "secret", "mac/", "--exact-key", ""], out _, out _);

        await Assert.That(valid).IsFalse();
    }
}
