using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShingarERP.Utilities
{
    /// <summary>
    /// Simple report generator for plain-text and HTML reports.
    /// Full RDLC/PDF support can be added via SSRS RDL or FastReport.
    /// </summary>
    public class ReportGenerator
    {
        // ── Plain-text / tabular reports ─────────────────────────────

        /// <summary>Generate a simple tabular text report.</summary>
        public string GenerateTextReport<T>(
            string title,
            IEnumerable<T> data,
            IEnumerable<(string Header, Func<T, string> Value, int Width)> columns)
        {
            var rows = data.ToList();
            var cols = columns.ToList();

            var sb = new StringBuilder();

            // Title
            sb.AppendLine(new string('=', 80));
            sb.AppendLine(CenterText(title, 80));
            sb.AppendLine(CenterText($"Generated: {DateTime.Now:dd-MM-yyyy HH:mm:ss}", 80));
            sb.AppendLine(new string('=', 80));
            sb.AppendLine();

            // Header row
            foreach (var col in cols)
                sb.Append(col.Header.PadRight(col.Width).Substring(0, Math.Min(col.Header.Length, col.Width)).PadRight(col.Width));
            sb.AppendLine();
            sb.AppendLine(new string('-', cols.Sum(c => c.Width)));

            // Data rows
            foreach (var row in rows)
            {
                foreach (var col in cols)
                {
                    var val = col.Value(row) ?? string.Empty;
                    sb.Append(val.Length > col.Width
                        ? val.Substring(0, col.Width - 3) + "..."
                        : val.PadRight(col.Width));
                }
                sb.AppendLine();
            }

            sb.AppendLine(new string('-', cols.Sum(c => c.Width)));
            sb.AppendLine($"Total rows: {rows.Count}");

            return sb.ToString();
        }

        /// <summary>Generate an HTML report suitable for printing or email.</summary>
        public string GenerateHtmlReport<T>(
            string title,
            IEnumerable<T> data,
            IEnumerable<(string Header, Func<T, string?> Value)> columns,
            string? footer = null)
        {
            var rows = data.ToList();
            var cols = columns.ToList();

            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'>");
            sb.AppendLine($"<title>{HtmlEncode(title)}</title>");
            sb.AppendLine(@"<style>
                body{font-family:Arial,sans-serif;font-size:12px;}
                h2{color:#1F4975;}
                table{border-collapse:collapse;width:100%;}
                th{background:#1F4975;color:white;padding:6px 8px;text-align:left;}
                td{padding:5px 8px;border-bottom:1px solid #ddd;}
                tr:nth-child(even){background:#EBF0FA;}
                .footer{margin-top:20px;font-size:10px;color:#666;}
            </style></head><body>");

            sb.AppendLine($"<h2>{HtmlEncode(title)}</h2>");
            sb.AppendLine($"<p style='color:#666;'>Generated: {DateTime.Now:dd-MM-yyyy HH:mm:ss}</p>");

            sb.AppendLine("<table><thead><tr>");
            foreach (var col in cols)
                sb.AppendLine($"<th>{HtmlEncode(col.Header)}</th>");
            sb.AppendLine("</tr></thead><tbody>");

            foreach (var row in rows)
            {
                sb.AppendLine("<tr>");
                foreach (var col in cols)
                    sb.AppendLine($"<td>{HtmlEncode(col.Value(row) ?? string.Empty)}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table>");
            sb.AppendLine($"<p>Total records: {rows.Count}</p>");

            if (!string.IsNullOrEmpty(footer))
                sb.AppendLine($"<div class='footer'>{HtmlEncode(footer)}</div>");

            sb.AppendLine("</body></html>");
            return sb.ToString();
        }

        /// <summary>Save HTML report to a file.</summary>
        public async Task SaveHtmlReportAsync(string html, string filePath)
        {
            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);
            await File.WriteAllTextAsync(filePath, html, Encoding.UTF8);
        }

        // ── Private helpers ──────────────────────────────────────────

        private static string CenterText(string text, int width)
        {
            if (text.Length >= width) return text;
            var pad = (width - text.Length) / 2;
            return new string(' ', pad) + text;
        }

        private static string HtmlEncode(string s)
            => System.Net.WebUtility.HtmlEncode(s);
    }
}
