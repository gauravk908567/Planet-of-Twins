using System;
using System.Text;

namespace PoT.Diagnostics
{
    /// <summary>
    /// The fallback when a report can't be sent from the game (game.md §27.2): a prefilled email. A <c>mailto:</c> link
    /// can't attach files (RFC 6068), so the body asks the player to attach the zip from the folder the game opens.
    /// </summary>
    public static class ReportMail
    {
        // Long mailto links are cut off by some mail apps; the description is in the zip anyway.
        private const int DescriptionChars = 600;

        /// <summary>A <c>mailto:</c> URL for <see cref="UnityEngine.Application.OpenURL"/>.</summary>
        public static string MailtoUrl(string address, string productName, ReportPackage package, string zipFileName)
        {
            string subject = $"{productName} bug report {package.ReportId}" +
                             (string.IsNullOrEmpty(package.Category) ? string.Empty : $" ({package.Category})");

            var body = new StringBuilder(1024);
            body.Append("Report ID: ").Append(package.ReportId).Append('\n');
            if (!string.IsNullOrEmpty(package.Category)) body.Append("Type: ").Append(package.Category).Append('\n');
            if (!string.IsNullOrEmpty(package.Description))
            {
                string description = package.Description.Length <= DescriptionChars
                    ? package.Description
                    : package.Description.Substring(0, DescriptionChars) + " …";
                body.Append("\nWhat happened:\n").Append(description).Append('\n');
            }
            body.Append("\nPlease attach the file ").Append(zipFileName)
                .Append(" from the folder the game just opened (drag it into this email).\n");

            return "mailto:" + address.Trim() +
                   "?subject=" + Uri.EscapeDataString(subject) +
                   "&body=" + Uri.EscapeDataString(body.ToString());
        }
    }
}
