using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using LinearPro_.Model;

namespace LinearPro_.Algorithms
{
    // Revised Simplex implementation (Phase II only) with detailed iteration logs.
    // Note: This class expects ToCanonical to create slack columns so an identity
    // basis exists. If not present, the solver will report Phase I is required.
    internal sealed class RevisedSimplex : IAlgorithm
    {
        public string Name => "Revised Simplex";

        // === Build canonical tableau ===
        // (kept mostly as provided by you so it integrates with your LPModel)
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
            // keep zRow as-is; we will use model.ObjectiveCoefficients for algorithm logic later
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

        // === Helper: extract A,b and c (original objective) from tableau ===
        private (double[,] A, double[] b, double[] origC, double[] cAlg, int m, int n, int numVars) ExtractMatricesForAlgorithm(LPModel model, List<double[]> tableau, List<string> colHeads)
        {
            int m = tableau.Count - 1;     // number of constraints
            int n = colHeads.Count - 1;    // number of variables (excluding RHS)
            int numVars = model.ObjectiveCoefficients.Length;

            double[,] A = new double[m, n];
            for (int i = 0; i < m; i++)
                for (int j = 0; j < n; j++)
                    A[i, j] = tableau[i + 1][j];

            double[] b = new double[m];
            for (int i = 0; i < m; i++)
                b[i] = tableau[i + 1][n];

            // origC: original objective coefficients extended with zeros for added columns
            double[] origC = new double[n];
            for (int j = 0; j < n; j++) origC[j] = 0.0;
            for (int j = 0; j < Math.Min(numVars, n); j++) origC[j] = model.ObjectiveCoefficients[j];

            // cAlg: internal objective used by algorithm (we convert min -> max by flipping signs)
            double[] cAlg = new double[n];
            double sign = model.IsMax ? 1.0 : -1.0; // if minimization, multiply by -1 to maximize
            for (int j = 0; j < n; j++) cAlg[j] = 0.0;
            for (int j = 0; j < Math.Min(numVars, n); j++) cAlg[j] = sign * model.ObjectiveCoefficients[j];

            return (A, b, origC, cAlg, m, n, numVars);
        }

