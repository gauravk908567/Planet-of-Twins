using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Persistent singleton. Performs a soft reset (respawn-in-place) without
/// reloading any scene.
///
/// WHAT IT DOES:
///   1. Fades to black (covers the reset).
///   2. Despawns all active enemies back to the pool.
///   3. Pauses spawning.
///   4. Resets zone/rescue state.
///   5. Teleports twins to checkpoint positions.
///   6. Restores HP and unfreezes movement.
///   7. Restores skill tree state (points + node levels).
///   8. Restores sword pickups.
///   9. Resumes the frame — fades back in.
///  10. Spawning resumes naturally when the twins re-enter a SpawnZone trigger.
///
/// REPLACES: CheckpointLoader + the SceneManager.LoadScene() call in CheckpointManager.
///
/// WIRING:
///   • Lives in Persistent.unity (under ------SYSTEM------).
///   • CheckpointManager calls BeginSoftReset(data) instead of creating a CheckpointLoader.
///   • Wire Inspector refs or leave null for auto-find (auto-find fires at reset time,
///     which is fine since all these objects are in Persistent).
/// </summary>
public class SoftResetController : MonoBehaviour
{
    public static SoftResetController Instance { get; private set; }

    public event Action OnSoftReset;

    [Header("Wire in Inspector (all Persistent — R1 same-scene)")]
    [SerializeField] private EnemySpawner enemySpawner;
    [SerializeField] private SharedHealthPool sharedHealthPool;
    [SerializeField] private SkillTreeManager skillTreeManager;
    [SerializeField] private RescueEventController rescueController;
    [SerializeField] private Player _leftTwin;
    [SerializeField] private Player _rightTwin;

    [Header("Fade")]
    [Tooltip("A full-screen black Image used to cover the reset frame. " +
             "Assign the FadeCanvas Image from Persistent's UI group.")]
    [SerializeField] private UnityEngine.UI.Image fadeImage;
    [SerializeField] private float fadeOutDuration = 0.35f;
    [SerializeField] private float holdBlackDuration = 0.1f;
    [SerializeField] private float fadeInDuration = 0.5f;

    private bool _resetting;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Called by CheckpointManager. Begins the full soft-reset sequence.</summary>
    public void BeginSoftReset(CheckpointData data)
    {
        if (_resetting)
        {
            Debug.LogWarning("[SoftResetController] Reset already in progress — ignoring.");
            return;
        }
        StartCoroutine(ResetSequence(data));
    }

    // ── Reset sequence ────────────────────────────────────────────────────────

    private IEnumerator ResetSequence(CheckpointData data)
    {
        _resetting = true;

        // Clear all timeScale requests (Setsuna / teleport / pause may have left them active)
        TimeScaleService.Instance?.ReleaseAll();

        // 1. Fade to black
        yield return StartCoroutine(Fade(0f, 1f, fadeOutDuration));
        yield return new WaitForSecondsRealtime(holdBlackDuration);

        // 2. Abort any active QTE and force-end all power states
        QTEManager.Instance?.AbortQTE();
        SetsunaSystem.Instance?.ForceEnd();
        AccordStateSystem.Instance?.ForceDeactivate();
        EmpowerSystem.Instance?.ForceEnd();
        SoulConvergenceSystem.Instance?.ForceDeactivate();

        // 3. Auto-find any still-null refs (belt-and-suspenders — wire Inspector first)
        AutoFindRefs();

        // 4. Fire OnSoftReset so SpawnZones and traps can clear occupancy / release grabs
        OnSoftReset?.Invoke();

        // 5. Despawn enemies
        enemySpawner?.DespawnAll();

        // 6. Reset rescue state
        rescueController?.ForceReset();

        // 7. Restore skill tree
        RestoreSkillTree(data);

        // 8. Ensure checkpoint area is loaded before teleporting
        if (data.checkpointLocation != null && SceneFlowManager.Instance != null
            && !SceneFlowManager.Instance.IsLoaded(data.checkpointLocation))
            yield return StartCoroutine(WaitForLocation(data.checkpointLocation));

        // 9. Teleport twins + restore HP + unfreeze + notify streaming
        yield return StartCoroutine(ApplyTwinState(data));

        // 10. Restore sword pickups
        RestoreSwords(data);

        // 11. One frame for everything to settle
        yield return null;

        // 12. Fade back in
        yield return StartCoroutine(Fade(1f, 0f, fadeInDuration));

        _resetting = false;
        Debug.Log("[SoftResetController] Soft reset complete.");
    }

    // ── State restoration ─────────────────────────────────────────────────────

