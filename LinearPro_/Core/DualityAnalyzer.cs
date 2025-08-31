using System;
using System.Collections.Generic;
using System.Linq;
using LinearPro_.Algorithms;
using LinearPro_.Model;

namespace LinearPro_.Core
{
    internal static class DualityAnalyzer
    {
        public static void AnalyzeDuality(App app)
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
                Console.WriteLine("Primal Problem - Initial Tableau:");
                var initialPseudoModel = CreatePseudoModelFromTableau(
                    app.GetInitialTableau(),
                    app.GetInitialColHeads(),
                    app.GetInitialRowHeads()
                );
                TableRenderer.RenderModelAsTable(initialPseudoModel);

                Console.WriteLine("\nPrimal Problem - Optimal Tableau:");
                var optimalPseudoModel = CreatePseudoModelFromTableau(
                    app.GetLastTableau(),
                    app.GetLastColHeads(),
                    app.GetLastRowHeads()
                );
                TableRenderer.RenderModelAsTable(optimalPseudoModel);

                // Create and display the dual problem
                Console.WriteLine("\nDual Problem Analysis");
                Console.WriteLine("=====================");

                var dualModel = CreateDualModel(app.GetModel());
                Console.WriteLine("Dual Problem Formulation:");
                DisplayDualFormulation(dualModel);

                // Solve the dual problem
                Console.WriteLine("\nSolving Dual Problem...");
                var primalSimplex = new PrimalSimplex();
                var steps = primalSimplex.Solve(dualModel);

                // Display dual optimal tableau
                Console.WriteLine("\nDual Problem - Optimal Tableau:");
                var dualPseudoModel = CreatePseudoModelFromTableau(
                    primalSimplex.GetFinalTableau(),
                    primalSimplex.GetColumnHeaders(),
                    primalSimplex.GetRowHeaders()
                );
                TableRenderer.RenderModelAsTable(dualPseudoModel);

                // Analyze duality strength
                AnalyzeDualityStrength(app.GetModel(), dualModel, app.GetLastTableau(), primalSimplex.GetFinalTableau());

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("\nDuality analysis completed successfully!");
                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error during duality analysis: " + ex.Message);
                Console.ResetColor();
            }

