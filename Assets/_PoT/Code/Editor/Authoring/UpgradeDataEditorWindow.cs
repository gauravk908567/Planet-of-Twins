using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
 /// Tools ▸ Planet of Twins ▸ Upgrade Data Editor (P15). One table per
    /// AbilityUpgradeData tree: rows = nodes, columns = the node stats, edited in place through
    /// SerializedObject (Undo-recorded, assets saved by Unity as usual). Columns auto-hide when
    /// EVERY node in the tree still has the field's default value — AbilityUpgradeNode is a wide
 /// union of per-ability stats and each tree uses a few. Tier VFX is the `_t[n]`
    /// naming convention in the CueBookData — no node column (help box below explains it).
    /// </summary>
    public class UpgradeDataEditorWindow : EditorWindow
    {
        private List<AbilityUpgradeData> _assets = new List<AbilityUpgradeData>();
        private int _assetIndex;
        private bool _showAllColumns;
        private Vector2 _scroll;

        // Fields never shown as table columns (edited in the normal Inspector instead).
        private static readonly HashSet<string> Skip = new HashSet<string> { "previewClip", "description" };
        private static readonly AbilityUpgradeNode Defaults = new AbilityUpgradeNode();

        [MenuItem("Tools/Planet of Twins/Upgrade Data Editor")]
        public static void Open()
        {
            var w = GetWindow<UpgradeDataEditorWindow>("PoT Upgrades");
            w.minSize = new Vector2(700, 300);
            w.Reload();
        }

        private void Reload()
        {
            _assets = AssetDatabase.FindAssets("t:AbilityUpgradeData")
                .Select(g => AssetDatabase.LoadAssetAtPath<AbilityUpgradeData>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(a => a != null)
                .OrderBy(a => a.name)
                .ToList();
            _assetIndex = Mathf.Clamp(_assetIndex, 0, Mathf.Max(0, _assets.Count - 1));
        }

        private void OnGUI()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("Reload", EditorStyles.toolbarButton, GUILayout.Width(60))) Reload();
                if (_assets.Count > 0)
                    _assetIndex = EditorGUILayout.Popup(_assetIndex, _assets.Select(a => a.name).ToArray(),
                        EditorStyles.toolbarPopup, GUILayout.Width(260));
                _showAllColumns = GUILayout.Toggle(_showAllColumns, "All columns", EditorStyles.toolbarButton);
                GUILayout.FlexibleSpace();
                if (_assets.Count > 0 && GUILayout.Button("Ping asset", EditorStyles.toolbarButton, GUILayout.Width(70)))
                    EditorGUIUtility.PingObject(_assets[_assetIndex]);
            }

            if (_assets.Count == 0)
            {
                EditorGUILayout.HelpBox("No AbilityUpgradeData assets found.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
 "TIER VFX: to change an effect at upgrade tier N, author a cue id named " +
                "<baseId>_tN in the ability's Cue Book (e.g. stun_cast_t2). Every id the ability plays " +
                "resolves per-id: _tN → _t(N-1) → … → _t1 → base. An id WITHOUT _t[n] variants is used " +
                "for ALL tiers — so a 3-id effect can tier just one id and leave the rest alone.",
                MessageType.Info);

            DrawTable(_assets[_assetIndex]);
            DrawBookPanel(_assets[_assetIndex]);
        }

        // ── Linked Cue Book panel (2026-07-10 tool merge: upgrade-data ↔ cue-book helper) ──
        // Shows the linked book's ids grouped by base id with the tiers each has, and generates a
        // missing <base>_tN entry as a DEEP COPY of the highest existing variant (SerializedProperty
        // DuplicateCommand — Unity's own array duplicate, Undo-recorded; never deletes or renames).
        private Vector2 _bookScroll;

        private void DrawBookPanel(AbilityUpgradeData asset)
        {
            EditorGUILayout.Space(6);
            GUILayout.Label("Linked Cue Book (tier VFX authoring)", EditorStyles.boldLabel);

            var assetSo = new SerializedObject(asset);
            var bookProp = assetSo.FindProperty("cueBook");
            EditorGUILayout.PropertyField(bookProp, new GUIContent("Cue Book"));
            if (assetSo.ApplyModifiedProperties()) EditorUtility.SetDirty(asset);

            var book = asset.cueBook;
            if (book == null)
            {
                EditorGUILayout.HelpBox("Link this ability's Cue Book to see its ids and author _tN tier variants here.", MessageType.None);
                return;
            }

            int maxTier = asset.nodes?.Count ?? 0;

            // group ids: "stun_cast_t2" → base "stun_cast", tier 2; "stun_cast" → tier 0 (base).
            var groups = new SortedDictionary<string, SortedSet<int>>();
            foreach (var entry in book.effects)
            {
                if (entry == null || string.IsNullOrEmpty(entry.id)) continue;
                string baseId = entry.id;
                int tier = 0;
                var m = System.Text.RegularExpressions.Regex.Match(entry.id, @"^(.*)_t(\d+)$");
                if (m.Success) { baseId = m.Groups[1].Value; tier = int.Parse(m.Groups[2].Value); }
                if (!groups.TryGetValue(baseId, out var tiers)) groups[baseId] = tiers = new SortedSet<int>();
                tiers.Add(tier);
            }

            _bookScroll = EditorGUILayout.BeginScrollView(_bookScroll, GUILayout.MaxHeight(160));
            foreach (var kv in groups)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(kv.Key, GUILayout.Width(220));
                    GUILayout.Label(string.Join(", ", kv.Value.Select(t => t == 0 ? "base" : $"_t{t}")),
                        EditorStyles.miniLabel, GUILayout.Width(160));

                    int next = kv.Value.Max + 1;
                    using (new EditorGUI.DisabledScope(next > maxTier))
                        if (GUILayout.Button($"+ _t{next}", GUILayout.Width(56)))
                            AddTierVariant(book, kv.Key, kv.Value.Max, next);
                    if (next > maxTier)
                        GUILayout.Label(maxTier == 0 ? "(tree has no nodes)" : "(all tiers authored)", EditorStyles.miniLabel);
                }
            }
            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label($"{book.effects.Count} effect(s) · tree has {maxTier} node(s) → tiers _t1…_t{maxTier}", EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Ping book", GUILayout.Width(70))) EditorGUIUtility.PingObject(book);
            }
        }

        /// <summary>Duplicates the &lt;baseId&gt;[_tSource] entry as &lt;baseId&gt;_tNext (deep copy, Undo-able).</summary>
        private static void AddTierVariant(CueBookData book, string baseId, int sourceTier, int nextTier)
        {
            string sourceId = sourceTier == 0 ? baseId : $"{baseId}_t{sourceTier}";
            string newId = $"{baseId}_t{nextTier}";
            if (book.Has(newId)) return; // never overwrite

            var so = new SerializedObject(book);
            var effects = so.FindProperty("effects");
            for (int i = 0; i < effects.arraySize; i++)
            {
                if (effects.GetArrayElementAtIndex(i).FindPropertyRelative("id").stringValue != sourceId) continue;
                effects.GetArrayElementAtIndex(i).DuplicateCommand(); // Unity's own deep array duplicate → index i+1
                effects.GetArrayElementAtIndex(i + 1).FindPropertyRelative("id").stringValue = newId;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(book);
                Debug.Log($"[Upgrades] Added '{newId}' to '{book.name}' (copy of '{sourceId}') — tune its elements for tier {nextTier}.", book);
                return;
            }
            Debug.LogError($"[Upgrades] Source id '{sourceId}' not found in '{book.name}' — book changed underneath? Reload.", book);
        }

        private void DrawTable(AbilityUpgradeData asset)
        {
            var so = new SerializedObject(asset);
            var nodesProp = so.FindProperty("nodes");
            if (nodesProp == null) { EditorGUILayout.HelpBox("No nodes list.", MessageType.Warning); return; }

            var columns = ColumnsFor(asset);

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            // header
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label("#", EditorStyles.boldLabel, GUILayout.Width(24));
                GUILayout.Label("label", EditorStyles.boldLabel, GUILayout.Width(130));
                GUILayout.Label("cost", EditorStyles.boldLabel, GUILayout.Width(44));
                foreach (var col in columns)
                    GUILayout.Label(Shorten(col.Name), EditorStyles.boldLabel, GUILayout.Width(ColWidth(col)));
                GUILayout.Label("tier vfx", EditorStyles.boldLabel, GUILayout.Width(120));
            }

            for (int i = 0; i < nodesProp.arraySize; i++)
            {
                var node = nodesProp.GetArrayElementAtIndex(i);
                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Label(i.ToString(), GUILayout.Width(24));
                    Field(node, "label", 130);
                    Field(node, "pointCost", 44);
                    foreach (var col in columns)
                        Field(node, col.Name, ColWidth(col));
                    // Tier N = node index+1 — the suffix this node's unlock activates in the book.
                    GUILayout.Label($"ids: <base>_t{i + 1}", EditorStyles.miniLabel, GUILayout.Width(120));
                }
            }

            EditorGUILayout.EndScrollView();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("+ Add node", GUILayout.Width(90)))
                    nodesProp.arraySize++;
                GUILayout.FlexibleSpace();
                GUILayout.Label($"{nodesProp.arraySize} node(s) — edits are Undo-recorded", EditorStyles.miniLabel);
            }

            // ApplyModifiedProperties registers Undo and dirties the asset — the whole contract.
            if (so.ApplyModifiedProperties())
                EditorUtility.SetDirty(asset);
        }

        private static void Field(SerializedProperty node, string field, float width)
        {
            var prop = node.FindPropertyRelative(field);
            if (prop == null) { GUILayout.Label("—", GUILayout.Width(width)); return; }
            EditorGUILayout.PropertyField(prop, GUIContent.none, GUILayout.Width(width));
        }

        /// <summary>Numeric/string node fields worth a column: skip-list removed, and (unless
        /// "All columns") only fields where ANY node deviates from the class default.</summary>
        private List<FieldInfo> ColumnsFor(AbilityUpgradeData asset)
        {
            var fields = typeof(AbilityUpgradeNode)
                .GetFields(BindingFlags.Public | BindingFlags.Instance)
                .Where(f => !Skip.Contains(f.Name) && f.Name != "label" && f.Name != "pointCost")
                .Where(f => f.FieldType == typeof(float) || f.FieldType == typeof(int))
                .ToList();

            if (_showAllColumns) return fields;

            return fields.Where(f => asset.nodes != null && asset.nodes.Any(n =>
                n != null && !Equals(f.GetValue(n), f.GetValue(Defaults)))).ToList();
        }

        private static float ColWidth(FieldInfo f) => 58f;

        // "coalesceRadiusBonus" → "coalRadius" style shortening so headers fit their columns.
        private static string Shorten(string name)
        {
            string s = name
                .Replace("Reduction", "−").Replace("Bonus", "+").Replace("Multiplier", "×")
                .Replace("coalesce", "coal").Replace("empower", "emp").Replace("accord", "acc")
                .Replace("soul", "soul").Replace("pulse", "pulse");
            return s.Length > 12 ? s.Substring(0, 12) : s;
        }
    }
}
