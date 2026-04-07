using UnityEngine;

/// <summary>
/// Manages looping VFX for enemy states.
/// SETUP: Wire _rageLoopPS, _fearLoopPS, _panicLoopPS as child ParticleSystems.
///        Set each PS: Loop ON, Play On Awake ON.
///        Leave each child GO inactive by default in prefab.
///        EnemyVFXController toggles GO active/inactive to play/stop.
/// </summary>
public class EnemyVFXController : MonoBehaviour
{
    [Header("One-shot VFX prefabs (spawned at enemy position)")]
    [SerializeField] private GameObject _rageVFXPrefab;
    [SerializeField] private GameObject _fearVFXPrefab;
    [SerializeField] private GameObject _panicVFXPrefab;

    [Header("Continuous VFX (looping — stopped when state ends)")]
    [SerializeField] private ParticleSystem _rageLoopPS;
    [SerializeField] private ParticleSystem _fearLoopPS;
    [SerializeField] private ParticleSystem _panicLoopPS;
    [SerializeField] private ParticleSystem _buffLoopPS;

    public void PlayRage()
    {
        SpawnOneShot(_rageVFXPrefab);
        SetLoop(_rageLoopPS, true);
    }

    public void StopRage() => SetLoop(_rageLoopPS, false);

    public void PlayFear()
    {
        SpawnOneShot(_fearVFXPrefab);
        SetLoop(_fearLoopPS, true);
    }

    public void StopFear() => SetLoop(_fearLoopPS, false);

    public void PlayPanic()
    {
        SpawnOneShot(_panicVFXPrefab);
        SetLoop(_panicLoopPS, true);
    }

    public void StopPanic() => SetLoop(_panicLoopPS, false);

    public void PlayBuff() => SetLoop(_buffLoopPS, true);
    public void StopBuff() => SetLoop(_buffLoopPS, false);

    public void StopAll()
    {
        SetLoop(_rageLoopPS, false);
        SetLoop(_fearLoopPS, false);
        SetLoop(_panicLoopPS, false);
        SetLoop(_buffLoopPS, false);
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