using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

/// <summary>
/// P8.1 second pass: plays the real boot the way a player does. Bootstrap (build index 0) → FrontEnd: New Game →
/// an EMPTY save slot → Character Select (P1 Lyra, P2 Kai) → Start → Intro, which loads Persistent + the first area
/// and hands over by itself (it has no video). The screens are driven through their own wired buttons
/// (<c>Button.onClick</c>, the same call a mouse click or pad Submit makes), read from the serialized fields
/// <see cref="FrontEndFlowController"/> uses. A renamed field fails loudly here with the name to fix.
/// Only ever picks a slot that is empty when the test starts; <see cref="DeleteSlotsCreatedSince"/> removes any
/// file the run wrote, so a real save is never touched.
/// </summary>
public static class BootHarness
{
    /// <summary>Real seconds allowed for one wait (a cold Persistent + area load takes ~15–25 s in the Editor).</summary>
    public const float WaitTimeoutSeconds = 180f;

    private static bool _introFinished;

    // ── Save slots ─────────────────────────────────────────────

    /// <summary>The slots that hold no save file right now.</summary>
    public static List<int> EmptySlots()
    {
        var empty = new List<int>();
        for (int i = 0; i < SaveSystem.SlotCount; i++)
            if (!SaveSystem.HasSave(i)) empty.Add(i);
        return empty;
    }

    /// <summary>The first slot in <paramref name="emptyAtStart"/> that is still empty, or -1.</summary>
    public static int FirstStillEmpty(IReadOnlyList<int> emptyAtStart)
    {
        foreach (int slot in emptyAtStart)
            if (!SaveSystem.HasSave(slot)) return slot;
        return -1;
    }

    /// <summary>Delete every save the run wrote into a slot that was empty when it started.</summary>
    public static void DeleteSlotsCreatedSince(IReadOnlyList<int> emptyAtStart)
    {
        if (emptyAtStart == null) return;
        foreach (int slot in emptyAtStart)
            if (SaveSystem.HasSave(slot)) SaveSystem.Delete(slot);
    }

    // ── Boot ───────────────────────────────────────────────────

    /// <summary>A fresh launch: load Bootstrap alone, then play the front-end into a New Game in
    /// <paramref name="slot"/> and wait until the intro has handed over to gameplay.</summary>
    public static IEnumerator BootNewGame(int slot)
    {
        yield return WithErrorCheck("the boot", LaunchAndPlay(slot));
    }

    /// <summary>The game-over screen's Restart, pressed through its own button (GameOverController reloads Bootstrap
    /// alone), then the same front-end → New Game → intro as a fresh launch.</summary>
    public static IEnumerator RestartIntoNewGame(int slot)
    {
        yield return WithErrorCheck("Restart and the boot after it", RestartAndPlay(slot));
    }

    private static IEnumerator LaunchAndPlay(int slot)
    {
        SceneManager.LoadScene(0);   // Bootstrap is build index 0 (CLAUDE.md scene architecture)
        yield return PlayNewGame(slot);
    }

    private static IEnumerator RestartAndPlay(int slot)
    {
        var gameOver = Object.FindAnyObjectByType<GameOverController>(FindObjectsInactive.Include);
        Assert.IsTrue(gameOver != null, "No GameOverController is loaded, so Restart can't be pressed.");
        Click(gameOver, "restartButton");

        yield return WaitFor(() => SceneFlowManager.Instance == null, "Persistent to unload after Restart");
        yield return PlayNewGame(slot);
    }

    // ── Errors ─────────────────────────────────────────────────
    // Any error or exception during a boot fails the test, with one exception by design (CLAUDE.md E1/E2): the sealed
    // AI core's singletons (MonoBehaviourSingleton<T>) put themselves in DontDestroyOnLoad, so the ones from an
    // earlier Persistent outlive the reload, and the reloaded Persistent's copies log "Destroying duplicate <T> on
    // <name>" and remove themselves. That message is allowed ONLY for a core type that had a survivor before the load.

    private static readonly List<string> _errors = new();

    private static IEnumerator WithErrorCheck(string what, IEnumerator body)
    {
        var allowed = new HashSet<string>();
        foreach (var survivor in Object.FindObjectsByType<CommonCore.MonoBehaviourSingleton>(
                     FindObjectsInactive.Include, FindObjectsSortMode.None))
            allowed.Add($"Destroying duplicate {survivor.GetType()} on {survivor.gameObject.name}");

        _errors.Clear();
        LogAssert.ignoreFailingMessages = true;   // this check replaces the Test Runner's "unhandled error" fail
        Application.logMessageReceived += HandleLog;
        try
        {
            yield return body;
        }
        finally
        {
            Application.logMessageReceived -= HandleLog;
            LogAssert.ignoreFailingMessages = false;
        }

        var unexpected = new List<string>();
        foreach (var e in _errors)
            if (!allowed.Contains(e)) unexpected.Add(e);
        Assert.IsEmpty(unexpected, $"Errors during {what}:\n" + string.Join("\n", unexpected));
    }

