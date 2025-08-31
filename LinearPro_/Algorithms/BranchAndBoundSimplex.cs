using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using LinearPro_.Core;          // TableRenderer
using LinearPro_.Model;
using ModelConstraint = LinearPro_.Model.Constraint;

namespace LinearPro_.Algorithms
{
    internal sealed class BranchAndBoundSimplex : IAlgorithm
    {

        public string Name => "Branch & Bound (simplex-based)";
        private const double EPS = 1e-9;
        private const double FRAC_EPS = 1e-6;
        private readonly List<string> _steps = new List<string>();

        // =============================================================================================================== Entry
        public List<string> Solve(LPModel model)
        {
            _steps.Clear();
            _steps.Add("[B&B] Starting.");

            // Build standardized root model (<= rows only; sign restrictions handled like CuttingPlane)
            if (!TryBuildStandardModel(model, out StdModel stdRoot, out string err))
            {
                _steps.Add("[Validate] " + err);
                WriteOutputFile(model, null, "Validation/standardization failed: " + err, null);
                return new List<string>(_steps);
            }

            // Root node: LP relaxation
            var root = MakeNode(stdRoot, parent: null, branchHeader: "", label: "p1");
            if (!SolveLP(ref root))
            {
                _steps.Add("[B&B] Root relaxation infeasible/unbounded.");
                WriteOutputFile(model, root.Tableau, "Root infeasible/unbounded.", root.Std, null);
                return new List<string>(_steps);
            }

            // Incumbent (best integer solution)
            double bestZ = double.NegativeInfinity;
            Dictionary<string, double> bestSol = null;

            // Active set (best-first search by LP bound since we maximize)
            var active = new List<Node> { root };

            int expanded = 0;
            const int NODE_LIMIT = 2000;

            while (active.Count > 0)
            {
                active.Sort((a, b) => b.LPBound.CompareTo(a.LPBound));
                var node = active[0];
                active.RemoveAt(0);
                expanded++;

                _steps.Add($"[Expand] {node.Label}  Bound Z={node.LPBound:0.######}  {node.BranchHeader}");

                // Bound prune
                if (node.LPBound <= bestZ + 1e-9)
                {
                    _steps.Add($"[Prune] {node.Label} by bound (LPBound {node.LPBound:0.######} <= incumbent {bestZ:0.######}).");
                    continue;
                }

                // Integrality check
                var fracs = FindFractionalIntegers(node.Std, node.OrigSolution);
                if (fracs.Count == 0)
                {
                    if (node.LPBound > bestZ + 1e-9)
                    {
                        bestZ = node.LPBound;
                        bestSol = new Dictionary<string, double>(node.OrigSolution, StringComparer.OrdinalIgnoreCase);
                        _steps.Add($"[Incumbent] {node.Label}  Z={bestZ:0.######}");
                    }
                    continue;
                }

                // Choose var to branch on (closest to .5)
                var choice = fracs.OrderBy(f => Math.Abs((f.value - Math.Floor(f.value)) - 0.5)).First();
                int k = choice.idx;          // original index (0-based)
                double v = choice.value;

                // SAFE split: always progresses for fractional v
                int floorV = (int)Math.Floor(v);
                int ceilV = floorV + 1;

                // Left child: Xk <= floorV
                var left = MakeChild(node, k, floorV, isUpper: true, childIndex: 1);
                if (left != null && SolveLP(ref left))
                {
                    if (left.LPBound > bestZ + 1e-9) active.Add(left);
                    else _steps.Add($"[Prune] {left.Label} by bound (Z={left.LPBound:0.######}).");
                }
                else
                {
                    _steps.Add($"[Prune] {node.Label}.1 infeasible or duplicate.");
                }

                // Right child: Xk >= ceilV  ->  -Xk <= -ceilV
                var right = MakeChild(node, k, ceilV, isUpper: false, childIndex: 2);
                if (right != null && SolveLP(ref right))
                {
                    if (right.LPBound > bestZ + 1e-9) active.Add(right);
                    else _steps.Add($"[Prune] {right.Label} by bound (Z={right.LPBound:0.######}).");
                }
                else
                {
                    _steps.Add($"[Prune] {node.Label}.2 infeasible or duplicate.");
                }

                if (expanded >= NODE_LIMIT)
                {
                    _steps.Add("[B&B] Node limit reached. Stopping.");
                    break;
                }
            }

            // Wrap-up
            if (double.IsNegativeInfinity(bestZ))
            {
                WriteOutputFile(model, null, "No integer feasible solution found.", stdRoot, null);
            }
            else
            {
                WriteOutputFile(model, null, "Best integer solution found.", stdRoot, bestZ, bestSol);
            }

            return new List<string>(_steps);
        }

