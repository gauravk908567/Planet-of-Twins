using UnityEngine;
using TMPro;

public class SkillPointsHUDView : MonoBehaviour
{
    [SerializeField] private MonoBehaviour pointBankMono;
    [SerializeField] private TMP_Text pointsText;
    [SerializeField] private string format = "SP: {0}";

    private IPointBank _pointBank;

    private void Awake()
    {
        _pointBank = pointBankMono as IPointBank;
    }

    private void Start()
    {
        _pointBank ??= SkillTreeManager.Instance;
        if (_pointBank == null)
        {
            Debug.LogError("[SkillPointsHUDView] IPointBank unresolved — is Persistent loaded?", this);
            enabled = false;
            return;
        }
        Refresh(_pointBank.CurrentPoints);
    }

    private void OnEnable()
    {
        if (_pointBank == null) return;
        _pointBank.OnPointsChanged += Refresh;
        Refresh(_pointBank.CurrentPoints);
    }

    private void OnDisable()
    {
        if (_pointBank != null)
            _pointBank.OnPointsChanged -= Refresh;
    }

    private void Refresh(int points)
    {
        if (pointsText != null)
            pointsText.text = string.Format(format, points);
    }
}