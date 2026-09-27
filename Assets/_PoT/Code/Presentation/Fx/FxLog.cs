using PoT.Diagnostics;

namespace PoT.Fx
{
    /// <summary>
    /// The Fx package's handle on the game's "Fx" log channel. PoT.Fx can't see <c>PoTLog</c> (it lives in PoT.Gameplay,
    /// which references this assembly), but the registry hands every assembly the same channel by name, so this and
    /// <c>PoTLog.Fx</c> switch together.
    /// </summary>
    internal static class FxLog
    {
        private static readonly LogChannel _channel = LogChannelRegistry.Get("Fx");

        /// <summary>The Fx channel while it is on, otherwise null: <c>FxLog.Channel?.Info($"...")</c>.</summary>
        public static LogChannel Channel => _channel.IfEnabled;
    }
}
