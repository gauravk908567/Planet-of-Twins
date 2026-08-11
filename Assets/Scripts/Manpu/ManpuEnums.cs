/// <summary>
/// Player ability effects that take the manpu slot (R1) and run a held→closing arc.
/// </summary>
public enum ManpuAbility
{
    Stun,
    Possess,
    Grab,
    Freeze,
}

/// <summary>
/// P19 seam: the Manpu package's OWN mood key. Mirrors the game's <c>EnemyMood</c> MEMBER-FOR-MEMBER
/// IN THE SAME ORDER — the game-side glue (ManpuDirector) converts with an int cast, and the
/// serialized vocabulary rows (stored as ints) survive unchanged. NEVER reorder or insert mid-list;
/// append only, and mirror any append in EnemyMood (and vice versa).
/// </summary>
public enum ManpuMood
{
    Normal,
    Confident,
    Opportunistic,
    Aggressive,
    Enraged,
    Cautious,
    Wounded,
    Frustrated,
    Panicked,
    Grieving,
    Contemptuous,
    Territorial,
    Curious,
}

/// <summary>
/// P19 seam: package-side perception key mirroring <c>EnemySearchState</c> — same int-cast contract
/// and same append-only rule as <see cref="ManpuMood"/>.
/// </summary>
public enum ManpuSearchState
{
    Idle,
    Pursuing,
    Searching,
    Suspecting,
    Investigating,
}
