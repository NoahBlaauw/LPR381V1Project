using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace LinearPro_.IO
{
    internal sealed class NLPParser
    {
        //NB: Splits the NLP string into a list of terms (handles + and - as separators)
        public List<string> SplitTerms(string nlpContent)
        {
            if (string.IsNullOrWhiteSpace(nlpContent))
                return new List<string>();

            // Remove "f(x)=" or similar prefix if present
            var expr = nlpContent.Trim();
            var eqIdx = expr.IndexOf('=');
            if (eqIdx >= 0)
                expr = expr.Substring(eqIdx + 1).Trim();

            // Use regex to split on '+' and '-' but keep the sign with the term
            var matches = Regex.Matches(expr, @"([+-]?\s*[^+-]+)");
            var terms = new List<string>();
            foreach (Match m in matches)
            {
                var term = m.Value.Trim();
                if (!string.IsNullOrEmpty(term))
                    terms.Add(term);
            }
            return terms;
        }
    }
}
