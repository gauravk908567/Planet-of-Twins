using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PoT.Diagnostics
{
    /// <summary>
    /// A text log of whole lines that stays under a size limit without losing its start or its end.
    ///
    /// The first <see cref="HeadBytes"/> are never removed (a session log's header and startup). When the file passes
    /// <see cref="MaxBytes"/>, the lines between the head and the newest <see cref="TailBytes"/> are removed and ONE
    /// marker line says how much was removed in total (a new trim replaces the old marker). So the file always holds
    /// its start and the lines that led up to now, which is what a bug report needs.
    ///
    /// Trimming happens in place: the newest lines are copied down to just after the head, and the file is cut short
    /// only after the copy. A crash during the copy still leaves the newest lines at the end of the file.
    ///
    /// Entries: a line that starts with a space or a tab continues the line before (a stack trace, a wrapped message).
    /// Callers mark the end of each entry with <see cref="EndEntry"/>; the head ends and trims run only between
    /// entries, and the kept tail always starts on an entry's first line.
    ///
    /// Every line is flushed at once (the tail survives a hard crash). Not thread-safe: the owner locks.
    /// </summary>
    public sealed class BoundedLogFile : IDisposable
    {
        private const string LineEnd = "\n";
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        private FileStream _stream;
        private byte[] _lineBuffer = new byte[1024];
        private byte[] _tailBuffer;
        private long _headEnd = -1;   // offset of the first entry after the head; -1 until the head is full
        private int _markerBytes;     // length of the trim marker that sits at _headEnd

        public string FilePath { get; }
        public long MaxBytes { get; }
        public long HeadBytes { get; }
        public long TailBytes { get; }

        /// <summary>Current size of the file in bytes.</summary>
        public long Length => _stream != null ? _stream.Length : 0;

        /// <summary>Bytes of older lines removed so far, and how many trims removed them.</summary>
        public long RemovedBytes { get; private set; }
        public int Trims { get; private set; }

        /// <summary>Set when a trim failed: the file then takes no more lines (it would grow without a limit).</summary>
        public string StopReason { get; private set; }
        public bool Stopped => StopReason != null;

        /// <summary>Creates the file (it must not exist). Others may read, and delete, it while it is open.</summary>
        public BoundedLogFile(string path, long maxBytes, long headBytes, long tailBytes)
        {
            if (headBytes <= 0 || tailBytes <= 0 || headBytes + tailBytes >= maxBytes)
                throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                    "BoundedLogFile needs head + tail < max (head {0}, tail {1}, max {2}).", headBytes, tailBytes, maxBytes));
            FilePath = path;
            MaxBytes = maxBytes;
            HeadBytes = headBytes;
            TailBytes = tailBytes;
            _stream = new FileStream(path, FileMode.CreateNew, FileAccess.ReadWrite,
                                     FileShare.ReadWrite | FileShare.Delete);
        }

        /// <summary>Appends one line (a line break is added) and flushes it to the OS.</summary>
        public void WriteLine(string line)
        {
            if (_stream == null || Stopped) return;
            if (line == null) line = string.Empty;
            int needed = Utf8.GetMaxByteCount(line.Length + LineEnd.Length);
            if (_lineBuffer.Length < needed) _lineBuffer = new byte[Math.Max(needed, _lineBuffer.Length * 2)];
            int count = Utf8.GetBytes(line, 0, line.Length, _lineBuffer, 0);
            count += Utf8.GetBytes(LineEnd, 0, LineEnd.Length, _lineBuffer, count);
            _stream.Write(_lineBuffer, 0, count);
            _stream.Flush();
        }

        /// <summary>Marks the end of an entry: the head may end here, and an over-size file is trimmed now.</summary>
        public void EndEntry()
        {
            if (_stream == null || Stopped) return;
            long length = _stream.Length;
            if (_headEnd < 0 && length >= HeadBytes) _headEnd = length;
            if (length <= MaxBytes || _headEnd < 0) return;

            try { Trim(length); }
            catch (Exception e)
            {
                StopReason = e.Message;
                try
                {
                    _stream.Seek(0, SeekOrigin.End);
                    string note = "=== The log could not be trimmed (" + e.Message + "): later lines are not written ===";
                    byte[] bytes = Utf8.GetBytes(note + LineEnd);
                    _stream.Write(bytes, 0, bytes.Length);
                    _stream.Flush();
                }
                catch (Exception) { /* the file is gone or locked: nothing more to write */ }
            }
        }

        public void Dispose()
        {
            try { _stream?.Dispose(); }
            catch (Exception) { /* already broken */ }
            _stream = null;
        }

        private void Trim(long length)
        {
            long from = length - TailBytes;
            if (from <= _headEnd + _markerBytes) return;   // nothing between the head and the tail yet

            if (_tailBuffer == null || _tailBuffer.Length < TailBytes) _tailBuffer = new byte[TailBytes];
            _stream.Seek(from, SeekOrigin.Begin);
            int read = ReadFully(_stream, _tailBuffer, (int)TailBytes);

            // Keep from the first entry that starts inside the tail: a line start that isn't a continuation.
            int keep = read;
            for (int i = 0; i < read - 1; i++)
            {
                if (_tailBuffer[i] != (byte)'\n') continue;
                byte next = _tailBuffer[i + 1];
                if (next != (byte)' ' && next != (byte)'\t') { keep = i + 1; break; }
            }

            long removedNow = from + keep - (_headEnd + _markerBytes);
            RemovedBytes += removedNow;
            Trims++;
            byte[] marker = Utf8.GetBytes(string.Format(CultureInfo.InvariantCulture,
                "=== {0} of older lines removed here ({1} trim{2}): this log keeps its first {3} and its newest {4} ==={5}",
                Size(RemovedBytes), Trims, Trims == 1 ? "" : "s", Size(HeadBytes), Size(TailBytes), LineEnd));

            _stream.Seek(_headEnd, SeekOrigin.Begin);
            _stream.Write(marker, 0, marker.Length);
            _stream.Write(_tailBuffer, keep, read - keep);
            _stream.SetLength(_stream.Position);   // last: until here the newest lines are still at the old end
            _stream.Flush();
            _markerBytes = marker.Length;
        }

        private static int ReadFully(Stream stream, byte[] buffer, int count)
        {
            int total = 0;
            while (total < count)
            {
                int n = stream.Read(buffer, total, count - total);
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        private static string Size(long bytes) =>
            bytes >= 1024 * 1024
                ? (bytes / (1024.0 * 1024.0)).ToString("0.# MB", CultureInfo.InvariantCulture)
                : (bytes / 1024.0).ToString("0.# KB", CultureInfo.InvariantCulture);
    }
}
