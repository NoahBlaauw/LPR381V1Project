using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using System.Linq;

namespace LinearPro_.Model
{
    internal sealed class Constraint
    {
        public double[] Coefficients { get; }
        public Relation Relation { get; }
        public double Rhs { get; }

        public Constraint(double[] coeffs, Relation relation, double rhs)
        {
            Coefficients = coeffs.ToArray();
            Relation = relation;
            Rhs = rhs;
        }
    }
}
