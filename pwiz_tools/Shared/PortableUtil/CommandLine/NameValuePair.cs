/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Globalization;
using System.IO;
using System.Linq;

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// A parsed "--name[=value]" token, matched against a declared <see cref="ArgumentBase"/>
    /// and exposing strongly-typed coercions (bool/int/double/date/path) that throw the
    /// framework's value exceptions on malformed input.
    /// </summary>
    public class NameValuePair
    {
        public static readonly NameValuePair EMPTY = new NameValuePair(null, null);

        public NameValuePair(string name, string value)
        {
            Name = name;
            Value = value;
        }

        public string Name { get; private set; }
        public string Value { get; private set; }

        public ArgumentBase Match { get; private set; }

        public bool ValueBool
        {
            get
            {
                if (IsNameOnly)
                    return true;
                if (bool.TryParse(Value, out var result))
                    return result;
                throw new ValueInvalidBoolException(Match, Value);
            }
        }

        public int ValueInt
        {
            get
            {
                AssertMatched();
                try
                {
                    return int.Parse(Value);
                }
                catch (FormatException)
                {
                    throw new ValueInvalidIntException(Match, Value);
                }
            }
        }

        public int GetValueInt(int minVal, int maxVal)
        {
            int v = ValueInt;
            if (minVal > v || v > maxVal)
                throw new ValueOutOfRangeIntException(Match, v, minVal, maxVal);
            return v;
        }

        public double ValueDouble
        {
            get
            {
                AssertMatched();
                double valueDouble;
                // Try both local and invariant formats to make batch files more portable
                if (!double.TryParse(Value, out valueDouble) && !double.TryParse(Value, NumberStyles.Float, CultureInfo.InvariantCulture, out valueDouble))
                    throw new ValueInvalidDoubleException(Match, Value);
                return valueDouble;
            }
        }

        public double GetValueDouble(double minVal, double maxVal)
        {
            double v = ValueDouble;
            if (minVal > v || v > maxVal)
                throw new ValueOutOfRangeDoubleException(Match, v, minVal, maxVal);
            return v;
        }

        public DateTime ValueDate
        {
            get
            {
                AssertMatched();
                try
                {
                    // Try local format
                    return Convert.ToDateTime(Value);
                }
                catch (Exception)
                {
                    try
                    {
                        // Try invariant format to make command-line batch files more portable
                        return Convert.ToDateTime(Value, CultureInfo.InvariantCulture);
                    }
                    catch (Exception)
                    {
                        throw new ValueInvalidDateException(Match, Value);
                    }
                }
            }
        }

        public string ValueFullPath
        {
            get
            {
                try
                {
                    if (ArgUsage.IsRemoteUrl(Value))
                        return Value;
                    return Path.GetFullPath(Value);
                }
                catch (Exception)
                {
                    throw new ValueInvalidPathException(Match, Value);
                }
            }
        }

        public bool IsEmpty { get { return string.IsNullOrEmpty(Name); } }
        public bool IsNameOnly { get { return string.IsNullOrEmpty(Value); } }

        public bool IsMatch(ArgumentBase arg)
        {
            if (!Name.Equals(arg.Name))
                return false;
            if (arg.ValueExample == null && !IsNameOnly)
                throw new ValueUnexpectedException(arg);
            if (arg.ValueExample != null)
            {
                if (IsNameOnly)
                {
                    if (!arg.OptionalValue)
                        throw new ValueMissingException(arg);
                }
                else
                {
                    var val = Value;
                    if (arg.Values != null && !arg.HasValueChecking && !arg.Values.Any(v => v.Equals(val, StringComparison.CurrentCultureIgnoreCase)))
                        throw new ValueInvalidException(arg, Value, arg.Values);
                }
            }

            Match = arg;
            return true;
        }

        public bool IsValue(string value)
        {
            return value.Equals(Value, StringComparison.CurrentCultureIgnoreCase);
        }

        private void AssertMatched()
        {
            // Was Assume.IsNotNull(Match) in Skyline. Inlined as a throw to keep PortableUtil
            // a pure-BCL leaf. Indicates a coding defect: a coercion was read before the pair
            // was matched to a declared argument.
            if (Match == null)
                throw new InvalidOperationException(@"NameValuePair value accessed before being matched to an argument.");
        }
    }
}
