using TMPro;
using UnityEngine;

public class TutorialHintDisplay : MonoBehaviour
{
    public static TutorialHintDisplay Instance { get; private set; }

    [SerializeField] private GameObject _panel;
    [SerializeField] private TMP_Text _text;

    private void Awake()
    {
        Instance = this;
        _panel.SetActive(false);
    }

    public void Show(string message)
    {
        _text.text = message;
        _panel.SetActive(true);
    }

    public void Hide() => _panel.SetActive(false);
}