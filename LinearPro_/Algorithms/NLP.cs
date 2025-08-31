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
        public double xlow, xhigh; //Upper and lower bounds for golden section search


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
            if (GetVariableCount() > 2)
            {
                Console.WriteLine("The function has more than 2 variables, apologies but we cannot process this.");
                return steps;
            }
            Console.WriteLine("To solve your NLP with golden section search, we will need a lower and an upper bound:");
            GetBounds();

            steps.Add("Now lets check if the function has a local maximum or a local minimum");
            steps.AddRange(ConcavityCheck());
            if(GetVariableCount() == 1)
            {
                steps.AddRange(GoldenSectionSearch());
            }
            

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

        public List<string> GoldenSectionSearch()
        {
            var steps = new List<string>();
            if (localMaxOrMin.ToLower() != "max" && localMaxOrMin.ToLower() != "min")
            {
                steps.Add("The curviture test for the function was inconclusive or the function lies at a saddle point");
                return steps;
            }
            double r = (Math.Sqrt(5) - 1) / 2; //Golden ratio constant
            double d = r * (xhigh - xlow), x1 = xlow + d, x2 = xhigh - d, fx1 = Substitute("x", x1), fx2 = Substitute("x", x2), stoppingCriteria = xhigh - xlow;
            
            string smallestVar;
            steps.Add($"Initial bounds: xlow = {xlow}, xhigh = {xhigh}");
            steps.Add("xlow\txhigh\td\tx1\tx2\tf(x1)\tf(x2)\tƐ");//NB Make more visually appealing later
            steps.Add(goldenSelectionRow(x1, x2, fx1, fx2, d, stoppingCriteria));


            while (stoppingCriteria>0.05)
            {
                d = r * (xhigh - xlow);
                x1 = xlow + d;
                x2 = xhigh - d;
                fx1 = Substitute("x", x1);
                fx2 = Substitute("x", x2);
                if(localMaxOrMin.ToLower() == "max")
                {
                    if(fx1 > fx2)
                    {
                        xlow = x2;
                    }
                    else
                    {
                        xhigh = x1;
                    }

                }
                else if(localMaxOrMin.ToLower() == "min")
                {
                    if(fx1 > fx2)
                    {
                        xhigh = x1;
                    }
                    else
                    {
                        xlow = x2;
                    }

                }
                
                stoppingCriteria = xhigh - xlow;
                steps.Add(goldenSelectionRow(x1, x2, fx1, fx2, d, stoppingCriteria));
                //break; //Temporary break to avoid infinite loop
            }

            steps.Add($"\nOptimal solution found at x = {(xlow + xhigh) / 2}, f(x) = {Substitute("x", (xlow + xhigh) / 2)}");

            return steps;
        }
        
        public string goldenSelectionRow(double x1, double x2, double fx1,double fx2,double d,double stoppingCriteria)
        {
            return $"{Math.Round(xlow, 3)}\t{Math.Round(xhigh, 3)}\t{Math.Round(d, 3)}\t" +
       $"{Math.Round(x1, 3)}\t{Math.Round(x2, 3)}\t{Math.Round(fx1, 3)}\t" +
       $"{Math.Round(fx2, 3)}\t{Math.Round(stoppingCriteria, 3)}";


        }

        public void GetBounds()//Get bounds for golden search from user
        {
            Console.Write("Enter lower bound (xlow): ");
            while (!double.TryParse(Console.ReadLine(), out xlow))
            {
                Console.Write("Invalid input. Please enter a numeric value for xlow: ");
            }

            Console.Write("Enter upper bound (xhigh): ");
            while (!double.TryParse(Console.ReadLine(), out xhigh) || xhigh <= xlow)
            {
                Console.Write("Invalid input. Upper bound must be numeric and greater than lower bound. Try again: ");
            }

            Console.WriteLine($"Bounds set: xlow = {xlow}, xhigh = {xhigh}");
        }
        public double Substitute(string variable, double value)
        {
            var expr = function.ToEntity();                    
            var substituted = expr.Substitute(variable, value); // Replace variable with value
            return (double)substituted.EvalNumerical();         // Evaluate to double
        }


    }
}
