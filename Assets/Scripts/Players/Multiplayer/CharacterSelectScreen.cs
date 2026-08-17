using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Couch M2 — greybox two-player character-select screen bound to <see cref="CharacterSelectController"/>.
/// Per slot: a <b>Cycle</b> button (Lyra → Kai → Random) and a <b>Ready</b> toggle; plus a shared status
/// line and a <b>Back</b> button. Both-ready + distinct auto-starts (the controller writes the roster and
/// raises OnSelectionComplete); the same explicit twin → status warns and it won't start.
///
/// <para>Mouse-driven for the flow test. Real per-device (P1/P2) input via <c>PlayerInputRouter</c> is the
/// M2.2-proper pass; this greybox just proves the pick → ready → roster flow end-to-end.</para>
/// </summary>
[DisallowMultipleComponent]
public class CharacterSelectScreen : MonoBehaviour
{
    [SerializeField] private GameObject panel;
    [SerializeField] private CharacterSelectController controller;

    [Header("Player 1 (slot One)")]
    [SerializeField] private Button p1Cycle;
    [SerializeField] private Button p1Ready;
    [SerializeField] private TMP_Text p1Label;

    [Header("Player 2 (slot Two)")]
    [SerializeField] private Button p2Cycle;
    [SerializeField] private Button p2Ready;
    [SerializeField] private TMP_Text p2Label;

    [Header("Shared")]
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Button backButton;

    /// <summary>Raised when Back is pressed (FrontEndFlowController returns to the Start Menu).</summary>
    public event Action BackRequested;

    private bool _subscribed;

    private void Awake()
    {
        if (p1Cycle != null) p1Cycle.onClick.AddListener(CycleP1);
        if (p2Cycle != null) p2Cycle.onClick.AddListener(CycleP2);
        if (p1Ready != null) p1Ready.onClick.AddListener(ToggleReadyP1);
        if (p2Ready != null) p2Ready.onClick.AddListener(ToggleReadyP2);
        if (backButton != null) backButton.onClick.AddListener(RaiseBack);
    }

    private void OnDestroy()
    {
        if (p1Cycle != null) p1Cycle.onClick.RemoveListener(CycleP1);
        if (p2Cycle != null) p2Cycle.onClick.RemoveListener(CycleP2);
        if (p1Ready != null) p1Ready.onClick.RemoveListener(ToggleReadyP1);
        if (p2Ready != null) p2Ready.onClick.RemoveListener(ToggleReadyP2);
        if (backButton != null) backButton.onClick.RemoveListener(RaiseBack);
        Unsubscribe();
    }

    public void Show()
    {
        if (controller != null)
        {
            controller.ResetSelection();
            if (!_subscribed) { controller.OnChanged += Refresh; _subscribed = true; }
        }
        if (panel != null) panel.SetActive(true);
        Refresh();
    }

    public void Hide()
    {
        Unsubscribe();
        if (panel != null) panel.SetActive(false);
    }

    private void Unsubscribe()
    {
        if (_subscribed && controller != null) controller.OnChanged -= Refresh;
        _subscribed = false;
    }

    private void CycleP1() => controller?.Cycle(PlayerSlot.One, +1);
    private void CycleP2() => controller?.Cycle(PlayerSlot.Two, +1);
    private void ToggleReadyP1() => ToggleReady(PlayerSlot.One);
    private void ToggleReadyP2() => ToggleReady(PlayerSlot.Two);
    private void RaiseBack() => BackRequested?.Invoke();

    private void ToggleReady(PlayerSlot slot)
    {
        if (controller == null) return;
        controller.SetReady(slot, !controller.IsReady(slot));
    }

    private void Refresh()
    {
        if (controller == null) return;
        if (p1Label != null) p1Label.text = LabelFor(PlayerSlot.One);
        if (p2Label != null) p2Label.text = LabelFor(PlayerSlot.Two);

        if (statusText != null)
        {
            if (controller.IsComplete)       statusText.text = "Starting…";
            else if (controller.HasConflict) statusText.text = "Same twin — choose different!";
            else if (controller.CanStart)    statusText.text = "Both ready — starting…";
            else                             statusText.text = "Pick a twin, then Ready (both players)";
        }
    }

    private string LabelFor(PlayerSlot slot)
    {
        string who   = slot == PlayerSlot.One ? "P1" : "P2";
        string pick  = controller.PickOf(slot).ToString();
        string ready = controller.IsReady(slot) ? "  [READY]" : "";
        return $"{who}: {pick}{ready}";
    }
}
