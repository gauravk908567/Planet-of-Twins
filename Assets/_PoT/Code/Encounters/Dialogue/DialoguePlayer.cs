using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// Singleton orchestrator for all story/ambient dialogue.
/// Routes each line to the correct display based on SO DisplayMode.
///
/// Simultaneous lines: when line.playSimultaneous = true, the line
/// fires on the other twin's nameplate at the same time as the
/// previous line, sharing that line's duration.
///
/// Display systems receive only: speakerName (string), text (string), duration (float).
/// They know nothing about DialogueSpeaker enum or game logic.
/// </summary>
public class DialoguePlayer : MonoBehaviour
{
    public static DialoguePlayer Instance { get; private set; }

    [Header("Nameplates — one per twin")]
    [SerializeField] private NameplateDisplay _lyraNameplate;
    [SerializeField] private NameplateDisplay _kaiNameplate;

    [Header("Subtitle bar")]
    [SerializeField] private SubtitleBarDisplay _subtitleBar;

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;

    [Header("Speaker display names")]
    [SerializeField] private string _lyraDisplayName = "Lyra";
    [SerializeField] private string _kaiDisplayName = "Kai";

    [Header("Subtitle bar speaker colours")]
    [SerializeField] private Color _lyraColour = new Color(0.6f, 0.8f, 1.0f);
    [SerializeField] private Color _kaiColour = new Color(1.0f, 0.75f, 0.4f);

    [Header("Twins — idle detection")]
    [SerializeField] private Transform _lyraTransform;
    [SerializeField] private Transform _kaiTransform;
    [SerializeField] private float _movementThreshold = 0.05f;

    // ── Internal ──────────────────────────────────────────────
    private DialogueSequenceSO _currentSequence;
    private Coroutine _playCoroutine;
    private readonly Dictionary<DialogueSequenceSO, float> _cooldowns = new();
    private Vector3 _lyraLastPos;
    private Vector3 _kaiLastPos;

    public bool IsAnyTwinMoving { get; private set; }
    public bool IsPlaying => _playCoroutine != null;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        if (_audioSource == null) _audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void Update()
    {
        if (_lyraTransform != null && _kaiTransform != null)
        {
            float lm = Vector3.Distance(_lyraTransform.position, _lyraLastPos);
            float km = Vector3.Distance(_kaiTransform.position, _kaiLastPos);
            IsAnyTwinMoving = lm > _movementThreshold || km > _movementThreshold;
            _lyraLastPos = _lyraTransform.position;
            _kaiLastPos = _kaiTransform.position;
        }

        var keys = new List<DialogueSequenceSO>(_cooldowns.Keys);
        foreach (var k in keys) { _cooldowns[k] -= Time.deltaTime; if (_cooldowns[k] <= 0f) _cooldowns.Remove(k); }
    }

    // ── Public API ────────────────────────────────────────────
    public void Play(DialogueSequenceSO sequence)
    {
        if (sequence == null || _cooldowns.ContainsKey(sequence)) return;
        if (IsPlaying)
        {
            if (sequence.skipIfBusy) return;
            if (_currentSequence != null && sequence.priority <= _currentSequence.priority) return;
            if (_currentSequence != null && !_currentSequence.isInterruptible) return;
            StopSequence();
        }
        _playCoroutine = StartCoroutine(PlaySequence(sequence));
    }

    public void PlaySingleLine(DialogueLine line, DialogueDisplayMode mode)
    {
        if (IsPlaying) return;
        _playCoroutine = StartCoroutine(PlayOneLine(line, mode, null));
    }

    public void StopSequence()
    {
        if (_playCoroutine != null) StopCoroutine(_playCoroutine);
        _playCoroutine = null; _currentSequence = null;
        _audioSource?.Stop();
        _lyraNameplate?.HideDialogue();
        _kaiNameplate?.HideDialogue();
        _subtitleBar?.Hide();
    }

