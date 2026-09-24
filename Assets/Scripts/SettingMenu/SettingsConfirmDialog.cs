using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// The project's Yes/No modal. The settings screen uses three preset confirmations:
///   • Exit        — "Are you sure you want to exit?" + last-save time (Exit / Cancel).
///   • Keep/Revert — after a disruptive display change; auto-reverts on a countdown (Keep / Revert).
///   • Restart     — the graphics API only changes on relaunch (Restart Now / Later).
/// Other screens use the generic <see cref="Show"/> (e.g. the save-slot screen's overwrite / delete) with their own
/// instance of the same UI. Put this component on an always-active object and point <c>_root</c> at the dialog
/// child (Awake hides <c>_root</c>).
/// Hidden by default; appears only when a Show* method is called. Runs on UNSCALED time because the
/// screen is open while the game is paused (timeScale 0). Focus lands on the safe choice (Cancel /
/// Revert / Later) so a stray Submit is never destructive.
/// </summary>
public sealed class SettingsConfirmDialog : MonoBehaviour
{
    [SerializeField] private GameObject _root;
    [SerializeField] private TMP_Text _titleText;
    [SerializeField] private TMP_Text _messageText;
    [SerializeField] private Button _confirmButton;
    [SerializeField] private TMP_Text _confirmLabel;
    [SerializeField] private Button _cancelButton;
    [SerializeField] private TMP_Text _cancelLabel;

    private Action _onConfirm;
    private Action _onCancel;
    private bool _countdown;
    private float _deadline;            // unscaled time
    private string _countdownBase;

    public bool IsOpen => _root != null && _root.activeSelf;

    private void Awake()
    {
        if (_root != null) _root.SetActive(false);
        if (_confirmButton != null) _confirmButton.onClick.AddListener(Confirm);
        if (_cancelButton != null) _cancelButton.onClick.AddListener(Cancel);
    }

    public void ShowExit(string lastSaveLabel, Action onExit) =>
        Configure("Exit Game", $"Are you sure you want to exit?\n<size=70%>{lastSaveLabel}</size>",
                  "Exit", "Cancel", onExit, null, false, 0f, null);

    public void ShowKeepRevert(float seconds, Action onRevert) =>
        Configure("Keep Settings?", CountdownText("Keep these display settings?", seconds),
                  "Keep", "Revert", null, onRevert, true, seconds, "Keep these display settings?");

    public void ShowRestart(Action onRestart) =>
        Configure("Restart Required", "The graphics API changes after the game restarts.",
                  "Restart Now", "Later", onRestart, null, false, 0f, null);

    /// <summary>Any other confirmation. Focus lands on <paramref name="cancelLabel"/> (the safe choice).</summary>
    public void Show(string title, string message, string confirmLabel, string cancelLabel,
                     Action onConfirm, Action onCancel = null) =>
        Configure(title, message, confirmLabel, cancelLabel, onConfirm, onCancel, false, 0f, null);

    /// <summary>Back out from input (B / Esc) — same as pressing the cancel button.</summary>
    public void Dismiss()
    {
        if (IsOpen) Cancel();
    }

    /// <summary>The dialog's buttons' parent — for wiring pad navigation between just these two while open.</summary>
    public GameObject ButtonRow => _confirmButton != null ? _confirmButton.transform.parent.gameObject : null;

    private void Configure(string title, string message, string confirm, string cancel,
        Action onConfirm, Action onCancel, bool countdown, float seconds, string countdownBase)
    {
        _onConfirm = onConfirm;
        _onCancel = onCancel;
        if (_titleText != null) _titleText.text = title;
        if (_messageText != null) _messageText.text = message;
        if (_confirmLabel != null) _confirmLabel.text = confirm;
        if (_cancelLabel != null) _cancelLabel.text = cancel;

        _countdown = countdown;
        _countdownBase = countdownBase;
        _deadline = Time.unscaledTime + seconds;

        if (_root != null) _root.SetActive(true);
        UINavFocus.Focus(_cancelButton);   // safe/non-destructive default
    }

    private void Update()
    {
        if (!_countdown || !IsOpen) return;
        float remain = _deadline - Time.unscaledTime;
        if (_messageText != null) _messageText.text = CountdownText(_countdownBase, remain);
        if (remain <= 0f) Cancel();        // for Keep/Revert, Cancel == Revert
    }

    private static string CountdownText(string baseMsg, float remain) =>
        $"{baseMsg}\n<size=70%>Reverting in {Mathf.CeilToInt(Mathf.Max(0f, remain))}s…</size>";

    private void Confirm() { var a = _onConfirm; HideImmediate(); a?.Invoke(); }
    private void Cancel()  { var a = _onCancel;  HideImmediate(); a?.Invoke(); }

    public void HideImmediate()
    {
        _countdown = false;
        if (_root != null) _root.SetActive(false);
    }
}
