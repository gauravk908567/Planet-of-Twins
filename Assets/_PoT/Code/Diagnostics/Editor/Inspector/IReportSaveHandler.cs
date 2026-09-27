namespace PoT.Diagnostics.Editor
{
    /// <summary>
    /// The game's side of the Report Inspector's "Load this save": it knows where its save files live and how to
    /// start the game to reproduce a report. The package knows neither. The Inspector finds implementations with
    /// TypeCache; each needs a public parameterless constructor.
    /// </summary>
    public interface IReportSaveHandler
    {
        /// <summary>Whether this handler can load <paramref name="zipPath"/>, a file in the report (e.g. <c>save/slot_2.json</c>).</summary>
        bool CanLoad(string zipPath);

        /// <summary>The button text, e.g. "Load into Slot 2".</summary>
        string ButtonLabel(string zipPath);

        /// <summary>What loading will do, for the confirm dialog: what it replaces and where the backup goes.</summary>
        string Explain(string zipPath);

        /// <summary>Puts <paramref name="contents"/> in place and says what happened. Throws when it can't.</summary>
        string Load(string zipPath, byte[] contents);

        /// <summary>Starts the game the way a player would, so the loaded save can be continued.</summary>
        void Play();
    }
}
