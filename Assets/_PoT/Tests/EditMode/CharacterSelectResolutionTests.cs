using NUnit.Framework;

/// <summary>
/// Couch M2: <see cref="CharacterSelectController.TryResolve"/>, the distinct-twin resolver. Random takes the other
/// twin; two Randoms follow the coin; the same explicit twin twice can't start. (Was the CharacterSelectSelfTest
/// menu item.)
/// </summary>
public class CharacterSelectResolutionTests
{
    private const CharacterPick Lyra = CharacterPick.Lyra;
    private const CharacterPick Kai = CharacterPick.Kai;
    private const CharacterPick Random = CharacterPick.Random;

    [TestCase(Lyra, Kai, true, Lyra, Kai, TestName = "Lyra + Kai → (Lyra, Kai)")]
    [TestCase(Kai, Lyra, true, Kai, Lyra, TestName = "Kai + Lyra → (Kai, Lyra)")]
    [TestCase(Lyra, Random, true, Lyra, Kai, TestName = "Lyra + Random → (Lyra, Kai)")]
    [TestCase(Random, Kai, true, Lyra, Kai, TestName = "Random + Kai → (Lyra, Kai)")]
    [TestCase(Kai, Random, true, Kai, Lyra, TestName = "Kai + Random → (Kai, Lyra)")]
    [TestCase(Random, Lyra, true, Kai, Lyra, TestName = "Random + Lyra → (Kai, Lyra)")]
    [TestCase(Random, Random, true, Lyra, Kai, TestName = "Random + Random, coin Lyra → (Lyra, Kai)")]
    [TestCase(Random, Random, false, Kai, Lyra, TestName = "Random + Random, coin Kai → (Kai, Lyra)")]
    public void Resolves(CharacterPick p1, CharacterPick p2, bool coin, CharacterPick expected1, CharacterPick expected2)
    {
        Assert.IsTrue(CharacterSelectController.TryResolve(p1, p2, coin, out var a, out var b), "startable");
        Assert.AreEqual(expected1, a, "P1");
        Assert.AreEqual(expected2, b, "P2");
    }

    [TestCase(Kai, TestName = "Kai + Kai → blocked")]
    [TestCase(Lyra, TestName = "Lyra + Lyra → blocked")]
    public void SameTwinTwice_CannotStart(CharacterPick pick)
    {
        Assert.IsFalse(CharacterSelectController.TryResolve(pick, pick, true, out _, out _));
    }
}
