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

        protected AbstractFeatureCalculatorList(FeatureNameList names)
        {
            FeatureNames = names;
            _list = ImmutableList.ValueOf(names.AsCalculators().Cast<T>());
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

        protected bool Equals(AbstractFeatureCalculatorList<T> other)
        {
            return FeatureNames.Equals(other.FeatureNames);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AbstractFeatureCalculatorList<T>) obj);
        }

        public override int GetHashCode()
        {
            return FeatureNames.GetHashCode();
        }
    }

    public class FeatureCalculators : AbstractFeatureCalculatorList<IPeakFeatureCalculator>
    {
        public static readonly FeatureCalculators ALL = new FeatureCalculators(PeakFeatureCalculator.Calculators);

        public static readonly FeatureCalculators NONE =
            new FeatureCalculators(ImmutableList.Empty<IPeakFeatureCalculator>());
        public FeatureCalculators(IEnumerable<IPeakFeatureCalculator> calculators) : base(calculators)
        {
            Detailed = new DetailedFeatureCalculators(this.OfType<DetailedPeakFeatureCalculator>());
        }

        public FeatureCalculators(FeatureNameList names) : base(names)
        {
        }

        public static FeatureCalculators FromCalculators(IEnumerable<IPeakFeatureCalculator> calculators)
        {
            return calculators as FeatureCalculators ?? new FeatureCalculators(calculators);
        }

        public DetailedFeatureCalculators Detailed { get; }
    }

    public class DetailedFeatureCalculators : AbstractFeatureCalculatorList<DetailedPeakFeatureCalculator>
    {
        public DetailedFeatureCalculators(IEnumerable<DetailedPeakFeatureCalculator> calculators) : base(calculators)
        {
        }
    }
}
