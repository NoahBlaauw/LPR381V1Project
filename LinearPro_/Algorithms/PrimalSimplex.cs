using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LinearPro_.Model;

namespace LinearPro_.Algorithms
{
    internal sealed class PrimalSimplex : IAlgorithm
    {
        public string Name => "Primal Simplex";

        // === Build canonical tableau ===
        private List<double[]> ToCanonical(LPModel model, out List<string> colHeads, out List<string> rowHeads)
        {
            int numVars = model.ObjectiveCoefficients.Length;
            var constraints = model.Constraints;
            var variableColumns = new List<string>(model.VariableColumns);
            var extraColumns = new List<string>();

            // Count extras
            int slackCols = 0, surplusCols = 0, artificialCols = 0, binaryCols = 0;
            foreach (var c in constraints)
            {
                if (c.Relation == Relation.LE) slackCols++;
                else if (c.Relation == Relation.GE) surplusCols++;
                else artificialCols++;
            }
            foreach (var s in model.SignRestrictions)
                if (s == SignRestriction.Binary) binaryCols++;

            int totalCols = numVars + slackCols + surplusCols + artificialCols + binaryCols + 1; // +1 RHS
            var tableau = new List<double[]>();

            // === Z row ===
            var zRow = new double[totalCols];
            for (int v = 0; v < numVars; v++)
                zRow[v] = model.ObjectiveCoefficients[v];
            if (model.IsMax)
            {
                for (int v = 0; v < totalCols; v++)
                    zRow[v] *= -1.0;
            }
            // RHS already 0 by default
            tableau.Add(zRow);

            // === Constraints ===
            int slackCounter = 0, surplusCounter = 0, artCounter = 0;
            for (int i = 0; i < constraints.Count; i++)
            {
                var c = constraints[i];
                var row = new double[totalCols];

                // decision variable coefficients
                for (int v = 0; v < numVars; v++)
                    row[v] = c.Coefficients[v];

                // slack/surplus/artificial marker column
                if (c.Relation == Relation.LE)
                {
                    int col = numVars + slackCounter++;
                    row[col] = 1.0;
                    extraColumns.Add($"s{slackCounter}");
                }
                else if (c.Relation == Relation.GE)
                {
                    int col = numVars + slackCols + surplusCounter++;
                    row[col] = -1.0;
                    extraColumns.Add($"e{surplusCounter}");
                }
                else // EQ
                {
                    int col = numVars + slackCols + surplusCols + artCounter++;
                    row[col] = 0.0;
                    extraColumns.Add($"a{artCounter}");
                }

                // RHS
                row[totalCols - 1] = c.Rhs;

                // flip for >= (to make RHS positive if possible)
                if (c.Relation == Relation.GE)
                {
                    for (int k = 0; k < totalCols; k++)
                        row[k] *= -1.0;
                }

                tableau.Add(row);
            }

            // === Binary variable rows ===
            int binCounter = 0;
            for (int j = 0; j < model.SignRestrictions.Count; j++)
            {
                if (model.SignRestrictions[j] == SignRestriction.Binary)
                {
                    var row = new double[totalCols];
                    row[j] = 1.0; // x_j

                    // add its own slack to make x_j + s = 1
                    int col = numVars + slackCols + surplusCols + artificialCols + binCounter;
                    row[col] = 1.0;
                    extraColumns.Add($"sb{binCounter + 1}");

                    row[totalCols - 1] = 1.0; // RHS
                    tableau.Add(row);

                    binCounter++;
                }
            }

            // Build headers
            colHeads = new List<string>(variableColumns);
            colHeads.AddRange(extraColumns);
            colHeads.Add("RHS");

            // Row heads
            rowHeads = new List<string> { "Z" };
            for (int i = 1; i < tableau.Count; i++)
                rowHeads.Add($"C{i}");

            return tableau;
        }

        // === Build printable table string ===
        private string BuildTable(List<double[]> tableau, List<string> rowHeads, List<string> colHeads)
        {
            int colWidth = 12;
            var sb = new StringBuilder();

            sb.AppendLine("Tableau:");
            sb.Append("".PadRight(colWidth)); // row label spacer
            sb.AppendLine(string.Join("", colHeads.Select(h => h.PadRight(colWidth))));

            for (int i = 0; i < tableau.Count; i++)
            {
                var rowValues = tableau[i]
                    .Select(x => x.ToString("G6", CultureInfo.InvariantCulture).PadRight(colWidth));
                sb.AppendLine(rowHeads[i].PadRight(colWidth) + string.Join("", rowValues));
            }

            return sb.ToString();
        }

        // === Utility checks ===
        private static bool HasNegativeRHS(List<double[]> T)
        {
            int rows = T.Count;
            int rhs = T[0].Length - 1;
            for (int r = 1; r < rows; r++)
                if (T[r][rhs] < 0) return true;
            return false;
        }

        private static bool HasNegativeInZ(List<double[]> T)
        {
            int cols = T[0].Length;
            for (int c = 0; c < cols - 1; c++) // exclude RHS
                if (T[0][c] < 0) return true;
            return false;
        }

        // === Core pivot operation (Gauss-Jordan on pivot element) ===
        private static void ApplyPivot(List<double[]> T, int pr, int pc)
        {
            int rows = T.Count;
            int cols = T[0].Length;

            double pivot = T[pr][pc];
            if (Math.Abs(pivot) < 1e-12)
                throw new InvalidOperationException("Pivot element is zero; cannot pivot.");

            // Normalize pivot row
            for (int c = 0; c < cols; c++)
                T[pr][c] /= pivot;

            // Eliminate pivot column in other rows
            for (int r = 0; r < rows; r++)
            {
                if (r == pr) continue;
                double factor = T[r][pc];
                if (Math.Abs(factor) < 1e-15) continue;
                for (int c = 0; c < cols; c++)
                    T[r][c] -= factor * T[pr][c];
            }
        }

