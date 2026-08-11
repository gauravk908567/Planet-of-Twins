// BirdWaypointHopper.cs

using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class BirdWaypointHopper : MonoBehaviour
{
    [Header("Hop Waypoints (fence path)")]
    public List<Transform> Waypoints;
    public List<float> WaitTimes;

    [Header("Fly Waypoints (sky path)")]
    [Tooltip("Place these in the sky - bird will arc smoothly through them")]
    public List<Transform> FlyWaypoints;
    public float FlySpeed = 6f;
    [Tooltip("How much the bird arcs between fly waypoints (0 = straight line)")]
    public float ArcStrength = 0.5f;

    [Header("Hop Settings")]
    public float HopSpeed = 2f;
    public float HopHeight = 0.15f;

    [Header("Idle Settings")]
    public float MinIdleInterval = 2f;
    public float MaxIdleInterval = 5f;

    [Header("Fly Delay")]
    [Tooltip("Seconds bird waits at last waypoint (worried anim plays) before flying")]
    public float FlyDelayAtLastWaypoint = 3.5f;

    // Events
    public System.Action OnReachedLastWaypoint;
    public System.Action OnFlyComplete;

    // ─────────────────────────────────────
    private Animator _anim;
    private bool _started = false;
    private bool _flying = false;
    private bool _waitingToFly = false;
    private Coroutine _idleRoutine;

    // hop int values from animator
    private const int HOP_FORWARD = 1;
    private const int HOP_BACKWARD = -1;
    private const int HOP_RIGHT = 2;
    private const int HOP_LEFT = -2;
    private const int HOP_IDLE = 0;

    private string[] _idleAnims = { "sing", "preen", "ruffle", "peck" };

    private void Awake()
    {
        _anim = GetComponentInChildren<Animator>();
        if (_anim == null)
            Debug.LogError($"[BirdWaypointHopper] No Animator found on {gameObject.name}!");
    }

    private void Start()
    {
        _idleRoutine = StartCoroutine(IdleLoop());
    }

    // ─────────────────────────────────────
    public void StartHopping()
    {
        if (_started) return;
        _started = true;
        StartCoroutine(HopSequence());
    }

    public void TriggerFlyAway()
    {
        if (!_waitingToFly)
        {
            StartCoroutine(WaitThenFly());
            return;
        }
        _waitingToFly = false;
        StartCoroutine(FlySequence());
    }

    IEnumerator WaitThenFly()
    {
        yield return new WaitUntil(() => _waitingToFly);
        _waitingToFly = false;
        StartCoroutine(FlySequence());
    }

    // ─────────────────────────────────────
    IEnumerator HopSequence()
    {
        PauseIdleLoop();

        for (int i = 0; i < Waypoints.Count; i++)
        {
            // ── Hop animation — Int parameter ──
            if (_anim != null)
            {
                _anim.SetInteger("hop", HOP_FORWARD);
            }

            yield return StartCoroutine(HopTo(Waypoints[i].position));

            // Reset hop int after arriving
            if (_anim != null)
                _anim.SetInteger("hop", HOP_IDLE);

            // Face next waypoint
            if (i < Waypoints.Count - 1)
            {
                Vector3 dir = Waypoints[i + 1].position - transform.position;
                dir.y = 0;
                if (dir != Vector3.zero) transform.forward = dir.normalized;
            }

            if (i == Waypoints.Count - 1)
            {
                // Last waypoint — play worried, auto fly after delay
                PauseIdleLoop();
                if (_anim != null) _anim.SetTrigger("worried");
                _waitingToFly = true;
                OnReachedLastWaypoint?.Invoke();

                // Wait the delay — worried animation plays during this
                yield return new WaitForSeconds(FlyDelayAtLastWaypoint);

                // Fly regardless of whether signal came or not
                _waitingToFly = false;
                StartCoroutine(FlySequence());
                yield break;
            }
            else
            {
                ResumeIdleLoop();
                float waitTime = i < WaitTimes.Count ? WaitTimes[i] : 1f;
                yield return new WaitForSeconds(waitTime);
                PauseIdleLoop();
            }
        }
    }

    IEnumerator HopTo(Vector3 target)
    {
        Vector3 start = transform.position;
        float elapsed = 0f;
        float duration = Mathf.Max(Vector3.Distance(start, target) / HopSpeed, 0.2f);

        while (elapsed < duration)
        {
            float t = elapsed / duration;
            Vector3 pos = Vector3.Lerp(start, target, t);
            pos.y += Mathf.Sin(t * Mathf.PI) * HopHeight;
            transform.position = pos;
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = target;
    }

    IEnumerator FlySequence()
    {
        _flying = true;
        PauseIdleLoop();

        // ── Flying animation — Bool parameter ──
        if (_anim != null)
        {
            _anim.SetInteger("hop", HOP_IDLE);   // make sure hop is cleared
            _anim.SetBool("flying", true);        // flying is a Bool
        }

        if (FlyWaypoints == null || FlyWaypoints.Count == 0)
        {
            float elapsed = 0f;
            while (elapsed < 3f)
            {
                transform.position += Vector3.up * FlySpeed * Time.deltaTime;
                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        else
        {
            for (int i = 0; i < FlyWaypoints.Count; i++)
            {
                Vector3 startPos = transform.position;
                Vector3 endPos = FlyWaypoints[i].position;

                Vector3 mid = (startPos + endPos) * 0.5f;
                mid += Vector3.Cross((endPos - startPos).normalized, Vector3.up) * ArcStrength;
                mid += Vector3.up * ArcStrength;

                float duration = Mathf.Max(Vector3.Distance(startPos, endPos) / FlySpeed, 0.3f);
                float elapsed = 0f;
                Vector3 prevPos = startPos;

                while (elapsed < duration)
                {
                    float t = elapsed / duration;
                    Vector3 pos = QuadraticBezier(startPos, mid, endPos, t);
                    transform.position = pos;

                    Vector3 dir = pos - prevPos;
                    if (dir != Vector3.zero)
                        transform.forward = Vector3.Lerp(
                            transform.forward, dir.normalized, Time.deltaTime * 8f);

                    prevPos = pos;
                    elapsed += Time.deltaTime;
                    yield return null;
                }

                transform.position = endPos;
            }
        }

        if (_anim != null) _anim.SetBool("flying", false);
        OnFlyComplete?.Invoke();
    }

    // ─────────────────────────────────────
    IEnumerator IdleLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(MinIdleInterval, MaxIdleInterval));
            if (!_started && !_flying && _anim != null)
                _anim.SetTrigger(_idleAnims[Random.Range(0, _idleAnims.Length)]);
        }
    }

    void PauseIdleLoop()
    {
        if (_idleRoutine != null) { StopCoroutine(_idleRoutine); _idleRoutine = null; }
    }

    void ResumeIdleLoop()
    {
        PauseIdleLoop();
        _idleRoutine = StartCoroutine(IdleLoop());
    }

    Vector3 QuadraticBezier(Vector3 a, Vector3 b, Vector3 c, float t)
    {
        float u = 1 - t;
        return (u * u * a) + (2 * u * t * b) + (t * t * c);
    }

    public bool IsFlying => _flying;
    public bool IsWaitingToFly => _waitingToFly;
    public Vector3 GetPosition() => transform.position;
}