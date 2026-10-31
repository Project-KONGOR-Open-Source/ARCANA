namespace Distribution.DeleteFromObjectStorage.Tests;

/// <summary>
///     Verifies that an exact-key deletion selects only keys that are listed exactly as requested, and selects none when any is not.
/// </summary>
public sealed class KeySelectionTests
{
    private static readonly string[] ListedKeys = ["mac/game/resources0.s2z", "mac/game/resources0.s2z.backup", "mac/manifest.json"];

    [Test]
    public async Task An_Exact_Key_Deletion_Refuses_A_Key_That_Is_Not_Listed()
    {
        (IReadOnlyList<string> keys, IReadOnlyList<string> missing) = KeySelection.SelectExactKeys(ListedKeys, ["mac/manifest.json", "mac/game/resources1.s2z"]);

        await Assert.That(keys).IsEmpty();
        await Assert.That(missing).IsEquivalentTo(["mac/game/resources1.s2z"]);
    }

    [Test]
    [Arguments("mac/game/resources0")]
    [Arguments("mac/game/")]
    [Arguments("mac")]
    [Arguments("MAC/manifest.json")]
    public async Task An_Exact_Key_Deletion_Never_Treats_A_Key_As_A_Prefix(string requestedKey)
    {
        (IReadOnlyList<string> keys, IReadOnlyList<string> missing) = KeySelection.SelectExactKeys(ListedKeys, [requestedKey]);

        await Assert.That(keys).IsEmpty();
        await Assert.That(missing).IsEquivalentTo([requestedKey]);
    }

    [Test]
    public async Task An_Exact_Key_Deletion_Selects_Every_Listed_Key()
    {
        (IReadOnlyList<string> keys, IReadOnlyList<string> missing) = KeySelection.SelectExactKeys(ListedKeys, ["mac/manifest.json", "mac/game/resources0.s2z"]);

        await Assert.That(keys).IsEquivalentTo(["mac/manifest.json", "mac/game/resources0.s2z"]);
        await Assert.That(missing).IsEmpty();
    }

    [Test]
    public async Task A_Key_Requested_Twice_Is_Selected_Once()
    {
        (IReadOnlyList<string> keys, IReadOnlyList<string> missing) = KeySelection.SelectExactKeys(ListedKeys, ["mac/manifest.json", "mac/manifest.json"]);

        await Assert.That(string.Join('|', keys)).IsEqualTo("mac/manifest.json");
        await Assert.That(missing).IsEmpty();
    }
}
