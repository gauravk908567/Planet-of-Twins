using System.Collections;
using UnityEngine;

/// <summary>
/// Couch M2 — front-end sequencer (Persistent, R3 singleton). Runs the pre-game flow:
/// <b>Start Menu → (New Game | Continue) → Save-Slot select → Character Select → writes PlayerRoster → done</b>.
///
/// <para><see cref="GameBootstrapper"/> calls <see cref="Begin"/> after Persistent loads (so the roster
/// exists) and waits on <see cref="IsFrontEndComplete"/> before loading the area. It then reads
/// <see cref="SaveService.PendingLoad"/> to branch: New Game → intro/cutscene, Continue → load the saved area.
/// Both front-end modes run Character Select (the user may swap seats between sessions — the save is progress,
/// not seat assignment). <b>Fail-open</b>: if the core UI refs are unwired it logs and completes immediately so
/// boot still reaches gameplay; the save-slot screen is <b>optional</b> (unwired → skip straight to select).</para>
///
/// <para>Back from the Save-Slot screen or Character Select returns to the Start Menu (the loop re-shows it).
/// Cheap flow-test wiring; final art is a later pass.</para>
/// </summary>
[DisallowMultipleComponent]
public class FrontEndFlowController : MonoBehaviour
{
    public static FrontEndFlowController Instance { get; private set; }

    [SerializeField] private MainMenuController mainMenu;
    [SerializeField] private SaveSlotScreen saveSlots;   // optional — unwired skips straight to Character Select
    [SerializeField] private CharacterSelectScreen characterSelect;
    [SerializeField] private CharacterSelectController selection;

    /// <summary>True once the flow has finished (both twins assigned) — GameBootstrapper waits on this.</summary>
    public bool IsFrontEndComplete { get; private set; }

    private bool _newGameRequested;
    private bool _continueRequested;
    private bool _slotChosen;
    private bool _slotBackRequested;
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
        mainMenu.NewGameRequested  += OnNewGame;
        mainMenu.ContinueRequested += OnContinue;
        mainMenu.OptionsRequested  += OnOptions;
        if (saveSlots != null) { saveSlots.SlotChosen += OnSlotChosen; saveSlots.BackRequested += OnSlotBack; }
        characterSelect.BackRequested += OnBack;

        // Save/Continue feature gate: the save-slot screen appears only when the feature is live. While it's
        // dormant (SaveService.enableSaving off — world-state contract deferred), the flow is menu → New Game →
        // Character Select, exactly as before slice 4. Continue is greyed by MainMenuController in that state, so
        // only New Game can fire here.
        bool useSlots = saveSlots != null && SaveService.Instance != null && SaveService.Instance.SavingEnabled;

        // Loop: Start Menu → (Save-Slot) → Character Select. Back from either sub-screen returns to the menu;
        // Character-Select completion (both twins assigned) exits.
        while (!selection.IsComplete)
        {
            // ── Start Menu ──
            _newGameRequested = _continueRequested = false;
            characterSelect.Hide();
            saveSlots?.Hide();
            mainMenu.Show();
            yield return new WaitUntil(() => _newGameRequested || _continueRequested);
            mainMenu.Hide();

            // ── Save-Slot select (only when the feature is live). New Game → pick a target slot; Continue → pick
            //    a save to resume (which stages SaveService.PendingLoad for the boot path). Back returns to menu. ──
            if (useSlots)
            {
                var mode = _continueRequested ? SaveSlotScreen.Mode.Continue : SaveSlotScreen.Mode.NewGame;
                _slotChosen = _slotBackRequested = false;
                saveSlots.Show(mode);
                yield return new WaitUntil(() => _slotChosen || _slotBackRequested);
                saveSlots.Hide();
                if (_slotBackRequested) continue;   // back up to the Start Menu
            }

            // ── Character Select (Show() resets the selection each entry). Runs for BOTH modes. ──
            _backRequested = false;
            characterSelect.Show();
            yield return new WaitUntil(() => selection.IsComplete || _backRequested);
            characterSelect.Hide();
        }

        mainMenu.NewGameRequested  -= OnNewGame;
        mainMenu.ContinueRequested -= OnContinue;
        mainMenu.OptionsRequested  -= OnOptions;
        if (saveSlots != null) { saveSlots.SlotChosen -= OnSlotChosen; saveSlots.BackRequested -= OnSlotBack; }
        characterSelect.BackRequested -= OnBack;

        IsFrontEndComplete = true;
        _running = false;
    }

    private void OnNewGame()        => _newGameRequested = true;
    private void OnContinue()       => _continueRequested = true;
    private void OnSlotChosen(int slot) => _slotChosen = true;
    private void OnSlotBack()       => _slotBackRequested = true;
    private void OnBack()           => _backRequested = true;
    private void OnOptions()        => Debug.Log("[FrontEndFlowController] Options pressed — TODO: open settings (stub).");
}
