using System.Collections;
using UnityEngine;

/// <summary>
/// Place in scene on a trigger collider or any GO.
/// Fires its DialogueSequenceSO when the condition is met.
///
/// Trigger types handled here:
///   OnEnterZone    — collider trigger
///   OnIdle         — player stands still for idleDelay seconds in zone
///   OnTaskProgress — looping hint, call StopLoop() when task completes
///   Manual         — call Play() directly from code
///   OnEvent        — call Play() from any game event listener
///
/// SETUP:
///   Add to a GO with a Trigger collider for zone triggers.
///   For event/manual triggers, no collider needed — call Play() directly.
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    [Header("Sequence")]
    [SerializeField] private DialogueSequenceSO _sequence;

    [Header("Trigger type")]
    [SerializeField] private DialogueTriggerType _triggerType = DialogueTriggerType.OnEnterZone;

    [Tooltip("Fire once ever, or every time conditions are met (respecting SO cooldown).")]
    [SerializeField] private bool _fireOnce = false;

    [Header("Idle trigger")]
    [Tooltip("Seconds player must be still in zone before dialogue fires (OnIdle only).")]
    [SerializeField] private float _idleDelay = 3f;

    [Header("Task progress / looping")]
    [Tooltip("Seconds between hint lines when looping (overrides SO hintInterval if > 0).")]
    [SerializeField] private float _loopIntervalOverride = 0f;

    // ── Internal ──────────────────────────────────────────────
    private bool _hasFired;
    private bool _playerInZone;
    private float _idleTimer;
    private Coroutine _loopCoroutine;

    // ── Public API ────────────────────────────────────────────
    /// <summary>Fire sequence manually from code or game event.</summary>
    public void Play()
    {
        if (_fireOnce && _hasFired) return;
        if (_sequence == null) return;

        DialoguePlayer.Instance?.Play(_sequence);
        _hasFired = true;
    }

    /// <summary>Stop a looping hint sequence.</summary>
    public void StopLoop()
    {
        if (_loopCoroutine != null)
        {
            StopCoroutine(_loopCoroutine);
            _loopCoroutine = null;
        }
    }

    // ── Zone triggers ─────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        if (_triggerType != DialogueTriggerType.OnEnterZone &&
            _triggerType != DialogueTriggerType.OnIdle &&
            _triggerType != DialogueTriggerType.OnTaskProgress) return;

        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;

        _playerInZone = true;

        switch (_triggerType)
        {
            case DialogueTriggerType.OnEnterZone:
                Play();
                break;

            case DialogueTriggerType.OnTaskProgress:
                if (_loopCoroutine == null && _sequence != null && _sequence.isLooping)
                    _loopCoroutine = StartCoroutine(LoopHints());
                break;

                // OnIdle handled in Update
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<Player>();
        if (player == null || player is SoulPlayer) return;
        _playerInZone = false;
        _idleTimer = 0f;
    }

    private void Update()
    {
        if (_triggerType != DialogueTriggerType.OnIdle) return;
        if (!_playerInZone) return;
        if (_fireOnce && _hasFired) return;

        // Accumulate idle time — reset if player moves
        // Movement check via DialoguePlayer's cached twin positions
        if (DialoguePlayer.Instance != null && DialoguePlayer.Instance.IsAnyTwinMoving)
        {
            _idleTimer = 0f;
            return;
        }

        _idleTimer += Time.deltaTime;
        if (_idleTimer >= _idleDelay)
        {
            _idleTimer = 0f;
            Play();
        }
    }

    // ── Looping hints ─────────────────────────────────────────
    private IEnumerator LoopHints()
    {
        if (_sequence.hintLines == null || _sequence.hintLines.Length == 0)
            yield break;

        float interval = _loopIntervalOverride > 0f
            ? _loopIntervalOverride
            : _sequence.hintInterval;

        int idx = 0;

        while (true)
        {
            yield return new WaitForSeconds(interval);

            if (!_playerInZone) continue;

            // Build a single-line temporary sequence for the hint
            var hint = _sequence.hintLines[idx % _sequence.hintLines.Length];
            DialoguePlayer.Instance?.PlaySingleLine(hint, _sequence.displayMode);
            idx++;
        }
    }

    private void OnDestroy() => StopLoop();
}