using System;
using System.Collections.Generic;
using System.Linq;
using LinearPro_.Model;

namespace LinearPro_.Core
{
    internal static class TableRenderer
    {
        public static void RenderModelAsTable(LPModel model)
        {
            if (model == null)
            {
                Console.WriteLine("No model loaded.");
                return;
            }

            // Build table grid with row/column headings
            var colHeads = new List<string>();
            colHeads.AddRange(model.VariableColumns); // e.g., X1, X2, S1, E2
            colHeads.Add("RHS");

            var rows = new List<Row>();
            rows.Add(new Row("Z", model.ObjectiveCoefficients.Concat(new[] { 0.0 }).ToArray()));

            for (int i = 0; i < model.Constraints.Count; i++)
            {
                var c = model.Constraints[i];
                var data = c.Coefficients.Concat(new[] { c.Rhs }).ToArray();
                rows.Add(new Row("C" + (i + 1), data));
            }

            DrawTable(colHeads, rows);
        }

        private static void DrawTable(List<string> headers, List<Row> rows)
        {
            int consoleWidth = Console.WindowWidth;
            if (consoleWidth < 60) consoleWidth = 60;

            // Compute preferred col widths
            var widths = new List<int> { Math.Max(3, rows.Select(r => r.RowHead.Length).Concat(new[] { 2 }).Max()) };
            foreach (var h in headers)
            {
                var maxData = rows.Select(r => FormatCell(r.Data[headers.IndexOf(h)])).Max(s => s.Length);
                widths.Add(Math.Max(h.Length, maxData) + 2);
            }

            // Scale if overflowing
            int total = widths.Sum() + headers.Count + 2; // separators margin
            if (total > consoleWidth)
            {
                double scale = (consoleWidth - headers.Count - 2) / (double)widths.Sum();
                for (int i = 0; i < widths.Count; i++)
                {
                    widths[i] = Math.Max(5, (int)Math.Floor(widths[i] * scale));
                }
            }

            // Draw top border
            WriteLineBorder('╔', '═', '╦', '╗', widths);

            // Header row
            WriteCell(" ", widths[0]);
            for (int i = 0; i < headers.Count; i++)
            {
                WriteSeparator('╦');
                WriteCell(headers[i], widths[i + 1], true);
            }
            Console.WriteLine();
            WriteLineBorder('╠', '═', '╬', '╣', widths);

            // Data rows
            foreach (var r in rows)
            {
                WriteCell(r.RowHead, widths[0], true);
                for (int i = 0; i < headers.Count; i++)
                {
                    WriteSeparator('║');
                    var s = FormatCell(r.Data[i]);
                    WriteCell(s, widths[i + 1]);
                }
                Console.WriteLine();
            }

            // Bottom
            WriteLineBorder('╚', '═', '╩', '╝', widths);
        }

        private static string FormatCell(double value)
        {
            return value.ToString("0.###");
        }

        private static void WriteCell(string text, int width, bool bold = false)
        {
            if (text.Length > width) text = text.Substring(0, Math.Max(0, width - 1)) + "…";
            if (bold) Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write((" " + text).PadRight(width));
            if (bold) Console.ResetColor();
        }

        private static void WriteSeparator(char ch)
        {
            Console.Write(ch);
        }

        private static void WriteLineBorder(char left, char fill, char cross, char right, List<int> widths)
        {
            Console.Write(left);
            for (int i = 0; i < widths.Count; i++)
            {
                Console.Write(new string(fill, widths[i]));
                Console.Write(i == widths.Count - 1 ? right : cross);
            }
            Console.WriteLine();
        }

        private sealed class Row
        {
            public string RowHead { get; }
            public double[] Data { get; }
            public Row(string head, double[] data) { RowHead = head; Data = data; }
        }
    }
}

