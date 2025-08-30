using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
//NB: Requires AngouriMath NuGet package
using AngouriMath;
using AngouriMath.Extensions;

namespace LinearPro_.Algorithms
{
    internal class NLP
    {
        public string FunctionString { get; set; }
        public string function => FunctionString.Split('=').Last().Trim();


        public string Differentiate(string variable)
        {
            var expr = FunctionString.ToEntity();
            var derivative = expr.Differentiate(variable).Simplify();
            return derivative.ToString();
        }

        // This method performs all operations and returns a list of steps/results
        public List<string> Solve()
        {
            var steps = new List<string>();
            steps.Add("NLP function: " + FunctionString);

            // Add more steps/results as needed
            // steps.Add("Operation 1 result: ...");
            // steps.Add("Operation 2 result: ...");

            return steps;
        }
    }
}
