using System;
using System.Collections.Generic;
using LinearPro_.IO;
using LinearPro_.Model;
using LinearPro_.Algorithms;
using System.Linq;
using LinearPro_.Core;

namespace LinearPro_.Core
{
    internal sealed class App
    {
        private List<double[]> _lastTableau;
        private List<string> _lastColHeads;
        private List<string> _lastRowHeads;

        private List<double[]> _initialTableau;
        private List<string> _initialColHeads;
        private List<string> _initialRowHeads;

        private readonly FileService _fileService = new FileService();
        private readonly List<IAlgorithm> _algorithms = new List<IAlgorithm>();
        private LPModel _model;
        private List<string> _lastSolveSteps;

        public LPModel GetModel() => _model;
        public List<double[]> GetLastTableau() => _lastTableau;
        public List<string> GetLastColHeads() => _lastColHeads;
        public List<string> GetLastRowHeads() => _lastRowHeads;

        public void SetLastTableau(List<double[]> tableau) => _lastTableau = tableau;
        public void SetLastColHeads(List<string> colHeads) => _lastColHeads = colHeads;
        public void SetLastRowHeads(List<string> rowHeads) => _lastRowHeads = rowHeads;
        public List<double[]> GetInitialTableau() => _initialTableau;
        public List<string> GetInitialColHeads() => _initialColHeads;
        public List<string> GetInitialRowHeads() => _initialRowHeads;

        public void SetModel(LPModel model) => _model = model;
        public void SetInitialTableau(List<double[]> tableau) => _initialTableau = tableau;
        public void SetInitialColHeads(List<string> colHeads) => _initialColHeads = colHeads;
        public void SetInitialRowHeads(List<string> rowHeads) => _initialRowHeads = rowHeads;

        public App()
        {
            // Reserve space
            _algorithms.Add(new PrimalSimplex());
            _algorithms.Add(new RevisedSimplex());
            _algorithms.Add(new BranchAndBoundSimplex());
            _algorithms.Add(new CuttingPlane());
            _algorithms.Add(new Knapsack());
        }

        public void Run()
        {
            while (true)
            {
                DrawHeader();
                PrintMenu();

                var input = Console.ReadKey(intercept: true).KeyChar;
                Console.WriteLine();
                if (!char.IsDigit(input)) continue;

                var choice = (MenuOption)(input - '0');
                if (_lastTableau == null && choice == MenuOption.SensitivityAnalysis)
                {
                    Console.WriteLine("Sensitivity analysis is only available after solving a problem.");
                    continue;
                }

                switch (choice)
                {
                    case MenuOption.ReadFile:
                        HandleReadFile();
                        break;
                    case MenuOption.Calculate:
                        HandleCalculate();
                        break;
                    case MenuOption.DisplaySteps:
                        HandleDisplaySteps();
                        break;
                    case MenuOption.SensitivityAnalysis:
                        HandleSensitivityAnalysis();
                        break;
                    case MenuOption.Exit:
                        Console.WriteLine("Goodbye!");
                        return;
                }
                Pause();
            }
        }

        private void HandleSensitivityAnalysis()
        {
            if (_lastTableau == null)
            {
                Console.WriteLine("Sensitivity analysis is only available after solving a problem.");
                Pause();
                return;
            }

            SensitivityMenu.ShowMenu(this);
        }

        private LPModel CreatePseudoModelFromTableau(List<double[]> tableau, List<string> colHeads, List<string> rowHeads)
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



        private static void DrawHeader()
        {
            Console.Clear();
            var w = Math.Max(60, Console.WindowWidth);
            var title = " LinearPro_ – LP/IP Solver Shell ";
            var pad = (w - title.Length) / 2;
            Console.WriteLine(new string('═', w));
            Console.WriteLine(new string(' ', Math.Max(0, pad)) + title);
            Console.WriteLine(new string('═', w));
        }

        private void PrintMenu()
        {
            Console.WriteLine("[1] Read File");
            Console.WriteLine("[2] Calculate");
            Console.WriteLine("[3] Display Steps");
            if (_lastTableau != null) // Only show if we have a solved problem
            {
                Console.WriteLine("[4] Sensitivity Analysis");
                Console.WriteLine("[5] Exit");
            }
            else
            {
                Console.WriteLine("[4] Exit");
            }
            Console.Write("Select option: ");
        }

        private void HandleReadFile()
        {
            try
            {
                var path = _fileService.ChooseInputFile();
                if (string.IsNullOrWhiteSpace(path))
                {
                    Console.WriteLine("No file selected.");
                    return;
                }

                var bar = new LoadingBar();
                bar.Start("Reading file...", ConsoleColor.Red);

                var text = _fileService.ReadAllText(path, onProgress: bar.Report);
                var parser = new Parser();
                _model = parser.Parse(text);

                bar.Complete("File read & model stored.", ConsoleColor.Green);

                // Show a quick preview table
                TableRenderer.RenderModelAsTable(_model);
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error: " + ex.Message);
                Console.ResetColor();
            }
        }

