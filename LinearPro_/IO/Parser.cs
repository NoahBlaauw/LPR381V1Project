using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using LinearPro_.Model;
using ModelConstraint = LinearPro_.Model.Constraint;


namespace LinearPro_.IO
{
    internal sealed class Parser
    {
        public LPModel Parse(string input)
        {
            // Normalize & split lines
            var lines = input
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            if (lines.Count < 2)
                throw new Exception("Input file must contain an objective row and at least one constraints/sign-restrictions row.");

            // 1) Objective line
            // Example:  max +2 +3 +3 +5 +2 +4
            var first = Tokenize(lines[0]);
            if (first.Count < 2) throw new Exception("Invalid objective line.");

            var isMax = string.Equals(first[0], "max", StringComparison.OrdinalIgnoreCase);
            var isMin = string.Equals(first[0], "min", StringComparison.OrdinalIgnoreCase);
            if (!isMax && !isMin) throw new Exception("Objective must start with 'max' or 'min'.");

            var objCoefs = ParseSignedNumbers(first.Skip(1)).ToList();
            if (objCoefs.Count == 0) throw new Exception("No objective coefficients found.");

            // The last line is sign restrictions per spec
            // But we don't know how many constraint lines—assume everything until the last line
            var signLine = Tokenize(lines[lines.Count - 1]);
            var signRestrictions = ParseSignRestrictions(signLine, objCoefs.Count);

            // Constraint lines are lines[1 .. last-1]
            var constraints = new List<ModelConstraint>();
            for (int i = 1; i < lines.Count - 1; i++)
            {
                var tokens = Tokenize(lines[i]);

                // If last token looks like "<=40", split it
                if (tokens.Count == objCoefs.Count + 1)
                {
                    string last = tokens.Last();
                    if (last.StartsWith("<=") || last.StartsWith(">=") || last.StartsWith("="))
                    {
                        string relation = last.StartsWith("<=") ? "<=" :
                                          last.StartsWith(">=") ? ">=" : "=";
                        string rhsStr = last.Substring(relation.Length);
                        tokens[tokens.Count - 1] = relation;
                        tokens.Add(rhsStr);
                    }
                }

                int n = objCoefs.Count;
                if (tokens.Count < n + 2)
                    throw new Exception("Constraint line too short: " + lines[i]);

                var coeffs = ParseSignedNumbers(tokens.Take(n)).ToArray();
                string relTok = tokens[n];
                Relation rel;
                if (relTok == "<=") rel = Relation.LE;
                else if (relTok == ">=") rel = Relation.GE;
                else if (relTok == "=") rel = Relation.EQ;
                else throw new Exception("Invalid relation in constraint: " + relTok);

                double rhs = ParseNumber(tokens[n + 1]);
                constraints.Add(new LinearPro_.Model.Constraint(coeffs, rel, rhs));
            }


            // Build variable labels: X1..Xn + slack/surplus/extra will be generated later by algorithms
            var varCols = new List<string>();
            for (int i = 0; i < objCoefs.Count; i++)
                varCols.Add("X" + (i + 1));

            return new LPModel(
                isMax,
                objCoefs.ToArray(),
                constraints,
                signRestrictions,
                varCols
            );
        }

        private static List<string> Tokenize(string s)
        {
            return s.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        private static IEnumerable<double> ParseSignedNumbers(IEnumerable<string> tokens)
        {
            foreach (var t in tokens)
                yield return ParseNumber(t);
        }

        private static double ParseNumber(string token)
        {
            // Accept "+2", "-3", "2", "3.5"
            return double.Parse(token, NumberStyles.AllowLeadingSign | NumberStyles.Float, CultureInfo.InvariantCulture);
        }

        private static List<SignRestriction> ParseSignRestrictions(List<string> tokens, int varCount)
        {
            if (tokens.Count < varCount)
                throw new Exception("Sign restriction line shorter than number of variables.");

            var res = new List<SignRestriction>();
            for (int i = 0; i < varCount; i++)
            {
                var t = tokens[i].ToLowerInvariant();
                switch (t)
                {
                    case "+": res.Add(SignRestriction.NonNegative); break;
                    case "-": res.Add(SignRestriction.NonPositive); break;
                    case "urs": res.Add(SignRestriction.Unrestricted); break;
                    case "int": res.Add(SignRestriction.Integer); break;
                    case "bin": res.Add(SignRestriction.Binary); break;
                    default: throw new Exception("Unknown sign restriction: " + t);
                }
            }
            return res;
        }
    }
}
