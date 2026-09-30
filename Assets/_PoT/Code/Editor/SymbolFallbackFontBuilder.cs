using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;

/// <summary>
/// BUG-150. TMP's default font (LiberationSans SDF) has no glyph for a few symbols our UI text writes (✓ in the skill
/// tree, ⚠ in the skill preview and the rebind conflict line), so TMP drew □ and logged a warning. This bakes exactly
/// those symbols from a font that has them (<see cref="PoTPaths.Named.SymbolFontSource"/>, Inter) into one small
/// STATIC font asset and puts it in TMP Settings' global fallback list, so every TMP text finds them.
///   • Static, not dynamic: the atlas never changes at runtime, so play mode never dirties the asset (the dynamic
///     "LiberationSans SDF - Fallback" is a never-commit file for exactly that reason), and the source font file is
///     not shipped in a build.
///   • Sampling size, padding and render mode are copied from TMP's default font: the same padding ratio gives a
///     symbol the same stroke weight as the text around it.
/// A new UI symbol the default font lacks → add it to <see cref="Symbols"/> and re-run. Fails loud when the source
/// font lacks one or the atlas is full. Idempotent. Menu: <b>Planet of Twins Tools ▸ UI ▸ Build Symbol Fallback Font</b>.
/// </summary>
public static class SymbolFallbackFontBuilder
{
    private const string Symbols   = "✓⚠";   // ✓ ⚠
    private const int    AtlasSize = 256;              // one atlas page; a few glyphs at ~86 pt fit (full → LogError)

    [MenuItem("Planet of Twins Tools/UI/Build Symbol Fallback Font")]
    public static void Build()
    {
        var source = PoTAssetLookup.FindUnique<Font>(PoTPaths.Named.SymbolFontSource);
        if (source == null) return;   // FindUnique already logged

        var reference = TMP_Settings.defaultFontAsset;
        if (reference == null)
        {
            Debug.LogError("[SymbolFont] TMP Settings has no default font asset: nothing to match the sampling size to.");
            return;
        }

        // Created Dynamic so TryAddCharacters can rasterise, then frozen to Static below.
        var font = TMP_FontAsset.CreateFontAsset(source, Mathf.RoundToInt(reference.faceInfo.pointSize),
                                                 reference.atlasPadding, reference.atlasRenderMode,
                                                 AtlasSize, AtlasSize, AtlasPopulationMode.Dynamic,
                                                 enableMultiAtlasSupport: false);
        if (font == null)
        {
            Debug.LogError($"[SymbolFont] TMP could not create a font asset from '{source.name}'.");
            return;
        }

        if (!font.TryAddCharacters(Symbols, out string missing) || !string.IsNullOrEmpty(missing))
        {
            Debug.LogError($"[SymbolFont] '{source.name}' could not bake \"{missing}\": the font lacks the glyph, or the " +
                           $"{AtlasSize}x{AtlasSize} atlas is full (raise AtlasSize).");
            Discard(font);
            return;
        }
        font.atlasPopulationMode = AtlasPopulationMode.Static;

        string name = PoTPaths.Named.SymbolFallbackFont;
        var existing = PoTAssetLookup.PathsOf<TMP_FontAsset>(name);
        if (existing.Count > 1)
        {
            Debug.LogError($"[SymbolFont] {existing.Count} font assets named '{name}' ({string.Join(", ", existing)}). " +
                           "Remove the extra copy first.");
            Discard(font);
            return;
        }

        // Write: replace wholesale so no stale sub-assets survive a re-bake (wherever the asset was moved to).
        string path = existing.Count == 1 ? existing[0] : $"{PoTPaths.Create.Fonts}/{name}.asset";
        PoTAssetLookup.EnsureFolder(Path.GetDirectoryName(path).Replace('\\', '/'));
        if (existing.Count == 1)
            AssetDatabase.DeleteAsset(path);

        var atlas = font.atlasTexture;
        font.name = name;
        atlas.name = name + " Atlas";
        font.material.name = name + " Material";
        AssetDatabase.CreateAsset(font, path);
        AssetDatabase.AddObjectToAsset(atlas, font);
        AssetDatabase.AddObjectToAsset(font.material, font);
        EditorUtility.SetDirty(font);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(path);
        var saved = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);

        // Register once in the global fallback list; a re-bake leaves the old entry dangling, so drop dead entries.
        var fallbacks = TMP_Settings.fallbackFontAssets;
        fallbacks.RemoveAll(f => f == null);
        if (!fallbacks.Contains(saved))
            fallbacks.Add(saved);
        EditorUtility.SetDirty(TMP_Settings.instance);
        AssetDatabase.SaveAssets();

        Debug.Log($"[SymbolFont] Baked {path}: {Symbols.Length} symbol(s) from '{source.name}' at " +
                  $"{saved.faceInfo.pointSize} pt / padding {saved.atlasPadding}, Static; TMP Settings fallbacks = " +
                  $"{fallbacks.Count}.");
    }

    // A failed bake leaves nothing behind: the font asset and the atlas + material TMP created with it.
    private static void Discard(TMP_FontAsset font)
    {
        if (font.material != null) Object.DestroyImmediate(font.material);
        foreach (var texture in font.atlasTextures)
            if (texture != null) Object.DestroyImmediate(texture);
        Object.DestroyImmediate(font);
    }
}
