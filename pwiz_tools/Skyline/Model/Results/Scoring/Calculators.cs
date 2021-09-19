using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class FeatureCalculators : IReadOnlyList<IPeakFeatureCalculator>
    {
        public static FeatureCalculators ALL = new FeatureCalculators(PeakFeatureCalculator.Calculators);
        private ImmutableList<IPeakFeatureCalculator> _all;
        private Dictionary<Type, int> _allIndexes;
        public FeatureCalculators(IEnumerable<IPeakFeatureCalculator> calculators)
        {
            _all = ImmutableList.ValueOf(calculators);
            _allIndexes = CollectionUtil.SafeToDictionary(Enumerable.Range(0, _all.Count)
                .Select(i => new KeyValuePair<Type, int>(_all[i].GetType(), i)));
            Detailed = ImmutableList.ValueOf(_all.OfType<DetailedPeakFeatureCalculator>());
            Summary = ImmutableList.ValueOf(_all.OfType<SummaryPeakFeatureCalculator>());
        }

        public int Count
        {
            get { return _all.Count; }
        }

        public int? IndexOf(Type type)
        {
            if (_allIndexes.TryGetValue(type, out int index))
            {
                return index;
            }

            return null;
        }


        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<IPeakFeatureCalculator> GetEnumerator()
        {
            return _all.GetEnumerator();
        }

        public IPeakFeatureCalculator this[int index] => _all[index];

        public ImmutableList<DetailedPeakFeatureCalculator> Detailed { get; }
        public ImmutableList<SummaryPeakFeatureCalculator> Summary { get; }
    }

    public class FeatureValues
    {
        public FeatureValues(FeatureCalculators calculators, ImmutableList<float> values)
        {
            Calculators = calculators;
            Values = values;
        }
        public FeatureCalculators Calculators { get; }
        public ImmutableList<float> Values { get; }

        public float? GetValue(IPeakFeatureCalculator calc)
        {
            return GetValue(calc.GetType());
        }
        public float? GetValue(Type type)
        {
            var index = Calculators.IndexOf(type);
            if (index.HasValue)
            {
                var value = Values[index.Value];
                if (float.IsNaN(value))
                {
                    return null;
                }

                return value;
            }

            return null;
        }
    }
}
