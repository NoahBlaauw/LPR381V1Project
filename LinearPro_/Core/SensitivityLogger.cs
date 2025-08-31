using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using LinearPro_.Model;

namespace LinearPro_.Core
{
    internal static class SensitivityLogger
    {
        private static readonly string LogFilePath = Path.Combine(Environment.CurrentDirectory, "sensitivity_analysis_log.txt");

        public static void LogCoefficientChange(string rowName, string colName, double oldValue, double newValue,
                                              List<string> rangeResults, LPModel model,
                                              List<double[]> initialTableau, List<string> initialColHeads, List<string> initialRowHeads,
                                              List<double[]> optimalTableau, List<string> optimalColHeads, List<string> optimalRowHeads)
        {
            var logContent = new StringBuilder();
            logContent.AppendLine($"=== Coefficient Change Log - {DateTime.Now} ===");
            logContent.AppendLine($"Changed coefficient at ({rowName}, {colName})");
            logContent.AppendLine($"Old value: {oldValue:F6}, New value: {newValue:F6}");
            logContent.AppendLine();

            logContent.AppendLine("Range Analysis:");
            foreach (var result in rangeResults)
            {
                logContent.AppendLine(result);
            }
            logContent.AppendLine();

            logContent.AppendLine("Updated Initial Tableau:");
            logContent.AppendLine(TableauToString(initialTableau, initialColHeads, initialRowHeads));
            logContent.AppendLine();

            logContent.AppendLine("Updated Optimal Tableau:");
            logContent.AppendLine(TableauToString(optimalTableau, optimalColHeads, optimalRowHeads));
            logContent.AppendLine();

            AppendToLogFile(logContent.ToString());
        }

        public static void LogNewConstraint(Constraint newConstraint, LPModel model,
                                          List<double[]> initialTableau, List<string> initialColHeads, List<string> initialRowHeads,
                                          List<double[]> optimalTableau, List<string> optimalColHeads, List<string> optimalRowHeads)
        {
            var logContent = new StringBuilder();
            logContent.AppendLine($"=== New Constraint Log - {DateTime.Now} ===");
            logContent.AppendLine("New Constraint Details:");

            // Format constraint coefficients
            var coeffs = newConstraint.Coefficients.Select((c, i) => $"{c:F2}{model.VariableColumns[i]}");
            logContent.Append(string.Join(" + ", coeffs));

            string relation;
            switch (newConstraint.Relation)
            {
                case Relation.LE: relation = "≤"; break;
                case Relation.GE: relation = "≥"; break;
                case Relation.EQ: relation = "="; break;
                default: relation = "?"; break;
            }

            logContent.AppendLine($" {relation} {newConstraint.Rhs:F2}");
            logContent.AppendLine();

            logContent.AppendLine("Updated Initial Tableau:");
            logContent.AppendLine(TableauToString(initialTableau, initialColHeads, initialRowHeads));
            logContent.AppendLine();

            logContent.AppendLine("Updated Optimal Tableau:");
            logContent.AppendLine(TableauToString(optimalTableau, optimalColHeads, optimalRowHeads));
            logContent.AppendLine();

            AppendToLogFile(logContent.ToString());
        }

        public static void LogNewVariable(string varName, double objCoeff, double[] constraintCoefficients,
                                        SignRestriction signRestriction, LPModel model,
                                        List<double[]> initialTableau, List<string> initialColHeads, List<string> initialRowHeads,
                                        List<double[]> optimalTableau, List<string> optimalColHeads, List<string> optimalRowHeads)
        {
            var logContent = new StringBuilder();
            logContent.AppendLine($"=== New Variable Log - {DateTime.Now} ===");
            logContent.AppendLine($"New Variable: {varName}");
            logContent.AppendLine($"Objective Coefficient: {objCoeff:F2}");

            logContent.AppendLine("Constraint Coefficients:");
            for (int i = 0; i < constraintCoefficients.Length; i++)
            {
                logContent.AppendLine($"  C{i + 1}: {constraintCoefficients[i]:F2}");
            }

            string restriction;
            switch (signRestriction)
            {
                case SignRestriction.NonNegative: restriction = "≥ 0"; break;
                case SignRestriction.NonPositive: restriction = "≤ 0"; break;
                case SignRestriction.Unrestricted: restriction = "urs"; break;
                case SignRestriction.Integer: restriction = "integer"; break;
                case SignRestriction.Binary: restriction = "binary"; break;
                default: restriction = "?"; break;
            }

            logContent.AppendLine($"Sign Restriction: {restriction}");
            logContent.AppendLine();

            logContent.AppendLine("Updated Initial Tableau:");
            logContent.AppendLine(TableauToString(initialTableau, initialColHeads, initialRowHeads));
            logContent.AppendLine();

            logContent.AppendLine("Updated Optimal Tableau:");
            logContent.AppendLine(TableauToString(optimalTableau, optimalColHeads, optimalRowHeads));
            logContent.AppendLine();

            AppendToLogFile(logContent.ToString());
        }

