using System.Collections.Generic;
using System.IO;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore;

/// <summary>
/// Editor tool for the button-glyph system, P2b (inline glyphs). Bakes ONE packed <see cref="TMP_SpriteAsset"/>
/// from the Kenney atlas so TMP text can draw a glyph inline via the name-only tag <c>&lt;sprite name="stem"&gt;</c>
/// (resolved against a label's assigned sprite asset — TMP rejects the combined <c>sprite="asset" name=…</c> form).
/// TMP inline sprites need a SINGLE texture + material (our 492 glyphs are individual PNGs), so this:
///   1. reads the already-baked <see cref="InputGlyphMap"/> (Resources/InputGlyphMap) — its entries carry both the
///      source <c>sprite</c> and the <c>tmpSpriteName</c>, so the packed names match the resolver 1:1 (no drift);
///   2. loads each used PNG as a readable texture and <see cref="Texture2D.PackTextures"/> into one atlas;
///   3. hand-builds spriteGlyphTable + spriteCharacterTable (uniform 1em height) + a TextMeshPro/Sprite material;
///   4. writes the Resources <c>Sprites/InputGlyphs</c> asset (the one InputGlyphText loads, wherever it lives; created
///      under <see cref="PoTPaths.Create.BakedResources"/> if none exists — TMP's default sprite search path, so the
///      bare <c>&lt;sprite="InputGlyphs" …&gt;</c> tag resolves without touching TMP Settings).
///
/// Idempotent: re-run after the glyph map changes. Menu: <b>Planet of Twins Tools ▸ Input ▸ Bake TMP Glyph Sprite Asset</b>.
/// </summary>
public static class InputGlyphTmpBaker
{
    private const string MapResourcePath = PoTPaths.ResourceKeys.InputGlyphMap;
    private const string OutResourceKey  = PoTPaths.ResourceKeys.InputGlyphSpriteAsset;   // <sprite="InputGlyphs" name="…">
    private const int    PointSize       = 128;                             // em reference for sprite scaling
    private const float  RenderEm        = 2.0f;                            // glyph height in em (bigger — reads clearly next to text)
    private const float  BaselineFrac    = 0.78f;                           // top-of-glyph above baseline as a fraction of its height (centers it on the line)
    private const int    Padding         = 2;
    private const int    MaxAtlasSize    = 2048;

    [MenuItem("Planet of Twins Tools/Input/Bake TMP Glyph Sprite Asset")]
    public static void BakeTmpSpriteAsset()
    {
        var map = Resources.Load<InputGlyphMap>(MapResourcePath);
        if (map == null)
        {
            Debug.LogError($"[TmpGlyphBaker] No '{MapResourcePath}' in Resources — run 'Bake Glyph Map' first.");
            return;
        }

        // Distinct (tmpSpriteName → source PNG path), preserving map order. Dedupe: one stem may back several paths.
        var stems = new List<string>();
        var srcPaths = new List<string>();
        var seen = new HashSet<string>();
        foreach (var e in map.Entries)
        {
            if (e.sprite == null || string.IsNullOrEmpty(e.tmpSpriteName) || !seen.Add(e.tmpSpriteName)) continue;
            string path = AssetDatabase.GetAssetPath(e.sprite);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                Debug.LogWarning($"[TmpGlyphBaker] Source PNG for '{e.tmpSpriteName}' not found ({path}) — skipped.");
                seen.Remove(e.tmpSpriteName);
                continue;
            }
            stems.Add(e.tmpSpriteName);
            srcPaths.Add(path);
        }

        if (stems.Count == 0)
        {
            Debug.LogError("[TmpGlyphBaker] No usable glyph entries in the map — nothing to bake.");
            return;
        }

