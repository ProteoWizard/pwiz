using System.Collections.Generic;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class ScoreQValueMap
    {
        public static readonly ScoreQValueMap EMPTY = new ScoreQValueMap(ImmutableSortedList<float, float>.EMPTY);
        private readonly ImmutableSortedList<float, float> _sortedList;
        public ScoreQValueMap(ImmutableSortedList<float, float> sortedList)
        {
            _sortedList = sortedList;
        }

        public double? GetQValue(double? score)
        {
            if (_sortedList.Count == 0 || !score.HasValue)
            {
                return null;
            }
            int index = _sortedList.BinarySearch((float) score, true);
            if (index >= 0)
            {
                return _sortedList.Values[index];
            }

            index = ~index;
            if (index <= 0)
            {
                return 1;
            }

            if (index >= _sortedList.Count)
            {
                return _sortedList.Values[_sortedList.Count - 1];
            }

            double leftDifference = score.Value - _sortedList.Keys[index - 1];
            double rightDifference = _sortedList.Keys[index] - score.Value;
            double totalDifference = _sortedList.Keys[index] - _sortedList.Keys[index - 1];
            if (totalDifference == 0)
            {
                return _sortedList.Keys[index];
            }

            return (leftDifference * _sortedList.Values[index] + rightDifference * _sortedList.Values[index - 1]) /
                   totalDifference;
        }

        public static ScoreQValueMap FromDocument(SrmDocument document)
        {
            var uniqueScores = new HashSet<float>();
            var entries = new List<KeyValuePair<float, float>>();
            foreach (var transitionGroupDocNode in document.MoleculeTransitionGroups)
            {
                if (transitionGroupDocNode.Results == null)
                {
                    continue;
                }

                foreach (var chromInfoList in transitionGroupDocNode.Results)
                {
                    foreach (var chromInfo in chromInfoList)
                    {
                        if (chromInfo.UserSet == UserSet.TRUE || !chromInfo.QValue.HasValue || !chromInfo.ZScore.HasValue)
                        {
                            continue;
                        }

                        if (!uniqueScores.Add(chromInfo.ZScore.Value))
                        {
                            continue;
                        }
                        entries.Add(new KeyValuePair<float, float>(chromInfo.ZScore.Value, chromInfo.QValue.Value));
                    }
                }
            }

            return new ScoreQValueMap(ImmutableSortedList.FromValues(entries));
        }
    }
}
