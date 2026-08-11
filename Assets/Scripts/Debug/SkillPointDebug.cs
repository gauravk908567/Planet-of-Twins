using UnityEngine;
using UnityEngine.UI;
using TMPro;

// ─────────────────────────────────────────────────────────────────────────────
// SkillPointDebug
// Testing only — remove before release.
//
// SETUP: Any always-active GameObject in scene.
//   Drag SkillTreeManager into _pointBankMono.
//   Optionally assign a Canvas/panel for on-screen buttons,
//   or just use the keyboard shortcuts — no UI required.
//
// KEYBOARD (always active):
//   F1  → +1 point
//   F2  → +10 points
//   F3  → +100 points
//   F4  → reset to 0
// ─────────────────────────────────────────────────────────────────────────────
public class SkillPointDebug : MonoBehaviour
{
    [Header("Inject")]
    [SerializeField] private MonoBehaviour _pointBankMono;
    [SerializeField] private SkillTreeManager _skillTreeManager; // for full reset

    [Header("Optional on-screen UI (leave empty to use keyboard only)")]
    [SerializeField] private Button _plus1Button;
    [SerializeField] private Button _plus10Button;
    [SerializeField] private Button _plus100Button;
    [SerializeField] private Button _resetButton;
    [SerializeField] private Button _resetUpgradesButton;
    [SerializeField] private TMP_Text _statusText;

    private IPointBank _pointBank;

    void Awake()
    {
        // Gated by the central dev switch — when the Trainer is off (master off, toggle off, or release build),
        // disable this whole GO so the keys never fire and the on-screen UI never shows. One flag rules it.
        if (!DevConfig.Trainer) { gameObject.SetActive(false); return; }

        _pointBank = _pointBankMono as IPointBank;
        if (_pointBank == null) Debug.LogError("[SkillPointDebug] Missing IPointBank");
    }

    void OnEnable()
    {
        _plus1Button?.onClick.AddListener(() => Add(1));
        _plus10Button?.onClick.AddListener(() => Add(10));
        _plus100Button?.onClick.AddListener(() => Add(100));
        _resetButton?.onClick.AddListener(() => Reset());
        _resetUpgradesButton?.onClick.AddListener(() => ResetUpgrades());

        if (_pointBank != null)
            _pointBank.OnPointsChanged += UpdateStatus;
    }

    void OnDisable()
    {
        _plus1Button?.onClick.RemoveAllListeners();
        _plus10Button?.onClick.RemoveAllListeners();
        _plus100Button?.onClick.RemoveAllListeners();
        _resetButton?.onClick.RemoveAllListeners();

        if (_pointBank != null)
            _pointBank.OnPointsChanged -= UpdateStatus;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.L)) Add(1);
        if (Input.GetKeyDown(KeyCode.O)) Add(10);
        if (Input.GetKeyDown(KeyCode.P)) Add(100);
        if (Input.GetKeyDown(KeyCode.I)) Reset();
        if (Input.GetKeyDown(KeyCode.K)) ResetUpgrades();
    }

    void Add(int amount)
    {
        if (_pointBank == null) return;
        _pointBank.AddPoints(amount);
        Debug.Log($"[SkillPointDebug] +{amount} → {_pointBank.CurrentPoints} pts");
    }

    void Reset()
    {
        if (_pointBank == null) return;
        _pointBank.TrySpendPoints(_pointBank.CurrentPoints);
        Debug.Log("[SkillPointDebug] Points reset to 0");
    }

    void ResetUpgrades()
    {
        if (_skillTreeManager == null) { Debug.LogWarning("[SkillPointDebug] _skillTreeManager not assigned"); return; }
        _skillTreeManager.Debug_ResetAll();
        Debug.Log("[SkillPointDebug] Full upgrade reset done");
    }

    void UpdateStatus(int pts)
    {
        if (_statusText) _statusText.text = $"[DEBUG] Points: {pts}";
    }
}