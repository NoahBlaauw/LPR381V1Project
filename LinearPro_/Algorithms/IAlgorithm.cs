using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using LinearPro_.Model;

namespace LinearPro_.Algorithms
{
    internal interface IAlgorithm
    {
        string Name { get; }
        // Returns displayable “steps”
        List<string> Solve(LPModel model);
    }
}
