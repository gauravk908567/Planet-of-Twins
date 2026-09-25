/// <summary>
/// Enemy mood states — affect utility scoring and BT action execution.
/// Not what enemies do, but HOW they do it.
/// Transitions driven by MoodEventBus events and HP thresholds.
/// </summary>
public enum EnemyMood
{
    // ── Neutral ────────────────────────────────────────────
    Normal,          // Default state — no modifiers

    // ── Positive / Aggressive ──────────────────────────────
    Confident,       // Full HP, allies nearby — press attack
    Opportunistic,   // Ally grabbed twin / twin pinned — greed overrides caution
    Aggressive,      // Energy burst or being taunted — full press
    Enraged,         // Severed grief rage / Penitent final phase — ignore self-preservation

    // ── Negative / Defensive ──────────────────────────────
    Cautious,        // Low HP or outnumbered — hang back
    Wounded,         // Very low HP — survival instinct dominant
    Frustrated,      // Twin escaped grab / ability failed — sloppy aggression
    Panicked,        // Sudden threat / ally died — erratic

    // ── Emotional / Story ─────────────────────────────────
    Grieving,        // Severed partner died (grace window) — reaching, slow
    Contemptuous,    // Penitent full HP — mocking, deliberate pace
    Territorial,     // Enemy near barrier or spawn zone — heightened defense
    Curious,         // Sound event detected — investigation mode
}
