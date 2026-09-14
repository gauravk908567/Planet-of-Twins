using System;
using System.Diagnostics;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

/// <summary>
/// Player-chosen graphics API (DirectX 11 / DirectX 12 / Auto), applied the only way a graphics API
/// CAN be applied: the device is created before any game code runs, so switching it requires the
/// process to relaunch with Unity's <c>-force-*</c> launch argument. This class is that mechanism.
///
/// FLOW (BUILD ONLY — the Editor cannot relaunch itself and ignores <c>-force-*</c>):
///   • <see cref="EnforceOnBoot"/> runs BeforeSceneLoad every launch. If the saved choice differs from
///     the API actually running, it relaunches the exe with the matching <c>-force-d3d11/-force-d3d12</c>
///     plus a one-shot guard arg, then quits — so the next process comes up on the chosen API.
///   • The relaunched (guarded) process does NOT relaunch again (no loop). It also VERIFIES the force
///     took: if the GPU can't honour it (e.g. no D3D12), it reverts the saved choice to Auto so future
///     launches stop trying. One wasted relaunch at worst, self-correcting.
///   • Auto = never force → the build's default API order (D3D11 primary here) is used.
///
/// The Settings UI (<see cref="GraphicsSettingsController"/>) writes <see cref="PrefKey"/> and calls
/// <see cref="ApplyNow"/> from a "Restart now" button. Vulkan is intentionally absent: it is not in this
/// project's Windows graphics-API build list, so <c>-force-vulkan</c> would be a no-op (add it to Player
/// Settings first to expose it here).
/// </summary>
public static class GraphicsApiPreference
{
    /// <summary>PlayerPrefs key: 0 = Auto, 1 = DirectX 11, 2 = DirectX 12 (see <see cref="ApiChoice"/>).</summary>
    public const string PrefKey = "gfx_api";

    // Added to a relaunch so the child process knows not to relaunch again (loop guard + verify marker).
    private const string GuardArg = "-potApiApplied";

    public enum ApiChoice { Auto = 0, Direct3D11 = 1, Direct3D12 = 2 }

    public static ApiChoice Saved =>
        (ApiChoice)Mathf.Clamp(PlayerPrefs.GetInt(PrefKey, 0), 0, 2);

    /// <summary>The API actually running this session.</summary>
    public static GraphicsDeviceType CurrentApi => SystemInfo.graphicsDeviceType;

    /// <summary>Human label for the running API (for the "Current: …" readout).</summary>
    public static string CurrentApiLabel()
    {
        switch (SystemInfo.graphicsDeviceType)
        {
            case GraphicsDeviceType.Direct3D11: return "DirectX 11";
            case GraphicsDeviceType.Direct3D12: return "DirectX 12";
            case GraphicsDeviceType.Vulkan:     return "Vulkan";
            default:                            return SystemInfo.graphicsDeviceType.ToString();
        }
    }

    private static GraphicsDeviceType DesiredApi(ApiChoice c) =>
        c == ApiChoice.Direct3D12 ? GraphicsDeviceType.Direct3D12 : GraphicsDeviceType.Direct3D11;

    // ── Boot enforcement ──────────────────────────────────────────────────────
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void EnforceOnBoot()
    {
        if (Application.isEditor) return;   // -force-* + self-relaunch are build-only

        if (HasGuardArg())
        {
            // We ARE the relaunched process — never relaunch again. Verify the force actually took;
            // if the GPU couldn't honour it, revert to Auto so we stop trying on every future launch.
            var choice = Saved;
            if (choice != ApiChoice.Auto && SystemInfo.graphicsDeviceType != DesiredApi(choice))
            {
                Debug.LogWarning($"[GraphicsApiPreference] Forced {choice} not honoured (running " +
                                 $"{SystemInfo.graphicsDeviceType}). Reverting graphics-API preference to Auto.");
                PlayerPrefs.SetInt(PrefKey, (int)ApiChoice.Auto);
                PlayerPrefs.Save();
            }
            return;
        }

        var saved = Saved;
        if (saved == ApiChoice.Auto) return;                          // no preference → build default
        if (SystemInfo.graphicsDeviceType == DesiredApi(saved)) return; // already correct

        RelaunchWith(ForceArgFor(saved));
    }

    // ── Applied from the Settings "Restart now" button ────────────────────────
    public static void ApplyNow(ApiChoice choice)
    {
        PlayerPrefs.SetInt(PrefKey, (int)choice);
        PlayerPrefs.Save();

        if (Application.isEditor)
        {
            Debug.LogWarning("[GraphicsApiPreference] The Editor ignores -force-* and cannot relaunch itself; " +
                             "the graphics-API choice is saved and applies in a standalone build.");
            return;
        }
        // Auto relaunches with no force (back to the build default); a specific choice forces it.
        RelaunchWith(choice == ApiChoice.Auto ? null : ForceArgFor(choice));
    }

    private static string ForceArgFor(ApiChoice c) =>
        c == ApiChoice.Direct3D12 ? "-force-d3d12" : "-force-d3d11";

    private static void RelaunchWith(string forceArg)
    {
        try
        {
            string exe = Process.GetCurrentProcess().MainModule?.FileName;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
            {
                Debug.LogError("[GraphicsApiPreference] Could not resolve the executable path — cannot relaunch " +
                               "to change the graphics API.");
                return;
            }

            string args = string.IsNullOrEmpty(forceArg) ? GuardArg : $"{forceArg} {GuardArg}";
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = args,
                UseShellExecute = false,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? string.Empty,
            };
            Process.Start(psi);
            Application.Quit();
        }
        catch (Exception e)
        {
            Debug.LogError($"[GraphicsApiPreference] Relaunch failed ({e.GetType().Name}: {e.Message}). " +
                           "Graphics API unchanged.");
        }
    }

    private static bool HasGuardArg()
    {
        foreach (var a in Environment.GetCommandLineArgs())
            if (string.Equals(a, GuardArg, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }
}
