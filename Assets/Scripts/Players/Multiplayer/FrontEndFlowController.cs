using System.Collections;
using UnityEngine;

/// <summary>
/// Couch M2 — front-end sequencer (Persistent, R3 singleton). Runs the pre-game flow:
/// <b>Start Menu → (New Game) → Character Select → writes PlayerRoster → done</b>.
///
/// <para><see cref="GameBootstrapper"/> calls <see cref="Begin"/> after Persistent loads (so the roster
/// exists) and waits on <see cref="IsFrontEndComplete"/> before loading the area. <b>Fail-open</b>: if the
/// UI refs are unwired it logs and completes immediately, so boot still reaches gameplay.</para>
///
/// <para>Back from Character Select returns to the Start Menu (the loop re-shows it). Cheap flow-test wiring;
/// intro-mode integration + save-slot select are later slices.</para>
/// </summary>
[DisallowMultipleComponent]
public class FrontEndFlowController : MonoBehaviour
{
    public static FrontEndFlowController Instance { get; private set; }

    [SerializeField] private MainMenuController mainMenu;
    [SerializeField] private CharacterSelectScreen characterSelect;
    [SerializeField] private CharacterSelectController selection;

    /// <summary>True once the flow has finished (both twins assigned) — GameBootstrapper waits on this.</summary>
    public bool IsFrontEndComplete { get; private set; }

    private bool _newGameRequested;
    private bool _backRequested;
    private bool _running;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void OnDestroy() { if (Instance == this) Instance = null; }

    /// <summary>Start the flow (idempotent). No-op if already running or complete.</summary>
    public void Begin()
    {
        if (_running || IsFrontEndComplete) return;

        if (mainMenu == null || characterSelect == null || selection == null)
        {
            Debug.LogError("[FrontEndFlowController] Unwired (mainMenu / characterSelect / selection) — " +
                           "skipping front-end so boot still reaches gameplay.", this);
            IsFrontEndComplete = true;
            return;
        }

        _running = true;
        StartCoroutine(RunRoutine());
    }

    private IEnumerator RunRoutine()
    {
        mainMenu.NewGameRequested += OnNewGame;
        mainMenu.OptionsRequested += OnOptions;
        characterSelect.BackRequested += OnBack;

        // Loop: Start Menu → Character Select; Back from select returns to the menu; completion exits.
        while (!selection.IsComplete)
        {
            // ── Start Menu ──
            _newGameRequested = false;
            characterSelect.Hide();
            mainMenu.Show();
            yield return new WaitUntil(() => _newGameRequested);
            mainMenu.Hide();

            // ── Character Select (Show() resets the selection each entry) ──
            _backRequested = false;
            characterSelect.Show();
            yield return new WaitUntil(() => selection.IsComplete || _backRequested);
            characterSelect.Hide();
        }

        mainMenu.NewGameRequested -= OnNewGame;
        mainMenu.OptionsRequested -= OnOptions;
        characterSelect.BackRequested -= OnBack;

        IsFrontEndComplete = true;
        _running = false;
    }

    private void OnNewGame() => _newGameRequested = true;
    private void OnBack()    => _backRequested = true;
    private void OnOptions() => Debug.Log("[FrontEndFlowController] Options pressed — TODO: open settings (stub).");
}
