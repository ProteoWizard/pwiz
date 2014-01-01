/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.ComponentModel;
using System.Globalization;

namespace pwiz.Topograph.Enrichment
{
    /// <summary>
    /// The precursor pool for a data file.  Users can specify a value for the precursor pool to use for a data file
    /// instead of whatever Topograph calculates.
    /// Currently this overridden precursor pool value is just a single percentage, but eventually it
    /// will be able to be any <see cref="TracerPercentFormula" />
    /// </summary>
    [TypeConverter(typeof(PrecursorPoolTypeConverter))]
    public struct PrecursorPoolValue
    {
        public PrecursorPoolValue(double doubleValue) : this()
        {
            DoubleValue = doubleValue;
        }

        public override string ToString()
        {
            return (DoubleValue * 100).ToString(CultureInfo.CurrentCulture) + "%";
        }
        public string ToPersistedString()
        {
            return DoubleValue.ToString(CultureInfo.InvariantCulture);
        }

        public double DoubleValue { get; private set; }

        public static PrecursorPoolValue? ParsePersistedString(string str)
        {
            if (string.IsNullOrEmpty(str))
            {
                return null;
            }
            double doubleValue;
            if (double.TryParse(str, NumberStyles.Float, CultureInfo.InvariantCulture, out doubleValue))
            {
                return new PrecursorPoolValue(doubleValue);
            }
            return null;
        }

        public class PrecursorPoolTypeConverter : TypeConverter
        {
            public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
            {
                return typeof(string) == sourceType || base.CanConvertFrom(context, sourceType);
            }

            public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
            {
                var strValue = value as string;
                if (null != strValue)
                {
                    strValue = strValue.Trim();
                    if (strValue.EndsWith("%"))
                    {
                        strValue = strValue.Substring(0, strValue.Length - 1).Trim();
                    }
                    double doubleValue = double.Parse(strValue, 
                        NumberStyles.AllowDecimalPoint | NumberStyles.AllowThousands, 
                        CultureInfo.CurrentCulture) / 100.0;
                    return new PrecursorPoolValue(doubleValue);
                }
                return base.ConvertFrom(context, culture, value);
            }
        }
    }
}