    private static void HandleLog(string message, string stackTrace, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
            _errors.Add(message);
    }

    private static IEnumerator PlayNewGame(int slot)
    {
        _introFinished = false;
        IntroController.OnIntroFinished += HandleIntroFinished;
        try
        {
            yield return PlayFrontEnd(slot);
            yield return WaitFor(() => _introFinished, "the intro to hand over to gameplay");
        }
        finally
        {
            IntroController.OnIntroFinished -= HandleIntroFinished;
        }

        // The intro unloads itself right after the event; Bootstrap unloaded once the intro was up.
        yield return WaitFor(() => Object.FindAnyObjectByType<IntroController>() == null, "the Intro scene to unload");
        yield return WaitFor(() => Object.FindAnyObjectByType<GameBootstrapper>() == null, "the Bootstrap scene to unload");
    }

    private static void HandleIntroFinished() => _introFinished = true;

    private static IEnumerator PlayFrontEnd(int slot)
    {
        FrontEndFlowController flow = null;
        yield return WaitFor(() => (flow = FrontEndFlowController.Instance) != null, "the FrontEnd scene");

        var menu = Field<MainMenuController>(flow, "mainMenu");
        var slots = OptionalField<SaveSlotScreen>(flow, "saveSlots");   // optional in the flow too
        var select = Field<CharacterSelectScreen>(flow, "characterSelect");
        var selection = Field<CharacterSelectController>(flow, "selection");

        // The flow must be listening before New Game is pressed, or the press is lost.
        yield return WaitFor(() => ValueField<bool>(flow, "_running") && Showing(menu), "the Main Menu");
        Click(menu, "newGameButton");

        if (slots != null)
        {
            // Each screen opens on the frame the previous one hides: wait for both, so a press can't land early.
            yield return WaitFor(() => !Showing(menu) && Showing(slots), "the save-slot screen");
            Assert.IsFalse(SaveSystem.HasSave(slot), $"Slot {slot} holds a save; the harness only uses empty slots.");
            Click(slots, $"slot{slot}Button");
            yield return WaitFor(() => !Showing(slots) && Showing(select), "Character Select");
        }
        else
        {
            yield return WaitFor(() => !Showing(menu) && Showing(select), "Character Select");
        }

        selection.SetPick(PlayerSlot.One, CharacterPick.Lyra);
        selection.SetPick(PlayerSlot.Two, CharacterPick.Kai);
        Click(select, "startButton");
        yield return WaitFor(() => selection.IsComplete, "Character Select to complete after Start");
    }

    // ── Helpers ────────────────────────────────────────────────

    /// <summary>Yield frames until <paramref name="done"/> is true; fail after <see cref="WaitTimeoutSeconds"/> of
    /// real time (menus and the tutorial may pause game time).</summary>
    public static IEnumerator WaitFor(Func<bool> done, string what)
    {
        float deadline = Time.realtimeSinceStartup + WaitTimeoutSeconds;
        while (!done())
        {
            if (Time.realtimeSinceStartup > deadline)
                Assert.Fail($"Timed out after {WaitTimeoutSeconds:0} s waiting for {what}.");
            yield return null;
        }
    }

    private static bool Showing(Component screen)
    {
        var panel = Field<GameObject>(screen, "panel");
        return panel.activeInHierarchy;
    }

    private static void Click(Component owner, string buttonField) =>
        Field<Button>(owner, buttonField).onClick.Invoke();

    private static T Field<T>(object owner, string name) where T : Object
    {
        var value = OptionalField<T>(owner, name);
        Assert.IsTrue(value != null, $"{owner.GetType().Name}.{name} is not wired.");
        return value;
    }

    private static T OptionalField<T>(object owner, string name) where T : Object =>
        FindField(owner, name).GetValue(owner) as T;

    private static T ValueField<T>(object owner, string name) where T : struct =>
        (T)FindField(owner, name).GetValue(owner);

    private static FieldInfo FindField(object owner, string name)
    {
        var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(field, $"{owner.GetType().Name} has no field '{name}'; update BootHarness to match.");
        return field;
    }
}
