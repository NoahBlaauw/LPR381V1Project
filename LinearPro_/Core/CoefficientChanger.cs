using System;
using System.Collections.Generic;
using System.Linq;
using LinearPro_.Algorithms;
using LinearPro_.Model;

namespace LinearPro_.Core
{
    internal static class CoefficientChanger
    {
        public static void ChangeCoefficient(App app)
        {
            if (app.GetLastTableau() == null)
            {
                Console.WriteLine("No solved problem available. Please solve a problem first.");
                Pause();
                return;
            }

            try
            {
                // Display current tableau for reference
                Console.Clear();
                Console.WriteLine("Current Tableau for Reference:");
                var pseudoModel = CreatePseudoModelFromTableau(
                    app.GetLastTableau(),
                    app.GetLastColHeads(),
                    app.GetLastRowHeads()
                );
                TableRenderer.RenderModelAsTable(pseudoModel);

                Console.WriteLine("\nChange Coefficient");
                Console.WriteLine("Enter the coordinate in the format 'Row Column'");
                Console.WriteLine("Examples: 'Z X1', 'C3 X4', 'C10 RHS'");
                Console.Write("Enter coordinate: ");

                string input = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("No coordinate specified.");
                    Pause();
                    return;
                }

                // Parse the input
                string[] parts = input.Split(' ');
                if (parts.Length < 2)
                {
                    Console.WriteLine("Invalid format. Please use 'Row Column' format.");
                    Pause();
                    return;
                }

                string rowName = parts[0];
                string colName = parts[1];

                // If multiple words were used for the column name, combine them
                if (parts.Length > 2)
                {
                    colName = string.Join(" ", parts.Skip(1));
                }

                // Find the row and column indices
                int rowIndex = -1;
                int colIndex = -1;

                // Find row index
                for (int i = 0; i < app.GetLastRowHeads().Count; i++)
                {
                    if (app.GetLastRowHeads()[i].Equals(rowName, StringComparison.OrdinalIgnoreCase))
                    {
                        rowIndex = i;
                        break;
                    }
                }

                // Find column index
                for (int i = 0; i < app.GetLastColHeads().Count; i++)
                {
                    if (app.GetLastColHeads()[i].Equals(colName, StringComparison.OrdinalIgnoreCase))
                    {
                        colIndex = i;
                        break;
                    }
                }

                if (rowIndex == -1)
                {
                    Console.WriteLine($"Row '{rowName}' not found.");
                    Pause();
                    return;
                }

                if (colIndex == -1)
                {
                    Console.WriteLine($"Column '{colName}' not found.");
                    Pause();
                    return;
                }

                // Get the current value
                double currentValue = app.GetLastTableau()[rowIndex][colIndex];
                Console.WriteLine($"Current value: {currentValue:F6}");
                Console.Write("Enter new value: ");

                if (!double.TryParse(Console.ReadLine(), out double newValue))
                {
                    Console.WriteLine("Invalid value. Please enter a valid number.");
                    Pause();
                    return;
                }

                // Check if the change is within the allowable range
                var rangeResults = SensitivityAnalyzer.AnalyzeCoefficientRange(
                    app.GetModel(),
                    app.GetLastTableau(),
                    app.GetLastColHeads(),
                    app.GetLastRowHeads(),
                    rowName,
                    colName
                );

                bool withinRange = IsWithinRange(newValue, rangeResults, currentValue);

                // Update the model first
                UpdateModel(app.GetModel(), rowIndex, colIndex, rowName, colName, newValue);

                if (!withinRange)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine("Warning: The new value is outside the allowable range.");
                    Console.WriteLine("This may affect the optimality of the solution.");
                    Console.ResetColor();
                    Console.Write("Do you want to proceed with re-optimization? (y/n): ");

                    var response = Console.ReadKey(intercept: true).KeyChar;
                    if (response != 'y' && response != 'Y')
                    {
                        Console.WriteLine("Operation cancelled.");
                        Pause();
                        return;
                    }

                    // Re-optimize
                    Console.WriteLine("Re-optimizing with the new coefficient...");
                    var primalSimplex = new PrimalSimplex();
                    var steps = primalSimplex.Solve(app.GetModel());

                    // Check if we need to use dual simplex due to negative RHS
                    if (HasNegativeRHS(primalSimplex.GetFinalTableau()))
                    {
                        Console.WriteLine("Negative RHS detected. Applying dual simplex...");
                        var dualSimplex = new DualSimplex();
                        var dualSteps = dualSimplex.Solve(app.GetModel());

                        // After dual simplex, run primal simplex again to ensure optimality
                        Console.WriteLine("Running primal simplex to ensure optimality...");
                        var primalSimplex2 = new PrimalSimplex();
                        var primalSteps2 = primalSimplex2.Solve(app.GetModel());
                    }

                    // Update the app with the new tableau
                    app.SetLastTableau(primalSimplex.GetFinalTableau());
                    app.SetLastColHeads(primalSimplex.GetColumnHeaders());
                    app.SetLastRowHeads(primalSimplex.GetRowHeaders());

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("Re-optimization completed.");
                    Console.ResetColor();
                }
                else
                {
                    // For within-range changes, update the tableau directly
                    app.GetLastTableau()[rowIndex][colIndex] = newValue;

                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine("The new value is within the allowable range.");
                    Console.WriteLine("The current solution remains optimal.");
                    Console.ResetColor();
                }

