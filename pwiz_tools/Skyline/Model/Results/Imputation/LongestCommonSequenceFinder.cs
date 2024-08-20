using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class LongestCommonSequenceFinder<TSymbol>
    {
        public LongestCommonSequenceFinder(IEnumerable<IEnumerable<TSymbol>> lists)
        {
            var rankingDictionaries = new List<Dictionary<TSymbol, int>>();
            var allSymbols = new List<TSymbol>();
            var symbolSet = new HashSet<TSymbol>();
            foreach (var list in lists)
            {
                var dictionary = new Dictionary<TSymbol, int>();
                foreach (var symbol in list)
                {
                    dictionary.Add(symbol, dictionary.Count);
                    if (symbolSet.Add(symbol))
                    {
                        allSymbols.Add(symbol);
                    }
                }

                rankingDictionaries.Add(dictionary);
            }

            var rows = new List<Row>();
            foreach (var symbol in allSymbols)
            {
                var replicateRankings = new List<double>();
                foreach (var dictionary in rankingDictionaries)
                {
                    if (dictionary.TryGetValue(symbol, out int ordinal))
                    {
                        replicateRankings.Add((double) ordinal / dictionary.Count);
                    }
                    else
                    {
                        replicateRankings.Add(double.NaN);
                    }
                }
                rows.Add(new Row(symbol, replicateRankings));
            }

            Rows = ImmutableList.ValueOf(rows);
        }

        public ImmutableList<Row> Rows { get; }

        public class Row : Immutable
        {
            public Row(TSymbol symbol, IEnumerable<double> replicateRankings)
            {
                Symbol = symbol;
                ReplicateRankings = ImmutableList.ValueOf(replicateRankings);
                MinRanking = ReplicateRankings.Where(d => !double.IsNaN(d)).Min();
                MaxRanking = ReplicateRankings.Where(d=>!double.IsNaN(d)).Max();
            }


            public TSymbol Symbol { get; }
            public ImmutableList<double> ReplicateRankings { get; }
            public double MinRanking { get; }
            public double MaxRanking { get; }

            public bool IsCompatible(Row other)
            {
                int consensusComparison = 0;
                for (int i = 0; i < Math.Min(ReplicateRankings.Count, other.ReplicateRankings.Count); i++)
                {
                    var myValue = ReplicateRankings[i];
                    var otherValue = other.ReplicateRankings[i];
                    if (double.IsNaN(myValue) || double.IsNaN(otherValue))
                    {
                        continue;
                    }

                    int compareToResult = Math.Sign(myValue.CompareTo(otherValue));
                    if (consensusComparison != 0 && consensusComparison != compareToResult)
                    {
                        return false;
                    }

                    consensusComparison = compareToResult;
                }

                return true;
            }
        }

        public IEnumerable<TSymbol> GetLongestCommonSubsequence()
        {
            return GreedyGetLongestSubset(Rows.OrderBy(row => row.MaxRanking - row.MinRanking)).OrderBy(row=>row.MinRanking).Select(row => row.Symbol);
        }

        public IEnumerable<Row> GreedyGetLongestSubset(IEnumerable<Row> rows)
        {
            var resultRows = new List<Row>();
            foreach (var row in rows)
            {
                if (resultRows.All(row.IsCompatible))
                {
                    resultRows.Add(row);
                }
            }

            return resultRows;
        }

    }
}