        // Load each PNG as a readable texture (LoadImage guarantees readable RGBA32 — no importer fiddling).
        var sources = new Texture2D[stems.Count];
        for (int i = 0; i < stems.Count; i++)
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            tex.LoadImage(File.ReadAllBytes(srcPaths[i]));
            tex.name = stems[i];
            sources[i] = tex;
        }

        // Pack into one atlas (UV rects, origin bottom-left — matches GlyphRect).
        var atlas = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = "InputGlyphs Atlas" };
        Rect[] uvs = atlas.PackTextures(sources, Padding, MaxAtlasSize);
        atlas.filterMode = FilterMode.Bilinear;
        atlas.wrapMode = TextureWrapMode.Clamp;
        atlas.Apply(false, false);
        foreach (var s in sources) Object.DestroyImmediate(s);

        int aw = atlas.width, ah = atlas.height;
        var glyphTable = new List<TMP_SpriteGlyph>(stems.Count);
        var charTable = new List<TMP_SpriteCharacter>(stems.Count);

        for (int i = 0; i < stems.Count; i++)
        {
            Rect uv = uvs[i];
            int px = Mathf.RoundToInt(uv.x * aw);
            int py = Mathf.RoundToInt(uv.y * ah);
            int pw = Mathf.RoundToInt(uv.width * aw);
            int ph = Mathf.RoundToInt(uv.height * ah);
            float aspect = ph > 0 ? (float)pw / ph : 1f;

            // Uniform RenderEm height regardless of source px; width follows aspect. bearingY < height dips the
            // glyph below the baseline so it sits centred on the text line rather than riding high like a superscript.
            float h = PointSize * RenderEm;
            float w = h * aspect;
            var metrics = new GlyphMetrics(w, h, 0f, h * BaselineFrac, w);
            var rect = new GlyphRect(px, py, pw, ph);

            var glyph = new TMP_SpriteGlyph
            {
                index = (uint)i,
                metrics = metrics,
                glyphRect = rect,
                scale = 1f,
            };
            glyphTable.Add(glyph);

            var ch = new TMP_SpriteCharacter(0xE000u + (uint)i, glyph)
            {
                name = stems[i],
                glyphIndex = (uint)i,
                scale = 1f,
            };
            charTable.Add(ch);
        }

        var shader = Shader.Find("TextMeshPro/Sprite");
        if (shader == null)
        {
            Debug.LogError("[TmpGlyphBaker] Shader 'TextMeshPro/Sprite' not found — is TMP essentials imported?");
            return;
        }
        var material = new Material(shader) { name = "InputGlyphs Material" };
        material.SetTexture(ShaderUtilities.ID_MainTex, atlas);

        var spriteAsset = ScriptableObject.CreateInstance<TMP_SpriteAsset>();
        spriteAsset.name = "InputGlyphs";
        // 'version' is external-read-only; stamp the backing field so UpdateLookupTables() skips the legacy
        // spriteInfoList→table UPGRADE path (which NREs on our fresh, directly-filled tables).
        typeof(TMP_Asset).GetField("m_Version", BindingFlags.Instance | BindingFlags.NonPublic)
                         ?.SetValue(spriteAsset, "1.1.0");
        spriteAsset.spriteSheet = atlas;
        spriteAsset.material = material;
        // The table properties are external-read-only (internal set) but expose the live backing list — fill in place.
        spriteAsset.spriteGlyphTable.Clear();
        spriteAsset.spriteGlyphTable.AddRange(glyphTable);
        spriteAsset.spriteCharacterTable.Clear();
        spriteAsset.spriteCharacterTable.AddRange(charTable);

        var fi = spriteAsset.faceInfo;
        fi.familyName = "InputGlyphs";
        fi.pointSize = PointSize;
        fi.scale = 1f;
        fi.lineHeight = PointSize;
        fi.ascentLine = PointSize;
        fi.descentLine = 0f;
        fi.baseline = 0f;
        spriteAsset.faceInfo = fi;

        spriteAsset.UpdateLookupTables();

        // Write: replace wholesale so no stale sub-assets survive a re-bake. Target = the asset the runtime loads
        // (by its Resources key, wherever it was moved); first bake → the PoTPaths create location.
        var existing = Resources.Load<TMP_SpriteAsset>(OutResourceKey);
        string outPath = existing != null
            ? AssetDatabase.GetAssetPath(existing)
            : PoTPaths.Create.BakedResourceAsset(OutResourceKey);
        PoTAssetLookup.EnsureFolder(Path.GetDirectoryName(outPath).Replace('\\', '/'));
        if (existing != null)
            AssetDatabase.DeleteAsset(outPath);

        AssetDatabase.CreateAsset(spriteAsset, outPath);
        atlas.name = "InputGlyphs Atlas";
        material.name = "InputGlyphs Material";
        AssetDatabase.AddObjectToAsset(atlas, spriteAsset);
        AssetDatabase.AddObjectToAsset(material, spriteAsset);
        EditorUtility.SetDirty(spriteAsset);
        AssetDatabase.SaveAssets();
        AssetDatabase.ImportAsset(outPath);

        Debug.Log($"[TmpGlyphBaker] Baked {outPath} — {stems.Count} glyphs into a {aw}x{ah} atlas. " +
                  "Inline via InputGlyphText.Apply (emits name-only <sprite name=\"keyboard_f\"> against this asset).");
    }
}
