using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Reads files for a report while other code may still be writing them (this run's session log, Unity's
    /// Player.log). A long text keeps its head (startup: versions, GPU init) and its tail (the part that led to
    /// the report); the cut is always made at a line break, so no line or UTF-8 character is split.
    /// </summary>
    internal static class ReportFileReader
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false);

        /// <summary>Reads a text file, cut to <paramref name="limit"/> bytes: the first <paramref name="headBytes"/>
        /// and the rest from the end. <paramref name="size"/> is the file's full size.</summary>
        internal static string ReadText(string path, long limit, int headBytes, out long size, out bool trimmed)
        {
            using (var stream = OpenShared(path))
            {
                size = stream.Length;
                if (size <= limit)
                {
                    trimmed = false;
                    return Decode(ReadUpTo(stream, (int)size), 0, -1);
                }

                trimmed = true;
                byte[] head = ReadUpTo(stream, headBytes);
                int headEnd = Array.LastIndexOf(head, (byte)'\n') + 1;
                if (headEnd <= 0) headEnd = head.Length;

                int tailLength = (int)(limit - headBytes);
                stream.Seek(size - tailLength, SeekOrigin.Begin);
                byte[] tail = ReadUpTo(stream, tailLength);
                int tailStart = Array.IndexOf(tail, (byte)'\n') + 1;   // 0 when there is no line break

                long cut = size - headEnd - (tail.Length - tailStart);
                string marker = string.Format(CultureInfo.InvariantCulture,
                    "\n… [{0:N0} bytes cut here: a report keeps the first {1} KB and the last {2:0.#} MB of a long log] …\n\n",
                    cut, headBytes / 1024, tailLength / (1024.0 * 1024.0));
                return Decode(head, 0, headEnd) + marker + Decode(tail, tailStart, -1);
            }
        }

        /// <summary>Reads a whole file as bytes.</summary>
        internal static byte[] ReadBytes(string path)
        {
            using (var stream = OpenShared(path))
                return ReadUpTo(stream, (int)stream.Length);
        }

        private static FileStream OpenShared(string path) =>
            new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);

        // A file still being written can change length under us: read what was there when we looked.
        private static byte[] ReadUpTo(Stream stream, int count)
        {
            var buffer = new byte[count];
            int total = 0;
            while (total < count)
            {
                int read = stream.Read(buffer, total, count - total);
                if (read <= 0) break;
                total += read;
            }
            if (total < count) Array.Resize(ref buffer, total);
            return buffer;
        }

        private static string Decode(byte[] bytes, int start, int end)
        {
            if (end < 0) end = bytes.Length;
            if (start == 0 && end >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF) start = 3;   // BOM
            return Utf8.GetString(bytes, start, Math.Max(0, end - start));
        }
    }
}