        // =============================================================================================================== Node structure
        private sealed class Node
        {
            public StdModel Std;
            public double[,] Tableau;
            public List<int> Basis;
            public List<string> ColNames;
            public string Label;             // p1, p1.1, p1.2, ...
            public string BranchHeader;      // "X2 <= 2" / "X2 >= 3"
            public double LPBound;           // relaxation objective
            public Dictionary<string, double> OrigSolution;
            public Node Parent;
        }

        private Node MakeNode(StdModel std, Node parent, string branchHeader, string label)
        {
            var T = BuildInitialTableau(std, out List<int> basis, out List<string> colNames);
            return new Node
            {
                Std = std,
                Tableau = T,
                Basis = basis,
                ColNames = colNames,
                Label = label,
                BranchHeader = string.IsNullOrWhiteSpace(branchHeader) ? "" : $"[{branchHeader}]",
                Parent = parent
            };
        }

        // duplicate-row detection helpers
        private static bool NearlyEqual(double a, double b, double eps = 1e-9) => Math.Abs(a - b) <= eps;

        private bool HasDuplicateLeRow(StdModel std, double[] row, double rhs, double eps = 1e-9)
        {
            int m = std.A.GetLength(0), n = std.A.GetLength(1);
            for (int i = 0; i < m; i++)
            {
                if (!NearlyEqual(std.b[i], rhs, eps)) continue;
                bool same = true;
                for (int j = 0; j < n; j++)
                {
                    if (!NearlyEqual(std.A[i, j], row[j], eps)) { same = false; break; }
                }
                if (same) return true;
            }
            return false;
        }

        private Node MakeChild(Node parent, int origIdx, int value, bool isUpper, int childIndex)
        {
            // Build a <= row in std-space for:
            //   isUpper=true:   Xk <= value
            //   isUpper=false:  -Xk <= -value  (i.e., Xk >= value)
            var row = BuildStdRowForOriginal(parent.Std, origIdx, coefForX: isUpper ? +1.0 : -1.0);
            double rhs = isUpper ? value : -value;

            // Avoid generating identical child (prevents loops)
            if (HasDuplicateLeRow(parent.Std, row, rhs))
            {
                _steps.Add($"[Skip] {parent.Label}.{childIndex} duplicate constraint "
                         + (isUpper ? $"X{origIdx + 1} <= {value}" : $"X{origIdx + 1} >= {value}"));
                return null;
            }

            var stdChild = AppendLeRow(parent.Std, row, rhs);
            var label = parent.Label + "." + childIndex;
            var desc = isUpper ? $"X{origIdx + 1} <= {value}" : $"X{origIdx + 1} >= {value}";

            return MakeNode(stdChild, parent, desc, label);
        }

        private bool SolveLP(ref Node node)
        {
            // Try primal; if it fails (infeasible/unbounded), try dual then primal.
            RenderSnapshot(node.Tableau, $"Node {node.Label} (start) {node.BranchHeader}", node.ColNames);

            if (!PrimalSimplex(node.Tableau, node.Basis, node.ColNames))
            {
                _steps.Add($"[Simplex] Primal failed at {node.Label}, trying Dual then Primal.");
                if (!DualSimplex(node.Tableau, node.Basis, node.ColNames))
                    return false;
                if (!PrimalSimplex(node.Tableau, node.Basis, node.ColNames))
                    return false;
            }

            node.LPBound = GetZ(node.Tableau);
            var stdSol = ExtractStdSolution(node.Tableau, node.Basis);
            node.OrigSolution = node.Std.MapBackToOriginal(stdSol);

            RenderSnapshot(node.Tableau, $"Node {node.Label} (optimal LP)  Z={node.LPBound:0.######}", node.ColNames);
            foreach (var kv in node.OrigSolution.OrderBy(k => k.Key))
                _steps.Add($"[Sol {node.Label}] {kv.Key} = {kv.Value:0.######}");

            return true;
        }

