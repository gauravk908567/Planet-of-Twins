using UnityEngine;

public class ProximityUIVisibility : MonoBehaviour
{
    [SerializeField] private GameObject _panelRoot;
    [SerializeField] private bool _requireBothPlayers = false;

    private int _playersInside = 0;

    private void Awake() => _panelRoot?.SetActive(false);

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("Player2")) return;
        _playersInside++;
        Evaluate();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player") && !other.CompareTag("Player2")) return;
        _playersInside = Mathf.Max(0, _playersInside - 1);
        Evaluate();
    }

    private void Evaluate()
    {
        bool show = _requireBothPlayers ? _playersInside >= 2 : _playersInside >= 1;
        _panelRoot?.SetActive(show);
    }

    public void ForceHide() { _playersInside = 0; _panelRoot?.SetActive(false); }
    public void ForceShow() => _panelRoot?.SetActive(true);
}