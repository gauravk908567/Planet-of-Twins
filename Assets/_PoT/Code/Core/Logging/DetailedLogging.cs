using UnityEngine;

/// <summary>
/// The player's "Detailed logging (for bug reports)" setting (game.md §27.3), saved in PlayerPrefs with the other
/// settings. On → every <see cref="PoTLog"/> channel writes to the session log, in any build. Off → only the channels
/// DevConfig allows, which is none in a release build. Default off.
/// </summary>
public static class DetailedLogging
{
    public const string PrefKey = "Diag_DetailedLogging";

    public static bool Enabled => PlayerPrefs.GetInt(PrefKey, 0) == 1;

    public static void Set(bool on)
    {
        PlayerPrefs.SetInt(PrefKey, on ? 1 : 0);
        PlayerPrefs.Save();
        PoTLog.RefreshChannels();
    }
}
