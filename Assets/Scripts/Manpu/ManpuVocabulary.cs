using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The data-driven Manpu vocabulary (R3) — the single tunable that decides which
/// states get a glyph and what it looks/sounds like. CONFIG ONLY (R7). Edit it with the custom
/// inspector (`ManpuVocabularyEditor`): every <c>EnemyMood</c> / search state / ability is listed
/// automatically (add a mood to the enum → a new row appears), and you drag a sprite + optional
/// particle + optional sound onto each.
///
/// **Empty Sprite = no glyph for that trigger (suppressed).** That is the curation: fill in only the
/// few high-value rows; everything else keeps the existing tint/VFX with no glyph (R3).
/// </summary>
[CreateAssetMenu(menuName = "PlanetOfTwins/Manpu/Vocabulary", fileName = "ManpuVocabulary")]
public class ManpuVocabulary : ScriptableObject
{
    /// <summary>Which visual channel(s) of a glyph play. Lets one trigger show the sprite only, the
    /// particle only, or both — even when a sprite AND a particle are both authored on the row.</summary>
    public enum ManpuChannel { Both, SpriteOnly, ParticleOnly }

    [Serializable]
    public class GlyphStyle
    {
        public Sprite sprite;
        public Color colorA = Color.white;
        public Color colorB = Color.white;

        [Tooltip("Optional accent ParticleSystem prefab played (pooled) when the glyph appears — via " +
                 "FxManager.PlayParticle. Looping prefab is held until the glyph clears.")]
        public ParticleSystem burstPrefab;

        [Tooltip("Optional one-shot sting played when the glyph appears — FxManager → AudioManager " +
                 "(no AudioManager change). Plays positionally on the enemy; gated by R1/R2 like the glyph.")]
        public SoundCueData sound;

        [Tooltip("What to play for this trigger:\n" +
                 "• Both = sprite + particle (whichever are assigned).\n" +
                 "• SpriteOnly = show the sprite, suppress the particle even if one is assigned.\n" +
                 "• ParticleOnly = play the particle with NO sprite shown (this is how you get a " +
                 "particle-only glyph — previously an empty sprite meant nothing played at all).")]
        public ManpuChannel channel = ManpuChannel.Both;

        /// <summary>The sprite actually renders: one is assigned and the channel isn't ParticleOnly.</summary>
        public bool PlaySprite => sprite != null && channel != ManpuChannel.ParticleOnly;
        /// <summary>The accent particle actually plays: one is assigned and the channel isn't SpriteOnly.</summary>
        public bool PlayParticle => burstPrefab != null && channel != ManpuChannel.SpriteOnly;

        /// <summary>This trigger has SOMETHING to show — a rendered sprite or a played particle. Nothing
        /// authored (or the only authored channel suppressed) = no glyph for that trigger (the R3 curation).
        /// Formerly sprite-only, which is why a particle with no sprite showed nothing.</summary>
        public bool HasVisual => PlaySprite || PlayParticle;
    }

    [Serializable]
    public class AbilityEntry
    {
        public ManpuAbility ability;
        public GlyphStyle held = new GlyphStyle();
        [Tooltip("Timed closing beats played in order on ability END — each a glyph held for holdSeconds " +
                 "(unscaled, E1). 1–4 beats; empty = no closing glyph (R3). e.g. Stun → [{sleep,0.75},{wake-!,0.5}].")]
        public List<ClosingBeat> closingSequence = new List<ClosingBeat>();
    }

 /// <summary>One beat of an ability's closing arc: a glyph shown for <see cref="holdSeconds"/>.</summary>
    [Serializable]
    public class ClosingBeat
    {
        public GlyphStyle glyph = new GlyphStyle();
        [Min(0f)] public float holdSeconds = 0.5f;
    }

    [Serializable]
    public class MoodEntry
    {
        public ManpuMood mood;
        public GlyphStyle glyph = new GlyphStyle();
        [Tooltip("R2: pulse only on the escalating ENTRY into this mood — skip drift between two " +
                 "already-glyphed (curated) moods.")]
        public bool escalatingOnly = true;

        [Tooltip("Optional SUSTAINED aura ParticleSystem prefab — held for exactly as long as this mood " +
                 "is active (started on mood ENTER, stopped on EXIT / pool despawn / scene unload). This is " +
 "the held-loop replacement for the EnemyVFXController rage/panic/etc. loops. It is " +
                 "INDEPENDENT of the glyph channel: it plays even when this row has no sprite (the sprite gates " +
                 "only the transient glyph pulse) and it is NOT suppressed while an ability owns the glyph slot " +
                 "(the aura is a body channel, not the glyph). Leave empty for no aura.")]
        public ParticleSystem loopPrefab;
    }

    [Serializable]
    public class PerceptionEntry
    {
        public ManpuSearchState state;   // start set: Pursuing
        public GlyphStyle glyph = new GlyphStyle();
    }

    public List<AbilityEntry> abilities = new List<AbilityEntry>();
    public List<MoodEntry> moods = new List<MoodEntry>();
    public List<PerceptionEntry> perception = new List<PerceptionEntry>();

    private Dictionary<ManpuAbility, AbilityEntry> _abilityMap;
    private Dictionary<ManpuMood, MoodEntry> _moodMap;
    private Dictionary<ManpuSearchState, PerceptionEntry> _perceptionMap;

    public AbilityEntry GetAbility(ManpuAbility a)
    {
        EnsureMaps();
        return _abilityMap.TryGetValue(a, out var e) ? e : null;
    }

    public bool TryGetMood(ManpuMood mood, out MoodEntry entry)
    {
        EnsureMaps();
        return _moodMap.TryGetValue(mood, out entry);
    }

    public bool TryGetPerception(ManpuSearchState state, out PerceptionEntry entry)
    {
        EnsureMaps();
        return _perceptionMap.TryGetValue(state, out entry);
    }

    private void EnsureMaps()
    {
        if (_moodMap != null) return;

        _abilityMap = new Dictionary<ManpuAbility, AbilityEntry>();
        foreach (var a in abilities) if (a != null) _abilityMap[a.ability] = a;

        _moodMap = new Dictionary<ManpuMood, MoodEntry>();
        foreach (var m in moods) if (m != null) _moodMap[m.mood] = m;

        _perceptionMap = new Dictionary<ManpuSearchState, PerceptionEntry>();
        foreach (var p in perception) if (p != null) _perceptionMap[p.state] = p;
    }

#if UNITY_EDITOR
    private void OnValidate() => _moodMap = null; // force rebuild after edits
#endif
}
