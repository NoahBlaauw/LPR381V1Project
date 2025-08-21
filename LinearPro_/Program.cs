using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System;
using LinearPro_.Core;

namespace LinearPro_
{
    internal static class Program
    {
        [STAThread] // Required for OpenFileDialog in a console app
        private static void Main(string[] args)
        {
            Console.Title = "LinearPro_ – Linear/Integer Programming Console";
            Console.OutputEncoding = System.Text.Encoding.UTF8; // Box drawing chars
            var app = new App();
            app.Run();
        }
    }
}
