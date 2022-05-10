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
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class FeatureScores : IFormattable
    {
        public FeatureScores(FeatureNames featureNames, IEnumerable<float> values)
        {
            FeatureNames = featureNames;
            Values = ImmutableList.ValueOf(values);
        }
        public FeatureNames FeatureNames { get; }
        public ImmutableList<float> Values { get; }
        public int Count
        {
            get { return FeatureNames.Count; }
        }

        public float? GetFeature(IPeakFeatureCalculator calc)
        {
            return GetFeature(calc.GetType());
        }
        public float? GetFeature(Type type)
        {
            var index = FeatureNames.IndexOf(type);
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

        public override string ToString()
        {
            return ToString(null, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var parts = new List<KeyValuePair<string, double>>();
            for (int i = 0; i < Count; i++)
            {
                var value = Values[i];
                if (float.IsNaN(value))
                {
                    continue;
                }

                var typeName = FeatureNames[i];
                string caption = FeatureNames.CalculatorFromTypeName(FeatureNames[i])?.Name ?? typeName;
                parts.Add(new KeyValuePair<string, double>(caption, value));
            }

            var items = new List<string>();
            foreach (var part in parts.OrderBy(part => part.Key, StringComparer.CurrentCultureIgnoreCase))
            {
                items.Add(string.Format(Resources.AlignedFile_AlignLibraryRetentionTimes__0__1__, part.Key, part.Value.ToString(format, formatProvider)));
            }
            return new FormattableList<string>(items).ToString(format, formatProvider);
        }

        protected bool Equals(FeatureScores other)
        {
            return Equals(FeatureNames, other.FeatureNames) && Equals(Values, other.Values);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((FeatureScores) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (FeatureNames.GetHashCode() * 397) ^ Values.GetHashCode();
            }
        }
    }
}