    // ── Sequence ──────────────────────────────────────────────
    private IEnumerator PlaySequence(DialogueSequenceSO sequence)
    {
        _currentSequence = sequence;
        if (sequence.lines != null)
        {
            for (int i = 0; i < sequence.lines.Length; i++)
            {
                var line = sequence.lines[i];
                if (line == null) continue;

                // Skip simultaneous lines here — handled when previous line plays
                if (line.playSimultaneous) continue;

                // Check if next line is simultaneous
                DialogueLine simLine = null;
                if (i + 1 < sequence.lines.Length && sequence.lines[i + 1]?.playSimultaneous == true)
                    simLine = sequence.lines[i + 1];

                bool done = false;
                yield return StartCoroutine(PlayOneLine(line, sequence.displayMode, () => done = true, simLine));
                yield return new WaitUntil(() => done);
                yield return new WaitForSeconds(line.pauseAfter);
            }
        }
        if (sequence.cooldown > 0f) _cooldowns[sequence] = sequence.cooldown;
        _currentSequence = null;
        _playCoroutine = null;
    }

    private IEnumerator PlayOneLine(DialogueLine line, DialogueDisplayMode mode,
                                     System.Action onComplete,
                                     DialogueLine simultaneous = null)
    {
        string text = GetText(line.localisedText);

        AudioClip clip = null;
        if (line.audioClip != null && !line.audioClip.IsEmpty)
        {
            var op = line.audioClip.LoadAssetAsync();
            yield return op;
            if (op.Status == AsyncOperationStatus.Succeeded) clip = op.Result;
        }

        float duration = clip != null ? clip.length : line.displayDuration;
        DialogueDisplayMode effective = ResolveMode(mode, text);

        // Show primary line
        ShowLine(line.speaker, text, duration, effective);

        // Show simultaneous line on the other twin's nameplate immediately
        if (simultaneous != null)
        {
            string simText = GetText(simultaneous.localisedText);
            DialogueSpeaker otherSpeaker = line.speaker == DialogueSpeaker.Lyra
                ? DialogueSpeaker.Kai
                : DialogueSpeaker.Lyra;
            ShowOnNameplate(otherSpeaker, simText, duration);
        }

        if (clip != null && _audioSource != null)
        {
            _audioSource.clip = clip;
            _audioSource.Play();
        }

        yield return new WaitForSeconds(duration);
        onComplete?.Invoke();
    }

    // ── Display routing ───────────────────────────────────────
    private void ShowLine(DialogueSpeaker speaker, string text, float duration,
                           DialogueDisplayMode mode)
    {
        if (mode == DialogueDisplayMode.Nameplate || mode == DialogueDisplayMode.Both)
            ShowOnNameplate(speaker, text, duration);
        else
            ShowOnBar(speaker, text, duration);
    }

    private void ShowOnNameplate(DialogueSpeaker speaker, string text, float duration)
    {
        string name = speaker == DialogueSpeaker.Lyra ? _lyraDisplayName : _kaiDisplayName;
        var nameplate = speaker == DialogueSpeaker.Lyra ? _lyraNameplate : _kaiNameplate;
        nameplate?.ShowDialogue(name, text, duration);
    }

    private void ShowOnBar(DialogueSpeaker speaker, string text, float duration)
    {
        string name = speaker == DialogueSpeaker.Lyra ? _lyraDisplayName : _kaiDisplayName;
        Color colour = speaker == DialogueSpeaker.Lyra ? _lyraColour : _kaiColour;
        _subtitleBar?.Show(name, colour, text, duration, null);
    }

    private DialogueDisplayMode ResolveMode(DialogueDisplayMode mode, string text)
    {
        if (mode != DialogueDisplayMode.Both) return mode;
        return text.Length <= 60 ? DialogueDisplayMode.Nameplate : DialogueDisplayMode.SubtitleBar;
    }

    private string GetText(LocalizedString ls)
    {
        if (ls == null || ls.IsEmpty) return "";
        try { return ls.GetLocalizedString(); } catch { return ""; }
    }
}