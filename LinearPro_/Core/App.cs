using System;
using System.Collections.Generic;
using LinearPro_.IO;
using LinearPro_.Model;
using LinearPro_.Algorithms;

namespace LinearPro_.Core
{
    internal sealed class App
    {
        private readonly FileService _fileService = new FileService();
        private readonly List<IAlgorithm> _algorithms = new List<IAlgorithm>();
        private LPModel _model;
        private List<string> _lastSolveSteps;

        public App()
        {
            // Reserve space
            _algorithms.Add(new PrimalSimplex());
          //  _algorithms.Add(new RevisedSimplex());
          //  _algorithms.Add(new BranchAndBoundSimplex());
            _algorithms.Add(new CuttingPlane());
         //   _algorithms.Add(new KnapsackBnB());
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
                    case MenuOption.Exit:
                        Console.WriteLine("Goodbye!");
                        return;
                }
                Pause();
            }
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

        private static void PrintMenu()
        {
            Console.WriteLine("[1] Read File");
            Console.WriteLine("[2] Calculate");
            Console.WriteLine("[3] Display Steps");
            Console.WriteLine("[4] Exit");
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
    }
}