        // === Phase 1: repair negative RHS (no artificials; heuristic) ===
        private (List<double[]> Tableau, List<string> Log) Phase1(
            List<double[]> tableau, List<string> rowHeads, List<string> colHeads)
        {
            var log = new List<string> { "=== Phase 1 (repair negative RHS) ===" };
            int rhs = tableau[0].Length - 1;
            int maxIter = 1000;
            int iter = 0;

            while (HasNegativeRHS(tableau) && iter < maxIter)
            {
                iter++;
                // Pick pivot row: most negative RHS
                int pivotRow = -1;
                double minRhs = double.PositiveInfinity;
                for (int r = 1; r < tableau.Count; r++)
                {
                    double val = tableau[r][rhs];
                    if (val < minRhs)
                    {
                        minRhs = val;
                        pivotRow = r;
                    }
                }
                if (pivotRow == -1)
                {
                    log.Add("No negative RHS row found; stopping Phase 1.");
                    break;
                }

                // Pick pivot col: among columns with negative in pivotRow, maximize |Z / a_prc|
                int pivotCol = -1;
                double bestScore = double.PositiveInfinity;
                for (int c = 0; c < rhs; c++)
                {
                    double a = tableau[pivotRow][c];
                    if (a < 0) // only negative entries
                    {
                        double Zc = tableau[0][c];
                        double score = Math.Abs(Zc / a);
                        if (score < bestScore)
                        {
                            bestScore = score;
                            pivotCol = c;
                        }
                    }
                }

                if (pivotCol == -1)
                {
                    log.Add($"Phase 1: infeasible (no valid pivot column for row {rowHeads[pivotRow]}).");
                    break;
                }

                Console.WriteLine($"Phase 1 pivot on row {rowHeads[pivotRow]} and column {colHeads[pivotCol]}");
                log.Add($"Pivot: row {rowHeads[pivotRow]}, col {colHeads[pivotCol]}");
                ApplyPivot(tableau, pivotRow, pivotCol);
                log.Add(BuildTable(tableau, rowHeads, colHeads));
            }

            if (iter >= maxIter)
                log.Add("Phase 1 aborted: max iterations reached.");

            return (tableau, log);
        }

        // === Phase 2: optimize objective with primal ratio test ===
        private (List<double[]> Tableau, List<string> Log) Phase2(
            List<double[]> tableau, List<string> rowHeads, List<string> colHeads)
        {
            var log = new List<string> { "=== Phase 2 (optimize) ===" };
            int rhs = tableau[0].Length - 1;
            int maxIter = 2000;
            int iter = 0;

            while (HasNegativeInZ(tableau) && iter < maxIter)
            {
                iter++;

                // Entering: most negative coefficient in Z row (exclude RHS)
                int enterCol = -1;
                double minZ = 0.0;
                for (int c = 0; c < rhs; c++)
                {
                    double zc = tableau[0][c];
                    if (zc < minZ)
                    {
                        minZ = zc;
                        enterCol = c;
                    }
                }
                if (enterCol == -1)
                {
                    log.Add("No negative in Z; optimal.");
                    break;
                }

                // Leaving: minimum positive ratio RHS / a_rc (skip Z row). Prefer +0 if present.
                int leaveRow = -1;
                double bestRatio = double.PositiveInfinity;
                bool foundZeroRatio = false;

                for (int r = 1; r < tableau.Count; r++)
                {
                    double a = tableau[r][enterCol];
                    if (a > 0)
                    {
                        double ratio = tableau[r][rhs] / a;

                        // Prefer exact +0 (degenerate pivot) if it occurs
                        if (ratio == 0.0 && tableau[r][rhs] >= 0)
                        {
                            leaveRow = r;
                            foundZeroRatio = true;
                            bestRatio = 0.0;
                            break;
                        }

                        if (ratio > 0 && ratio < bestRatio)
                        {
                            bestRatio = ratio;
                            leaveRow = r;
                        }
                    }
                }

                if (leaveRow == -1)
                {
                    log.Add($"Phase 2: unbounded (no positive entries in column {colHeads[enterCol]}).");
                    break;
                }

                Console.WriteLine($"Phase 2 pivot on row {rowHeads[leaveRow]} and column {colHeads[enterCol]}");
                log.Add($"Pivot: row {rowHeads[leaveRow]}, col {colHeads[enterCol]} (ratio {(foundZeroRatio ? "0 (degenerate)" : bestRatio.ToString("G6", CultureInfo.InvariantCulture))})");
                ApplyPivot(tableau, leaveRow, enterCol);
                log.Add(BuildTable(tableau, rowHeads, colHeads));
            }

            if (iter >= maxIter)
                log.Add("Phase 2 aborted: max iterations reached.");

            return (tableau, log);
        }

        // === Solve wrapper ===
        public List<string> Solve(LPModel model)
        {
            var tableau = ToCanonical(model, out var colHeads, out var rowHeads);

            var output = new List<string>();
            output.Add("[Primal Simplex] Canonical form created.");
            output.Add(BuildTable(tableau, rowHeads, colHeads));

            // Phase 1 (only if needed)
            if (HasNegativeRHS(tableau))
            {
                var (t1, log1) = Phase1(tableau, rowHeads, colHeads);
                output.AddRange(log1);
                tableau = t1;
            }
            else
            {
                output.Add("Phase 1 skipped: no negative RHS detected.");
            }

            // Phase 2
            var (t2, log2) = Phase2(tableau, rowHeads, colHeads);
            output.AddRange(log2);

            return output;
        }
    }
}
