using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public abstract class AbstractFeatureCalculatorList<T> : IReadOnlyList<T> where T : IPeakFeatureCalculator
    {
        private ImmutableList<T> _list;
        protected AbstractFeatureCalculatorList(IEnumerable<T> calculators)
        {
            _list = ImmutableList.ValueOf(calculators);
            FeatureNames = FeatureNameList.FromCalculators(_list.Cast<IPeakFeatureCalculator>());
        }

        public FeatureNameList FeatureNames { get; }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<T> GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        public int Count
        {
            get { return _list.Count; }
        }

        public T this[int index] => _list[index];

        public int IndexOf(IPeakFeatureCalculator calculator)
        {
            return FeatureNames.IndexOf(calculator);
        }

        public int IndexOf(Type type)
        {
            return FeatureNames.IndexOf(type);
        }
    }

    public class FeatureCalculators : AbstractFeatureCalculatorList<IPeakFeatureCalculator>
    {
        public static readonly FeatureCalculators ALL = new FeatureCalculators(PeakFeatureCalculator.Calculators);
        public FeatureCalculators(IEnumerable<IPeakFeatureCalculator> calculators) : base(calculators)
        {
            Detailed = new DetailedFeatureCalculators(this.OfType<DetailedPeakFeatureCalculator>());
        }

        public DetailedFeatureCalculators Detailed { get; }
    }

    public class DetailedFeatureCalculators : AbstractFeatureCalculatorList<DetailedPeakFeatureCalculator>
    {
        public DetailedFeatureCalculators(IEnumerable<DetailedPeakFeatureCalculator> calculators) : base(calculators)
        {
        }
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
            if (index >= 0)
            {
                var value = Values[index];
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