                // Regenerate the initial tableau from the updated model
                var newPrimalSimplex = new PrimalSimplex();
                var newInitialTableau = newPrimalSimplex.ToCanonical(app.GetModel(), out var newInitialColHeads, out var newInitialRowHeads);

                // Update the app with the new initial tableau
                app.SetInitialTableau(newInitialTableau);
                app.SetInitialColHeads(newInitialColHeads);
                app.SetInitialRowHeads(newInitialRowHeads);

                // Display the updated tableaus
                Console.Clear();
                Console.WriteLine("Updated Initial Tableau:");
                var updatedInitialPseudoModel = CreatePseudoModelFromTableau(
                    app.GetInitialTableau(),
                    app.GetInitialColHeads(),
                    app.GetInitialRowHeads()
                );
                TableRenderer.RenderModelAsTable(updatedInitialPseudoModel);

                Console.WriteLine("\nUpdated Optimal Tableau:");
                var updatedOptimalPseudoModel = CreatePseudoModelFromTableau(
                    app.GetLastTableau(),
                    app.GetLastColHeads(),
                    app.GetLastRowHeads()
                );
                TableRenderer.RenderModelAsTable(updatedOptimalPseudoModel);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error during coefficient change: " + ex.Message);
                Console.ResetColor();
            }


            Pause();
        }

        private static bool IsWithinRange(double newValue, List<string> rangeResults, double currentValue)
        {
            foreach (var result in rangeResults)
            {
                if (result.Contains("Range:"))
                {
                    try
                    {
                        // Extract range from the result string
                        int start = result.IndexOf('[') + 1;
                        int end = result.IndexOf(']');
                        if (start > 0 && end > start)
                        {
                            string rangeStr = result.Substring(start, end - start);
                            string[] rangeParts = rangeStr.Split(',');

                            if (rangeParts.Length == 2 &&
                                double.TryParse(rangeParts[0].Trim(), out double min) &&
                                double.TryParse(rangeParts[1].Trim(), out double max))
                            {
                                return newValue >= min && newValue <= max;
                            }
                        }
                    }
                    catch
                    {
                        // If parsing fails, use a simpler approach
                        return Math.Abs(newValue - currentValue) < 0.0001;
                    }
                }
            }

            // Default to true if we can't determine the range
            return true;
        }

        private static void UpdateModel(LPModel model, int rowIndex, int colIndex,
                                      string rowName, string colName, double newValue)
        {
            // Update the model based on the changed coefficient
            if (rowName.Equals("Z", StringComparison.OrdinalIgnoreCase))
            {
                // Objective coefficient change
                int varIndex = model.VariableColumns.FindIndex(v => v.Equals(colName, StringComparison.OrdinalIgnoreCase));
                if (varIndex >= 0)
                {
                    model.ObjectiveCoefficients[varIndex] = newValue;
                }
            }
            else if (colName.Equals("RHS", StringComparison.OrdinalIgnoreCase))
            {
                // Constraint RHS change
                int constraintIndex = int.Parse(rowName.Substring(1)) - 1;
                if (constraintIndex >= 0 && constraintIndex < model.Constraints.Count)
                {
                    var constraint = model.Constraints[constraintIndex];
                    model.Constraints[constraintIndex] = new Constraint(
                        constraint.Coefficients,
                        constraint.Relation,
                        newValue
                    );
                }
            }
            else
            {
                // Constraint coefficient change
                int constraintIndex = int.Parse(rowName.Substring(1)) - 1;
                int varIndex = model.VariableColumns.FindIndex(v => v.Equals(colName, StringComparison.OrdinalIgnoreCase));

                if (constraintIndex >= 0 && constraintIndex < model.Constraints.Count &&
                    varIndex >= 0 && varIndex < model.Constraints[constraintIndex].Coefficients.Length)
                {
                    var constraint = model.Constraints[constraintIndex];
                    var newCoefficients = constraint.Coefficients.ToArray();
                    newCoefficients[varIndex] = newValue;

                    model.Constraints[constraintIndex] = new Constraint(
                        newCoefficients,
                        constraint.Relation,
                        constraint.Rhs
                    );
                }
            }
        }

        private static bool HasNegativeRHS(List<double[]> tableau)
        {
            int rhsCol = tableau[0].Length - 1;
            for (int i = 1; i < tableau.Count; i++)
            {
                if (tableau[i][rhsCol] < 0)
                    return true;
            }
            return false;
        }

        private static LPModel CreatePseudoModelFromTableau(List<double[]> tableau, List<string> colHeads, List<string> rowHeads)
        {
            int numVars = colHeads.Count - 1; // Exclude RHS column
            int numConstraints = tableau.Count - 1; // Exclude Z row

            // Extract objective coefficients (negate because tableau stores -c for max)
            var objCoeffs = new double[numVars];
            for (int j = 0; j < numVars; j++)
            {
                objCoeffs[j] = -tableau[0][j];
            }

            // Extract constraints
            var constraints = new List<Constraint>();
            for (int i = 1; i < tableau.Count; i++)
            {
                var coeffs = new double[numVars];
                for (int j = 0; j < numVars; j++)
                {
                    coeffs[j] = tableau[i][j];
                }
                constraints.Add(new Constraint(coeffs, Relation.EQ, tableau[i][numVars]));
            }

            // Create variable names (exclude RHS)
            var variableColumns = colHeads.Take(numVars).ToList();

            return new LPModel(true, objCoeffs, constraints, new List<SignRestriction>(), variableColumns);
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.Write("Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}