        public static void LogDualityAnalysis(LPModel primalModel, LPModel dualModel,
                                            List<double[]> primalTableau, List<string> primalColHeads, List<string> primalRowHeads,
                                            List<double[]> dualTableau, List<string> dualColHeads, List<string> dualRowHeads,
                                            List<string> dualityResults)
        {
            var logContent = new StringBuilder();
            logContent.AppendLine($"=== Duality Analysis Log - {DateTime.Now} ===");

            logContent.AppendLine("Primal Problem:");
            logContent.AppendLine(ModelToString(primalModel));
            logContent.AppendLine();

            logContent.AppendLine("Dual Problem:");
            logContent.AppendLine(ModelToString(dualModel));
            logContent.AppendLine();

            logContent.AppendLine("Primal Optimal Tableau:");
            logContent.AppendLine(TableauToString(primalTableau, primalColHeads, primalRowHeads));
            logContent.AppendLine();

            logContent.AppendLine("Dual Optimal Tableau:");
            logContent.AppendLine(TableauToString(dualTableau, dualColHeads, dualRowHeads));
            logContent.AppendLine();

            logContent.AppendLine("Duality Analysis Results:");
            foreach (var result in dualityResults)
            {
                logContent.AppendLine(result);
            }
            logContent.AppendLine();

            AppendToLogFile(logContent.ToString());
        }

        private static string TableauToString(List<double[]> tableau, List<string> colHeads, List<string> rowHeads)
        {
            var sb = new StringBuilder();

            // Header row
            sb.Append("".PadRight(12));
            foreach (var header in colHeads)
            {
                sb.Append(header.PadRight(12));
            }
            sb.AppendLine();

            // Data rows
            for (int i = 0; i < tableau.Count; i++)
            {
                sb.Append(rowHeads[i].PadRight(12));
                foreach (var value in tableau[i])
                {
                    sb.Append(value.ToString("F6").PadRight(12));
                }
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private static string ModelToString(LPModel model)
        {
            var sb = new StringBuilder();

            sb.Append(model.IsMax ? "Maximize: " : "Minimize: ");

            // Objective function
            for (int i = 0; i < model.ObjectiveCoefficients.Length; i++)
            {
                if (i > 0) sb.Append(" + ");
                sb.Append($"{model.ObjectiveCoefficients[i]:F2}{model.VariableColumns[i]}");
            }
            sb.AppendLine();

            // Constraints
            sb.AppendLine("Subject to:");
            foreach (var constraint in model.Constraints)
            {
                for (int i = 0; i < constraint.Coefficients.Length; i++)
                {
                    if (i > 0) sb.Append(" + ");
                    sb.Append($"{constraint.Coefficients[i]:F2}{model.VariableColumns[i]}");
                }

                string relation;
                switch (constraint.Relation)
                {
                    case Relation.LE: relation = "≤"; break;
                    case Relation.GE: relation = "≥"; break;
                    case Relation.EQ: relation = "="; break;
                    default: relation = "?"; break;
                }

                sb.AppendLine($" {relation} {constraint.Rhs:F2}");
            }

            // Variable restrictions
            sb.Append("With: ");
            for (int i = 0; i < model.SignRestrictions.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                string restriction;
                switch (model.SignRestrictions[i])
                {
                    case SignRestriction.NonNegative: restriction = "≥ 0"; break;
                    case SignRestriction.NonPositive: restriction = "≤ 0"; break;
                    case SignRestriction.Unrestricted: restriction = "urs"; break;
                    case SignRestriction.Integer: restriction = "integer"; break;
                    case SignRestriction.Binary: restriction = "binary"; break;
                    default: restriction = "?"; break;
                }
                sb.Append($"{model.VariableColumns[i]} {restriction}");
            }
            sb.AppendLine();

            return sb.ToString();
        }

        private static void AppendToLogFile(string content)
        {
            try
            {
                File.AppendAllText(LogFilePath, content);
                Console.WriteLine($"Results logged to: {LogFilePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error writing to log file: {ex.Message}");
            }
        }
    }
}