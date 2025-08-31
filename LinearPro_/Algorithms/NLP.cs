using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms.VisualStyles;

//NB: Requires AngouriMath NuGet package
using AngouriMath;
using AngouriMath.Extensions;

namespace LinearPro_.Algorithms
{
    internal class NLP
    {
        public string FunctionString { get; set; }
        public string function => FunctionString.Split('=').Last().Trim();

        public string localMaxOrMin; //Convex(true) means local maximam and concave(false) means local minimum

        public int GetVariableCount()
        {
            var expr = function.ToEntity();
            return expr.Vars.Count();
        }


        public string Differentiate(string function, string variable)
        {
            var expr = function.ToEntity();
            var derivative = expr.Differentiate(variable).Simplify();
            return derivative.ToString();
        }

        // This method performs all operations and returns a list of steps/results
        public List<string> Solve()
        {
            var steps = new List<string>();
            steps.Add("NLP function: " + FunctionString);

            steps.Add("Number of variables = " + GetVariableCount().ToString());
            steps.Add("Now lets check if the function has a local maximum or a local minimum");
            steps.AddRange(ConcavityCheck());

            return steps;
        }

        public List<string> ConcavityCheck()//Concavity means local maximam and convexity means local minimum
        {
            var steps = new List<string>();
            string fx, fxx, fy, fyy, fxy;
            if (GetVariableCount() == 1)
            {
                steps.Add("f(x) = " + function);  
                fx = Differentiate(function,"x");
                steps.Add("f'(x) = " + fx.ToString().Replace(" * ", ""));
                fxx = Differentiate(fx, "x");
                steps.Add("f''(x) = " + fxx.ToString().Replace(" * ", ""));

                //NB: For now we are asuming that fxx is always a numerical value
                if (double.Parse(fxx) < 0)//testing if fxx>0 or <0
                {
                    localMaxOrMin = "Max"; 
                    steps.Add("Since f''(x) < 0, the function is convex and has a local maximum.");
                }
                else if(double.Parse(fxx) > 0)
                {
                    localMaxOrMin = "Min"; 
                    steps.Add("Since f''(x) > 0, the function is concave and has a local minimum.");
                }
                else
                {
                    localMaxOrMin = "Inconclusive";
                    steps.Add("Since f''(x) = 0, the test is inconclusive.");
                }

            }
            else if (GetVariableCount() == 2)
            {
                steps.Add("H = \t⎡ fxx   fxy ⎤\r\n\t⎣ fxy   fyy ⎦\r\n");
                fx = Differentiate(function, "x");
                steps.Add("fx = " + fx.ToString().Replace(" * ", ""));
                fxx = Differentiate(fx, "x");
                steps.Add("fxx = " + fxx.ToString().Replace(" * ", ""));
                fy = Differentiate(function, "y");
                steps.Add("fy = " + fy.ToString().Replace(" * ", ""));
                fyy = Differentiate(fy, "y");
                steps.Add("fyy = " + fyy.ToString().Replace(" * ", ""));
                fxy = Differentiate(fx, "y");
                steps.Add("fxy = " + fxy.ToString().Replace(" * ", ""));

                steps.Add($"\nH = \t⎡ {fxx.ToString().Replace(" * ", "")}   {fxy.ToString().Replace(" * ", "")} ⎤" +
                    $"\r\n\t⎣ {fxy.ToString().Replace(" * ", "")}   {fyy.ToString().Replace(" * ", "")} ⎦\r\n");



                //NB: Again, we assume that fxx, fyy and fxy are always numerical values

                double derH = double.Parse(fxx) * double.Parse(fyy) - Math.Pow(double.Parse(fxy), 2);
                steps.Add("|H| = fxx * fyy - (fxy)^2 = " + derH);
                if (derH > 0 && double.Parse(fxx) > 0)
                {
                    localMaxOrMin = "Min"; 
                    steps.Add("Since |H| > 0 and fxx > 0, the function is concave and has a local minimum.");
                }
                else if(derH > 0 && double.Parse(fxx) < 0)
                {
                    localMaxOrMin = "Max"; 
                    steps.Add("Since |H| > 0 and fxx < 0, the function is convex and has a local maximum.");
                }
                else if(derH < 0)
                {
                    localMaxOrMin = "Saddle Point"; 
                    steps.Add("Since |H| < 0, the function has a saddle point.");
                }
                else
                {
                    localMaxOrMin = "Inconclusive"; 
                    steps.Add("Since |H| = 0, the test is inconclusive.");
                }

                



            }
            else
            {
                steps.Add("The function has more than 2 variables, so we cannot check for concavity/convexity.");
            }

                return steps;
        }
    }
}
