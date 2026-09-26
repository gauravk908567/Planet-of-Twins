using System;
using System.Text;

namespace PoT.Diagnostics
{
    /// <summary>
    /// Short report ids the player can read out or type, e.g. <c>R-7F3K2Q</c>. The alphabet leaves out
    /// 0/O and 1/I/L so nothing reads ambiguously. 31^6 ≈ 900 million ids.
    /// </summary>
    internal static class ReportId
    {
        private const string Alphabet = "23456789ABCDEFGHJKMNPQRSTUVWXYZ";
        private const int Length = 6;

        internal static string New()
        {
            byte[] random = Guid.NewGuid().ToByteArray();
            var id = new StringBuilder("R-", 2 + Length);
            for (int i = 0; i < Length; i++) id.Append(Alphabet[random[i] % Alphabet.Length]);
            return id.ToString();
        }
    }
}
