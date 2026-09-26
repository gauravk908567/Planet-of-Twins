using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Sends a report to the team's Google Apps Script relay (game.md §27.4, source: <c>Tools/ReportRelay/Code.gs</c>),
    /// which emails the zip to the script owner's Gmail.
    ///
    /// Request: <c>POST</c> JSON <c>{format "pot-relay/1", reportId, fileName, category, description, contact, summary,
    /// zipBase64}</c>. Apps Script runs the script on the POST, then answers <c>302</c> with the result at another
    /// address. Redirects are NOT followed automatically (an HTTP client may repeat the POST there, which fails): the
    /// uploader reads the <c>Location</c> and fetches the result with a GET itself. A <c>200</c> straight away is
    /// accepted too. The result is JSON <c>{ok, message, id}</c>.
    ///
    /// The body (the zip as base64) is built on a worker thread; the requests run on the main thread.
    /// </summary>
    public sealed class AppsScriptUploader : IReportUploader
    {
        public const string Format = "pot-relay/1";
        private const int AnswerPreviewChars = 160;

        private readonly string _url;
        private readonly int _timeoutSeconds;
        private readonly long _maxZipBytes;

        public AppsScriptUploader(string url, int timeoutSeconds, long maxZipBytes)
        {
            _url = url;
            _timeoutSeconds = Math.Max(5, timeoutSeconds);
            _maxZipBytes = maxZipBytes;
        }

        public string Name => "Apps Script relay";

        [Serializable]
        private sealed class RelayAnswer
        {
            public bool ok;
            public string message;
            public string id;
        }

        private sealed class TooLargeException : Exception
        {
            public TooLargeException(string message) : base(message) { }
        }

        public IEnumerator Send(ReportPackage package, string zipPath, Action<ReportUploadResult> done)
        {
            var trace = new List<string>();
            var clock = System.Diagnostics.Stopwatch.StartNew();   // real time: the trace says where a slow send waited
            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                trace.Add("no network (Application.internetReachability)");
                done(ReportUploadResult.Failure("this PC looks offline", trace));
                yield break;
            }

            string summary = Summary(package);
            var build = Task.Run(() => BuildBody(package, zipPath, summary, _maxZipBytes));
            while (!build.IsCompleted) yield return null;
            if (build.IsFaulted || build.IsCanceled)
            {
                var error = build.Exception != null ? build.Exception.GetBaseException() : new OperationCanceledException();
                trace.Add("body not built: " + error.Message);
                done(ReportUploadResult.Failure(error is TooLargeException ? error.Message : "the report file couldn't be read", trace));
                yield break;
            }
            byte[] body = build.Result;
            trace.Add(string.Format(CultureInfo.InvariantCulture, "body {0:0.0} KB{1}", body.Length / 1024.0, At(clock)));

            string answer = null, redirect = null, failure = null;
            bool answerLost = false;   // the POST was taken (302), but reading its answer failed
            using (var post = new UnityWebRequest(_url, UnityWebRequest.kHttpVerbPOST))
            {
                post.uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json" };
                post.downloadHandler = new DownloadHandlerBuffer();
                post.redirectLimit = 0;   // see the class summary
                post.timeout = _timeoutSeconds;
                yield return post.SendWebRequest();

                long code = post.responseCode;
                string location = post.GetResponseHeader("Location");
                trace.Add($"POST → {code} {post.result}" +
                          (string.IsNullOrEmpty(location) ? string.Empty : $", Location {Shape(location)}") +
                          (string.IsNullOrEmpty(post.error) ? string.Empty : $", error '{post.error}'") + At(clock));

                if (code >= 300 && code < 400 && !string.IsNullOrEmpty(location)) redirect = location;
                else if (code == 200) answer = post.downloadHandler.text;
                else failure = Describe(post);
            }

            if (redirect != null)
            {
                using (var get = UnityWebRequest.Get(redirect))
                {
                    get.timeout = _timeoutSeconds;
                    yield return get.SendWebRequest();
                    trace.Add($"GET → {get.responseCode} {get.result}" +
                              (string.IsNullOrEmpty(get.error) ? string.Empty : $", error '{get.error}'") + At(clock));
                    if (get.result == UnityWebRequest.Result.Success) answer = get.downloadHandler.text;
                    else answerLost = true;
                }
            }

            // Apps Script answers the POST with its 302 only after the script has run, so the report most likely
            // arrived (the first report sent after a redeploy did: its answer GET returned 404 after ~50 s).
            if (answerLost)
            {
                done(ReportUploadResult.Unconfirmed("the server took it, but its confirmation didn't come back", trace));
                yield break;
            }
            if (answer == null)
            {
                done(ReportUploadResult.Failure(failure ?? "the server didn't answer", trace));
                yield break;
            }

            trace.Add("answer: " + Preview(answer));
            RelayAnswer reply = null;
            try { reply = JsonUtility.FromJson<RelayAnswer>(answer); }
            catch (Exception) { /* not JSON: e.g. a Google sign-in page when the deployment isn't open to "Anyone" */ }

            if (reply == null || (reply.message == null && reply.id == null))
                done(ReportUploadResult.Failure("the server's answer wasn't understood", trace));
            else if (!reply.ok)
                done(ReportUploadResult.Failure("the server refused it: " + reply.message, trace));
            else
                done(ReportUploadResult.Success(reply.message, trace));
        }

        // ── Body (worker thread: no Unity API) ─────────────────────────────

        private static byte[] BuildBody(ReportPackage package, string zipPath, string summary, long maxZipBytes)
        {
            var info = new FileInfo(zipPath);
            if (!info.Exists) throw new FileNotFoundException("The report zip is missing.", zipPath);
            if (info.Length > maxZipBytes)
                throw new TooLargeException(string.Format(CultureInfo.InvariantCulture,
                    "the report is too big to send from the game ({0:0.0} MB)", info.Length / (1024.0 * 1024.0)));

            string zip = Convert.ToBase64String(File.ReadAllBytes(zipPath));
            var json = new StringBuilder(zip.Length + 4096);
            json.Append('{');
            Field(json, "format", Format).Append(',');
            Field(json, "reportId", package.ReportId).Append(',');
            Field(json, "fileName", Path.GetFileName(zipPath)).Append(',');
            Field(json, "category", package.Category).Append(',');
            Field(json, "description", package.Description).Append(',');   // the player's words, as typed
            Field(json, "contact", package.Contact).Append(',');
            Field(json, "summary", summary).Append(',');
            json.Append("\"zipBase64\":\"").Append(zip).Append("\"}");
            return new UTF8Encoding(false).GetBytes(json.ToString());
        }

        private static StringBuilder Field(StringBuilder json, string key, string value) =>
            json.Append(ReportPackage.Quote(key)).Append(':').Append(ReportPackage.Quote(value ?? string.Empty));

        /// <summary>One line for the email: version, commit, OS, GPU.</summary>
        private static string Summary(ReportPackage package)
        {
            var parts = new List<string>(4);
            Pick(package, "app", "Version", "v", parts);
            Pick(package, "app", "Commit", "commit ", parts);
            Pick(package, "system", "OS", "", parts);
            Pick(package, "system", "GPU", "", parts);
            return string.Join(" · ", parts);
        }

        private static void Pick(ReportPackage package, string section, string key, string prefix, List<string> into)
        {
            foreach (var (k, v) in package.GetSection(section))
                if (k == key && !string.IsNullOrEmpty(v)) { into.Add(prefix + v); return; }
        }

        // ── Helpers ─────────────────────────────────────────────────────────

        private static string Describe(UnityWebRequest request)
        {
            switch (request.result)
            {
                case UnityWebRequest.Result.ConnectionError:
                    return "we couldn't reach our server";
                case UnityWebRequest.Result.ProtocolError:
                    return $"our server answered with an error ({request.responseCode})";
                default:
                    return string.IsNullOrEmpty(request.error) ? $"unexpected answer ({request.responseCode})" : request.error;
            }
        }

        private static string At(System.Diagnostics.Stopwatch clock) =>
            string.Format(CultureInfo.InvariantCulture, " (at {0:0.0} s)", clock.Elapsed.TotalSeconds);

        // Host + path + query parameter NAMES: enough to tell redirect forms apart, without the one-time key.
        private static string Shape(string url)
        {
            try
            {
                var uri = new Uri(url);
                var names = new List<string>();
                foreach (var pair in uri.Query.TrimStart('?').Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    if (pair.Length > 0) names.Add(eq >= 0 ? pair.Substring(0, eq) : pair);
                }
                return uri.Host + uri.AbsolutePath + (names.Count > 0 ? "?" + string.Join("&", names) : string.Empty);
            }
            catch (Exception) { return "?"; }
        }

        private static string Preview(string text)
        {
            text = (text ?? string.Empty).Replace('\n', ' ').Replace('\r', ' ');
            return text.Length <= AnswerPreviewChars ? text : text.Substring(0, AnswerPreviewChars) + " …";
        }
    }
}
