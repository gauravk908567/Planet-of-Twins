using Unity.Cinemachine;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Camera-occlusion see-through (revived 2026-07-16 — was fully drafted but commented out).
/// Never moves the camera: the shot, distance and framing stay exactly as authored.
/// Each frame, spherecast from the camera to the active vcam's LookAt target; every
/// renderer in between whose material carries the ObstacleFadeOut dither
/// (`_seeThroughDistance` — Walls/Tree_/Boundaries/Obstacle mats) gets faded via a
/// MaterialPropertyBlock, and restored to its material's own default when it no longer
/// blocks. Objects without the property are unaffected (no material swaps, ever).
///
/// SETUP: lives on the MainCamera GO in Persistent (next to the CinemachineBrain).
/// Tweak: _obstructionMask (layers that can block), _sphereRadius (corridor width),
/// _fadedValue (how see-through — matches the shadergraph's dither semantics).
/// </summary>
public class CameraObstruction : MonoBehaviour
{
    private static readonly int SeeThroughId = Shader.PropertyToID("_seeThroughDistance");

    [Tooltip("Layers that are allowed to fade when they block the shot.")]
    [SerializeField] private LayerMask _obstructionMask = ~0;

    [Tooltip("Radius of the camera→target corridor that must stay clear.")]
    [SerializeField] private float _sphereRadius = 0.6f;

    [Tooltip("Value written to _seeThroughDistance while a renderer blocks the shot.")]
    [SerializeField] private float _fadedValue = 0.3f;

    private CinemachineBrain _brain;
    private MaterialPropertyBlock _block;

    // renderer -> the material's own default, captured on first fade so restore is exact
    private readonly Dictionary<Renderer, float> _faded = new Dictionary<Renderer, float>();
    private readonly HashSet<Renderer> _hitThisFrame = new HashSet<Renderer>();
    private readonly List<Renderer> _toRestore = new List<Renderer>();

    private void Awake()
    {
        _block = new MaterialPropertyBlock();
        _brain = GetComponent<CinemachineBrain>();
        if (_brain == null)
        {
            Debug.LogError("[CameraObstruction] No CinemachineBrain on this object — disabling.", this);
            enabled = false;
        }
    }

    private void LateUpdate()
    {
        _hitThisFrame.Clear();

        var vcam = _brain.ActiveVirtualCamera as CinemachineVirtualCameraBase;   // CM3: LookAt lives on the base class
        Transform target = vcam != null ? vcam.LookAt : null;
        if (target != null)
        {
            Vector3 dir = target.position - transform.position;
            float dist = dir.magnitude;
            if (dist > 0.01f)
            {
                RaycastHit[] hits = Physics.SphereCastAll(transform.position, _sphereRadius, dir / dist, dist, _obstructionMask);
                for (int i = 0; i < hits.Length; i++)
                {
                    Renderer r = hits[i].collider.GetComponent<Renderer>();
                    if (r == null || !r.sharedMaterial || !r.sharedMaterial.HasProperty(SeeThroughId)) continue;

                    _hitThisFrame.Add(r);
                    if (!_faded.ContainsKey(r))
                    {
                        _faded[r] = r.sharedMaterial.GetFloat(SeeThroughId);   // exact restore value
                        r.GetPropertyBlock(_block);
                        _block.SetFloat(SeeThroughId, _fadedValue);
                        r.SetPropertyBlock(_block);
                    }
                }
            }
        }

        // restore anything that stopped blocking
        _toRestore.Clear();
        foreach (KeyValuePair<Renderer, float> kv in _faded)
        {
            if (kv.Key == null) { _toRestore.Add(kv.Key); continue; }
            if (_hitThisFrame.Contains(kv.Key)) continue;
            kv.Key.GetPropertyBlock(_block);
            _block.SetFloat(SeeThroughId, kv.Value);
            kv.Key.SetPropertyBlock(_block);
            _toRestore.Add(kv.Key);
        }
        for (int i = 0; i < _toRestore.Count; i++) _faded.Remove(_toRestore[i]);
    }

    private void OnDisable()
    {
        // never leave the world faded (scene unload / component toggle)
        foreach (KeyValuePair<Renderer, float> kv in _faded)
        {
            if (kv.Key == null) continue;
            kv.Key.GetPropertyBlock(_block);
            _block.SetFloat(SeeThroughId, kv.Value);
            kv.Key.SetPropertyBlock(_block);
        }
        _faded.Clear();
    }
}
