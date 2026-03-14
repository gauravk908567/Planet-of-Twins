using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Created by CheckpointManager before scene reload.
/// Survives the reload via DontDestroyOnLoad, then applies saved state
/// once the new scene has fully loaded.
///
/// Destroys itself after applying. No Inspector setup needed.
/// </summary>
public class CheckpointLoader : MonoBehaviour
{
    private CheckpointData _data;

    public void Initialise(CheckpointData data)
    {
        _data = data;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartCoroutine(ApplyAfterFrame());
    }

    private IEnumerator ApplyAfterFrame()
    {
        // Wait two frames — Awake/Start on all scene objects need to run first
        // so twins, SkillTreeManager etc. are fully initialised before we patch them.
        yield return null;
        yield return null;

        ApplyCheckpoint();
        Destroy(gameObject);
    }

    private void ApplyCheckpoint()
    {
        // ── Find twins ─────────────────────────────────────────
        // FIX: discovery order via FindObjectsByType is non-deterministic.
        // Match by saved position instead — the twin closest to each saved
        // position is the correct one to teleport there.
        var players = FindObjectsByType<Player>(FindObjectsSortMode.None);
        Player leftTwin = null;
        Player rightTwin = null;
        float bestLeft = float.MaxValue, bestRight = float.MaxValue;

        foreach (var p in players)
        {
            if (p is SoulPlayer) continue;
            float dL = Vector3.Distance(p.transform.position, _data.leftTwinPosition);
            float dR = Vector3.Distance(p.transform.position, _data.rightTwinPosition);
            if (dL < bestLeft) { bestLeft = dL; leftTwin = p; }
            if (dR < bestRight) { bestRight = dR; rightTwin = p; }
        }

        // Position
        TeleportPlayer(leftTwin, _data.leftTwinPosition);
        TeleportPlayer(rightTwin, _data.rightTwinPosition);

        // HP full (scene reload already resets HP — this is a safety call)
        leftTwin?.HealthTracker?.RestoreToFull();
        rightTwin?.HealthTracker?.RestoreToFull();

        // Unfreeze — scene reload resets traps so movement should already
        // be unfrozen, but call explicitly as belt-and-suspenders.
        (leftTwin?.Movement as IMovementFreezable)?.SetFrozen(false);
        (rightTwin?.Movement as IMovementFreezable)?.SetFrozen(false);
        leftTwin?.Movement?.SetMovementLocked(false);
        rightTwin?.Movement?.SetMovementLocked(false);

        // ── Find SkillTreeManager ──────────────────────────────
        var skillTree = FindAnyObjectByType<SkillTreeManager>();
        if (skillTree != null)
        {
            // SkillTreeManager.Awake() already called ResetAll — just restore saved state
            skillTree.AddPoints(_data.skillPoints);
            RestoreNodeLevels(skillTree, _data.nodeUnlockLevels);

            // FIX: RestoreNodeLevels calls UnlockNextNode() directly which increments
            // currentNodeIndex but never calls RaiseUnlockFlags(). Without this,
            // IsDualCastUnlocked / IsCoalesceUnlocked / IsSoulConvergenceUnlocked
            // all stay false — subscribed systems don't activate, HUD doesn't show
            // unlocked abilities, and SoulConvergence shows as 0/0 (IsActive = false).
            skillTree.RebuildUnlockFlags();
        }

        Debug.Log($"[CheckpointLoader] Applied checkpoint — " +
                  $"L={_data.leftTwinPosition} R={_data.rightTwinPosition} " +
                  $"pts={_data.skillPoints}");
    }

    private void TeleportPlayer(Player p, Vector3 pos)
    {
        if (p == null) return;
        var cc = p.GetComponent<CharacterController>();
        if (cc != null) cc.enabled = false;
        p.transform.position = pos;
        if (cc != null) cc.enabled = true;
    }

    private void RestoreNodeLevels(SkillTreeManager skillTree, int[] levels)
    {
        if (levels == null) return;

        var allData = new List<AbilityUpgradeData>
        {
            skillTree.StunData,
            skillTree.PossessData,
            skillTree.GateData,
            skillTree.HealthRegenData,
            skillTree.DualCastData,
            skillTree.CoalesceData,
            skillTree.SoulConvData
        };

        for (int i = 0; i < Mathf.Min(levels.Length, allData.Count); i++)
        {
            var data = allData[i];
            if (data == null) continue;
            int target = levels[i];
            while ((data.CurrentUnlockedLevel ?? 0) < target && data.HasNextNode)
                data.UnlockNextNode();
        }
    }
}