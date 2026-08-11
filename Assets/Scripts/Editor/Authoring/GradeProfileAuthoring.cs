using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PlanetOfTwins.EditorTools
{
    /// <summary>
    /// P17 authoring: creates the 6 story-grade VolumeProfiles + the FailureReset sting
 /// profile under Assets/Settings/Grading/. Idempotent — existing
    /// assets are left untouched (author-tuned values survive re-runs).
 /// Values are the starting grade; the user tunes in Unity.
    /// </summary>
    public static class GradeProfileAuthoring
    {
        private const string Dir = "Assets/Settings/Grading";

        // Menu retired (tool consolidation 2026-07-10) — invoked as a Fix from the Scene Health
        // Dashboard (Persistent Volumes recipe: missing grade profiles).
        public static void CreateAll()
        {
            if (!AssetDatabase.IsValidFolder(Dir))
                AssetDatabase.CreateFolder("Assets/Settings", "Grading");

            int created = 0;

            // ── Grade_Act1_Warm — warm gold lift, lowest vignette, sat 0 ─────────
            created += Create("Grade_Act1_Warm", p =>
            {
                var ca = Add<ColorAdjustments>(p);
                ca.contrast.Override(10f);
                var wb = Add<WhiteBalance>(p);
                wb.temperature.Override(10f);
                Vig(p, 0.20f);
                Grain(p, 0.15f);
            });

            // ── Grade_Shock — hard cut: crushed shadows, sat −30, CA spike, cold ─
            created += Create("Grade_Shock", p =>
            {
                var ca = Add<ColorAdjustments>(p);
                ca.contrast.Override(30f);
                ca.saturation.Override(-30f);
                var wb = Add<WhiteBalance>(p);
                wb.temperature.Override(-20f);
                var lgg = Add<LiftGammaGain>(p);
                lgg.lift.Override(new Vector4(0.92f, 0.92f, 0.96f, -0.05f)); // crushed, blue-leaning
                var cab = Add<ChromaticAberration>(p);
                cab.intensity.Override(0.4f);
                Vig(p, 0.35f);
            });

            // ── Grade_EarlyFear — cool shift, vignette 0.35, grain up ────────────
            created += Create("Grade_EarlyFear", p =>
            {
                var ca = Add<ColorAdjustments>(p);
                ca.contrast.Override(10f);
                ca.saturation.Override(-5f);
                var wb = Add<WhiteBalance>(p);
                wb.temperature.Override(-10f);
                Vig(p, 0.35f);
                Grain(p, 0.25f);
            });

            // ── Grade_MidPurpose — neutral-cool, contrast +15 (resolve) ──────────
            created += Create("Grade_MidPurpose", p =>
            {
                var ca = Add<ColorAdjustments>(p);
                ca.contrast.Override(15f);
                ca.saturation.Override(-5f);
                var wb = Add<WhiteBalance>(p);
                wb.temperature.Override(-5f);
                Vig(p, 0.30f);
                Grain(p, 0.18f);
            });

            // ── Grade_LateChaos — split-tone teal shadows / gold highlights ──────
            created += Create("Grade_LateChaos", p =>
            {
                var st = Add<SplitToning>(p);
                st.shadows.Override(FromHex("17909A"));   // Pure Current body teal
                st.highlights.Override(FromHex("FFCE52")); // Luminari gold
                st.balance.Override(0f);
                var bloom = Add<Bloom>(p);
                bloom.intensity.Override(0.7f);
                var ca = Add<ColorAdjustments>(p);
                ca.contrast.Override(12f);
                ca.saturation.Override(-8f);
                Vig(p, 0.30f);
                Grain(p, 0.18f);
            });

            // ── Grade_Ending_Losing — coldest + drained, lifted blacks ───────────
            created += Create("Grade_Ending_Losing", p =>
            {
                var ca = Add<ColorAdjustments>(p);
                ca.contrast.Override(8f);
                ca.saturation.Override(-20f);
                var wb = Add<WhiteBalance>(p);
                wb.temperature.Override(-15f);
                var lgg = Add<LiftGammaGain>(p);
                lgg.lift.Override(new Vector4(1f, 1f, 1.02f, 0.05f)); // lifted, cold blacks
                Vig(p, 0.35f);
                Grain(p, 0.22f);
            });

            // ── FailureReset_Sting — desat −80 + vignette 0.45 + CA 0.25 ─────────
            created += Create("FailureReset_Sting", p =>
            {
                var ca = Add<ColorAdjustments>(p);
                ca.saturation.Override(-80f);
                var cab = Add<ChromaticAberration>(p);
                cab.intensity.Override(0.25f);
                Vig(p, 0.45f);
            });

            AssetDatabase.SaveAssets();
            Debug.Log($"[GradeProfileAuthoring] Done — {created} profile(s) created (existing left untouched) in {Dir}.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────────
        private static int Create(string name, System.Action<VolumeProfile> author)
        {
            string path = Dir + "/" + name + ".asset";
            if (AssetDatabase.LoadAssetAtPath<VolumeProfile>(path) != null) return 0;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, path);
            author(profile);
            EditorUtility.SetDirty(profile);
            return 1;
        }

        private static T Add<T>(VolumeProfile p) where T : VolumeComponent
        {
            var c = p.Add<T>(false); // sub-asset saved with the profile
            AssetDatabase.AddObjectToAsset(c, p);
            return c;
        }

        private static void Vig(VolumeProfile p, float intensity)
        {
            var v = Add<Vignette>(p);
            v.intensity.Override(intensity);
            v.smoothness.Override(0.4f);
        }

        private static void Grain(VolumeProfile p, float intensity)
        {
            var g = Add<FilmGrain>(p);
            g.type.Override(FilmGrainLookup.Thin1);
            g.intensity.Override(intensity);
        }

        private static Color FromHex(string hex)
        {
            Color c;
            return ColorUtility.TryParseHtmlString("#" + hex, out c) ? c : Color.magenta;
        }
    }
}
