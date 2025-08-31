using LinearPro_.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace LinearPro_.Algorithms
{
    internal static class SensitivityAnalyzer
    {
        public static List<string> AnalyzeCoefficient(LPModel model, List<double[]> finalTableau,
                                                   List<string> colHeads, List<string> rowHeads,
                                                   string rowName, string colName)
        {
            var results = new List<string>();
            results.Add($"=== Sensitivity Analysis for ({rowName}, {colName}) ===");

            // Find the row and column indices (case-insensitive)
            int rowIndex = -1;
            int colIndex = -1;

            // Find row index
            for (int i = 0; i < rowHeads.Count; i++)
            {
                if (rowHeads[i].Equals(rowName, StringComparison.OrdinalIgnoreCase))
                {
                    rowIndex = i;
                    break;
                }
            }

            // Find column index
            for (int i = 0; i < colHeads.Count; i++)
            {
                if (colHeads[i].Equals(colName, StringComparison.OrdinalIgnoreCase))
                {
                    colIndex = i;
                    break;
                }
            }

            if (rowIndex == -1)
            {
                results.Add($"Row '{rowName}' not found.");
                return results;
            }

            if (colIndex == -1)
            {
                results.Add($"Column '{colName}' not found.");
                return results;
            }

            // Get the current value
            double currentValue = finalTableau[rowIndex][colIndex];
            results.Add($"Current value: {currentValue:F6}");

            // Determine the type of coefficient and perform appropriate analysis
            if (rowIndex == 0) // Z row - objective function coefficient
            {
                results.AddRange(AnalyzeObjectiveCoefficient(finalTableau, colHeads, rowHeads, colIndex));
            }
            else if (colIndex == colHeads.Count - 1) // RHS column
            {
                results.AddRange(AnalyzeRHS(finalTableau, rowHeads, rowIndex));
            }
            else // Constraint coefficient
            {
                results.AddRange(AnalyzeConstraintCoefficient(finalTableau, colHeads, rowHeads, rowIndex, colIndex));
            }

            return results;
        }

        private static List<string> AnalyzeObjectiveCoefficient(List<double[]> tableau,
            List<string> colHeads, List<string> rowHeads, int colIndex)
        {
            var analysis = new List<string>();
            analysis.Add("Type: Objective Function Coefficient");

            // Get basic variables
            var basicVars = GetBasicVariables(tableau, rowHeads, colHeads);
            string varName = colHeads[colIndex];

            double reducedCost = tableau[0][colIndex];
            double currentValue = reducedCost;

            string type = basicVars.ContainsKey(varName) ? "Basic" : "Non-Basic";
            analysis.Add($"Variable type: {type}");

            if (basicVars.ContainsKey(varName))
            {
                // Basic variable - need to calculate range using the basis
                int basicRow = basicVars[varName];
                double rangeLower = double.NegativeInfinity;
                double rangeUpper = double.PositiveInfinity;

                int rhsCol = tableau[0].Length - 1;

                for (int k = 0; k < rhsCol; k++)
                {
                    if (k == colIndex) continue;

                    if (tableau[basicRow][k] > 1e-10)
                    {
                        double ratio = -tableau[0][k] / tableau[basicRow][k];
                        if (ratio > rangeLower) rangeLower = ratio;
                    }
                    else if (tableau[basicRow][k] < -1e-10)
                    {
                        double ratio = -tableau[0][k] / tableau[basicRow][k];
                        if (ratio < rangeUpper) rangeUpper = ratio;
                    }
                }

                analysis.Add($"Allowable decrease: {rangeLower:F6}");
                analysis.Add($"Allowable increase: {rangeUpper:F6}");
                analysis.Add($"Range: [{currentValue + rangeLower:F6}, {currentValue + rangeUpper:F6}]");
            }
            else
            {
                // Non-basic variable
                double allowableIncrease = reducedCost < 0 ? double.PositiveInfinity : -reducedCost;
                double allowableDecrease = reducedCost > 0 ? double.PositiveInfinity : reducedCost;

                analysis.Add($"Reduced cost: {reducedCost:F6}");
                analysis.Add($"Allowable decrease: {allowableDecrease:F6}");
                analysis.Add($"Allowable increase: {allowableIncrease:F6}");
                analysis.Add($"Range: [{currentValue - allowableDecrease:F6}, {currentValue + allowableIncrease:F6}]");
            }

            return analysis;
        }

        private static List<string> AnalyzeRHS(List<double[]> tableau,
            List<string> rowHeads, int rowIndex)
        {
            var analysis = new List<string>();
            analysis.Add("Type: Right-Hand Side Value");

            int rhsCol = tableau[0].Length - 1;
            double currentRHS = tableau[rowIndex][rhsCol];

            // For RHS, we calculate the shadow price and allowable range
            // This is a simplified implementation - a real implementation would be more complex
            double shadowPrice = CalculateShadowPrice(tableau, rowHeads, rowIndex);
            double allowableIncrease = double.PositiveInfinity;
            double allowableDecrease = currentRHS;

            analysis.Add($"Current value: {currentRHS:F6}");
            analysis.Add($"Shadow price: {shadowPrice:F6}");
            analysis.Add($"Allowable decrease: {allowableDecrease:F6}");
            analysis.Add($"Allowable increase: {allowableIncrease:F6}");
            analysis.Add($"Range: [{currentRHS - allowableDecrease:F6}, ∞]");

            return analysis;
        }

        private static List<string> AnalyzeConstraintCoefficient(List<double[]> tableau,
            List<string> colHeads, List<string> rowHeads, int rowIndex, int colIndex)
        {
            var analysis = new List<string>();
            analysis.Add("Type: Constraint Coefficient");

            double currentValue = tableau[rowIndex][colIndex];
            analysis.Add($"Current value: {currentValue:F6}");

            // Analyzing constraint coefficients is complex and typically requires
            // re-solving the LP with perturbed coefficients or using more advanced techniques
            analysis.Add("Note: Complete sensitivity analysis for constraint coefficients");
            analysis.Add("requires advanced techniques beyond the scope of this implementation.");

            return analysis;
        }

        private static double CalculateShadowPrice(List<double[]> tableau, List<string> rowHeads, int rowIndex)
        {
            // Simplified shadow price calculation
            // In a real implementation, this would be more complex
            int rhsCol = tableau[0].Length - 1;

            // For a basic row, the shadow price is the negative of the reduced cost
            // of the corresponding slack variable in the objective row
            return -tableau[0][rhsCol] / tableau[rowIndex][rhsCol];
        }

        private static Dictionary<string, int> GetBasicVariables(List<double[]> tableau,
            List<string> rowHeads, List<string> colHeads)
        {
            var basicVars = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int rhsCol = tableau[0].Length - 1;

            for (int i = 1; i < tableau.Count; i++)
            {
                for (int j = 0; j < rhsCol; j++)
                {
                    if (Math.Abs(tableau[i][j] - 1.0) < 1e-10)
                    {
                        // Check if this is a basic variable (column has exactly one 1 and rest 0s)
                        bool isBasic = true;
                        for (int k = 0; k < tableau.Count; k++)
                        {
                            if (k != i && Math.Abs(tableau[k][j]) > 1e-10)
                            {
                                isBasic = false;
                                break;
                            }
                        }

                        if (isBasic)
                        {
                            basicVars[colHeads[j]] = i;
                            break;
                        }
                    }
                }
            }
            return basicVars;
        }
        public static List<string> AnalyzeCoefficientRange(LPModel model, List<double[]> finalTableau,
                                               List<string> colHeads, List<string> rowHeads,
                                               string rowName, string colName)
        {
            var results = AnalyzeCoefficient(model, finalTableau, colHeads, rowHeads, rowName, colName);

            // Filter to only include range information
            var rangeResults = new List<string>();
            foreach (var result in results)
            {
                if (result.Contains("Range:") || result.Contains("Allowable") ||
                    result.Contains("Current value:") || result.Contains("Variable type:"))
                {
                    rangeResults.Add(result);
                }
            }

            return rangeResults;
        }
    }
}