using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SkillTreeUI : MonoBehaviour
{
    [Header("Panel — starts inactive")]
    [SerializeField] private GameObject SkillTreePanel;

    [Header("Inject — drag SkillTreeManager into all three")]
    [SerializeField] private MonoBehaviour _dataStoreMono;
    [SerializeField] private MonoBehaviour _purchaserMono;
    [SerializeField] private MonoBehaviour _pointBankMono;

    [Header("Points display")]
    [SerializeField] private TMP_Text PointsText;

    [Header("Toggle key")]
    [SerializeField] private KeyCode ToggleKey = KeyCode.Tab;

    [Header("Tab root objects — parent of hand-placed buttons")]
    [SerializeField] private GameObject KaiTabContent;
    [SerializeField] private GameObject LyraTabContent;
    [SerializeField] private GameObject SharedTabContent;

    [Header("Tab buttons")]
    [SerializeField] private Button KaiTab;
    [SerializeField] private Button LyraTab;
    [SerializeField] private Button SharedTab;

    [Header("Tab colours")]
    [SerializeField] private Color ActiveTabCol = new Color(0.35f, 0.67f, 1.00f);
    [SerializeField] private Color InactiveTabCol = new Color(0.35f, 0.35f, 0.38f);

    private IAbilityDataStore _dataStore;
    private ISkillTreePurchaser _purchaser;
    private IPointBank _pointBank;

    void Awake()
    {
        _dataStore = _dataStoreMono as IAbilityDataStore;
        _purchaser = _purchaserMono as ISkillTreePurchaser;
        _pointBank = _pointBankMono as IPointBank;

        InitialiseTab(KaiTabContent, GetKaiData());
        InitialiseTab(LyraTabContent, GetLyraData());
        InitialiseTab(SharedTabContent, GetSharedData());
        ShowTab(0);
        SkillTreePanel?.SetActive(false);
    }

    void OnEnable()
    {
        if (_pointBank != null) _pointBank.OnPointsChanged += RefreshPoints;
        KaiTab?.onClick.AddListener(() => ShowTab(0));
        LyraTab?.onClick.AddListener(() => ShowTab(1));
        SharedTab?.onClick.AddListener(() => ShowTab(2));
    }

    void OnDisable()
    {
        if (_pointBank != null) _pointBank.OnPointsChanged -= RefreshPoints;
        KaiTab?.onClick.RemoveAllListeners();
        LyraTab?.onClick.RemoveAllListeners();
        SharedTab?.onClick.RemoveAllListeners();
    }

    void Update()
    {
        bool togglePressed = Input.GetKeyDown(ToggleKey)
                          || Input.GetKeyDown(KeyCode.Escape);

        if (!togglePressed) return;

        bool opening = !SkillTreePanel.activeSelf;

        // Escape only closes — never opens
        if (Input.GetKeyDown(KeyCode.Escape) && !SkillTreePanel.activeSelf) return;

        SkillTreePanel.SetActive(opening);

        if (opening)
        {
            Time.timeScale = 0f;
            RefreshPoints(_pointBank.CurrentPoints);
            ShowTab(0);
        }
        else
        {
            Time.timeScale = 1f;
        }
    }

    void InitialiseTab(GameObject root, AbilityUpgradeData[] _)
    {
        if (root == null) return;
        foreach (var btn in root.GetComponentsInChildren<SkillNodeButton>(true))
            btn.InitialiseFromScene(_purchaser, _pointBank);
    }

    void ShowTab(int index)
    {
        if (KaiTabContent) KaiTabContent.SetActive(index == 0);
        if (LyraTabContent) LyraTabContent.SetActive(index == 1);
        if (SharedTabContent) SharedTabContent.SetActive(index == 2);

        SetTabColour(KaiTab, index == 0);
        SetTabColour(LyraTab, index == 1);
        SetTabColour(SharedTab, index == 2);
    }

    void SetTabColour(Button btn, bool active)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? ActiveTabCol : InactiveTabCol;
    }

    void RefreshPoints(int pts)
    {
        if (PointsText) PointsText.text = $"Points: <b>{pts}</b>";
    }

    AbilityUpgradeData[] GetKaiData() => new[] { _dataStore?.StunData };
    AbilityUpgradeData[] GetLyraData() => new[] { _dataStore?.PossessData };

    AbilityUpgradeData[] GetSharedData() => new[]
    {
        _dataStore?.GateData,
        _dataStore?.HealthRegenData,
        _dataStore?.AccordSpiritsData,
        _dataStore?.CoalesceData,
        _dataStore?.SoulConvData,
        _dataStore?.EmpowerData,
        _dataStore?.AccordData     // ← Accord State node in Shared tab
    };
}