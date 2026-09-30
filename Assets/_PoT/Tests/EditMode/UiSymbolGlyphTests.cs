using NUnit.Framework;
using TMPro;

/// <summary>
/// BUG-150: TMP's default font (LiberationSans SDF) has no ✓ or ⚠, so the skill tree and the rebind screen drew □.
/// The symbols come from the static "PoTSymbols SDF" asset in TMP Settings' global fallback list (built by
/// Planet of Twins Tools ▸ UI ▸ Build Symbol Fallback Font). This fails if that asset or its fallback entry goes
/// missing, or a re-bake drops a symbol. tryAddCharacter: false = only what is already baked counts, as in a build.
/// </summary>
public class UiSymbolGlyphTests
{
    [TestCase('✓', TestName = "CheckMark_IsDrawable (skill tree: purchased node)")]
    [TestCase('⚠', TestName = "WarningSign_IsDrawable (skill preview + rebind conflict)")]
    public void DefaultFontChain_HasTheSymbol(char symbol)
    {
        var font = TMP_Settings.defaultFontAsset;
        Assert.IsNotNull(font, "TMP Settings has no default font asset.");
        Assert.IsTrue(font.HasCharacter(symbol, searchFallbacks: true, tryAddCharacter: false),
                      $"U+{(int)symbol:X4} is not in '{font.name}' or any fallback: TMP would draw □. " +
                      "Run Planet of Twins Tools ▸ UI ▸ Build Symbol Fallback Font.");
    }
}