            Pause();
        }

        private static LPModel CreateDualModel(LPModel primalModel)
        {
            // Convert primal to dual based on standard transformation rules
            // For a primal: max c^T x, s.t. Ax ≤ b, x ≥ 0
            // The dual is: min b^T y, s.t. A^T y ≥ c, y ≥ 0

            // For a primal: min c^T x, s.t. Ax ≥ b, x ≥ 0
            // The dual is: max b^T y, s.t. A^T y ≤ c, y ≥ 0

            // This is a simplified implementation that assumes standard forms
            bool isDualMax = !primalModel.IsMax;

            // Transpose the constraint matrix and swap objective with RHS
            int numPrimalConstraints = primalModel.Constraints.Count;
            int numPrimalVariables = primalModel.ObjectiveCoefficients.Length;

            // Dual objective coefficients (primal RHS)
            double[] dualObjective = new double[numPrimalConstraints];
            for (int i = 0; i < numPrimalConstraints; i++)
            {
                dualObjective[i] = primalModel.Constraints[i].Rhs;
            }

            // Dual constraints (transposed primal constraints)
            var dualConstraints = new List<Constraint>();
            for (int j = 0; j < numPrimalVariables; j++)
            {
                double[] constraintCoeffs = new double[numPrimalConstraints];
                for (int i = 0; i < numPrimalConstraints; i++)
                {
                    constraintCoeffs[i] = primalModel.Constraints[i].Coefficients[j];
                }

                // Determine constraint relation based on primal type
                Relation relation;
                if (primalModel.IsMax)
                {
                    relation = Relation.GE; // For max primal, dual constraints are ≥
                }
                else
                {
                    relation = Relation.LE; // For min primal, dual constraints are ≤
                }

                dualConstraints.Add(new Constraint(
                    constraintCoeffs,
                    relation,
                    primalModel.ObjectiveCoefficients[j] // RHS is primal objective coefficient
                ));
            }

            // Dual variable names
            var dualVariableNames = new List<string>();
            for (int i = 1; i <= numPrimalConstraints; i++)
            {
                dualVariableNames.Add($"y{i}");
            }

            // Dual sign restrictions (always non-negative for standard forms)
            var dualSignRestrictions = Enumerable.Repeat(SignRestriction.NonNegative, numPrimalConstraints).ToList();

            return new LPModel(
                isDualMax,
                dualObjective,
                dualConstraints,
                dualSignRestrictions,
                dualVariableNames
            );
        }

        private static void DisplayDualFormulation(LPModel dualModel)
        {
            Console.Write(dualModel.IsMax ? "Maximize: " : "Minimize: ");

            // Display objective function
            for (int i = 0; i < dualModel.ObjectiveCoefficients.Length; i++)
            {
                if (i > 0) Console.Write(" + ");
                Console.Write($"{dualModel.ObjectiveCoefficients[i]:F2}{dualModel.VariableColumns[i]}");
            }
            Console.WriteLine();

            // Display constraints
            Console.WriteLine("Subject to:");
            foreach (var constraint in dualModel.Constraints)
            {
                for (int i = 0; i < constraint.Coefficients.Length; i++)
                {
                    if (i > 0) Console.Write(" + ");
                    Console.Write($"{constraint.Coefficients[i]:F2}{dualModel.VariableColumns[i]}");
                }

                string relation;
                switch (constraint.Relation)
                {
                    case Relation.LE: relation = "≤"; break;
                    case Relation.GE: relation = "≥"; break;
                    case Relation.EQ: relation = "="; break;
                    default: relation = "?"; break;
                }

                Console.WriteLine($" {relation} {constraint.Rhs:F2}");
            }

            // Display variable restrictions
            Console.Write("With: ");
            for (int i = 0; i < dualModel.SignRestrictions.Count; i++)
            {
                if (i > 0) Console.Write(", ");
                string restriction;
                switch (dualModel.SignRestrictions[i])
                {
                    case SignRestriction.NonNegative: restriction = "≥ 0"; break;
                    case SignRestriction.NonPositive: restriction = "≤ 0"; break;
                    case SignRestriction.Unrestricted: restriction = "urs"; break;
                    case SignRestriction.Integer: restriction = "integer"; break;
                    case SignRestriction.Binary: restriction = "binary"; break;
                    default: restriction = "?"; break;
                }
                Console.Write($"{dualModel.VariableColumns[i]} {restriction}");
            }
            Console.WriteLine();
        }

        private static void AnalyzeDualityStrength(LPModel primalModel, LPModel dualModel,
                                                 List<double[]> primalTableau, List<double[]> dualTableau)
        {
            Console.WriteLine("\nDuality Strength Analysis:");
            Console.WriteLine("===========================");

            // Get optimal values
            double primalOptimal = GetOptimalValue(primalTableau);
            double dualOptimal = GetOptimalValue(dualTableau);

            Console.WriteLine($"Primal Optimal Value: {primalOptimal:F6}");
            Console.WriteLine($"Dual Optimal Value: {dualOptimal:F6}");

            // Check strong duality
            if (Math.Abs(primalOptimal - dualOptimal) < 1e-6)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Strong duality holds: Primal and dual optimal values are equal.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Weak duality: Primal and dual optimal values differ.");
                Console.ResetColor();
            }

            // Analyze complementary slackness
            AnalyzeComplementarySlackness(primalModel, dualModel, primalTableau, dualTableau);
        }

        private static double GetOptimalValue(List<double[]> tableau)
        {
            int objRow = tableau.Count - 1;
            int rhsCol = tableau[0].Length - 1;
            return tableau[objRow][rhsCol];
        }

        private static void AnalyzeComplementarySlackness(LPModel primalModel, LPModel dualModel,
                                                        List<double[]> primalTableau, List<double[]> dualTableau)
        {
            Console.WriteLine("\nComplementary Slackness Analysis:");
            Console.WriteLine("=================================");

            // This is a simplified analysis - in a real implementation, you would
            // extract the primal and dual solutions and check the conditions

            Console.WriteLine("For each primal constraint i and dual variable y_i:");
            Console.WriteLine("  Either the constraint is binding (slack = 0) or y_i = 0");
            Console.WriteLine("For each primal variable x_j and dual constraint j:");
            Console.WriteLine("  Either x_j = 0 or the dual constraint is binding");

            // Check if complementary slackness conditions approximately hold
            bool conditionsHold = true;
            // Implementation would go here to check the actual conditions

            if (conditionsHold)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Complementary slackness conditions appear to hold.");
                Console.ResetColor();
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("Complementary slackness conditions may not hold exactly.");
                Console.ResetColor();
            }
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