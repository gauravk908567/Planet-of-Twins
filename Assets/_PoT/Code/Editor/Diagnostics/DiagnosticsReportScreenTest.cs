using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using PoT.Diagnostics;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Play-mode check of the report screen's whole path (game.md §27.9, phase 3), in the real scene:
///   1. in game, opens pause the way ESC does, so the screenshot is taken; on the Main Menu there's no pause;
///   2. presses the player's entry button (Main Menu: Report a Problem; in game: the pause screen's Support row), types
///      a description, presses Send. From the crash notice, "Crash" must already be picked;
///   3. waits for the result, then checks the newest zip: report.json holds the description and the Detailed Logging
///      setting, save/ is there, screenshot.jpg is there in game (and not on the Main Menu), crash/ is there iff a
///      crash was pending, and a pending crash is cleared once it's in a report.
/// The report's screenshot is copied to <c>Temp/ReportTest_screenshot.jpg</c> to check by eye (right way up, true
/// colours), and the Game view is captured at each stage (<c>Temp/ReportTest_1_start/2_form/3_result.png</c>) to check
/// the layout. The result screen stays open for a look; press Done (or Esc) to close it.
///
/// Run: start Play from Bootstrap, then <b>Planet of Twins Tools ▸ Diagnostics ▸ Test Report Screen (Play mode)</b>.
/// </summary>
public static class DiagnosticsReportScreenTest
{
    private const double TimeoutSeconds = 30.0;
    private const string Description = "Automated test report from the Diagnostics menu.";
    private const string ScreenshotCopy = "Temp/ReportTest_screenshot.jpg";

    // Game-view captures (overlay UI included) of each stage, to check the layout by eye.
    private const string CaptureStart = "Temp/ReportTest_1_start.png";
    private const string CaptureForm = "Temp/ReportTest_2_form.png";
    private const string CaptureResult = "Temp/ReportTest_3_result.png";
    private const int FramesPerCapture = 3;   // a capture is written at the end of its frame

    private enum Step { StartShot, WaitPause, WaitScreenshot, PressEntry, WaitOpen, Send, WaitResult, ResultShot }

    private static Step _step;
    private static int _waitUntilFrame;
    private static double _deadline;
    private static bool _inGame;
    private static bool _waitForFreshShot;
    private static bool _hadCrash;
    private static ReportProblemScreen _screen;
    private static DateTime _startedUtc;

