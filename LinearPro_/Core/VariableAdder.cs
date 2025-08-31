using System;
using System.Collections.Generic;
using System.Linq;
using LinearPro_.Algorithms;
using LinearPro_.Model;

namespace LinearPro_.Core
{
    internal static class VariableAdder
    {
        public static void AddNewVariable(App app)
        {
            if (app.GetInitialTableau() == null)
            {
                Console.WriteLine("No problem available. Please solve a problem first.");
                Pause();
                return;
            }

            try
            {
                // Display initial tableau for reference
                Console.Clear();
                Console.WriteLine("Initial Tableau (Canonical Form):");
                var initialPseudoModel = CreatePseudoModelFromTableau(
                    app.GetInitialTableau(),
                    app.GetInitialColHeads(),
                    app.GetInitialRowHeads()
                );
                TableRenderer.RenderModelAsTable(initialPseudoModel);

                Console.WriteLine("\nAdding New Variable");
                Console.WriteLine("===================");

                // Prompt for variable name
                Console.Write("Enter variable name (e.g., X5): ");
                string varName = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(varName))
                {
                    Console.WriteLine("Invalid variable name. Using 'XNew' as default.");
                    varName = "XNew";
                }

                // Prompt for objective coefficient
                Console.Write("Enter objective coefficient: ");
                if (!double.TryParse(Console.ReadLine(), out double objCoeff))
                {
                    Console.WriteLine("Invalid coefficient. Using 0 as default.");
                    objCoeff = 0;
                }

                // Prompt for coefficients in each constraint
                int numConstraints = app.GetInitialTableau().Count - 1; // Exclude Z row
                var constraintCoefficients = new List<double>();
                for (int i = 1; i <= numConstraints; i++)
                {
                    Console.Write($"Enter coefficient for constraint C{i}: ");
                    if (double.TryParse(Console.ReadLine(), out double coeff))
                    {
                        constraintCoefficients.Add(coeff);
                    }
                    else
                    {
                        Console.WriteLine("Invalid coefficient. Using 0 as default.");
                        constraintCoefficients.Add(0);
                    }
                }

                // Prompt for sign restriction
                Console.Write("Enter sign restriction (+, -, urs, int, bin): ");
                string signInput = Console.ReadLine()?.Trim().ToLower();
                SignRestriction signRestriction;
                switch (signInput)
                {
                    case "+":
                        signRestriction = SignRestriction.NonNegative;
                        break;
                    case "-":
                        signRestriction = SignRestriction.NonPositive;
                        break;
                    case "urs":
                        signRestriction = SignRestriction.Unrestricted;
                        break;
                    case "int":
                        signRestriction = SignRestriction.Integer;
                        break;
                    case "bin":
                        signRestriction = SignRestriction.Binary;
                        break;
                    default:
                        Console.WriteLine("Invalid sign restriction. Using '+' as default.");
                        signRestriction = SignRestriction.NonNegative;
                        break;
                }

                // Update the model
                var updatedModel = UpdateModelWithNewVariable(
                    app.GetModel(),
                    varName,
                    objCoeff,
                    constraintCoefficients.ToArray(),
                    signRestriction
                );

                // Re-optimize the problem
                Console.WriteLine("Re-optimizing with the new variable...");
                var primalSimplex = new PrimalSimplex();
                var steps = primalSimplex.Solve(updatedModel);

                // Check if we need to use dual simplex due to negative RHS
                if (HasNegativeRHS(primalSimplex.GetFinalTableau()))
                {
                    Console.WriteLine("Negative RHS detected. Applying dual simplex...");
                    var dualSimplex = new DualSimplex();
                    steps.AddRange(dualSimplex.Solve(updatedModel));
                }

                // Update the app with the new model and tableau
                app.SetModel(updatedModel);
                app.SetLastTableau(primalSimplex.GetFinalTableau());
                app.SetLastColHeads(primalSimplex.GetColumnHeaders());
                app.SetLastRowHeads(primalSimplex.GetRowHeaders());
                app.SetInitialTableau(primalSimplex.GetInitialTableau());
                app.SetInitialColHeads(primalSimplex.GetInitialColumnHeaders());
                app.SetInitialRowHeads(primalSimplex.GetInitialRowHeaders());

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

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("New variable added successfully!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error adding new variable: " + ex.Message);
                Console.ResetColor();
            }

            Pause();
        }

        private static LPModel UpdateModelWithNewVariable(
            LPModel originalModel,
            string varName,
            double objCoeff,
            double[] constraintCoefficients,
            SignRestriction signRestriction)
        {
            // Update objective coefficients
            var updatedObjCoeffs = new List<double>(originalModel.ObjectiveCoefficients);
            updatedObjCoeffs.Add(objCoeff);

            // Update constraints
            var updatedConstraints = new List<Constraint>();
            for (int i = 0; i < originalModel.Constraints.Count; i++)
            {
                var oldCoeffs = new List<double>(originalModel.Constraints[i].Coefficients);
                oldCoeffs.Add(i < constraintCoefficients.Length ? constraintCoefficients[i] : 0);

                updatedConstraints.Add(new Constraint(
                    oldCoeffs.ToArray(),
                    originalModel.Constraints[i].Relation,
                    originalModel.Constraints[i].Rhs
                ));
            }

            // Update variable columns
            var updatedVarColumns = new List<string>(originalModel.VariableColumns);
            updatedVarColumns.Add(varName);

            // Update sign restrictions
            var updatedSignRestrictions = new List<SignRestriction>(originalModel.SignRestrictions);
            updatedSignRestrictions.Add(signRestriction);

            // Return a new model with the updated values
            return new LPModel(
                originalModel.IsMax,
                updatedObjCoeffs.ToArray(),
                updatedConstraints,
                updatedSignRestrictions,
                updatedVarColumns
            );
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