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
        yield return null;
        yield return null;

        ApplyCheckpoint();
        Destroy(gameObject);
    }

    private void ApplyCheckpoint()
    {
        // ── Find twins ─────────────────────────────────────────
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

        // HP full
        leftTwin?.HealthTracker?.RestoreToFull();
        rightTwin?.HealthTracker?.RestoreToFull();

        // Unfreeze
        (leftTwin?.Movement as IMovementFreezable)?.SetFrozen(false);
        (rightTwin?.Movement as IMovementFreezable)?.SetFrozen(false);
        leftTwin?.Movement?.SetMovementLocked(false);
        rightTwin?.Movement?.SetMovementLocked(false);

        // ── Restore sword state ────────────────────────────────
        Debug.Log($"[CheckpointLoader] Sword state to restore — left={_data.leftHasSword} right={_data.rightHasSword}");
        var swordPickups = FindObjectsByType<SwordPickup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        Debug.Log($"[CheckpointLoader] Found {swordPickups.Length} SwordPickup(s) in scene");
        foreach (var pickup in swordPickups)
        {
            bool shouldApply = pickup.IsForLeftTwin ? _data.leftHasSword : _data.rightHasSword;
            Debug.Log($"[CheckpointLoader] SwordPickup '{pickup.gameObject.name}' isLeft={pickup.IsForLeftTwin} shouldApply={shouldApply}");
            if (shouldApply) pickup.ForceApply();
        }

        // ── Skill tree ─────────────────────────────────────────
        var skillTree = FindAnyObjectByType<SkillTreeManager>();
        if (skillTree != null)
        {
            skillTree.AddPoints(_data.skillPoints);
            RestoreNodeLevels(skillTree, _data.nodeUnlockLevels);
            skillTree.RebuildUnlockFlags();
        }

        Debug.Log($"[CheckpointLoader] Applied checkpoint — " +
                  $"L={_data.leftTwinPosition} R={_data.rightTwinPosition} " +
                  $"pts={_data.skillPoints} " +
                  $"swords=({_data.leftHasSword},{_data.rightHasSword})");
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
            skillTree.AccordSpiritsData,
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