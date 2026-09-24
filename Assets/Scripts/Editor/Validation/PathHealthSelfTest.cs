using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Folder restructure Stage 0 (game.md §20.5) — the net under every folder move or rename. Checks every string the
/// project still resolves by path, key or name (all of them live in <see cref="PoTPaths"/>):
///   • every Resources key loads, as the type its runtime caller expects;
///   • every Named asset exists exactly once under Assets/, as the type its tool expects;
///   • every Create / Scan folder exists (a stale one would silently re-grow the old tree / scan nothing);
///   • baked Resources assets land on their runtime key (the Create.BakedResources root is a Resources folder);
///   • the folder-free enemy-prefab scan still finds enemies;
///   • no PoTPaths entry goes unchecked (a new key/name without a typed row below is itself a FAIL).
/// LogError per failure + one summary line. Run after EVERY restructure batch (and before committing one).
/// Menu: Planet of Twins Tools ▸ Validation ▸ Path Health.
/// </summary>
public static class PathHealthSelfTest
{
    // Resources key → the type its runtime caller loads it as.
    private static readonly (string key, Type type)[] ResourceChecks =
    {
        (PoTPaths.ResourceKeys.DevConfig,             typeof(DevConfig)),
        (PoTPaths.ResourceKeys.InputGlyphMap,         typeof(InputGlyphMap)),
        (PoTPaths.ResourceKeys.InputGlyphSpriteAsset, typeof(TMP_SpriteAsset)),
        (PoTPaths.ResourceKeys.ControllerDb,          typeof(TextAsset)),
        (PoTPaths.ResourceKeys.KeyLetterGlowGold,     typeof(Material)),
        (PoTPaths.ResourceKeys.KeyLetterGlowViolet,   typeof(Material)),
        (PoTPaths.ResourceKeys.KeyLetterGlowNeutral,  typeof(Material)),
        (PoTPaths.ResourceKeys.Keycap,                typeof(Material)),
    };

    // Named asset → the type its tool looks it up as.
    private static readonly (string name, Type type)[] NamedChecks =
    {
        (PoTPaths.Named.DualCheckpointPrefab,    typeof(GameObject)),
        (PoTPaths.Named.CheckpointTrailMaterial, typeof(Material)),
        (PoTPaths.Named.RingTimerSharedMaterial, typeof(Material)),
        (PoTPaths.Named.GameAudioMixer,          typeof(AudioMixer)),
        (PoTPaths.Named.PcRenderPipelineAsset,   typeof(UniversalRenderPipelineAsset)),
        (PoTPaths.Named.PcRendererData,          typeof(ScriptableRendererData)),
        (PoTPaths.Named.SeekEnergyUtilProfile,   typeof(UtilityWeightProfile)),
        (PoTPaths.Named.DefaultPoiEnergyProfile, typeof(PoiEnergyProfile)),
        (PoTPaths.Named.FxIdsScript,             typeof(MonoScript)),
    };

    [MenuItem("Planet of Twins Tools/Validation/Path Health")]
    public static void Run()
    {
        int pass = 0, fail = 0;
        void Check(bool ok, string label)
        {
            if (ok) pass++;
            else { fail++; Debug.LogError($"[PathHealth] FAIL: {label}"); }
        }

        // ── Coverage: every PoTPaths key/name has a typed row above ──
        foreach (var missing in ConstValues(typeof(PoTPaths.ResourceKeys)).Except(ResourceChecks.Select(c => c.key)))
            Check(false, $"PoTPaths.ResourceKeys '{missing}' has no typed row in PathHealthSelfTest.ResourceChecks");
        foreach (var missing in ConstValues(typeof(PoTPaths.Named)).Except(NamedChecks.Select(c => c.name)))
            Check(false, $"PoTPaths.Named '{missing}' has no typed row in PathHealthSelfTest.NamedChecks");

        // ── Resources keys load as their caller's type ──
        foreach (var (key, type) in ResourceChecks)
            Check(Resources.Load(key, type) != null,
                  $"Resources key '{key}' does not load as {type.Name} — the asset left its Resources folder or was renamed");

        // ── Named assets: exactly one each ──
        foreach (var (name, type) in NamedChecks)
        {
            var paths = PoTAssetLookup.PathsOf(name, type);
            Check(paths.Count == 1, paths.Count == 0
                ? $"no {type.Name} named '{name}' under Assets/ (renamed or deleted — update PoTPaths.Named)"
                : $"{paths.Count} {type.Name} assets named '{name}' ({string.Join(", ", paths)}) — ambiguous");
        }

        // ── Create / Scan folders exist ──
        foreach (var folder in ConstValues(typeof(PoTPaths.Create)).Concat(ConstValues(typeof(PoTPaths.Scan))))
            Check(AssetDatabase.IsValidFolder(folder),
                  $"folder '{folder}' does not exist — it moved; update PoTPaths (tools would re-create or scan nothing)");

        // ── Baked Resources assets resolve on their runtime key ──
        Check(PoTPaths.Create.BakedResources.EndsWith("/Resources"),
              $"PoTPaths.Create.BakedResources '{PoTPaths.Create.BakedResources}' is not a Resources folder — a first bake would be unloadable");

        // ── Folder-free scans still find their content ──
        int enemies = PoTAssetLookup.PrefabsWith<Enemy>().Count;
        Check(enemies > 0, "the folder-free enemy prefab scan found no prefab with an Enemy component");

        string summary = $"[PathHealth] {pass} passed, {fail} failed ({enemies} enemy prefab(s) found folder-free).";
        if (fail == 0) Debug.Log(summary + " Every path, key and name in PoTPaths resolves.");
        else Debug.LogError(summary + " Fix PoTPaths (or the moved asset) before committing.");
    }

    // The string values of every const field on a PoTPaths nested class.
    private static IEnumerable<string> ConstValues(Type holder) =>
        holder.GetFields(BindingFlags.Public | BindingFlags.Static)
              .Where(f => f.IsLiteral && f.FieldType == typeof(string))
              .Select(f => (string)f.GetRawConstantValue());
}
