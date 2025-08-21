using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinearPro_.Model
{
    internal sealed class VariableInfo
    {
        public string Name { get; }
        public SignRestriction Restriction { get; }

        public VariableInfo(string name, SignRestriction restriction)
        {
            Name = name;
            Restriction = restriction;
        }
    }
}
