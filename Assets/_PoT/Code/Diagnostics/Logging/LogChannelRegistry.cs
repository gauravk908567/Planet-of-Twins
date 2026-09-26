using System;
using System.Collections.Generic;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Every log channel by name. Any assembly asks for a channel by name and gets the same instance, so a channel
    /// used in several assemblies (e.g. "Fx" in both the Fx package and gameplay) is switched in one place. The game
    /// decides which channels exist and which are on; this package only stores them.
    /// </summary>
    public static class LogChannelRegistry
    {
        private static readonly Dictionary<string, LogChannel> Channels =
            new Dictionary<string, LogChannel>(StringComparer.Ordinal);
        private static readonly object Gate = new object();

        /// <summary>The channel called <paramref name="name"/>, created (disabled) on first use.</summary>
        public static LogChannel Get(string name)
        {
            if (string.IsNullOrEmpty(name)) throw new ArgumentException("A log channel needs a name.", nameof(name));
            lock (Gate)
            {
                if (!Channels.TryGetValue(name, out var channel))
                {
                    channel = new LogChannel(name);
                    Channels.Add(name, channel);
                }
                return channel;
            }
        }

        /// <summary>Copies every known channel into <paramref name="into"/> (cleared first), e.g. for a report.</summary>
        public static void CopyAll(List<LogChannel> into)
        {
            into.Clear();
            lock (Gate) into.AddRange(Channels.Values);
        }
    }
}
