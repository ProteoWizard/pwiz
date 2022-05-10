/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class FeatureNames : AbstractReadOnlyList<string>
    {
        public static readonly FeatureNames EMPTY = new FeatureNames(ImmutableList.Empty<string>());

        private static readonly Dictionary<string, IPeakFeatureCalculator> _calculatorsByTypeName;
        static FeatureNames()
        {
            _calculatorsByTypeName = PeakFeatureCalculator.Calculators.ToDictionary(calc => calc.FullyQualifiedName);
        }
        private readonly ImmutableList<string> _names;
        private readonly Dictionary<string, int> _dictionary;
        private readonly int _hashCode;

        public static FeatureNames FromCalculators(IEnumerable<IPeakFeatureCalculator> calculators)
        {
            return FromScoreTypes(calculators.Select(calc => calc.GetType()));
        }

        public static FeatureNames FromScoreTypes(IEnumerable<Type> types)
        {
            return new FeatureNames(types.Select(type => type.FullName));
        }
        
        public FeatureNames(IEnumerable<string> names)
        {
            _names = ImmutableList.ValueOfOrEmpty(names);
            _hashCode = _names.GetHashCode();
            _dictionary = new Dictionary<string, int>();
            for (int i = 0; i < _names.Count; i++)
            {
                string name = _names[i];
                if (name != null && !_dictionary.ContainsKey(name))
                {
                    _dictionary.Add(name, i);
                }
            }
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
            return IndexOf(calculator.FullyQualifiedName);
        }

        protected bool Equals(FeatureNames other)
        {
            return Equals(_names, other._names);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((FeatureNames) obj);
        }

        public override int GetHashCode()
        {
            return _hashCode;
        }

        public IEnumerable<IPeakFeatureCalculator> AsCalculators()
        {
            return this.Select(CalculatorFromTypeName);
        }

        public IEnumerable<Type> AsCalculatorTypes()
        {
            return AsCalculators().Select(calc => calc?.GetType());
        }

        public static IPeakFeatureCalculator CalculatorFromTypeName(string name)
        {
            _calculatorsByTypeName.TryGetValue(name, out var calculator);
            return calculator;
        }
    }
}
