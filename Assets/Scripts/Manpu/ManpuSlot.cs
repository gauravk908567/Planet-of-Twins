using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The one manpu slot per enemy. Arbitrates the single <c>ManpuGlyph</c> between
/// the two competitors and enforces the rules that make the Hybrid win:
///   • R1 — while an ability effect owns the slot, mood/perception pulses are SUPPRESSED (dropped).
///   • R2 — a debounce stops an enemy oscillating moods in a brawl from strobing; mood pulses also
///          fire only on escalating entry (skip drift between two already-glyphed moods).
///   • R3 — a trigger with no authored sprite shows no glyph (the vocabulary's curation).
///   • E1 — forwards slow-mode to the glyph (pulses hold during Setsuna).
///   • Pooling — <see cref="Clear"/> wipes the slot on pool return (no glyph leak across reuse).
/// Presentation arbiter only — it owns no mood/ability state; the existing systems do.
/// </summary>
public class ManpuSlot : MonoBehaviour, IManpuGlyphTarget
{
    [SerializeField] private ManpuGlyph _glyph;
    [SerializeField] private ManpuVocabulary _vocabulary;

    [Tooltip("Min unscaled seconds between mood/perception pulses (R2 anti-strobe).")]
    [SerializeField] private float _pulseDebounce = 1.5f;

    private bool _abilityOwns;
    private ManpuAbility _owner;
    private float _debounceUntil;
 private Coroutine _closingRoutine; // running closing-sequence arc (kept while _abilityOwns)

 // held mood-loop (the "aura" channel). Separate from the glyph slot: a sustained ParticleSystem
    // that lives exactly as long as the current mood — started on mood ENTER, stopped on EXIT / Clear (pool).
    // Independent of the glyph sprite (plays with no sprite authored) and of ability slot ownership (it is a
    // body aura, not the glyph). Replaces the EnemyVFXController rage/panic/etc. loops.
    private CueHandle _moodLoop;
    private ManpuMood _moodLoopMood;
    private bool _hasMoodLoop;

    public bool AbilityOwnsSlot => _abilityOwns;

    private void Awake()
    {
        if (_glyph == null) _glyph = GetComponentInChildren<ManpuGlyph>(true);
    }

    // ── Ability (persistent — owns the slot, R1) ──────────────────────────────
    public void ClaimAbility(ManpuAbility ability)
    {
        if (!_vocabulary || _glyph == null) return;
        var entry = _vocabulary.GetAbility(ability);
        if (entry == null || entry.held == null || !entry.held.HasVisual) return;

        StopClosing();   // a new claim cancels any running closing arc (newest ability wins)
        _abilityOwns = true;
        _owner = ability;
        _glyph.ShowHeld(entry.held);
    }

    public void ReleaseAbility(ManpuAbility ability)
    {
        if (!_abilityOwns || _owner != ability) return;   // a different effect owns it — ignore

        var entry = _vocabulary ? _vocabulary.GetAbility(ability) : null;
        var seq = entry != null ? entry.closingSequence : null;
        if (seq != null && seq.Count > 0)
        {
 // keep _abilityOwns = true through the whole closing; mood stays suppressed until the last beat.
            StopClosing();
            _closingRoutine = StartCoroutine(ClosingArc(seq));
        }
        else
        {
            _abilityOwns = false;
            _glyph?.Clear();   // explicit no-glyph release; the next natural mood pulse shows current state (Q2)
        }
    }

 // play each closing beat (a glyph held for holdSeconds, unscaled per E1/R10), then release the slot.
    private IEnumerator ClosingArc(IReadOnlyList<ManpuVocabulary.ClosingBeat> seq)
    {
        for (int i = 0; i < seq.Count; i++)
        {
            var beat = seq[i];
            if (beat == null || beat.glyph == null || !beat.glyph.HasVisual) continue;
            _glyph.ShowHeld(beat.glyph);
            yield return new WaitForSecondsRealtime(beat.holdSeconds);   // unscaled — ignores Setsuna/pause (E1)
        }
        _closingRoutine = null;
        _abilityOwns = false;
        _glyph?.Clear();   // explicit release — never a frame where both ability and mood believe they own the slot
    }

    private void StopClosing()
    {
        if (_closingRoutine != null) { StopCoroutine(_closingRoutine); _closingRoutine = null; }
    }

