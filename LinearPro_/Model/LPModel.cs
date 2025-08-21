using System.Collections.Generic;
using System.Linq;

namespace LinearPro_.Model
{
    internal sealed class LPModel
    {
        public bool IsMax { get; }
        public double[] ObjectiveCoefficients { get; }
        public List<Constraint> Constraints { get; }
        public List<SignRestriction> SignRestrictions { get; }
        public List<string> VariableColumns { get; } // X1..Xn (+ later S/E cols per algorithm)

        public LPModel(
            bool isMax,
            double[] objectiveCoefficients,
            List<Constraint> constraints,
            List<SignRestriction> signRestrictions,
            List<string> variableColumns)
        {
            IsMax = isMax;
            ObjectiveCoefficients = objectiveCoefficients.ToArray();
            Constraints = constraints;
            SignRestrictions = signRestrictions;
            VariableColumns = variableColumns;
        }
    }
}

