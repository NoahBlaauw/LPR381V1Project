using System;
using System.Collections.Generic;

namespace LinearPro_.Core
{
    internal static class SensitivityMenu
    {
        public static void ShowMenu(App app)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Sensitivity Analysis Menu");
                Console.WriteLine("=========================");
                Console.WriteLine("[1] Check Sensitivity Analysis");
                Console.WriteLine("[2] Change Coefficient");
                Console.WriteLine("[3] Add New Constraint");
                Console.WriteLine("[4] Add New Variable");
                Console.WriteLine("[5] Duality Analysis");
                Console.WriteLine("[6] Back to Main Menu");
                Console.Write("Select option: ");

                var input = Console.ReadKey(intercept: true).KeyChar;
                Console.WriteLine();

                switch (input)
                {
                    case '1':
                        app.RunSensitivityAnalysis();
                        Pause();
                        break;
                    case '2':
                        CoefficientChanger.ChangeCoefficient(app);
                        break;
                    case '3':
                        ConstraintAdder.AddNewConstraint(app);
                        break;
                    case '4':
                        VariableAdder.AddNewVariable(app);
                        break;
                    case '5':
                        DualityAnalyzer.AnalyzeDuality(app);
                        break;
                    case '6':
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        Pause();
                        break;
                }
            }
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.Write("Press any key to continue...");
            Console.ReadKey(true);
        }
    }
}