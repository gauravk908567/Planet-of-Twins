/// <summary>
/// Breadcrumb categories for <see cref="PoTLog.Crumb"/>: one vocabulary, so a report's trail can be filtered by
/// them. The diagnostics package records "Scene" and "App" crumbs by itself.
/// </summary>
public static class PoTCrumb
{
    /// <summary>Boot shape, New Game / Continue, game over, exit.</summary>
    public const string Flow = "Flow";
    /// <summary>A twin entered a location or was teleported (scene loads are recorded automatically).</summary>
    public const string Area = "Area";
    /// <summary>Checkpoint saved, respawn (soft reset).</summary>
    public const string Save = "Save";
    public const string Rescue = "Rescue";
    /// <summary>Joint and twin powers: Accord, Accord Spirits, Soul Convergence, Setsuna, Empower.</summary>
    public const string Power = "Power";
    public const string QTE = "QTE";
    public const string Tutorial = "Tutorial";
    /// <summary>Pause / resume.</summary>
    public const string Pause = "Pause";
    /// <summary>A setting changed.</summary>
    public const string Settings = "Settings";
    /// <summary>A device connected or disconnected; P1/P2 pairing.</summary>
    public const string Input = "Input";
}
