using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace ShingarERP.Utilities
{
    /// <summary>
    /// Utility for exporting data to Excel (XLSX) using EPPlus.
    /// EPPlus license set to NonCommercial for dev; switch to commercial for production.
    /// </summary>
    public static class ExcelExporter
    {
        static ExcelExporter()
        {
            // Set EPPlus license context (NonCommercial = free for non-commercial use)
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        /// <summary>Export a list of objects to an XLSX file.</summary>
        /// <typeparam name="T">Type of row data</typeparam>
        /// <param name="data">Collection of rows</param>
        /// <param name="sheetName">Worksheet name</param>
        /// <param name="columns">Column definitions: (header, propertySelector)</param>
        /// <param name="filePath">Output file path</param>
        public static async Task ExportAsync<T>(
            IEnumerable<T> data,
            string sheetName,
            IEnumerable<(string Header, Func<T, object?> Value)> columns,
            string filePath)
        {
            var rows   = data.ToList();
            var cols   = columns.ToList();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(sheetName);

            // Header row
            for (int c = 0; c < cols.Count; c++)
            {
                ws.Cells[1, c + 1].Value = cols[c].Header;
                ws.Cells[1, c + 1].Style.Font.Bold = true;
                ws.Cells[1, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[1, c + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(31, 73, 125));
                ws.Cells[1, c + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
            }

            // Data rows
            for (int r = 0; r < rows.Count; r++)
            {
                for (int c = 0; c < cols.Count; c++)
                {
                    var value = cols[c].Value(rows[r]);
                    ws.Cells[r + 2, c + 1].Value = value;

                    // Format dates
                    if (value is DateTime dt)
                        ws.Cells[r + 2, c + 1].Style.Numberformat.Format = "dd-MM-yyyy";

                    // Format decimals
                    if (value is decimal || value is double || value is float)
                        ws.Cells[r + 2, c + 1].Style.Numberformat.Format = "#,##0.00";

                    // Alternating row colour
                    if ((r + 1) % 2 == 0)
                    {
                        ws.Cells[r + 2, c + 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                        ws.Cells[r + 2, c + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(235, 240, 250));
                    }
                }
            }

            // Auto-fit columns
            ws.Cells[ws.Dimension?.Address ?? "A1"].AutoFitColumns();

            // Add filter to header row
            if (ws.Dimension != null)
                ws.Cells[1, 1, 1, cols.Count].AutoFilter = true;

            // Freeze header row
            ws.View.FreezePanes(2, 1);

            var dir = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            await package.SaveAsAsync(new FileInfo(filePath));
        }

        /// <summary>Export data as a byte array (for download without saving to disk).</summary>
        public static async Task<byte[]> ExportToBytesAsync<T>(
            IEnumerable<T> data,
            string sheetName,
            IEnumerable<(string Header, Func<T, object?> Value)> columns)
        {
            var rows   = data.ToList();
            var cols   = columns.ToList();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(sheetName);

            for (int c = 0; c < cols.Count; c++)
            {
                ws.Cells[1, c + 1].Value = cols[c].Header;
                ws.Cells[1, c + 1].Style.Font.Bold = true;
            }

            for (int r = 0; r < rows.Count; r++)
                for (int c = 0; c < cols.Count; c++)
                    ws.Cells[r + 2, c + 1].Value = cols[c].Value(rows[r]);

            ws.Cells.AutoFitColumns();

            return await package.GetAsByteArrayAsync();
        }
    }
}