    [MenuItem("Planet of Twins Tools/Diagnostics/Test Report Screen (Play mode)")]
    public static void Run()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("[Report screen test] Start Play from Bootstrap first (Main Menu or in game).");
            return;
        }
        _screen = ReportProblemScreen.Instance;
        if (_screen == null)
        {
            Debug.LogError("[Report screen test] FAIL: no ReportProblemScreen in the loaded scenes.");
            return;
        }
        if (_screen.IsOpen)
        {
            Debug.LogError("[Report screen test] The report screen is already open; close it and run again.");
            return;
        }

        _startedUtc = DateTime.UtcNow;
        _hadCrash = CrashMarker.HasPendingCrash;
        _inGame = PauseMenuController.Instance != null;
        ScreenCapture.CaptureScreenshot(CaptureStart);
        WaitFrames(Step.StartShot);
        _deadline = EditorApplication.timeSinceStartup + TimeoutSeconds;
        EditorApplication.update -= Tick;
        EditorApplication.update += Tick;
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying) { Stop("Play stopped"); return; }
        if (EditorApplication.timeSinceStartup > _deadline) { Stop($"timed out at step {_step}"); return; }
        if (Time.frameCount < _waitUntilFrame) return;

        var so = new SerializedObject(_screen);
        switch (_step)
        {
            case Step.StartShot:
                var pause = PauseMenuController.Instance;
                _waitForFreshShot = _inGame && !pause.IsPauseOpen;   // already paused → the capture from that pause counts
                if (_waitForFreshShot) pause.OpenPause();
                _step = Step.WaitPause;
                return;

            case Step.WaitPause:
                if (_inGame && !PauseMenuController.Instance.IsPauseOpen) return;
                _step = Step.WaitScreenshot;
                return;

            case Step.WaitScreenshot:
                // The readback lands a frame or two after pause opens.
                if (_inGame && !ScreenshotCapture.HasCapture) return;
                if (_waitForFreshShot && ScreenshotCapture.CapturedUtc < _startedUtc) return;
                _step = Step.PressEntry;
                return;

            case Step.PressEntry:
                string missing = PressEntryButton();
                if (missing != null) { Stop(missing); return; }
                _step = Step.WaitOpen;   // the Main Menu flow opens the screen on its next frame
                return;

            case Step.WaitOpen:
                if (!_screen.IsOpen) return;
                if (_hadCrash && !_inGame && SelectedChip(so) != ReportProblemScreen.CrashCategory)
                    Debug.LogError("[Report screen test] FAIL: opened from the crash notice, but 'Crash' isn't picked.");
                var field = so.FindProperty("_description").objectReferenceValue as TMP_InputField;
                if (field == null) { Stop("the prefab's description field is unwired"); return; }
                field.text = Description;
                ScreenCapture.CaptureScreenshot(CaptureForm);
                WaitFrames(Step.Send);
                return;

            case Step.Send:
                // A keyboard player lands in the description, typing: game keys must read silent (UITextEntry).
                if (LastUsedDeviceTracker.LastUsed == InputDeviceKind.KeyboardMouse && !UITextEntry.IsTyping)
                    Debug.LogError("[Report screen test] FAIL: the description box has no keyboard focus, so typed " +
                                   "letters would reach game keys.");
                var send = so.FindProperty("_sendButton").objectReferenceValue as Button;
                if (send == null) { Stop("the prefab's Send button is unwired"); return; }
                if (!send.interactable) { Stop("Send stayed disabled with a description typed"); return; }
                send.onClick.Invoke();
                _step = Step.WaitResult;
                return;

            case Step.WaitResult:
                var result = so.FindProperty("_resultRoot").objectReferenceValue as GameObject;
                if (result == null || !result.activeInHierarchy) return;
                ScreenCapture.CaptureScreenshot(CaptureResult);
                WaitFrames(Step.ResultShot);
                return;

            case Step.ResultShot:
                EditorApplication.update -= Tick;
                Verify();
                return;
        }
    }

    private static void WaitFrames(Step next)
    {
        _step = next;
        _waitUntilFrame = Time.frameCount + FramesPerCapture;
    }

    // The player's way in: the Main Menu's Report a Problem button, or in game the pause screen's Support row.
    // Returns what's missing, or null once pressed.
    private static string PressEntryButton()
    {
        if (_inGame)
        {
            var settings = SettingsScreenController.Instance;
            if (settings == null || !settings.IsOpen) return "the pause screen isn't open";
            foreach (var binding in settings.GetComponentsInChildren<SettingBinding>(true))
                if (binding.Id == SettingsCatalog.ReportProblem && binding.Button != null)
                {
                    binding.Button.onClick.Invoke();
                    return null;
                }
            return $"no '{SettingsCatalog.ReportProblem}' row on the pause screen (run Settings ▸ Add Missing Catalog Rows)";
        }

        var menu = UnityEngine.Object.FindAnyObjectByType<MainMenuController>();   // editor test: a scene sweep is fine
        var report = menu != null ? new SerializedObject(menu).FindProperty("reportButton").objectReferenceValue as Button : null;
        if (report == null) return "no Main Menu Report button (run Diagnostics ▸ Place Report Screen in Loaded Scenes)";
        if (!report.gameObject.activeInHierarchy) return "the Main Menu isn't showing";
        report.onClick.Invoke();
        return null;
    }

    private static string SelectedChip(SerializedObject screen)
    {
        var chips = screen.FindProperty("_chips");
        for (int i = 0; i < chips.arraySize; i++)
        {
            var el = chips.GetArrayElementAtIndex(i);
            if (el.FindPropertyRelative("toggle").objectReferenceValue is Toggle t && t.isOn)
                return el.FindPropertyRelative("category").stringValue;
        }
        return null;
    }

    private static void Stop(string why)
    {
        EditorApplication.update -= Tick;
        Debug.LogError($"[Report screen test] FAIL: {why}.");
    }

    private static void Verify()
    {
        var problems = new List<string>();
        var report = new StringBuilder();

        var zip = Directory.GetFiles(ReportCollector.ReportsFolder, ReportCollector.FilePrefix + "*.zip")
                           .OrderBy(p => p, StringComparer.Ordinal).LastOrDefault();
        if (zip == null || File.GetLastWriteTimeUtc(zip) < _startedUtc.AddSeconds(-1))
        {
            Debug.LogError("[Report screen test] FAIL: no new zip in " + ReportCollector.ReportsFolder);
            return;
        }

        using (var archive = new ZipArchive(File.OpenRead(zip), ZipArchiveMode.Read))
        {
            var names = archive.Entries.Select(e => e.FullName).ToList();
            string json = ReadEntry(archive, ReportPackage.JsonName);
            if (json == null) problems.Add("report.json is missing");
            else
            {
                if (!json.Contains(Description)) problems.Add("report.json doesn't hold the typed description");
                if (!json.Contains(SettingsCatalog.DetailedLogging))
                    problems.Add($"report.json's settings section has no '{SettingsCatalog.DetailedLogging}'");
            }

            // A dev-direct or TestLab boot has no active slot, and the report's save section says so.
            bool noSlot = json != null && Regex.IsMatch(json, "\"Active slot\"\\s*:\\s*\"none");
            if (!noSlot && !names.Any(n => n.StartsWith("save/", StringComparison.Ordinal)))
                problems.Add("no save/ file (is there a save on disk?)");
            if (noSlot) report.Append("  no active save slot (dev boot): save/ not expected\n");

            var shot = archive.GetEntry(ReportPackage.ScreenshotName);
            if (_inGame && shot == null) problems.Add("in game, but no screenshot.jpg");
            if (!_inGame && shot != null) problems.Add("Main Menu report holds a screenshot.jpg");
            if (shot != null)
            {
                using (var s = shot.Open())
                using (var copy = File.Create(ScreenshotCopy)) s.CopyTo(copy);
                var tex = new Texture2D(2, 2);
                if (!tex.LoadImage(File.ReadAllBytes(ScreenshotCopy))) problems.Add("screenshot.jpg doesn't decode");
                else
                {
                    report.Append($"  screenshot {tex.width}×{tex.height}, copied to {ScreenshotCopy}\n");
                    if (tex.width > ScreenshotCapture.DefaultMaxWidth) problems.Add($"screenshot is {tex.width} wide");
                }
                UnityEngine.Object.DestroyImmediate(tex);
            }

            bool hasCrash = names.Any(n => n.StartsWith("crash/", StringComparison.Ordinal));
            if (_hadCrash && !hasCrash) problems.Add("a crash was pending but crash/ is empty");
            if (!_hadCrash && hasCrash) problems.Add("crash/ files without a pending crash");
            report.Append($"  {names.Count} files: {string.Join(", ", names)}\n");
        }
        if (_hadCrash && CrashMarker.HasPendingCrash) problems.Add("the pending crash wasn't cleared after the report");

        string where = _inGame ? "in game (via pause)" : "Main Menu";
        if (problems.Count == 0)
            Debug.Log($"[Report screen test] PASS ({where}{(_hadCrash ? ", with a pending crash" : "")})\n{zip}\n{report}");
        else
            Debug.LogError($"[Report screen test] FAIL ({where}): {string.Join("; ", problems)}\n{zip}\n{report}");
    }

    private static string ReadEntry(ZipArchive archive, string name)
    {
        var entry = archive.GetEntry(name);
        if (entry == null) return null;
        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8)) return reader.ReadToEnd();
    }
}