        private void HandleCalculate()
        {
            if (_model == null)
            {
                Console.WriteLine("Please read an input file first.");
                return;
            }

            Console.WriteLine("Choose algorithm:");
            for (int i = 0; i < _algorithms.Count; i++)
            {
                Console.WriteLine($"[{i + 1}] {_algorithms[i].Name}");
            }
            Console.Write("Selection: ");
            var key = Console.ReadKey(intercept: true).KeyChar;
            Console.WriteLine();
            if (!char.IsDigit(key)) return;

            int idx = (key - '0') - 1;
            if (idx < 0 || idx >= _algorithms.Count)
            {
                Console.WriteLine("Invalid selection.");
                return;
            }

            var algo = _algorithms[idx];
            Console.WriteLine($"Running: {algo.Name}");
            _lastSolveSteps = algo.Solve(_model);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("Done.");
            Console.ResetColor();

            if (algo is PrimalSimplex primalSimplex)
            {
                _lastTableau = primalSimplex.GetFinalTableau();
                _lastColHeads = primalSimplex.GetColumnHeaders();
                _lastRowHeads = primalSimplex.GetRowHeaders();

                // Store the initial tableau
                _initialTableau = primalSimplex.GetInitialTableau();
                _initialColHeads = primalSimplex.GetInitialColumnHeaders();
                _initialRowHeads = primalSimplex.GetInitialRowHeaders();
            }
        }

        private void HandleDisplaySteps()
        {
            if (_lastSolveSteps == null || _lastSolveSteps.Count == 0)
            {
                Console.WriteLine("No steps to display. Run Calculate first.");
                return;
            }

            var page = 0;
            const int pageSize = 20;
            while (true)
            {
                Console.Clear();
                Console.WriteLine($"Algorithm Steps (page {page + 1})");
                Console.WriteLine(new string('-', 30));
                var start = page * pageSize;
                for (int i = start; i < Math.Min(start + pageSize, _lastSolveSteps.Count); i++)
                    Console.WriteLine(_lastSolveSteps[i]);

                Console.WriteLine();
                Console.WriteLine("[N]ext page  [P]rev page  [Q]uit");
                var k = Console.ReadKey(true).Key;
                if (k == ConsoleKey.N && (page + 1) * pageSize < _lastSolveSteps.Count) page++;
                else if (k == ConsoleKey.P && page > 0) page--;
                else if (k == ConsoleKey.Q) break;
            }
        }

        private static void Pause()
        {
            Console.WriteLine();
            Console.Write("Press any key to continue...");
            Console.ReadKey(true);
        }

        public void RunSensitivityAnalysis()
        {
            try
            {
                Console.Clear();

                // Display the tableau for reference
                Console.WriteLine("Current Tableau for Reference:");
                var pseudoModel = CreatePseudoModelFromTableau(_lastTableau, _lastColHeads, _lastRowHeads);
                TableRenderer.RenderModelAsTable(pseudoModel);

                Console.WriteLine("\nSensitivity Analysis - Individual Coefficient");
                Console.WriteLine("Enter the coordinate in the format 'Row Column'");
                Console.WriteLine("Examples: 'Z X1', 'C3 X4', 'C10 RHS'");
                Console.Write("Enter coordinate: ");

                string input = Console.ReadLine()?.Trim();
                if (string.IsNullOrWhiteSpace(input))
                {
                    Console.WriteLine("No coordinate specified.");
                    return;
                }

                // Parse the input
                string[] parts = input.Split(' ');
                if (parts.Length < 2)
                {
                    Console.WriteLine("Invalid format. Please use 'Row Column' format.");
                    return;
                }

                string rowName = parts[0];
                string colName = parts[1];

                // If multiple words were used for the column name, combine them
                if (parts.Length > 2)
                {
                    colName = string.Join(" ", parts.Skip(1));
                }

                List<string> results = SensitivityAnalyzer.AnalyzeCoefficient(_model, _lastTableau, _lastColHeads, _lastRowHeads, rowName, colName);

                Console.Clear();
                Console.WriteLine("Sensitivity Analysis Results:");
                Console.WriteLine(new string('=', 50));

                foreach (var result in results)
                {
                    // Format the output to be more readable
                    if (result.Contains("==="))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(result);
                        Console.ResetColor();
                    }
                    else if (result.Contains("Type:") || result.Contains("Variable type:"))
                    {
                        Console.ForegroundColor = ConsoleColor.Cyan;
                        Console.WriteLine(result);
                        Console.ResetColor();
                    }
                    else if (result.Contains("Range:") || result.Contains("Allowable") || result.Contains("Current value:"))
                    {
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine(result);
                        Console.ResetColor();
                    }
                    else if (result.Contains("Note:"))
                    {
                        Console.ForegroundColor = ConsoleColor.Yellow;
                        Console.WriteLine(result);
                        Console.ResetColor();
                    }
                    else
                    {
                        Console.WriteLine(result);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Error during sensitivity analysis: " + ex.Message);
                Console.ResetColor();
            }
        }
    }
}

