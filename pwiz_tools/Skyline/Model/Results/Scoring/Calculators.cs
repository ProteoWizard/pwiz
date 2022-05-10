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
            FeatureNames = FeatureNames.FromCalculators(_list.Cast<IPeakFeatureCalculator>());
        }

        protected AbstractFeatureCalculatorList(FeatureNames names)
        {
            FeatureNames = names;
            _list = ImmutableList.ValueOf(names.AsCalculators().Cast<T>());
        }

        public FeatureNames FeatureNames { get; }

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

        public FeatureCalculators(FeatureNames names) : base(names)
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
