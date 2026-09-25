/// <summary>
/// Upgrade-tier cue resolution (P15 —, tier-SUFFIX model, user call 2026-07-04).
/// An ability's cues upgrade visually with its skill tier WITHOUT multiple books:
///
///   • All tier variants live in the SAME CueBookData, named `&lt;baseId&gt;_t[n]`
///     (e.g. `stun_cast`, `stun_cast_t1`, `stun_cast_t2`). Suffix, never prefix —
///     ids stay grouped by base name and FxIds constants generate beside their base.
///   • At tier N (= unlocked node count) the caller resolves EVERY id it plays through
///     Resolve(book, data, defaultId): try `&lt;id&gt;_tN`, then `_t(N-1)` … `_t1`
///     (highest authored tier ≤ current wins), else the base id.
///   • PER-SUB-ID OPT-IN: an ability playing 3 ids can tier just one — author
///     `id1_t2` and leave id2/id3 alone; they fall back to their base ids automatically.
///     An id with no `_t[n]` variants is simply used for ALL tiers.
///
/// The book stays progression-ignorant (tier knowledge = the node count in progression
/// data + the naming convention in the book). No per-node override field — a tier that
/// changes no art just has no `_t[n]` ids authored.
/// </summary>
public static class UpgradeCueResolver
{
    public static string Resolve(CueBookData book, AbilityUpgradeData data, string defaultId)
    {
        if (book == null || data == null || string.IsNullOrEmpty(defaultId)) return defaultId;

        int tier = data.currentNodeIndex;   // computed over SkillTreeManager runtime state (R7)
        for (int t = tier; t >= 1; t--)
        {
            string tiered = defaultId + "_t" + t;
            if (book.Has(tiered)) return tiered;
        }
        return defaultId;
    }
}
