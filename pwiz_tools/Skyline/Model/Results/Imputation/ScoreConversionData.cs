using System;
using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class ScoreConversionData : Immutable
    {
        public static readonly ScoreConversionData EMPTY =
            new ScoreConversionData(ImmutableList.Empty<float>());

        public ScoreConversionData(IEnumerable<float> sortedScores)
        {
            SortedScores = ImmutableList.ValueOf(sortedScores);
        }
        public ImmutableList<float> SortedScores { get; private set; }

        public ScoreConversionData ChangeSortedScores(ImmutableList<float> value)
        {
            return ChangeProp(ImClone(this), im => im.SortedScores = value);
        }

        public double? GetScoreAtPercentile(double value)
        {
            return GetValueAtPercentile(value, SortedScores);
        }
        public double? GetPercentileOfScore(double score)
        {
            return GetPercentileOfValue(score, SortedScores);
        }

        public static double? GetValueAtPercentile(double percentile, IList<float> list)
        {
            if (list.Count == 0)
            {
                return null;
            }

            double doubleIndex = percentile * list.Count;
            if (doubleIndex <= 0)
            {
                return list[0];
            }

            if (doubleIndex >= list.Count - 1)
            {
                return list[list.Count - 1];
            }

            int prevIndex = (int)Math.Floor(doubleIndex);
            int nextIndex = (int)Math.Ceiling(doubleIndex);
            var prevValue = list[prevIndex];
            if (prevIndex == nextIndex)
            {
                return prevValue;
            }
            var nextValue = list[nextIndex];
            return prevValue * (nextIndex - doubleIndex) + nextValue * (doubleIndex - prevIndex);
        }
        public static double? GetPercentileOfValue(double value, IList<float> list)
        {
            if (list.Count == 0)
            {
                return null;
            }
            var index = CollectionUtil.BinarySearch(list, (float)value);
            if (index >= 0)
            {
                return (double)index / list.Count;
            }
            index = ~index;

            if (index <= 0)
            {
                return list[0];
            }

            if (index >= list.Count - 1)
            {
                return list[list.Count - 1];
            }

            double prev = list[index];
            double next = list[index + 1];
            return (index + (value - prev) / (next - prev)) / list.Count;
        }

    }
}
