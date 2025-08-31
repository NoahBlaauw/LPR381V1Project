using System;
using System.Collections.Generic;
using System.Linq;
using LinearPro_.Algorithms;
using LinearPro_.Model;

namespace LinearPro_.Core
{
    internal static class ConstraintAdder
    {
        public static void AddNewConstraint(App app)
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

                Console.WriteLine("\nAdding New Constraint");
                Console.WriteLine("=====================");

                // Get the number of variables in the original problem
                int numVars = app.GetInitialColHeads().Count - 1; // Exclude RHS

                // Prompt for coefficients for each variable
                var coefficients = new List<double>();
                for (int i = 0; i < numVars; i++)
                {
                    Console.Write($"Enter coefficient for {app.GetInitialColHeads()[i]}: ");
                    if (double.TryParse(Console.ReadLine(), out double coeff))
                    {
                        coefficients.Add(coeff);
                    }
                    else
                    {
                        Console.WriteLine("Invalid coefficient. Please enter a valid number.");
                        i--; // Retry this variable
                    }
                }

                // Prompt for relation
                Console.Write("Enter relation (<=, >=, =): ");
                string relationInput = Console.ReadLine()?.Trim().ToLower();
                Relation relation;
                switch (relationInput)
                {
                    case "<=":
                        relation = Relation.LE;
                        break;
                    case ">=":
                        relation = Relation.GE;
                        break;
                    case "=":
                        relation = Relation.EQ;
                        break;
                    default:
                        Console.WriteLine("Invalid relation. Using '<=' as default.");
                        relation = Relation.LE;
                        break;
                }

                // Prompt for RHS
                Console.Write("Enter RHS value: ");
                if (!double.TryParse(Console.ReadLine(), out double rhs))
                {
                    Console.WriteLine("Invalid RHS value. Using 0 as default.");
                    rhs = 0;
                }

                // Create the new constraint
                var newConstraint = new Constraint(coefficients.ToArray(), relation, rhs);

                // Update the model
                var updatedModel = UpdateModelWithNewConstraint(app.GetModel(), newConstraint);

                // Re-optimize the problem
                Console.WriteLine("Re-optimizing with the new variable...");
                var primalSimplex = new PrimalSimplex();
                var steps = primalSimplex.Solve(updatedModel);

                // Check if we need to use dual simplex due to negative RHS
                if (HasNegativeRHS(primalSimplex.GetFinalTableau()))
                {
                    Console.WriteLine("Negative RHS detected. Applying dual simplex...");
                    var dualSimplex = new DualSimplex();
                    var dualSteps = dualSimplex.Solve(updatedModel);
                    steps.AddRange(dualSteps);

                    // After dual simplex, run primal simplex again to ensure optimality
                    Console.WriteLine("Running primal simplex to ensure optimality...");
                    var primalSimplex2 = new PrimalSimplex();
                    var primalSteps2 = primalSimplex2.Solve(updatedModel);
                    steps.AddRange(primalSteps2);

                    // Update the app with the final tableau
                    app.SetLastTableau(primalSimplex2.GetFinalTableau());
                    app.SetLastColHeads(primalSimplex2.GetColumnHeaders());
                    app.SetLastRowHeads(primalSimplex2.GetRowHeaders());
                }
                else
                {
                    // Update the app with the primal simplex results
                    app.SetLastTableau(primalSimplex.GetFinalTableau());
                    app.SetLastColHeads(primalSimplex.GetColumnHeaders());
                    app.SetLastRowHeads(primalSimplex.GetRowHeaders());
                }

                // Always update the initial tableau
                app.SetInitialTableau(primalSimplex.GetInitialTableau());
                app.SetInitialColHeads(primalSimplex.GetInitialColumnHeaders());
                app.SetInitialRowHeads(primalSimplex.GetInitialRowHeaders());

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
                Console.WriteLine("New constraint added successfully!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error adding new constraint: " + ex.Message);
                Console.ResetColor();
            }

            Pause();
        }

        private static LPModel UpdateModelWithNewConstraint(LPModel originalModel, Constraint newConstraint)
        {
            // Create a new list of constraints with the new one added
            var updatedConstraints = new List<Constraint>(originalModel.Constraints);
            updatedConstraints.Add(newConstraint);

            // Return a new model with the updated constraints
            return new LPModel(
                originalModel.IsMax,
                originalModel.ObjectiveCoefficients,
                updatedConstraints,
                originalModel.SignRestrictions,
                originalModel.VariableColumns
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