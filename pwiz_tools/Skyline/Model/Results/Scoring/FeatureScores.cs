using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Scoring
{
    public class FeatureScores : IFormattable
    {
        public FeatureScores(FeatureCalculators calculators, IEnumerable<float> values)
        {
            Calculators = calculators;
            Values = ImmutableList.ValueOf(values);
        }
        public FeatureCalculators Calculators { get; }
        public ImmutableList<float> Values { get; }
        public int Count
        {
            get { return Calculators.Count; }
        }

        public float? GetFeature(IPeakFeatureCalculator calc)
        {
            return GetFeature(calc.GetType());
        }
        public float? GetFeature(Type type)
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

        public override string ToString()
        {
            return ToString(null, CultureInfo.CurrentCulture);
        }

        public string ToString(string format, IFormatProvider formatProvider)
        {
            var parts = new List<string>();
            foreach (var calc in Calculators.OrderBy(entry => entry.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                var value = GetFeature(calc);
                if (value.HasValue)
                {
                    parts.Add(calc.Name + @":" + value.Value.ToString(format, formatProvider));
                }
            }

            return new FormattableList<string>(parts).ToString(format, formatProvider);
        }
    }
}