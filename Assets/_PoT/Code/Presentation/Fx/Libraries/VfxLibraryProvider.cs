using UnityEngine;

/// <summary>
/// Lives in **Persistent**. The single access point to the per-domain VFX libraries (player / enemy / spawn …).
/// Runtime systems resolve it by the R4 manager pattern — an optional serialized slot cast in Awake, then
/// <c>field ??= VfxLibraryProvider.Instance</c> in Start(); LogError + disable self if still null. This replaces
/// the ~21 scattered <c>[SerializeField] CueBookData</c> fields with one Inspector wiring point: drag each
/// library SO here once, and every consumer pulls its book from the relevant library.
///
/// R3-safe singleton: no DontDestroyOnLoad (we live in Persistent), null Instance in OnDestroy. CONFIG holder
/// only — the libraries themselves are SOs (R7); the provider holds no runtime state.
/// </summary>
public class VfxLibraryProvider : MonoBehaviour
{
    public static VfxLibraryProvider Instance { get; private set; }

    [Tooltip("All player-side Cue Books (abilities, melee, soul, Accord, footsteps).")]
    [SerializeField] private PlayerVfxLibrary player;

    [Tooltip("All enemy archetype Cue Books (melee, ranged, chain, grab, witness, siphon…).")]
    [SerializeField] private EnemyVfxLibrary enemy;

    [Tooltip("Cross-cutting Cue Books both sides trigger (hit spark, buff/debuff, enemy spawn, reactions).")]
    [SerializeField] private CommonVfxLibrary common;

    /// <summary>The player VFX library, or null if unwired (consumers LogError + disable on null — R4).</summary>
    public PlayerVfxLibrary Player => player;

    /// <summary>The enemy VFX library, or null if unwired (consumers LogError + disable on null — R4).</summary>
    public EnemyVfxLibrary Enemy => enemy;

    /// <summary>The shared/common VFX library, or null if unwired (consumers LogError + disable on null — R4).</summary>
    public CommonVfxLibrary Common => common;

    // Spawn / environment libraries drop in here as their slices land (one field each), same access shape.

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        if (player == null)
            Debug.LogError("[VfxLibraryProvider] PlayerVfxLibrary slot is empty — player VFX consumers " +
                           "will fail to resolve their books. Wire it in Persistent.unity.", this);
        if (enemy == null)
            Debug.LogError("[VfxLibraryProvider] EnemyVfxLibrary slot is empty — enemy VFX consumers " +
                           "will fail to resolve their books. Wire it in Persistent.unity.", this);
        if (common == null)
            Debug.LogError("[VfxLibraryProvider] CommonVfxLibrary slot is empty — shared VFX consumers " +
                           "(hit/buff/debuff/spawn) will fail to resolve their books. Wire it in Persistent.unity.", this);
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }
}
