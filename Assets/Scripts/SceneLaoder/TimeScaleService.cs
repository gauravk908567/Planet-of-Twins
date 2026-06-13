using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Persistent singleton. Min-value-wins timeScale arbiter.
/// All Time.timeScale writes must go through this service (R10).
///
/// API:
///   Request(owner, value) — registers or updates a request; min of all requests applied.
///   Release(owner)        — removes one owner's request; min recalculated.
///   ReleaseAll()          — clears every request (Restart / SoftReset entry point only).
/// Empty request table → timeScale = 1.
/// </summary>
public class TimeScaleService : MonoBehaviour
{
    public static TimeScaleService Instance { get; private set; }

    private readonly Dictionary<object, float> _requests = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Request(object owner, float scale)
    {
        _requests[owner] = Mathf.Clamp(scale, 0f, 1f);
        Apply();
    }

    public void Release(object owner)
    {
        _requests.Remove(owner);
        Apply();
    }

    public void ReleaseAll()
    {
        _requests.Clear();
        Apply();
    }

    private void Apply()
    {
        float min = 1f;
        foreach (var v in _requests.Values)
            if (v < min) min = v;
        Time.timeScale = min;
    }
}