        // === Pretty printers ===
        private static string FormatVector(double[] v, string title, string[] names = null, string fmt = "G6")
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(title)) sb.AppendLine(title);
            for (int i = 0; i < v.Length; i++)
            {
                string label = names == null ? $"[{i}]" : names[i];
                sb.AppendLine($"{label}: {v[i].ToString(fmt, CultureInfo.InvariantCulture)}");
            }
            return sb.ToString();
        }

        private static string FormatMatrix(double[,] M, string title, string fmt = "G6")
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(title)) sb.AppendLine(title);
            int r = M.GetLength(0), c = M.GetLength(1);
            for (int i = 0; i < r; i++)
            {
                var row = new string[c];
                for (int j = 0; j < c; j++)
                    row[j] = M[i, j].ToString(fmt, CultureInfo.InvariantCulture);
                sb.AppendLine(string.Join("\t", row));
            }
            return sb.ToString();
        }

        // === Linear algebra helpers ===
        private static double[] GetColumn(double[,] A, int j)
        {
            int m = A.GetLength(0);
            var col = new double[m];
            for (int i = 0; i < m; i++) col[i] = A[i, j];
            return col;
        }

        private static double[,] GetColumns(double[,] A, IList<int> idx)
        {
            int m = A.GetLength(0);
            int k = idx.Count;
            var B = new double[m, k];
            for (int j = 0; j < k; j++)
                for (int i = 0; i < m; i++)
                    B[i, j] = A[i, idx[j]];
            return B;
        }

        private static double[] MatVec(double[,] M, double[] v)
        {
            int r = M.GetLength(0), c = M.GetLength(1);
            var y = new double[r];
            for (int i = 0; i < r; i++)
            {
                double s = 0.0;
                for (int j = 0; j < c; j++) s += M[i, j] * v[j];
                y[i] = s;
            }
            return y;
        }

        private static double[] VecMat(double[] v, double[,] M) // v^T * M
        {
            int r = M.GetLength(0), c = M.GetLength(1);
            var y = new double[c];
            for (int j = 0; j < c; j++)
            {
                double s = 0.0;
                for (int i = 0; i < r; i++) s += v[i] * M[i, j];
                y[j] = s;
            }
            return y;
        }

        private static double Dot(double[] a, double[] b)
        {
            double s = 0.0;
            for (int i = 0; i < a.Length; i++) s += a[i] * b[i];
            return s;
        }

        private static double[,] Inverse(double[,] M)
        {
            int n = M.GetLength(0);
            if (n != M.GetLength(1)) throw new InvalidOperationException("Only square matrices can be inverted.");

            double[,] A = new double[n, 2 * n];
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < n; j++) A[i, j] = M[i, j];
                for (int j = 0; j < n; j++) A[i, n + j] = (i == j) ? 1.0 : 0.0;
            }

            for (int col = 0; col < n; col++)
            {
                int pivot = col;
                double maxAbs = Math.Abs(A[pivot, col]);
                for (int r = col + 1; r < n; r++)
                {
                    double v = Math.Abs(A[r, col]);
                    if (v > maxAbs) { maxAbs = v; pivot = r; }
                }
                if (Math.Abs(A[pivot, col]) < 1e-12) throw new InvalidOperationException("Basis matrix is singular or ill-conditioned.");

                if (pivot != col)
                {
                    for (int j = 0; j < 2 * n; j++)
                    {
                        double tmp = A[col, j]; A[col, j] = A[pivot, j]; A[pivot, j] = tmp;
                    }
                }

                double diag = A[col, col];
                for (int j = 0; j < 2 * n; j++) A[col, j] /= diag;

                for (int r = 0; r < n; r++)
                {
                    if (r == col) continue;
                    double factor = A[r, col];
                    if (Math.Abs(factor) < 1e-15) continue;
                    for (int j = 0; j < 2 * n; j++) A[r, j] -= factor * A[col, j];
                }
            }

            var inv = new double[n, n];
            for (int i = 0; i < n; i++)
                for (int j = 0; j < n; j++)
                    inv[i, j] = A[i, n + j];
            return inv;
        }

        // === Try to find an identity basis in A; returns basis column indices (size m) or null ===
        private static List<int> FindIdentityBasis(double[,] A, double eps = 1e-9)
        {
            int m = A.GetLength(0), n = A.GetLength(1);
            var used = new bool[n];
            var basis = new List<int>(new int[m]);

            for (int i = 0; i < m; i++)
            {
                int found = -1;
                for (int j = 0; j < n; j++)
                {
                    if (used[j]) continue;
                    bool ok = Math.Abs(A[i, j] - 1.0) <= eps;
                    if (!ok) continue;
                    for (int r = 0; r < m; r++)
                    {
                        if (r == i) continue;
                        if (Math.Abs(A[r, j]) > eps) { ok = false; break; }
                    }
                    if (ok) { found = j; break; }
                }
                if (found == -1) return null;
                used[found] = true;
                basis[i] = found; // basis aligned by row
            }
            return basis;
        }

        // === Revised Simplex Phase II ===
        private List<string> RevisedSimplexPhaseII(LPModel model, List<double[]> tableau, List<string> colHeads)
        {
            var logs = new List<string>();
            var (A, b, origC, cAlg, m, n, numVars) = ExtractMatricesForAlgorithm(model, tableau, colHeads);

            // Attempt to find initial basis (identity columns)
            var basis = FindIdentityBasis(A);
            if (basis == null || basis.Count != m)
            {
                logs.Add("[Revised Simplex] Could not find an initial identity basis in A. Phase I required.");
                return logs;
            }

            string[] varNames = colHeads.Take(n).ToArray();

            int maxIter = 500;
            double eps = 1e-9;

            for (int iter = 0; iter < maxIter; iter++)
            {
                var B = GetColumns(A, basis);
                double[,] Binv;
                try { Binv = Inverse(B); }
                catch (Exception ex)
                {
                    logs.Add($"Unable to invert B at iteration {iter + 1}: {ex.Message}");
                    break;
                }

                var xB = MatVec(Binv, b);
                var cB = basis.Select(j => cAlg[j]).ToArray();
                var yT = VecMat(cB, Binv); // y^T = cB^T * Binv

                // reduced costs for non-basic
                var nonBasic = Enumerable.Range(0, n).Where(j => !basis.Contains(j)).ToList();
                var redCosts = new Dictionary<int, double>();
                foreach (var j in nonBasic)
                {
                    var Aj = GetColumn(A, j);
                    double yAj = Dot(yT, Aj);
                    double rc = cAlg[j] - yAj; // c_j - y^T A_j
                    redCosts[j] = rc;
                }

                // Logging
                var sb = new StringBuilder();
                sb.AppendLine($"=== Iteration {iter + 1} ===");
                sb.AppendLine("Basis (row -> col -> name -> Cb):");
                for (int i = 0; i < basis.Count; i++) sb.AppendLine($"  row {i}: col {basis[i]} -> {varNames[basis[i]]}, Cb={cAlg[basis[i]].ToString("G6", CultureInfo.InvariantCulture)}");
                sb.AppendLine(FormatMatrix(Binv, "B^-1:"));
                sb.AppendLine(FormatVector(xB, "x_B (basic solution):"));
                sb.AppendLine("Reduced costs (non-basic):");
                foreach (var kv in redCosts.OrderBy(k => k.Key)) sb.AppendLine($"  {varNames[kv.Key]} (j={kv.Key}): cbar={kv.Value.ToString("G6", CultureInfo.InvariantCulture)}");
                logs.Add(sb.ToString());

                // Choose entering variable (maximization): choose j with rc > eps
                int entering = -1;
                double bestRC = eps;
                foreach (var kv in redCosts)
                {
                    if (kv.Value > bestRC)
                    {
                        bestRC = kv.Value;
                        entering = kv.Key;
                    }
                }

                if (entering == -1)
                {
                    // optimal
                    var fullX = new double[n];
                    for (int i = 0; i < m; i++) fullX[basis[i]] = xB[i];
                    double z = 0.0;
                    for (int j = 0; j < n; j++) z += origC[j] * fullX[j];

                    var sb2 = new StringBuilder();
                    sb2.AppendLine("**Optimal solution found (Phase II)**");
                    sb2.AppendLine("Basic variables:");
                    for (int i = 0; i < m; i++) sb2.AppendLine($"  {varNames[basis[i]]} = {xB[i].ToString("G6", CultureInfo.InvariantCulture)}");
                    sb2.AppendLine("Non-basic variables = 0");
                    sb2.AppendLine($"Objective (original) = {z.ToString("G6", CultureInfo.InvariantCulture)}");
                    logs.Add(sb2.ToString());
                    break;
                }

                // Compute direction d = B^-1 * A_entering
                var Aent = GetColumn(A, entering);
                var d = MatVec(Binv, Aent);

                // Ratio test
                double theta = double.PositiveInfinity;
                int leavingRow = -1;
                var rlog = new StringBuilder();
                rlog.AppendLine($"Entering variable: {varNames[entering]} (j={entering}, cbar={redCosts[entering].ToString("G6", CultureInfo.InvariantCulture)})");
                rlog.AppendLine("Ratio test (only d_i > 0):");
                for (int i = 0; i < m; i++)
                {
                    if (d[i] > eps)
                    {
                        double t = xB[i] / d[i];
                        rlog.AppendLine($"  row {i}: theta = {xB[i].ToString("G6", CultureInfo.InvariantCulture)} / {d[i].ToString("G6", CultureInfo.InvariantCulture)} = {t.ToString("G6", CultureInfo.InvariantCulture)}");
                        if (t < theta - 1e-12)
                        {
                            theta = t; leavingRow = i;
                        }
                    }
                    else
                    {
                        rlog.AppendLine($"  row {i}: d[{i}] <= 0 -> ignore");
                    }
                }

                if (double.IsPositiveInfinity(theta))
                {
                    rlog.AppendLine("Problem is unbounded in the entering direction (no positive d_i).");
                    logs.Add(rlog.ToString());
                    break;
                }

                rlog.AppendLine($"Leaving variable: row {leavingRow} -> {varNames[basis[leavingRow]]}");
                rlog.AppendLine($"Pivot: {varNames[entering]} enters, {varNames[basis[leavingRow]]} leaves\n");
                logs.Add(rlog.ToString());

                // Update basis (replace leavingRow's column index with entering)
                basis[leavingRow] = entering;
            }

            return logs;
        }

        // === Solve (print tableau + matrices + Phase II iterations) ===
        public List<string> Solve(LPModel model)
        {
            var tableau = ToCanonical(model, out var colHeads, out var rowHeads);
            var output = new List<string>();
            output.Add("[Revised Simplex] Canonical form created.");
            output.Add(BuildTable(tableau, rowHeads, colHeads));

            // Print A, b, c for transparency
            {
                int m = tableau.Count - 1;
                int n = colHeads.Count - 1;
                var sb = new StringBuilder();
                sb.AppendLine("Matrix A:");
                for (int i = 0; i < m; i++)
                {
                    var row = new string[n];
                    for (int j = 0; j < n; j++) row[j] = tableau[i + 1][j].ToString("G6", CultureInfo.InvariantCulture);
                    sb.AppendLine(string.Join("\t", row));
                }
                sb.AppendLine("Vector b:");
                sb.AppendLine(string.Join("\t", Enumerable.Range(0, m).Select(i => tableau[i + 1][n].ToString("G6", CultureInfo.InvariantCulture))));
                output.Add(sb.ToString());
            }

            // Run Phase II
            output.AddRange(RevisedSimplexPhaseII(model, tableau, colHeads));
            return output;
        }
    }
}
