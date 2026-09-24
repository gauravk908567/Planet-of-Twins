using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Editor tooling for the button-glyph system (item 5). Two one-shot actions under
/// <b>Planet of Twins Tools ▸ Input</b>:
///   • <b>Import Glyphs as Sprites</b> — set every texture under <see cref="GlyphRoot"/> to a UI-ready Single
///     Sprite import (Kenney PNGs land as Default in a 3D/URP project, unusable as UI Image sprites / TMP).
///   • <b>Bake Glyph Map</b> — (re)generate the Resources <c>InputGlyphMap</c> (the one the runtime loads, wherever it
///     lives; created under <see cref="PoTPaths.Create.BakedResources"/> if none exists) from the atlas using the
///     canonical control-path → Kenney-filename table below, so <see cref="InputGlyphResolver"/> can look up a
///     glyph by (device kind, control path). Re-run after adding a binding/glyph.
/// Both are idempotent.
/// </summary>
public static class InputGlyphImporter
{
    private const string GlyphRoot = PoTPaths.Scan.InputGlyphAtlas;

    // ── Canonical table: (device kind, Input System control path, atlas folder, Kenney filename stem) ──
    // The control paths are what IInputProvider.TryGetBindingControlPath yields; the stems are verified to exist
    // in the imported Kenney subset. Gamepad → Xbox glyphs for ALL pads (OW2 model + our SDL normalisation).
    private static readonly (InputDeviceKind kind, string path, string folder, string stem)[] Table =
    {
        // ── Keyboard & Mouse ──
        (InputDeviceKind.KeyboardMouse, "w",           "Keyboard", "keyboard_w"),
        (InputDeviceKind.KeyboardMouse, "a",           "Keyboard", "keyboard_a"),
        (InputDeviceKind.KeyboardMouse, "s",           "Keyboard", "keyboard_s"),
        (InputDeviceKind.KeyboardMouse, "d",           "Keyboard", "keyboard_d"),
        (InputDeviceKind.KeyboardMouse, "e",           "Keyboard", "keyboard_e"),
        (InputDeviceKind.KeyboardMouse, "q",           "Keyboard", "keyboard_q"),
        (InputDeviceKind.KeyboardMouse, "c",           "Keyboard", "keyboard_c"),
        (InputDeviceKind.KeyboardMouse, "r",           "Keyboard", "keyboard_r"),
        (InputDeviceKind.KeyboardMouse, "f",           "Keyboard", "keyboard_f"),
        (InputDeviceKind.KeyboardMouse, "x",           "Keyboard", "keyboard_x"),
        (InputDeviceKind.KeyboardMouse, "b",           "Keyboard", "keyboard_b"),
        (InputDeviceKind.KeyboardMouse, "h",           "Keyboard", "keyboard_h"),
        (InputDeviceKind.KeyboardMouse, "leftShift",   "Keyboard", "keyboard_shift"),
        (InputDeviceKind.KeyboardMouse, "tab",         "Keyboard", "keyboard_tab"),
        (InputDeviceKind.KeyboardMouse, "escape",      "Keyboard", "keyboard_escape"),
        (InputDeviceKind.KeyboardMouse, "space",       "Keyboard", "keyboard_space"),
        (InputDeviceKind.KeyboardMouse, "enter",       "Keyboard", "keyboard_enter"),
        (InputDeviceKind.KeyboardMouse, "upArrow",     "Keyboard", "keyboard_arrow_up"),
        (InputDeviceKind.KeyboardMouse, "downArrow",   "Keyboard", "keyboard_arrow_down"),
        (InputDeviceKind.KeyboardMouse, "leftArrow",   "Keyboard", "keyboard_arrow_left"),
        (InputDeviceKind.KeyboardMouse, "rightArrow",  "Keyboard", "keyboard_arrow_right"),
        (InputDeviceKind.KeyboardMouse, "leftButton",  "Keyboard", "mouse_left"),
        (InputDeviceKind.KeyboardMouse, "rightButton", "Keyboard", "mouse_right"),

        // ── Gamepad (Xbox glyphs for all pads) ──
        (InputDeviceKind.Gamepad, "buttonSouth",     "Xbox", "xbox_button_a"),
        (InputDeviceKind.Gamepad, "buttonEast",      "Xbox", "xbox_button_b"),
        (InputDeviceKind.Gamepad, "buttonWest",      "Xbox", "xbox_button_x"),
        (InputDeviceKind.Gamepad, "buttonNorth",     "Xbox", "xbox_button_y"),
        (InputDeviceKind.Gamepad, "leftShoulder",    "Xbox", "xbox_lb"),
        (InputDeviceKind.Gamepad, "rightShoulder",   "Xbox", "xbox_rb"),
        (InputDeviceKind.Gamepad, "leftTrigger",     "Xbox", "xbox_lt"),
        (InputDeviceKind.Gamepad, "rightTrigger",    "Xbox", "xbox_rt"),
        (InputDeviceKind.Gamepad, "start",           "Xbox", "xbox_button_menu"),
        (InputDeviceKind.Gamepad, "select",          "Xbox", "xbox_button_view"),
        (InputDeviceKind.Gamepad, "leftStick",       "Xbox", "xbox_stick_l"),
        (InputDeviceKind.Gamepad, "rightStick",      "Xbox", "xbox_stick_r"),
        (InputDeviceKind.Gamepad, "leftStickPress",  "Xbox", "xbox_ls"),
        (InputDeviceKind.Gamepad, "rightStickPress", "Xbox", "xbox_rs"),
        (InputDeviceKind.Gamepad, "dpad",            "Xbox", "xbox_dpad"),
    };