    private IEnumerator ApplyTwinState(CheckpointData data)
    {
        // Use serialized refs — twins are Persistent, direct R1 wiring is correct.
        // Fall back to TwinSelector if Inspector slots are unwired.
        Player left = _leftTwin ?? TwinSelector.Instance?.LeftTwin;
        Player right = _rightTwin ?? TwinSelector.Instance?.RightTwin;

        if (left == null || right == null)
            Debug.LogError("[SoftResetController] Twin refs null — wire _leftTwin/_rightTwin in Inspector.", this);

        TeleportPlayer(left, data.leftTwinPosition);
        TeleportPlayer(right, data.rightTwinPosition);

        // Notify streaming so SceneFlowManager tracks the new location (3.7b)
        if (data.checkpointLocation != null && SceneFlowManager.Instance != null)
        {
            if (left  != null) SceneFlowManager.Instance.NotifyTeleported(left,  data.checkpointLocation);
            if (right != null) SceneFlowManager.Instance.NotifyTeleported(right, data.checkpointLocation);
        }

        yield return null; // one frame for position to settle

        left?.HealthTracker?.RestoreToFull();
        right?.HealthTracker?.RestoreToFull();
        sharedHealthPool?.ForceSetHealth(sharedHealthPool.MaxCombinedHealth);

        (left?.Movement as IMovementFreezable)?.SetFrozen(false);
        (right?.Movement as IMovementFreezable)?.SetFrozen(false);
        left?.Movement?.SetMovementLocked(false);
        right?.Movement?.SetMovementLocked(false);
    }

    private void RestoreSkillTree(CheckpointData data)
    {
        var tree = skillTreeManager ?? SkillTreeManager.Instance; // R4
        if (tree == null) return;

        // Drain current balance, then restore saved amount
        if (tree.CurrentPoints > 0) tree.TrySpendPoints(tree.CurrentPoints);
        tree.AddPoints(data.skillPoints);

        // Snapshot restore covers all 9 trees via the dictionary (no hand-listed arrays).
        // RebuildUnlockFlags fires inside RestoreSkillSnapshot.
        tree.RestoreSkillSnapshot(data.skillTreeSnapshot ?? SkillTreeRuntimeState.Snapshot.Empty);
    }

    private void RestoreSwords(CheckpointData data)
    {
        // Twins own their sword GO (R1) — restore the saved per-twin state directly, no cross-scene SwordPickup ref.
        Player left = _leftTwin ?? TwinSelector.Instance?.LeftTwin;
        Player right = _rightTwin ?? TwinSelector.Instance?.RightTwin;
        left?.GetComponentInChildren<PlayerAttackController>(true)?.SetHasWeapon(data.leftHasSword);
        right?.GetComponentInChildren<PlayerAttackController>(true)?.SetHasWeapon(data.rightHasSword);

        // Re-arm the area's sword pickups to match the checkpoint (BUG-090). A collected SwordPickup
        // SetActive(false)'s itself; a soft reset never reloads the scene, so it stays hidden unless we
        // re-enable it here — otherwise a checkpoint saved BEFORE pickup leaves the melee permanently
        // ungrabbable after respawn. Include inactive so we can find already-collected pickups (scene-scoped
        // non-singleton sweep — R4). A pickup is available exactly when its twin did NOT have the sword at
        // save time; forcing it hidden when the twin DID also closes a latent double-grab if the area was
        // streamed in fresh (active) while the twin already carries the sword.
        foreach (var pickup in FindObjectsByType<SwordPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            bool collectedAtCheckpoint = pickup.IsForLeftTwin ? data.leftHasSword : data.rightHasSword;
            pickup.gameObject.SetActive(!collectedAtCheckpoint);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private IEnumerator WaitForLocation(WorldLocationSO location)
    {
        bool loaded = false;
        void OnLoaded(WorldLocationSO loc) { if (loc == location) loaded = true; }
        SceneFlowManager.Instance.OnLocationLoaded += OnLoaded;
        SceneFlowManager.Instance.NotifyTeleported(
            _leftTwin ?? TwinSelector.Instance?.LeftTwin, location); // trigger occupancy → load
        yield return new WaitUntil(() => loaded || SceneFlowManager.Instance.IsLoaded(location));
        SceneFlowManager.Instance.OnLocationLoaded -= OnLoaded;
        yield return null; // one frame for scene objects to Awake/Start
    }

    private void AutoFindRefs()
    {
        if (enemySpawner == null)    enemySpawner    = EnemySpawner.Instance;
        if (sharedHealthPool == null) sharedHealthPool = FindAnyObjectByType<SharedHealthPool>(); // no Instance yet
        if (skillTreeManager == null) skillTreeManager = SkillTreeManager.Instance;
        if (rescueController == null) rescueController = RescueEventController.Instance;
    }

    private static void TeleportPlayer(Player p, Vector3 pos)
    {
        if (p == null) return;
        var cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        if (fadeImage == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            fadeImage.color = new Color(0, 0, 0, Mathf.Lerp(from, to, t / duration));
            yield return null;
        }
        fadeImage.color = new Color(0, 0, 0, to);
    }
}
