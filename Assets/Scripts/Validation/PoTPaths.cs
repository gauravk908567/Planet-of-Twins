/// <summary>
/// The ONE home for every path, key and asset name the project still resolves by STRING (folder restructure Stage 0,
/// game.md §20.5). Everything else finds assets by type, or by type + exact name (<see cref="PoTAssetLookup"/>), so
/// moving a folder breaks nothing. What remains here is what cannot be found that way:
///   • <see cref="ResourceKeys"/> — <c>Resources.Load</c> keys (runtime). A key is the path RELATIVE to a Resources
///     folder: the asset may move to another Resources folder but must keep this sub-path.
///   • <see cref="Named"/> — exact asset names editor tools look up (type + name, anywhere under Assets/).
///   • <see cref="Create"/> — folders where editor tools CREATE new assets. Never used for lookups.
///   • <see cref="Scan"/> — folders editor tools read as their whole INPUT set (first-party code, the glyph atlas).
/// When something moves or is renamed, update THIS file (never the tools), then run
/// Planet of Twins Tools ▸ Validation ▸ Path Health — it checks every entry below.
/// </summary>
public static class PoTPaths
{
    /// <summary>Runtime <c>Resources.Load</c> keys. Each has a typed check in PathHealthSelfTest.</summary>
    public static class ResourceKeys
    {
        public const string DevConfig             = "DevConfig";            // DevConfig.Instance
        public const string InputGlyphMap         = "InputGlyphMap";        // InputGlyphResolver + both glyph bakers
        public const string InputGlyphSpriteAsset = "Sprites/InputGlyphs";  // InputGlyphText (TMP inline glyphs)
        public const string ControllerDb          = "gamecontrollerdb";     // SdlControllerDb (SDL_GameControllerDB text)
        public const string KeyLetterGlowGold     = "AbilityHUD/M_UIKeyLetterGlow_Gold";     // AccordIconSlot keycaps
        public const string KeyLetterGlowViolet   = "AbilityHUD/M_UIKeyLetterGlow_Violet";
        public const string KeyLetterGlowNeutral  = "AbilityHUD/M_UIKeyLetterGlow_Neutral";
        public const string Keycap                = "AbilityHUD/M_UIKeycap";
    }

#if UNITY_EDITOR
    /// <summary>Asset names editor tools resolve by type + exact name. Path Health requires each to exist exactly once.</summary>
    public static class Named
    {
        public const string DualCheckpointPrefab    = "DualCheckpoint";          // CheckpointVisualBuilder
        public const string CheckpointTrailMaterial = "M_CheckpointTrail";       // CheckpointVisualBuilder
        public const string RingTimerSharedMaterial = "M_UIRingTimer_Shared";    // CheckpointVisualBuilder (used unmodified)
        public const string GameAudioMixer          = "GameAudioMixer";          // UnifiedSettingsScreenBuilder
        public const string PcRenderPipelineAsset   = "PC_RPAsset";              // UnifiedSettingsScreenBuilder
        public const string PcRendererData          = "PC_Renderer";             // UnifiedSettingsScreenBuilder
        public const string SeekEnergyUtilProfile   = "SeekEnergyUtilProfile";   // PoiEcologyAuthoring
        public const string DefaultPoiEnergyProfile = "DefaultPoiEnergyProfile"; // PoiEcologyAuthoring (created if absent)
        public const string FxIdsScript             = "FxIds";                   // Cue Id Verifier ▸ Generate FxIds
    }

    /// <summary>Where editor tools CREATE new assets. Path Health requires each folder to exist, so a stale entry
    /// after a move is caught instead of silently re-growing the old folder tree.</summary>
    public static class Create
    {
        public const string AreaScenes        = "Assets/_PoT/Scenes/Areas";             // Area Tools: <root>/<Area>/<Area>.unity + kit
        public const string SpawnAreaConfigs  = "Assets/_PoT/Data/Spawn";               // Area Tools ▸ Setup Zone: <root>/<scene>/
        public const string WorldLocations    = "Assets/_PoT/Data/Locations";           // Scene Health fix: Location_<scene>.asset
        public const string GradeProfiles     = "Assets/_PoT/Settings/Grading";         // GradeProfileAuthoring
        public const string PoiEnergyProfiles = "Assets/_PoT/Data/AI/POI";              // PoiEcologyAuthoring
        public const string FxIds             = "Assets/Scripts/Fx/Generated";          // must stay inside the PoT.Fx asmdef folder
        public const string BakedResources    = "Assets/Resources";                     // must be a Resources folder (see below)

        /// <summary>Where a baker creates a Resources asset that does not exist yet: <c>&lt;BakedResources&gt;/&lt;key&gt;.asset</c>,
        /// so the runtime key resolves by construction. Bakers overwrite the EXISTING asset (wherever it lives) first.</summary>
        public static string BakedResourceAsset(string resourceKey) => $"{BakedResources}/{resourceKey}.asset";
    }

    /// <summary>Folders editor tools read as their whole input set. Path Health requires each to exist.</summary>
    public static class Scan
    {
        public const string FirstPartyCode  = "Assets/Scripts";            // Code lint + Cue Id Verifier read every .cs here
        public const string InputGlyphAtlas = "Assets/_PoT/Art/UI/InputGlyphs"; // Kenney PNGs: <root>/<Keyboard|Xbox|…>/<stem>.png
    }
#endif
}
