using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class FeatureNameList : AbstractReadOnlyList<string>
    {
        public static readonly FeatureNameList EMPTY = new FeatureNameList(ImmutableList.Empty<string>());
        private readonly ImmutableList<string> _names;
        private readonly Dictionary<string, int> _dictionary;

        public static FeatureNameList FromCalculators(IEnumerable<IPeakFeatureCalculator> calculators)
        {
            return FromScoreTypes(calculators.Select(calc => calc.GetType()));
        }

        public static FeatureNameList FromScoreTypes(IEnumerable<Type> types)
        {
            return new FeatureNameList(types.Select(type => type.FullName));
        }
        
        public FeatureNameList(IEnumerable<string> names)
        {
            _names = ImmutableList.ValueOf(names);
            _dictionary = Enumerable.Range(0, _names.Count).ToDictionary(i => _names[i], i => i);
        }

        public override int Count
        {
            get { return _names.Count; }
        }

        public override string this[int index] => _names[index];

        public override int IndexOf(string item)
        {
            if (_dictionary.TryGetValue(item, out int index))
            {
                return index;
            }

            return -1;
        }

        public int IndexOf(Type type)
        {
            return IndexOf(type.FullName);
        }

        public int IndexOf(IPeakFeatureCalculator calculator)
        {
            return IndexOf(calculator.GetType().FullName);
        }

        protected bool Equals(FeatureNameList other)
        {
            return Equals(_names, other._names);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FeatureNameList) obj);
        }

        public override int GetHashCode()
        {
            return (_names != null ? _names.GetHashCode() : 0);
        }
    }
}
