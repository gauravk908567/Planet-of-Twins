using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// P1.4 (button glyphs) — serialized data mapping a (device kind, control path) to its glyph Sprite and TMP
/// sprite name. Pure config (R7): the runtime lookup dictionary is built by <see cref="InputGlyphResolver"/>,
/// not here. Authored by the baker (<b>Planet of Twins Tools ▸ Input ▸ Bake Glyph Map</b>) from the Kenney atlas
/// using a canonical control-path table — don't hand-edit unless adding a control the baker doesn't cover.
///
/// Loaded from Resources by the resolver (single tiny asset; the sprites load on demand), so it needs no
/// cross-scene serialized reference (R2) and no Persistent-scene wiring.
/// </summary>
[CreateAssetMenu(fileName = "InputGlyphMap", menuName = "PlanetOfTwins/Input/Glyph Map")]
public class InputGlyphMap : ScriptableObject
{
    [Serializable]
    public struct Entry
    {
        public InputDeviceKind kind;
        public string controlPath;    // e.g. "buttonSouth", "f", "leftButton"
        public Sprite sprite;
        public string tmpSpriteName;  // name in the TMP sprite asset, for inline <sprite name="…"> (P2)
    }

    [SerializeField] private List<Entry> entries = new List<Entry>();

    public IReadOnlyList<Entry> Entries => entries;

#if UNITY_EDITOR
    /// <summary>Baker-only: replace the whole entry list.</summary>
    public void EditorSetEntries(List<Entry> baked) => entries = baked;
#endif
}
