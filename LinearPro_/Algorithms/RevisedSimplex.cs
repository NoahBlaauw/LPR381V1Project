using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LinearPro_.Model;

namespace LinearPro_.Algorithms
{
    internal sealed class RevisedSimplex : IAlgorithm
    {
        public string Name => "Revised Simplex";

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
            tableau.Add(zRow);

            // === Constraints ===
            int slackCounter = 0, surplusCounter = 0, artCounter = 0;
            for (int i = 0; i < constraints.Count; i++)
            {
                var c = constraints[i];
                var row = new double[totalCols];

                for (int v = 0; v < numVars; v++)
                    row[v] = c.Coefficients[v];

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
                else
                {
                    int col = numVars + slackCols + surplusCols + artCounter++;
                    row[col] = 0.0;
                    extraColumns.Add($"a{artCounter}");
                }

                row[totalCols - 1] = c.Rhs;

                if (c.Relation == Relation.GE)
                {
                    for (int k = 0; k < totalCols; k++)
                        row[k] *= -1.0;
                }

                tableau.Add(row);
            }

            // Binary variable rows (if any)
            int binCounter = 0;
            for (int j = 0; j < model.SignRestrictions.Count; j++)
            {
                if (model.SignRestrictions[j] == SignRestriction.Binary)
                {
                    var row = new double[totalCols];
                    row[j] = 1.0;

                    int col = numVars + slackCols + surplusCols + artificialCols + binCounter;
                    row[col] = 1.0;
                    extraColumns.Add($"sb{binCounter + 1}");

                    row[totalCols - 1] = 1.0;
                    tableau.Add(row);

                    binCounter++;
                }
            }

            // Headers
            colHeads = new List<string>(variableColumns);
            colHeads.AddRange(extraColumns);
            colHeads.Add("RHS");

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
            sb.Append("".PadRight(colWidth));
            sb.AppendLine(string.Join("", colHeads.Select(h => h.PadRight(colWidth))));

            for (int i = 0; i < tableau.Count; i++)
            {
                var rowValues = tableau[i]
                    .Select(x => x.ToString("G6", CultureInfo.InvariantCulture).PadRight(colWidth));
                sb.AppendLine(rowHeads[i].PadRight(colWidth) + string.Join("", rowValues));
            }

            return sb.ToString();
        }

        // === Extract matrices A, b, c for Revised Simplex ===
        private string BuildMatrices(List<double[]> tableau, List<string> colHeads)
        {
            int m = tableau.Count - 1;     // number of constraints
            int n = colHeads.Count - 1;    // number of variables (excluding RHS)

            // A matrix (m x n)
            double[,] A = new double[m, n];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                    A[i, j] = tableau[i + 1][j];

            // b vector (RHS)
            double[] b = new double[m];
            for (int i = 0; i < m; i++)
                b[i] = tableau[i + 1][n];

            // c vector (objective)
            double[] c = new double[n];
            for (int j = 0; j < n; j++)
                c[j] = tableau[0][j];

            // Print them
            var sb = new StringBuilder();
            sb.AppendLine("Matrix A:");
            for (int i = 0; i < m; i++)
                sb.AppendLine(string.Join("\t", Enumerable.Range(0, n).Select(j => A[i, j].ToString("G4", CultureInfo.InvariantCulture))));

            sb.AppendLine("Vector b:");
            sb.AppendLine(string.Join("\t", b.Select(x => x.ToString("G4", CultureInfo.InvariantCulture))));

            sb.AppendLine("Vector c:");
            sb.AppendLine(string.Join("\t", c.Select(x => x.ToString("G4", CultureInfo.InvariantCulture))));

            return sb.ToString();
        }

        // === Solve (for now: print tableau + matrices) ===
        public List<string> Solve(LPModel model)
        {
            var tableau = ToCanonical(model, out var colHeads, out var rowHeads);

            var output = new List<string>();
            output.Add("[Revised Simplex] Canonical form created.");
            output.Add(BuildTable(tableau, rowHeads, colHeads));

            // Extract & print A, b, c
            output.Add(BuildMatrices(tableau, colHeads));

            return output;
        }
    }
}
