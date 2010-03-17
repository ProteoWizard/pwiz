using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Chemistry;

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
    public class TracerPercentFormula : Formula<TracerPercentFormula>
    {
    }
    public class TracerPercentEnumerator : IEnumerator<TracerPercentFormula>
    {
        private readonly SortedDictionary<String,TracerDef> _tracerDefs = new SortedDictionary<string, TracerDef>();
        private readonly int _intermediateLevels;
        public TracerPercentEnumerator(ICollection<TracerDef> tracerDefs, int intermediateLevels)
        {
            _intermediateLevels = intermediateLevels;
            foreach (TracerDef tracerDef in tracerDefs)
            {
                _tracerDefs.Add(tracerDef.Name, tracerDef);
            }
        }

        public void Dispose()
        {
        }

        public bool MoveNext()
        {
            var remainingSymbolCounts = new Dictionary<string, int>();
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
                    int remainingSymbolCount = remainingSymbolCounts[tracerDef.TraceeSymbol];
                    int tracerCount = Math.Min(remainingSymbolCount,
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
                int currentLevel = Current.GetElementCount(tracerDef.Name);
                int remainingSymbolCount = remainingSymbolCounts[tracerDef.TraceeSymbol];
                int nextLevel = Math.Min(apesToTry.Max(), remainingSymbolCount + currentLevel);
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
        private List<int> ListApes(TracerDef tracerDef)
        {
            var apes = new List<double>();
            apes.Add(tracerDef.InitialApe);
            for (int i = 0; i < _intermediateLevels; i++)
            {
                var ape = (tracerDef.InitialApe*(_intermediateLevels - i)
                           + tracerDef.FinalApe*(i + 1))/(_intermediateLevels + 1);
                apes.Add(ape);
            }
            apes.Add(tracerDef.FinalApe);
            apes.Sort();
            var result = new List<int>();
            foreach (double ape in apes)
            {
                int apeInt = (int) ape;
                if (result.Count == 0 || apeInt != result[result.Count - 1])
                {
                    result.Add(apeInt);
                }
            }
            return result;
        }
    }
}
