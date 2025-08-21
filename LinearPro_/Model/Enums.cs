using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LinearPro_.Model
{
    internal enum Relation { LE, GE, EQ }

    internal enum SignRestriction
    {
        NonNegative,  // +
        NonPositive,  // -
        Unrestricted, // urs
        Integer,      // int
        Binary        // bin
    }
}