    // ── Mood / perception pulses (transient — suppressed under ability, R1) ───
    public void RequestMoodPulse(ManpuMood oldMood, ManpuMood newMood)
    {
 // Aura channel: sustained loop for the new mood — INDEPENDENT of the glyph sprite, of R1
        // ability ownership, and of the R2 debounce. Runs first so an aura shows even for un-glyphed moods.
        UpdateMoodLoop(newMood);

        // Glyph channel: gated by R3 (sprite), R2 (escalation + debounce) and R1 (ability owns the slot).
        if (!_vocabulary || !_vocabulary.TryGetMood(newMood, out var e) || !e.glyph.HasVisual) return;   // R3
        if (e.escalatingOnly && _vocabulary.TryGetMood(oldMood, out var prev) && prev.glyph.HasVisual)
            return;                                                                                       // R2 — curated→curated drift
        Pulse(e.glyph);
    }

 /// <summary> — swap the held aura loop to the new mood's <c>loopPrefab</c> (mood ENTER), stopping the
    /// previous mood's aura (mood EXIT). No-ops when the aura for <paramref name="newMood"/> is already playing,
    /// and stops the aura entirely when the new mood has no authored loop. Pool-safe: a stale (reclaimed) handle
    /// reads <c>!IsPlaying</c> so it is restarted cleanly rather than double-stopped.</summary>
    private void UpdateMoodLoop(ManpuMood newMood)
    {
        var fx = FxManager.Instance;
        if (_hasMoodLoop)
        {
            if (_moodLoopMood == newMood && fx != null && fx.IsPlaying(_moodLoop)) return;   // already the right aura
            fx?.Stop(_moodLoop);
            _hasMoodLoop = false;
        }
        if (_vocabulary != null && _vocabulary.TryGetMood(newMood, out var e) && e.loopPrefab != null && fx != null)
        {
            _moodLoop = fx.PlayParticle(e.loopPrefab, CueContext.Follow(transform));   // rides the enemy body
            if (!_moodLoop.IsNone) { _hasMoodLoop = true; _moodLoopMood = newMood; }
        }
    }

    private void StopMoodLoop()
    {
        if (!_hasMoodLoop) return;
        FxManager.Instance?.Stop(_moodLoop);
        _hasMoodLoop = false;
    }

    public void RequestPerceptionPulse(ManpuSearchState state)
    {
        if (_vocabulary && _vocabulary.TryGetPerception(state, out var e) && e.glyph.HasVisual) Pulse(e.glyph);
    }

    private void Pulse(ManpuVocabulary.GlyphStyle style)
    {
        if (_abilityOwns) return;                           // R1 — ability owns the slot
        if (Time.unscaledTime < _debounceUntil) return;     // R2 — anti-strobe
        if (style == null || !style.HasVisual || _glyph == null) return;
        _debounceUntil = Time.unscaledTime + _pulseDebounce;
        _glyph.ShowPulse(style);
    }

 // ── Cue Book "Manpu" element ────────────────────────────────────────
    /// <summary>Pulse a glyph authored in a Cue Book effect (not the vocabulary) on this slot. Respects R1
    /// (a held ability glyph that owns the slot wins — the pulse is dropped) but bypasses the R2 mood
    /// anti-strobe debounce, since a cue pulse is a deliberate authored accent, not state drift. Transient.</summary>
    public void RequestCuePulse(Sprite sprite, Color colorA, Color colorB)
    {
        if (_abilityOwns || _glyph == null || sprite == null) return;   // R1 — ability owns the slot
        _glyph.ShowPulse(new ManpuVocabulary.GlyphStyle { sprite = sprite, colorA = colorA, colorB = colorB });
    }

 /// <summary> reaction path — play a cue-book reaction effect (a Manpu pulse + its VFX/sound) on THIS
    /// slot by id. A thin forward to <c>FxManager.PlayBook</c>; the cue's Manpu element resolves back to this
    /// slot, so R1 still drops it if an ability owns the slot. Deliberate accent (the pulse bypasses R2).</summary>
    public void PlayReaction(CueBookData book, string id)
    {
        if (book == null) return;
        FxManager.Instance?.PlayBook(book, id, CueContext.Follow(transform));
    }

    // ── E1 + pooling ──────────────────────────────────────────────────────────
    public void SetSlowMode(bool slow) => _glyph?.SetSlowMode(slow);

    /// <summary>Pool reclaim — wipe the slot so nothing leaks onto the next reused enemy. Aborts a running
 /// closing arc (a closing coroutine is exactly the kind that would leak onto a pooled enemy) and
 /// stops the held mood aura (a sustained loop is the other thing that would leak across reuse).</summary>
    public void Clear()
    {
        StopClosing();
        StopMoodLoop();
        _abilityOwns = false;
        _debounceUntil = 0f;
        _moodLoopMood = default;
        _glyph?.Clear();
    }
}
