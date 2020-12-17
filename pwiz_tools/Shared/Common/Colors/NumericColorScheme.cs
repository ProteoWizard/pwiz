/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Drawing;
using System.Linq;
using pwiz.Common.DataAnalysis.Clustering;

namespace pwiz.Common.Colors
{
    public class NumericColorScheme : IColorScheme
    {
        public double MaxValue { get; set; }
        public double MinValue { get; set; }

        public void  AddValues(IEnumerable values)
        {
            double minValue = MinValue;
            double maxValue = MaxValue;
            foreach (var value in values.OfType<object>().Select(ToDouble).OfType<double>())
            {
                minValue = Math.Min(minValue, value);
                maxValue = Math.Max(maxValue, value);
            }

            MinValue = minValue;
            MaxValue = maxValue;
        }

        public Color? GetColor(object value)
        {
            var doubleValue = ToDouble(value);
            if (!doubleValue.HasValue)
            {
                return null;
            }

            int lightness;
            double maxAbsValue = Math.Max(MaxValue, MinValue);
            if (Math.Abs(doubleValue.Value) < maxAbsValue)
            {
                lightness = (int)((maxAbsValue - Math.Abs(doubleValue.Value)) * (255 / maxAbsValue));
            }
            else
            {
                lightness = 0;
            }

            if (doubleValue >= 0)
            {
                return Color.FromArgb(255, lightness, lightness);
            }
            else
            {
                return Color.FromArgb(lightness, lightness, 255);
            }
        }

        public static double? ToDouble(object value)
        {
            return ClusterRole.ToDouble(value);
        }
    }
}
