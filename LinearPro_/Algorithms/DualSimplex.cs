using System;
using System.Collections.Generic;
using System.Linq;
using LinearPro_.Model;

namespace LinearPro_.Algorithms
{
    internal sealed class DualSimplex : IAlgorithm
    {
        public string Name => "Dual Simplex";

        public List<string> Solve(LPModel model)
        {
            var steps = new List<string>();
            steps.Add("Dual Simplex Method");
            steps.Add("===================");

            // Convert to canonical form
            var tableau = ToCanonical(model, out var colHeads, out var rowHeads);
            steps.Add("Initial Tableau:");
            steps.Add(BuildTable(tableau, rowHeads, colHeads));

            int m = tableau.Count - 1;
            int n = tableau[0].Length - 1;
            int objRow = m;

            for (int iter = 0; iter < 1000; iter++)
            {
                // Find the most negative RHS (pivot row)
                int pivotRow = -1;
                double minRhs = 0;
                for (int i = 1; i <= m; i++)
                {
                    if (tableau[i][n] < minRhs)
                    {
                        minRhs = tableau[i][n];
                        pivotRow = i;
                    }
                }

                // If no negative RHS, we're done
                if (pivotRow == -1)
                {
                    steps.Add("No negative RHS found. Solution is feasible.");
                    break;
                }

                // Find the pivot column
                int pivotCol = -1;
                double minRatio = double.MaxValue;

                // For dual simplex, we need to consider the problem type (max or min)
                bool isMaxProblem = model.IsMax;

                for (int j = 0; j < n; j++)
                {
                    if (tableau[pivotRow][j] < 0) // Only consider negative coefficients in pivot row
                    {
                        double ratio;
                        if (isMaxProblem)
                        {
                            // For max problems: ratio = (objective coefficient) / (pivot element)
                            ratio = tableau[objRow][j] / tableau[pivotRow][j];
                        }
                        else
                        {
                            // For min problems: ratio = (objective coefficient) / (pivot element)
                            // But we need to adjust for minimization
                            ratio = tableau[objRow][j] / tableau[pivotRow][j];
                        }

                        // We want the minimum positive ratio
                        if (ratio > 0 && ratio < minRatio)
                        {
                            minRatio = ratio;
                            pivotCol = j;
                        }
                    }
                }

                if (pivotCol == -1)
                {
                    steps.Add("No valid pivot column found. Problem is infeasible.");
                    break;
                }

                // Perform pivot operation
                steps.Add($"Pivot on row {pivotRow}, column {pivotCol}");
                Pivot(tableau, pivotRow, pivotCol);
                steps.Add(BuildTable(tableau, rowHeads, colHeads));
            }

            return steps;
        }

        private List<double[]> ToCanonical(LPModel model, out List<string> colHeads, out List<string> rowHeads)
        {
            // Use the same implementation as PrimalSimplex
            var primal = new PrimalSimplex();
            return primal.ToCanonical(model, out colHeads, out rowHeads);
        }

        private string BuildTable(List<double[]> tableau, List<string> rowHeads, List<string> colHeads)
        {
            // Use the same implementation as PrimalSimplex
            var primal = new PrimalSimplex();
            return primal.BuildTable(tableau, rowHeads, colHeads);
        }

        private void Pivot(List<double[]> tableau, int pivotRow, int pivotCol)
        {
            int rows = tableau.Count;
            int cols = tableau[0].Length;

            double pivotElement = tableau[pivotRow][pivotCol];

            // Normalize the pivot row
            for (int j = 0; j < cols; j++)
            {
                tableau[pivotRow][j] /= pivotElement;
            }

            // Update other rows
            for (int i = 0; i < rows; i++)
            {
                if (i == pivotRow) continue;

                double factor = tableau[i][pivotCol];
                for (int j = 0; j < cols; j++)
                {
                    tableau[i][j] -= factor * tableau[pivotRow][j];
                }
            }
        }
    }
}