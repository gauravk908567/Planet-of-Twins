using PoT.Diagnostics;
using UnityEngine;

/// <summary>
/// Planet of Twins' logging entry point (game.md §27).
///
///   • Detailed lines: <c>PoTLog.AI?.Info($"{name} → {mood}")</c>. A channel accessor returns null while the
///     channel is off, so the message string is never built. Don't repeat the channel name in the message.
///   • Breadcrumbs: <c>PoTLog.Crumb(PoTCrumb.Save, "checkpoint saved")</c> at key events, never per frame.
///   • Warnings and errors stay <c>Debug.LogWarning</c> / <c>Debug.LogError</c> (fail loud). The session log
///     captures them whatever the channels say.
///
/// Which channels are on: DevConfig ▸ Log Channels in the Editor and Development builds (needs Master ON). A
/// release build starts with every channel off; the player's "Detailed logging" setting arrives in phase 3.
/// </summary>
public static class PoTLog
{
    private static readonly LogChannel _ai        = Channel(PoTLogChannels.AI);
    private static readonly LogChannel _spawn     = Channel(PoTLogChannels.Spawn);
    private static readonly LogChannel _streaming = Channel(PoTLogChannels.Streaming);
    private static readonly LogChannel _save      = Channel(PoTLogChannels.Save);
    private static readonly LogChannel _combat    = Channel(PoTLogChannels.Combat);
    private static readonly LogChannel _twins     = Channel(PoTLogChannels.Twins);
    private static readonly LogChannel _qte       = Channel(PoTLogChannels.QTE);
    private static readonly LogChannel _tutorial  = Channel(PoTLogChannels.Tutorial);
    private static readonly LogChannel _input     = Channel(PoTLogChannels.Input);
    private static readonly LogChannel _ui        = Channel(PoTLogChannels.UI);
    private static readonly LogChannel _fx        = Channel(PoTLogChannels.Fx);
    private static readonly LogChannel _world     = Channel(PoTLogChannels.World);

    public static LogChannel AI        => _ai.IfEnabled;
    public static LogChannel Spawn     => _spawn.IfEnabled;
    public static LogChannel Streaming => _streaming.IfEnabled;
    public static LogChannel Save      => _save.IfEnabled;
    public static LogChannel Combat    => _combat.IfEnabled;
    public static LogChannel Twins     => _twins.IfEnabled;
    public static LogChannel QTE       => _qte.IfEnabled;
    public static LogChannel Tutorial  => _tutorial.IfEnabled;
    public static LogChannel Input     => _input.IfEnabled;
    public static LogChannel UI        => _ui.IfEnabled;
    public static LogChannel Fx        => _fx.IfEnabled;
    public static LogChannel World     => _world.IfEnabled;

    /// <summary>Records a key event in the bug-report trail (and the session log). Use a <see cref="PoTCrumb"/> category.</summary>
    public static void Crumb(string category, string text) => Breadcrumbs.Add(category, text);

    /// <summary>Turns each channel on or off from <paramref name="mask"/>.</summary>
    public static void ApplyChannelMask(PoTLogChannels mask)
    {
        foreach (PoTLogChannels flag in System.Enum.GetValues(typeof(PoTLogChannels)))
            if (flag != PoTLogChannels.None)
                LogChannelRegistry.Get(flag.ToString()).Enabled = (mask & flag) != 0;
    }

    /// <summary>Re-reads the channel mask from DevConfig (called when the active DevConfig changes).</summary>
    public static void RefreshChannels() => ApplyChannelMask(DevConfig.LogChannels);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplyStartupChannels() => RefreshChannels();

    private static LogChannel Channel(PoTLogChannels flag) => LogChannelRegistry.Get(flag.ToString());
}