        // =============================================================================================================== Standardization (same policy as CuttingPlane)
        private bool TryBuildStandardModel(LPModel model, out StdModel std, out string error)
        {
            std = null;
            error = null;

            foreach (var c in model.Constraints)
            {
                if (c.Relation != Relation.LE)
                {
                    error = "Only <= constraints supported in this implementation.";
                    return false;
                }
                if (c.Rhs < -EPS)
                {
                    error = "Negative RHS encountered; pre-processing not implemented.";
                    return false;
                }
            }

            int nOrig = model.ObjectiveCoefficients.Length;
            int mOrig = model.Constraints.Count;

            var signs = (model.SignRestrictions != null && model.SignRestrictions.Count >= nOrig)
                ? model.SignRestrictions
                : Enumerable.Repeat(SignRestriction.NonNegative, nOrig).ToList();

            for (int i = 0; i < nOrig; i++)
            {
                var s = signs[i];
                if (s == SignRestriction.Binary && s == SignRestriction.Unrestricted)
                {
                    error = "Binary with URS is unsupported.";
                    return false;
                }
                if (s == SignRestriction.Binary && s == SignRestriction.NonPositive)
                {
                    error = "Binary with <= 0 is unsupported.";
                    return false;
                }
            }

            // Original A,b
            var rows = new List<double[]>();
            var b = new List<double>();
            foreach (var c in model.Constraints) { rows.Add(c.Coefficients.ToArray()); b.Add(c.Rhs); }
            var Aorig = rows;
            var borig = b;

            // Build std-columns based on sign restrictions
            var stdCols = new List<StdCol>();
            for (int j = 0; j < nOrig; j++)
            {
                int jj = j;
                var s = signs[jj];
                string baseName = "X" + (jj + 1);
                bool isInt = (s == SignRestriction.Integer || s == SignRestriction.Binary);
                bool isBin = (s == SignRestriction.Binary);

                if (s == SignRestriction.NonNegative || s == SignRestriction.Integer || s == SignRestriction.Binary)
                {
                    stdCols.Add(new StdCol
                    {
                        Name = baseName,
                        CoeffSelector = (col => col[jj]),
                        ObjCoefSelector = (obj => obj[jj]),
                        Scale = 1.0,
                        IsInteger = isInt,
                        IsBinary = isBin,
                        OrigIndex = jj,
                        Part = StdPart.Plus
                    });
                }
                else if (s == SignRestriction.NonPositive)
                {
                    stdCols.Add(new StdCol
                    {
                        Name = baseName + "~",
                        CoeffSelector = (col => -col[jj]),
                        ObjCoefSelector = (obj => -obj[jj]),
                        Scale = 1.0,
                        IsInteger = isInt,
                        IsBinary = false,
                        OrigIndex = jj,
                        Part = StdPart.Flipped
                    });
                }
                else if (s == SignRestriction.Unrestricted)
                {
                    stdCols.Add(new StdCol
                    {
                        Name = baseName + "+",
                        CoeffSelector = (col => col[jj]),
                        ObjCoefSelector = (obj => obj[jj]),
                        Scale = 1.0,
                        IsInteger = isInt,
                        IsBinary = false,
                        OrigIndex = jj,
                        Part = StdPart.Plus
                    });
                    stdCols.Add(new StdCol
                    {
                        Name = baseName + "-",
                        CoeffSelector = (col => -col[jj]),
                        ObjCoefSelector = (obj => -obj[jj]),
                        Scale = 1.0,
                        IsInteger = isInt,
                        IsBinary = false,
                        OrigIndex = jj,
                        Part = StdPart.Minus
                    });
                }
                else
                {
                    error = "Unsupported sign restriction encountered.";
                    return false;
                }
            }

            // Assemble std A and c
            int nStd = stdCols.Count;
            int mStd = mOrig;
            var A = new double[mStd, nStd];
            var cvec = new double[nStd];

            for (int i = 0; i < mStd; i++)
                for (int k = 0; k < nStd; k++)
                    A[i, k] = stdCols[k].CoeffSelector(Aorig[i]);

            for (int k = 0; k < nStd; k++)
                cvec[k] = stdCols[k].ObjCoefSelector(model.ObjectiveCoefficients);

            // Add x <= 1 rows for binary std-cols
            var ubRows = new List<double[]>();
            var ubB = new List<double>();
            for (int k = 0; k < nStd; k++)
            {
                if (stdCols[k].IsBinary)
                {
                    var row = new double[nStd];
                    row[k] = 1.0;
                    ubRows.Add(row);
                    ubB.Add(1.0);
                }
            }

            // Merge rows
            int totalRows = mStd + ubRows.Count;
            var Aall = new double[totalRows, nStd];
            var ball = new double[totalRows];

            for (int i = 0; i < mStd; i++)
            {
                for (int k = 0; k < nStd; k++) Aall[i, k] = A[i, k];
                ball[i] = borig[i];
            }
            for (int r = 0; r < ubRows.Count; r++)
            {
                int i = mStd + r;
                for (int k = 0; k < nStd; k++) Aall[i, k] = ubRows[r][k];
                ball[i] = ubB[r];
            }

            std = new StdModel
            {
                A = Aall,
                b = ball,
                c = cvec,
                StdCols = stdCols,
                ColNames = stdCols.Select(s => s.Name).ToList(),
                OrigCount = nOrig
            };

            return true;
        }

