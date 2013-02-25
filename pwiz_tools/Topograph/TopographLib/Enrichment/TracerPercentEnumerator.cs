using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;

namespace pwiz.Topograph.Enrichment
{
    /// <summary>
    /// Represents the amount of tracers present in a molecule.
    /// This is similar to TracerFormula, except the numbers in this object
    /// represent percentages.
    /// The sum of the percentages for a given tracee symbol cannot exceed 100.
    /// For instance, if there are tracers Argsix,Argten, and Leuthree, the following are valid
    /// TracerPercentFormulae:
    /// Argsix75Leuthree75
    /// Argsix50Argten50Leuthree100
    /// but the following is not:
    /// Argsix75Argten75
    /// </summary>
    public class TracerPercentFormula : AbstractFormula<TracerPercentFormula, double>
    {
        public static TracerPercentFormula Parse(string formula)
        {
            var result = new Dictionary<string, double>();
            string currentElement = null;
            StringBuilder currentQuantity = new StringBuilder();
            for (int ich = 0; ich < formula.Length; ich++)
            {
                char ch = formula[ich];
                if (ch == '%')
                {
                    continue;
                }
                
                if (char.IsDigit(ch) || ch == '.')
                {
                    currentQuantity.Append(ch);
                }
                else if (Char.IsUpper(ch))
                {
                    if (currentElement != null)
                    {
                        double quantity = currentQuantity.Length == 0 ? 100 : double.Parse(currentQuantity.ToString());
                        if (result.ContainsKey(currentElement))
                        {
                            result[currentElement] = result[currentElement] + quantity;
                        }
                        else
                        {
                            result[currentElement] = quantity;
                        }
                    }
                    currentQuantity = new StringBuilder();
                    currentElement = "" + ch;
                }
                else if (Char.IsLower(ch))
                {
                    currentElement = currentElement + ch;
                }
            }
            if (currentElement != null)
            {
                double quantity = currentQuantity.Length == 0 ? 100 : double.Parse(currentQuantity.ToString());
                if (result.ContainsKey(currentElement))
                {
                    result[currentElement] = result[currentElement] + quantity;
                }
                else
                {
                    result[currentElement] = quantity;
                }
            }
            return new TracerPercentFormula { Dictionary = ImmutableSortedList.FromValues(result) };
        }
        public override String ToString()
        {
            var result = new StringBuilder();
            foreach (var entry in this)
            {
                result.Append(entry.Key);
                result.Append(entry.Value);
                result.Append("%");
            }
            return result.ToString();
        }
    }
    public class TracerPercentEnumerator : IEnumerator<TracerPercentFormula>
    {
        private readonly SortedDictionary<String,TracerDef> _tracerDefs = new SortedDictionary<string, TracerDef>();
        private readonly int _intermediateLevels;
        private readonly double _stepsize;
        public TracerPercentEnumerator(ICollection<TracerDef> tracerDefs, int intermediateLevels)
        {
            _intermediateLevels = intermediateLevels;
            foreach (TracerDef tracerDef in tracerDefs)
            {
                _tracerDefs.Add(tracerDef.Name, tracerDef);
            }
        }
        public TracerPercentEnumerator(ICollection<TracerDef> tracerDefs) : this(tracerDefs, 0)
        {
            _stepsize = 1;
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            var remainingSymbolCounts = new Dictionary<string, double>();
            foreach (var tracerDef in _tracerDefs.Values)
            {
                if (remainingSymbolCounts.ContainsKey(tracerDef.TraceeSymbol))
                {
                    continue;
                }
                remainingSymbolCounts.Add(tracerDef.TraceeSymbol, 100);
            }
            if (Current == null)
            {
                Current = TracerPercentFormula.Empty;
                foreach (var tracerDef in _tracerDefs.Values)
                {
                    double remainingSymbolCount = remainingSymbolCounts[tracerDef.TraceeSymbol];
                    double tracerCount = Math.Min(remainingSymbolCount,
                                               (int) Math.Min(tracerDef.InitialApe, tracerDef.FinalApe));
                    Current = Current.SetElementCount(tracerDef.Name, tracerCount);
                    remainingSymbolCounts[tracerDef.TraceeSymbol] = remainingSymbolCount - tracerCount;
                }
                return true;
            }
            foreach (var entry in Current)
            {
                var tracerDef = _tracerDefs[entry.Key];
                remainingSymbolCounts[tracerDef.TraceeSymbol] = remainingSymbolCounts[tracerDef.TraceeSymbol]
                                                                - entry.Value;
            }
            foreach (var tracerDef in _tracerDefs.Values)
            {
                var apesToTry = ListApes(tracerDef);
                double currentLevel = Current.GetElementCount(tracerDef.Name);
                double remainingSymbolCount = remainingSymbolCounts[tracerDef.TraceeSymbol];
                double nextLevel = Math.Min(apesToTry.Max(), remainingSymbolCount + currentLevel);
                if (nextLevel > currentLevel)
                {
                    foreach (var level in apesToTry)
                    {
                        if (level <= currentLevel)
                        {
                            continue;
                        }
                        nextLevel = Math.Min(nextLevel, level);
                    }
                    Current = Current.SetElementCount(tracerDef.Name, nextLevel);
                    return true;
                }
                nextLevel = Math.Min(remainingSymbolCount, apesToTry.Min());
                Current = Current.SetElementCount(tracerDef.Name, nextLevel);
                remainingSymbolCounts[tracerDef.TraceeSymbol]
                    = remainingSymbolCount + currentLevel - nextLevel;
            }
            return false;
        }

        public void Reset()
        {
            Current = null;
        }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        public TracerPercentFormula Current
        {
            get; private set;
        }
        private List<double> ListApes(TracerDef tracerDef)
        {
            var apes = new List<double>();
            if (_stepsize != 0)
            {
                double ape1 = Math.Min(tracerDef.InitialApe, tracerDef.FinalApe);
                double ape2 = Math.Max(tracerDef.InitialApe, tracerDef.FinalApe);
                for (double ape = ape1; ape < ape2; ape += _stepsize)
                {
                    apes.Add(ape);
                }
                apes.Add(ape2);
            }
            else
            {
                apes.Add(tracerDef.InitialApe);
                for (int i = 0; i < _intermediateLevels; i++)
                {
                    var ape = (tracerDef.InitialApe * (_intermediateLevels - i)
                               + tracerDef.FinalApe * (i + 1)) / (_intermediateLevels + 1);
                    apes.Add(ape);
                }
                apes.Add(tracerDef.FinalApe);
                apes.Sort();
            }
            
            var result = new List<double>();
            foreach (double ape in apes)
            {
                double apeInt = Math.Floor(ape);
                if (result.Count == 0 || apeInt != result[result.Count - 1])
                {
                    result.Add(apeInt);
                }
            }
            return result;
        }
    }
}
