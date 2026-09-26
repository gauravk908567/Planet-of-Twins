using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// One named, switchable log channel. Game code reaches a channel through an accessor that returns NULL while
    /// the channel is off (<see cref="IfEnabled"/>), so a call site reads <c>Log.AI?.Info($"...")</c> and a
    /// disabled channel never even builds the message string.
    ///
    /// <see cref="Info"/> writes to the session log, and to Unity's console only in the Editor and Development
    /// builds, so a release build never pays Unity's stack walk for it. Warnings and errors are NOT channel calls:
    /// use <c>Debug.LogWarning</c> / <c>Debug.LogError</c> (fail loud). The session log captures those through
    /// Unity's log hook, whatever channel state is.
    ///
    /// Get channels from <see cref="LogChannelRegistry"/>, never construct them.
    /// </summary>
    public sealed class LogChannel
    {
        public string Name { get; }

        /// <summary>Whether Info lines are written. Set by the game's channel mask; new channels start off.</summary>
        public bool Enabled { get; set; }

        internal LogChannel(string name) { Name = name; }

        /// <summary>This channel while it is enabled, otherwise null: the accessor behind <c>?.Info</c>.</summary>
        public LogChannel IfEnabled => Enabled ? this : null;

        /// <summary>Writes a detailed line. The channel name is the line's tag, so don't prefix the message with it.</summary>
        public void Info(string message, Object context = null)
        {
            if (!Enabled) return;
            SessionLog.WriteChannel(Name, message);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.Log($"[{Name}] {message}", context);   // LogType.Log: the session-log hook skips it (no double line)
#endif
        }
    }
}