    [MenuItem("Planet of Twins Tools/Input/Import Glyphs as Sprites")]
    public static void ImportGlyphsAsSprites()
    {
        if (!AssetDatabase.IsValidFolder(GlyphRoot))
        {
            Debug.LogError($"[InputGlyphImporter] Folder not found: {GlyphRoot}");
            return;
        }

        var guids = AssetDatabase.FindAssets("t:Texture2D", new[] { GlyphRoot });
        int changed = 0;

        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) continue;

            bool dirty = false;
            if (importer.textureType != TextureImporterType.Sprite)   { importer.textureType = TextureImporterType.Sprite; dirty = true; }
            if (importer.spriteImportMode != SpriteImportMode.Single) { importer.spriteImportMode = SpriteImportMode.Single; dirty = true; }
            if (!importer.alphaIsTransparency)                        { importer.alphaIsTransparency = true; dirty = true; }
            if (importer.mipmapEnabled)                               { importer.mipmapEnabled = false; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Clamp)           { importer.wrapMode = TextureWrapMode.Clamp; dirty = true; }

            if (dirty) { importer.SaveAndReimport(); changed++; }
        }

        Debug.Log($"[InputGlyphImporter] Set {changed}/{guids.Length} glyph texture(s) to UI Sprite import under {GlyphRoot}.");
    }

    [MenuItem("Planet of Twins Tools/Input/Bake Glyph Map")]
    public static void BakeGlyphMap()
    {
        var entries = new List<InputGlyphMap.Entry>(Table.Length);
        int missing = 0;

        foreach (var t in Table)
        {
            string assetPath = $"{GlyphRoot}/{t.folder}/{t.stem}.png";
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
            if (sprite == null)
            {
                Debug.LogWarning($"[GlyphBaker] Missing sprite {assetPath} (control '{t.path}', {t.kind}) — run 'Import Glyphs as Sprites' first if it exists as a texture.");
                missing++;
                continue;
            }
            entries.Add(new InputGlyphMap.Entry
            {
                kind = t.kind,
                controlPath = t.path,
                sprite = sprite,
                tmpSpriteName = t.stem,   // TMP sprite name (P2 builds the sprite asset with these names)
            });
        }

        // Update the map the runtime actually loads (by its Resources key, any Resources folder); create one only if none exists.
        var map = Resources.Load<InputGlyphMap>(PoTPaths.ResourceKeys.InputGlyphMap);
        bool created = map == null;
        string mapPath;
        if (created)
        {
            mapPath = PoTPaths.Create.BakedResourceAsset(PoTPaths.ResourceKeys.InputGlyphMap);
            PoTAssetLookup.EnsureFolder(System.IO.Path.GetDirectoryName(mapPath).Replace('\\', '/'));
            map = ScriptableObject.CreateInstance<InputGlyphMap>();
            AssetDatabase.CreateAsset(map, mapPath);
        }
        else mapPath = AssetDatabase.GetAssetPath(map);

        map.EditorSetEntries(entries);
        EditorUtility.SetDirty(map);
        AssetDatabase.SaveAssets();

        Debug.Log($"[GlyphBaker] {(created ? "Created" : "Updated")} {mapPath} — {entries.Count}/{Table.Length} entries baked, {missing} missing.");
    }
}
