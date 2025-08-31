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


 
    internal sealed class CuttingPlane : IAlgorithm
    {

        public string Name => "Cutting Plane (Gomory, simplex-based)";
        private const double EPS = 1e-9;
        private readonly List<string> _steps = new List<string>();

        // ================================================================================================================================================================================================== Entry point ===
        public List<string> Solve(LPModel model)
        {
            _steps.Clear();
            _steps.Add("[CuttingPlane] Starting.");

            //Build standardized model with sign-restriction handling and binary upper bounds
            if (!TryBuildStandardModel(model, out StdModel std, out string err))
            {
                _steps.Add("[Validate] " + err);
                WriteOutputFile(model, null, "Validation/standardization failed: " + err, null);
                return new List<string>(_steps);
            }

            //Build initial tableau (<= rows only, slack basis, flips >= to <= and *-1 all variables)
            var T = BuildInitialTableau(std, out List<int> basis, out List<string> colNames);
            RenderSnapshot(T, "Initial Tableau", colNames);

            //Optimize LP relaxation with primal simplex
            if (!PrimalSimplex(T, basis, colNames))
            {
                var z = GetZ(T);
                _steps.Add("[PrimalSimplex] Failed (unbounded or numerical).");
                WriteOutputFile(model, T, "Primal simplex failed.", std, z);
                return new List<string>(_steps);
            }
            RenderSnapshot(T, "After Initial Primal Simplex", colNames);

            //Cutting-plane loop
            int cutCount = 0;
            const int CUT_LIMIT = 50;

            while (true)
            {
                // Extract solution
                var stdSol = ExtractStdSolution(T, basis);
                var origSol = std.MapBackToOriginal(stdSol);
                var z = GetZ(T);

                // Always render the current tableau, even if no cut will be added
                RenderSnapshot(T, "Current Tableau (Iteration " + (cutCount + 1) + ")", colNames);

                // Check integrality for all integer/binary vars
                var fracs = FindFractionalIntegers(std, origSol);
                if (fracs.Count == 0)
                {
                    _steps.Add("[CuttingPlane] All integer/binary variables integral. Stopping.");
                    WriteOutputFile(model, T, "Optimal integer solution found.", std, z, origSol);
                    return new List<string>(_steps);
                }

                // Log which are fractional
                foreach (var (idx, v) in fracs)
                    _steps.Add($"[Fractional] Variable X{idx + 1} = {v:0.######}");

                // Pick the “worst” fractional variable to cut on (closest to 0.5)
                var target = fracs.OrderBy(f => Math.Abs((f.value - Math.Floor(f.value)) - 0.5)).First();


                if (++cutCount > CUT_LIMIT)
                {
                    _steps.Add("[CuttingPlane] Cut limit reached. Stopping.");
                    WriteOutputFile(model, T, "Stopped after reaching cut limit.", std, z, origSol);
                    return new List<string>(_steps);
                }

                // Try to select a basic row for Gomory cut
                if (!TryPickGomoryRow(std, T, basis, out int sourceRow, out int basicStdIdx, out double basicVal))
                {
                    _steps.Add("[CuttingPlane] No suitable basic row for cut, but fractional variable remains.");
                    WriteOutputFile(model, T, "No suitable cut row; fractional variable remains.", std, z, origSol);
                    return new List<string>(_steps);
                }

                // Add Gomory cut
                AddGomoryFractionalCut(ref T, sourceRow, ref colNames, basis);
                RenderSnapshot(T, $"After Cut #{cutCount}", colNames);

                // Restore feasibility (dual simplex)
                if (!DualSimplex(T, basis, colNames))
                {
                    _steps.Add("[CuttingPlane] Dual simplex failed after cut.");
                    WriteOutputFile(model, T, "Dual simplex failed after cut.", std, z, origSol);
                    return new List<string>(_steps);
                }

                // Re-optimize (primal simplex)
                if (!PrimalSimplex(T, basis, colNames))
                {
                    _steps.Add("[CuttingPlane] Primal simplex failed after cut.");
                    WriteOutputFile(model, T, "Primal simplex failed after cut.", std, z, origSol);
                    return new List<string>(_steps);
                }
            }

        }

        // ================================================================================================================================================================================= Validation & Standardization ===

        private bool TryBuildStandardModel(LPModel model, out StdModel std, out string error)
        {
            std = null;
            error = null;

            // Only <= constraints are supported here to keep a pure slack-start tableau
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

            // Default any missing sign restrictions to NonNegative
            var signs = (model.SignRestrictions != null && model.SignRestrictions.Count >= nOrig)
                ? model.SignRestrictions
                : Enumerable.Repeat(SignRestriction.NonNegative, nOrig).ToList();

            // Reject impossible combos cleanly
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

            // Build A, b, c in std-space (+ var names, int flags, binary flags, mapping)
            var rows = new List<double[]>();
            var b = new List<double>();
            // Start with original constraints
            foreach (var c in model.Constraints) { rows.Add(c.Coefficients.ToArray()); b.Add(c.Rhs); }
            var Aorig = rows; // mOrig x nOrig
            var borig = b;    // mOrig

            var stdCols = new List<StdCol>(); // columns we will build
            // For each original variable, expand per sign restriction
            for (int j = 0; j < nOrig; j++)
            {
                int jj = j;  // <<< important: capture per-iteration copy

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
                    // y = -x >= 0
                    stdCols.Add(new StdCol
                    {
                        Name = baseName + "~",
                        CoeffSelector = (col => -col[jj]),
                        ObjCoefSelector = (obj => -obj[jj]),
                        Scale = 1.0,
                        IsInteger = isInt,
                        IsBinary = false,
                        OrigIndex = jj,
                        Part = StdPart.Flipped   // keep as Flipped; see mapping fix below
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


            // Assemble std-space c and A
            int nStd = stdCols.Count;
            int mStd = mOrig;
            var A = new double[mStd, nStd];
            var cvec = new double[nStd];

            for (int i = 0; i < mStd; i++)
            {
                for (int k = 0; k < nStd; k++)
                {
                    A[i, k] = stdCols[k].CoeffSelector(Aorig[i]);
                }
            }
            for (int k = 0; k < nStd; k++)
            {
                cvec[k] = stdCols[k].ObjCoefSelector(model.ObjectiveCoefficients);
            }

            // Add x <= 1 rows for Binary std-columns (enforce 0/1 bounds)
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

            // Merge original rows and UB rows
            int totalRows = mStd + ubRows.Count;
            var Aall = new double[totalRows, nStd];
            var ball = new double[totalRows];

            // copy original
            for (int i = 0; i < mStd; i++)
            {
                for (int k = 0; k < nStd; k++) Aall[i, k] = A[i, k];
                ball[i] = borig[i];
            }
            // copy UBs
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

        // ===================================================================================================================================================================== Build Initial Tableau (<= rows + slacks) ===
        private double[,] BuildInitialTableau(StdModel std, out List<int> basis, out List<string> colNames)
        {
            int m = std.A.GetLength(0);
            int n = std.A.GetLength(1);

            // columns: [std vars] + [slacks m] + RHS
            int cols = n + m + 1;
            int rows = m + 1;

            var T = new double[rows, cols];
            colNames = new List<string>(std.ColNames);
            // add slacks S1..Sm
            for (int j = 0; j < m; j++) colNames.Add("S" + (j + 1));

            // Fill constraints
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++) T[i, j] = std.A[i, j];
                T[i, n + i] = 1.0;                   // slack
                T[i, cols - 1] = std.b[i];            // RHS
            }

            // Objective row: -c (maximize)
            int obj = rows - 1;
            for (int j = 0; j < n; j++) T[obj, j] = -std.c[j];

            // Basis: the slacks
            basis = new List<int>();
            for (int i = 0; i < m; i++) basis.Add(n + i);

            return T;
        }

        // ======================================================================================================================================================================================== Simplex (primal/dual) ===

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
            int m = T.GetLength(0) - 1; // number of constraint rows
            int n = T.GetLength(1) - 1; // number of variable cols
            int objRow = m;

            for (int iter = 0; iter < 2000; iter++)
            {
                //Find the most negative RHS (leaving row)
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

                //Find entering column: consider columns with a_ij < 0 & choose j that minimizes ratio = reducedCost_j / a_ij  (a_ij < 0)
                int enter = -1;
                double bestRatio = double.PositiveInfinity;

                for (int j = 0; j < n; j++)
                {
                    double aij = T[leave, j];
                    if (aij < -EPS)
                    {
                        double reducedCost = T[objRow, j];
                        double ratio = reducedCost / aij; // aij < 0, ratio may be negative
                                                          // We select the minimal ratio
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

                //Pivot
                Pivot(T, leave, enter);
                basis[leave] = enter;

                _steps.Add(string.Format(CultureInfo.InvariantCulture,
                    "[DualSimplex] Pivot r{0}, c{1} -> enter {2}, leave {3}.", leave + 1, enter + 1, colNames[enter], leave + 1));

                //render snapshot at each pivot if desired:
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

            // Normalize pivot row
            for (int j = 0; j < C; j++) T[r, j] /= p;

            // Eliminate
            for (int i = 0; i < R; i++)
            {
                if (i == r) continue;
                double f = T[i, c];
                if (Math.Abs(f) < EPS) continue;
                for (int j = 0; j < C; j++) T[i, j] -= f * T[r, j];
            }
        }

        // ========================================================================================================================================================================================================= Cuts ===

        private bool TryPickGomoryRow(StdModel std, double[,] T, List<int> basis, out int row, out int basicStdIdx, out double val)
        {
            int m = T.GetLength(0) - 1;
            int n = T.GetLength(1) - 1;
            row = -1; basicStdIdx = -1; val = 0;

            // Identify which columns correspond to std decision variables (not slacks)
            int nStd = std.StdCols.Count;

            // Pick the most fractional basic integer column
            double bestDist = double.PositiveInfinity;
            for (int i = 0; i < m; i++)
            {
                int col = basis[i];
                if (col >= 0 && col < nStd)
                {
                    if (std.StdCols[col].IsInteger)
                    {
                        double rhs = T[i, n];
                        double f = rhs - Math.Floor(rhs + EPS);
                        if (f > EPS && f < 1 - EPS)
                        {
                            double d = Math.Abs(f - 0.5); // closest to 0.5
                            if (d < bestDist)
                            {
                                bestDist = d;
                                row = i;
                                basicStdIdx = col;
                                val = rhs;
                            }
                        }
                    }
                }
            }
            return row != -1;
        }

        private void AddGomoryFractionalCut(ref double[,] T, int sourceRow, ref List<string> colNames, List<int> basis)
        {
            int oldRows = T.GetLength(0);
            int oldCols = T.GetLength(1);

            int m = oldRows - 1; // number of constraint rows currently
            int n = oldCols - 1; // number of variable columns currently (excluding RHS)

          // New tableau with +1 row and +1 column (new slack), and RHS will be the last col
            int newRows = oldRows + 1;
            int newCols = oldCols + 1;

            var NT = new double[newRows, newCols];

            //layout:
            // columns: [0..n-1] old variable cols, [n] new slack col, [n+1] RHS
            // rows: [0..m-1] old constraint rows (copied), [m] new cut row, [m+1] objective row (copied)

            int newSlackCol = n;
            int newRhsCol = n + 1;
            int newCutRow = m;
            int newObjRow = m + 1;

            //Copy old constraint rows into NT, mapping RHS to newRhsCol
            for (int i = 0; i < m; i++)
            {
                for (int j = 0; j < n; j++)
                    NT[i, j] = T[i, j];
                NT[i, newRhsCol] = T[i, n]; // copy RHS
                                            // new slack col (NT[i, newSlackCol]) remains 0 by default
            }

            //Copy old objective row into newObjRow, mapping its RHS similarly
            for (int j = 0; j < n; j++)
                NT[newObjRow, j] = T[m, j];
            NT[newObjRow, newRhsCol] = T[m, n];

            //Build Gomory cut row from fractional parts of sourceRow (using old T)
            for (int j = 0; j < n; j++)
            {
                double a = T[sourceRow, j];
                NT[newCutRow, j] = -Frac(a); // cut coefficients = -fractional part
            }
            // new slack has coefficient +1 in the cut row
            NT[newCutRow, newSlackCol] = 1.0;
            // RHS = -fractional part of source RHS
            double bsource = T[sourceRow, n];
            NT[newCutRow, newRhsCol] = -Frac(bsource);

            //Install NT as the new tableau
            T = NT;

            //Update column names (insert new slack before RHS)
            string sname = "SC" + (colNames.Count(c => c.StartsWith("SC", StringComparison.OrdinalIgnoreCase)) + 1);
            colNames.Insert(newSlackCol, sname);

            //Add new slack as basis for the new cut row
            basis.Add(newSlackCol);

            _steps.Add(string.Format(CultureInfo.InvariantCulture, "[Cut] Added Gomory cut (row {0}), slack {1}, RHS {2:0.######}.",
                newCutRow + 1, sname, NT[newCutRow, newRhsCol]));
        }

        // ==================================================================================================================================================================================================== Rendering ===

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

            // Wrap tableau into a pseudo LPModel for TableRenderer
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

            // NB: TableRenderer always shows Z RHS as 0 by design.
            // Print the true Z above (as shown) to satisfy the requirement without touching TableRenderer.
            var pseudo = new LPModel(true, objCoeffs, constraints, new List<SignRestriction>(), new List<string>(colNames));
            TableRenderer.RenderModelAsTable(pseudo);
        }

        private static double GetZ(double[,] T)
        {
            int obj = T.GetLength(0) - 1;
            int rhs = T.GetLength(1) - 1;
            return T[obj, rhs];
        }

        // ======================================================================================================================================================================================================= Output ===

        private void WriteOutputFile(LPModel originalModel, double[,] T, string note, StdModel std, double? zOpt = null, Dictionary<string, double> origSol = null)
        {
            try
            {
                var path = Path.Combine(Environment.CurrentDirectory, "CuttingPlane_Result_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".txt");
                using (var sw = new StreamWriter(path))
                {
                    sw.WriteLine("LinearPro_ – Cutting Plane (Gomory) Result");
                    sw.WriteLine("Timestamp: " + DateTime.Now);
                    sw.WriteLine();

                    if (zOpt.HasValue)
                        sw.WriteLine("Optimal Z (from tableau RHS): " + zOpt.Value.ToString("0.######", CultureInfo.InvariantCulture));

                    if (origSol != null && origSol.Count > 0)
                    {
                        sw.WriteLine();
                        sw.WriteLine("Solution (original variables):");
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

        // ====================================================================================================================================================================================================== Helpers ===

        private static double Frac(double x)
        {
            double f = x - Math.Floor(x);
            if (f < 0) f += 1.0;
            // Normalize tiny values
            if (f < 1e-12) f = 0.0;
            if (1.0 - f < 1e-12) f = 0.0;
            return f;
        }

        private double[] ExtractStdSolution(double[,] T, List<int> basis)
        {
            int m = T.GetLength(0) - 1;
            int n = T.GetLength(1) - 1;
            var x = new double[n];
            for (int i = 0; i < m; i++)
            {
                int j = basis[i];
                if (j >= 0 && j < n)
                    x[j] = T[i, n];
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
                        if (!(Math.Abs(v - 0.0) <= 1e-6 || Math.Abs(v - 1.0) <= 1e-6))
                            fracs.Add((j, v));
                    }
                    else
                    {
                        if (Math.Abs(v - Math.Round(v)) > 1e-6)
                            fracs.Add((j, v));
                    }
                }
            }
            return fracs;
        }


        // =========================================================================================================================================================================== Standard model container & mapping ===

        private sealed class StdModel
        {
            public double[,] A;       // rows x stdCols
            public double[] b;        // rows
            public double[] c;        // stdCols
            public List<StdCol> StdCols;
            public List<string> ColNames;
            public int OrigCount;

            public bool WasInteger(int origIndex)
            {
                // if any std part from this original index is integer, we consider the original as integer
                return StdCols.Any(s => s.OrigIndex == origIndex && s.IsInteger);
            }
            public bool WasBinary(int origIndex)
            {
                return StdCols.Any(s => s.OrigIndex == origIndex && s.IsBinary);
            }

            public Dictionary<string, double> MapBackToOriginal(double[] xStd)
            {
                var result = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
                for (int j = 0; j < OrigCount; j++)
                {
                    string name = "X" + (j + 1);
                    double sum = 0.0;
                    // sum contributions of all std parts for this original
                    foreach (var sc in StdCols.Where(s => s.OrigIndex == j))
                    {
                        int idx = StdCols.IndexOf(sc);
                        if (idx >= 0 && idx < xStd.Length)
                            sum += (sc.Part == StdPart.Minus || sc.Part == StdPart.Flipped)  // '+' adds, '-' subtracts, '~' (flipped) treated as plus on y = -x, but we named '~' with Part.Plus
                                     ? -xStd[idx]
                                     : xStd[idx]; 
                    }
                    result[name] = sum;
                }
                return result;
            }
        }

        private sealed class StdCol
        {
            public string Name;
            public Func<double[], double> CoeffSelector;   // from original column vector (row) pick/transform coefficient
            public Func<double[], double> ObjCoefSelector; // from original objective vector pick/transform coef
            public double Scale;
            public bool IsInteger;
            public bool IsBinary;
            public int OrigIndex;
            public StdPart Part;
        }

        private enum StdPart { Plus, Minus, Flipped }
    }
}
