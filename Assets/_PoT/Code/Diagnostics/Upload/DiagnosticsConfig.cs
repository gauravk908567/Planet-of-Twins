using UnityEngine;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Where bug reports go (game.md §27.3–27.4). Config only (R7): read by the report screen, never written at runtime.
    ///
    /// The relay URL sits in the build and in the repo, and that's accepted: the worst anyone can do with it is send
    /// junk to the team's inbox, and the relay guards against that (format check, size cap, rate limit). NEVER put mail
    /// credentials or API keys here.
    /// </summary>
    [CreateAssetMenu(menuName = "Diagnostics/Diagnostics Config", fileName = "DiagnosticsConfig")]
    public sealed class DiagnosticsConfig : ScriptableObject
    {
        [Tooltip("The Google Apps Script web app URL (https://script.google.com/macros/s/…/exec) that emails a report to " +
                 "the team (Tools/ReportRelay/README.md). Empty = reports are only saved on the player's PC.")]
        [SerializeField] private string _relayUrl = "";

        [Tooltip("The address 'Email It to Us' writes to when sending fails. Empty = the button just opens the " +
                 "report's folder. Players will see this address.")]
        [SerializeField] private string _fallbackEmail = "";

        [Tooltip("Seconds before a send gives up (each of the two requests).")]
        [SerializeField, Min(5)] private int _timeoutSeconds = 60;

        [Tooltip("A bigger zip isn't sent (Gmail takes 25 MB per email and the upload grows by a third as base64); " +
                 "it stays on the PC for 'Email It to Us'.")]
        [SerializeField, Range(1, 18)] private int _maxUploadMegabytes = 18;

        public string RelayUrl => (_relayUrl ?? string.Empty).Trim();
        public string FallbackEmail => (_fallbackEmail ?? string.Empty).Trim();
        public int TimeoutSeconds => Mathf.Max(5, _timeoutSeconds);
        public long MaxUploadBytes => Mathf.Clamp(_maxUploadMegabytes, 1, 18) * 1024L * 1024L;

        /// <summary>A usable relay: an https URL. Anything else keeps reports on the PC.</summary>
        public bool HasRelay => RelayUrl.StartsWith("https://", System.StringComparison.OrdinalIgnoreCase);

        public bool HasFallbackEmail => FallbackEmail.IndexOf('@') > 0;

        /// <summary>The uploader this config describes, or null when there's no relay (reports stay on the PC).</summary>
        public IReportUploader CreateUploader() =>
            HasRelay ? new AppsScriptUploader(RelayUrl, TimeoutSeconds, MaxUploadBytes) : null;
    }
}