        // =============================================================================================================== Build initial tableau (<= rows + slacks)
        private double[,] BuildInitialTableau(StdModel std, out List<int> basis, out List<string> colNames)
        {
            int m = std.A.GetLength(0);
            int n = std.A.GetLength(1);

            int cols = n + m + 1;  // std vars + slacks + RHS
            int rows = m + 1;      // constraints + objective

            var T = new double[rows, cols];
            colNames = new List<string>(std.ColNames);
            for (int j = 0; j < m; j++) colNames.Add("S" + (j + 1));

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) T[i, j] = std.A[i, j];
                T[i, n + i] = 1.0;               // slack
                T[i, cols - 1] = std.b[i];       // RHS
            }

            int obj = rows - 1;
            for (int j = 0; j < n; j++) T[obj, j] = -std.c[j];

            basis = new List<int>();
            for (int i = 0; i < m; i++) basis.Add(n + i);

            return T;
        }

        // =============================================================================================================== Std mutations for branching
        private double[] BuildStdRowForOriginal(StdModel std, int origIdx, double coefForX)
        {
            int nStd = std.StdCols.Count;
            var row = new double[nStd];
            for (int j = 0; j < nStd; j++)
            {
                var sc = std.StdCols[j];
                if (sc.OrigIndex != origIdx) continue;

                if (sc.Part == StdPart.Plus) row[j] += coefForX;
                else if (sc.Part == StdPart.Minus) row[j] -= coefForX;   // X- represents -x
                else if (sc.Part == StdPart.Flipped) row[j] -= coefForX; // y=-x >= 0
            }
            return row;
        }

        private StdModel AppendLeRow(StdModel baseStd, double[] row, double rhs)
        {
            int m = baseStd.A.GetLength(0);
            int n = baseStd.A.GetLength(1);

            var A2 = new double[m + 1, n];
            var b2 = new double[m + 1];

            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) A2[i, j] = baseStd.A[i, j];
                b2[i] = baseStd.b[i];
            }
            for (int j = 0; j < n; j++) A2[m, j] = row[j];
            b2[m] = rhs;

            return new StdModel
            {
                A = A2,
                b = b2,
                c = baseStd.c.ToArray(),
                StdCols = new List<StdCol>(baseStd.StdCols),
                ColNames = new List<string>(baseStd.ColNames),
                OrigCount = baseStd.OrigCount
            };
        }

        // =============================================================================================================== Rendering
        private void RenderSnapshot(double[,] T, string caption, List<string> colNames)
        {
            Console.WriteLine();
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("=== " + caption + " ===");
            Console.ResetColor();

            double z = GetZ(T);
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Z = " + z.ToString("0.######", CultureInfo.InvariantCulture));
            Console.ResetColor();

            int m = T.GetLength(0) - 1;
            int n = T.GetLength(1) - 1;

            var constraints = new List<ModelConstraint>();
            for (int i = 0; i < m; i++)
            {
                var coeffs = new double[n];
                for (int j = 0; j < n; j++) coeffs[j] = T[i, j];
                constraints.Add(new ModelConstraint(coeffs, Relation.EQ, T[i, n]));
            }

            var objCoeffs = new double[n];
            for (int j = 0; j < n; j++) objCoeffs[j] = -T[m, j];

            var pseudo = new LPModel(true, objCoeffs, constraints, new List<SignRestriction>(), new List<string>(colNames));
            TableRenderer.RenderModelAsTable(pseudo);
        }

        private static double GetZ(double[,] T)
        {
            int obj = T.GetLength(0) - 1;
            int rhs = T.GetLength(1) - 1;
            return T[obj, rhs];
        }

        private void WriteOutputFile(LPModel originalModel, double[,] T, string note, StdModel std, double? zOpt = null, Dictionary<string, double> origSol = null)
        {
            try
            {
                var path = Path.Combine(Environment.CurrentDirectory, "BranchAndBound_Result_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                using (var sw = new StreamWriter(path))
                {
                    sw.WriteLine("LinearPro_ – Branch & Bound Result");
                    sw.WriteLine("Timestamp: " + DateTime.Now);
                    sw.WriteLine();

                    if (zOpt.HasValue)
                        sw.WriteLine("Best integer Z: " + zOpt.Value.ToString("0.######", CultureInfo.InvariantCulture));

                    if (origSol != null && origSol.Count > 0)
                    {
                        sw.WriteLine();
                        sw.WriteLine("Incumbent solution (original variables):");
                        foreach (var kv in origSol.OrderBy(k => k.Key))
                            sw.WriteLine($"  {kv.Key} = {kv.Value:0.######}");
                    }

                    sw.WriteLine();
                    if (!string.IsNullOrWhiteSpace(note))
                        sw.WriteLine("Note: " + note);

                    sw.WriteLine();
                    sw.WriteLine("Steps:");
                    foreach (var s in _steps) sw.WriteLine("  " + s);
                }
                _steps.Add("[Output] Saved: " + path);
            }
            catch (Exception ex)
            {
                _steps.Add("[Output] Failed to write output: " + ex.Message);
            }
        }

        // =============================================================================================================== Simplex (same as in CuttingPlane)
        private bool PrimalSimplex(double[,] T, List<int> basis, List<string> colNames)
        {
            int m = T.GetLength(0) - 1;
            int n = T.GetLength(1) - 1;
            int obj = m;

            for (int it = 0; it < 2000; it++)
            {
                int enter = -1;
                double mostNeg = -EPS;
                for (int j = 0; j < n; j++)
                {
                    double cj = T[obj, j];
                    if (cj < mostNeg) { mostNeg = cj; enter = j; }
                }
                if (enter == -1) return true; // optimal

                int leave = -1;
                double best = double.PositiveInfinity;
                for (int i = 0; i < m; i++)
                {
                    double a = T[i, enter];
                    if (a > EPS)
                    {
                        double ratio = T[i, n] / a;
                        if (ratio < best - 1e-12) { best = ratio; leave = i; }
                    }
                }
                if (leave == -1) return false; // unbounded

                Pivot(T, leave, enter);
                basis[leave] = enter;
                RenderSnapshot(T, $"Primal Pivot: Column: {colNames[enter]}, Row: {leave + 1}", colNames);
            }
            _steps.Add("[PrimalSimplex] Iteration cap reached.");
            return false;
        }

        private bool DualSimplex(double[,] T, List<int> basis, List<string> colNames)
        {
            int m = T.GetLength(0) - 1;
            int n = T.GetLength(1) - 1;
            int objRow = m;

            for (int iter = 0; iter < 2000; iter++)
            {
                int leave = -1;
                double mostNegRhs = -EPS;
                for (int i = 0; i < m; i++)
                {
                    double rhs = T[i, n];
                    if (rhs < mostNegRhs)
                    {
                        mostNegRhs = rhs;
                        leave = i;
                    }
                }

                if (leave == -1)
                {
                    _steps.Add("[DualSimplex] Feasible (no negative RHS).");
                    return true;
                }

                int enter = -1;
                double bestRatio = double.PositiveInfinity;

                for (int j = 0; j < n; j++)
                {
                    double aij = T[leave, j];
                    if (aij < -EPS)
                    {
                        double reducedCost = T[objRow, j];
                        double ratio = reducedCost / aij; // aij < 0
                        if (ratio < bestRatio - 1e-12)
                        {
                            bestRatio = ratio;
                            enter = j;
                        }
                    }
                }

                if (enter == -1)
                {
                    _steps.Add(string.Format(CultureInfo.InvariantCulture,
                        "[DualSimplex] No entering column found for leave row {0} (infeasible). RHS={1:0.######}.", leave + 1, T[leave, n]));
                    return false;
                }

                Pivot(T, leave, enter);
                basis[leave] = enter;

                _steps.Add(string.Format(CultureInfo.InvariantCulture,
                    "[DualSimplex] Pivot r{0}, c{1} -> enter {2}, leave {3}.", leave + 1, enter + 1, colNames[enter], leave + 1));
                RenderSnapshot(T, string.Format("Dual Pivot (enter {0}, leave row {1})", colNames[enter], leave + 1), colNames);
            }

            _steps.Add("[DualSimplex] Iteration cap reached.");
            return false;
        }

        private void Pivot(double[,] T, int r, int c)
        {
            int R = T.GetLength(0), C = T.GetLength(1);
            double p = T[r, c];
            if (Math.Abs(p) < EPS) p = (p < 0 ? -EPS : EPS);

            for (int j = 0; j < C; j++) T[r, j] /= p;

            for (int i = 0; i < R; i++)
            {
                if (i == r) continue;
                double f = T[i, c];
                if (Math.Abs(f) < EPS) continue;
                for (int j = 0; j < C; j++) T[i, j] -= f * T[r, j];
            }
        }

        // =============================================================================================================== Helpers shared with CuttingPlane
        private double[] ExtractStdSolution(double[,] T, List<int> basis)
        {
            int m = T.GetLength(0) - 1;
            int n = T.GetLength(1) - 1;
            var x = new double[n];
            for (int i = 0; i < m; i++)
            {
                int j = basis[i];
                if (j >= 0 && j < n) x[j] = T[i, n];
            }
            return x;
        }

        private List<(int idx, double value)> FindFractionalIntegers(StdModel std, Dictionary<string, double> origSol)
        {
            var fracs = new List<(int idx, double value)>();
            for (int j = 0; j < std.OrigCount; j++)
            {
                bool wasInt = std.WasInteger(j);
                bool wasBin = std.WasBinary(j);
                if (wasInt || wasBin)
                {
                    string name = "X" + (j + 1);
                    double v = origSol.ContainsKey(name) ? origSol[name] : 0.0;

                    if (wasBin)
                    {
                        if (Math.Abs(v) > FRAC_EPS && Math.Abs(v - 1.0) > FRAC_EPS)
                            fracs.Add((j, v));
                    }
                    else
                    {
                        if (Math.Abs(v - Math.Round(v)) > FRAC_EPS)
                            fracs.Add((j, v));
                    }
                }
            }
            return fracs;
        }

        // =============================================================================================================== Std model container & mapping
        private sealed class StdModel
        {
            public double[,] A;
            public double[] b;
            public double[] c;
            public List<StdCol> StdCols;
            public List<string> ColNames;
            public int OrigCount;

            public bool WasInteger(int origIndex) => StdCols.Any(s => s.OrigIndex == origIndex && s.IsInteger);
            public bool WasBinary(int origIndex) => StdCols.Any(s => s.OrigIndex == origIndex && s.IsBinary);

            public Dictionary<string, double> MapBackToOriginal(double[] xStd)
            {
                var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < OrigCount; j++)
                {
                    string name = "X" + (j + 1);
                    double sum = 0.0;
                    foreach (var sc in StdCols.Where(s => s.OrigIndex == j))
                    {
                        int idx = StdCols.IndexOf(sc);
                        if (idx >= 0 && idx < xStd.Length)
                            sum += (sc.Part == StdPart.Minus || sc.Part == StdPart.Flipped) ? -xStd[idx] : xStd[idx];
                    }
                    result[name] = sum;
                }
                return result;
            }
        }

        private sealed class StdCol
        {
            public string Name;
            public Func<double[], double> CoeffSelector;
            public Func<double[], double> ObjCoefSelector;
            public double Scale;
            public bool IsInteger;
            public bool IsBinary;
            public int OrigIndex;
            public StdPart Part;
        }

        private enum StdPart { Plus, Minus, Flipped }
    }
}
