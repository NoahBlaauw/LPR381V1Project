using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Threading;

namespace LinearPro_.Core
{
    internal sealed class LoadingBar
    {
        private int _progress;
        private string _caption;
        private ConsoleColor _color;

        public void Start(string caption, ConsoleColor barColor)
        {
            _caption = caption ?? "Loading...";
            _color = barColor;
            _progress = 0;
            Draw();
        }

        // Accept progress from 0..100
        public void Report(int progress)
        {
            _progress = Math.Max(0, Math.Min(100, progress));
            Draw();
            Thread.Sleep(15); // smooth animation
        }

        public void Complete(string caption, ConsoleColor barColor)
        {
            _caption = caption ?? "Completed";
            _color = barColor;
            _progress = 100;
            Draw();
            Console.WriteLine();
        }

        private void Draw()
        {
            int width = Math.Max(40, Console.WindowWidth - 20);
            int barWidth = Math.Max(10, width - 20);
            int filled = (int)(barWidth * (_progress / 100.0));

            Console.CursorVisible = false;
            var left = Console.CursorLeft;
            var top = Console.CursorTop;

            Console.Write("\r"); // carriage return
            Console.Write(_caption.PadRight(18));

            Console.ForegroundColor = _color;
            Console.Write(" [");
            Console.Write(new string('■', filled));
            Console.Write(new string(' ', barWidth - filled));
            Console.Write("] ");
            Console.ResetColor();
            Console.Write($"{_progress,3}%");
        }
    }
}

