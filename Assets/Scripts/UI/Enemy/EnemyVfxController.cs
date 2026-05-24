using UnityEngine;

/// <summary>
/// Manages looping and one-shot VFX for enemy states.
/// PlayMoodReaction uses MoodVFXTag enum — no string errors.
/// </summary>
public class EnemyVFXController : MonoBehaviour
{
    [Header("One-shot VFX prefabs")]
    [SerializeField] private GameObject _rageVFXPrefab;
    [SerializeField] private GameObject _fearVFXPrefab;
    [SerializeField] private GameObject _panicVFXPrefab;
    [SerializeField] private GameObject _darkEnergyVFXPrefab;

    [Header("Continuous VFX (looping)")]
    [SerializeField] private ParticleSystem _rageLoopPS;
    [SerializeField] private ParticleSystem _fearLoopPS;
    [SerializeField] private ParticleSystem _panicLoopPS;
    [SerializeField] private ParticleSystem _buffLoopPS;
    [SerializeField] private ParticleSystem _darkEnergyLoopPS;  // Phase 3

    public void PlayRage() { SpawnOneShot(_rageVFXPrefab); SetLoop(_rageLoopPS, true); }
    public void StopRage() => SetLoop(_rageLoopPS, false);

    public void PlayFear() { SpawnOneShot(_fearVFXPrefab); SetLoop(_fearLoopPS, true); }
    public void StopFear() => SetLoop(_fearLoopPS, false);

    public void PlayPanic() { SpawnOneShot(_panicVFXPrefab); SetLoop(_panicLoopPS, true); }
    public void StopPanic() => SetLoop(_panicLoopPS, false);

    public void PlayBuff() => SetLoop(_buffLoopPS, true);
    public void StopBuff() => SetLoop(_buffLoopPS, false);

    // Phase 3 — dark energy threshold VFX
    public void PlayDarkEnergy() { SpawnOneShot(_darkEnergyVFXPrefab); SetLoop(_darkEnergyLoopPS, true); }
    public void StopDarkEnergy() => SetLoop(_darkEnergyLoopPS, false);

    public void StopAll()
    {
        SetLoop(_rageLoopPS, false);
        SetLoop(_fearLoopPS, false);
        SetLoop(_panicLoopPS, false);
        SetLoop(_buffLoopPS, false);
        SetLoop(_darkEnergyLoopPS, false);
    }

    /// <summary>Play mood reaction VFX using enum — no string errors.</summary>
    public void PlayMoodReaction(MoodVFXTag tag)
    {
        switch (tag)
        {
            case MoodVFXTag.GrimSatisfaction: SpawnOneShot(_rageVFXPrefab); break;
            case MoodVFXTag.Frustrated: SpawnOneShot(_panicVFXPrefab); break;
            case MoodVFXTag.Grief: SpawnOneShot(_fearVFXPrefab); break;
            case MoodVFXTag.Rage: SpawnOneShot(_rageVFXPrefab); break;
            case MoodVFXTag.Fear: SpawnOneShot(_fearVFXPrefab); break;
            case MoodVFXTag.Panic: SpawnOneShot(_panicVFXPrefab); break;
            case MoodVFXTag.DarkEnergy: SpawnOneShot(_darkEnergyVFXPrefab); break;
            case MoodVFXTag.None: default: break;
        }
    }

    private void SetLoop(ParticleSystem ps, bool active)
    {
        if (ps == null) return;
        ps.gameObject.SetActive(active);
    }

    private void SpawnOneShot(GameObject prefab)
    {
        if (prefab == null) return;
        var go = Instantiate(prefab, transform.position, Quaternion.identity);
        Destroy(go, 3f);
    }
}