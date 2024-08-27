/*
 * Original author: Clark Brace <clarkbrace@gmail.com>,
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
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace pwiz.Common.Collections
{
    /// <summary>
    /// Added by Clark Brace (cbrace3)
    /// Natural Sort class. Sorts strings into natural sort order rather than lexicographically.
    /// Designed for sorting file/folders into a more human readable format. In this implementation,
    /// numbers are prioritized over letters and the full value of the number is taken into account rather than
    /// just the independent values of each number in series (2, 22, 3 --> 2, 3, 22)
    /// e.g. (Skyline-64_22c, Skyline-64_3a, Skyline-64_4c --> Skyline-64_3a, Skyline-64_4c, Skyline-64_22c)
    /// e.g. (1AC6, 1AC66, 1AC7, 4C47 --> 1AC6, 1AC7, 1AC66, 4C47)
    /// https://www.pinvoke.net/default.aspx/shlwapi.strcmplogicalw
    /// </summary>
    public class NaturalFilenameComparer
    {
        [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int StrCmpLogicalW(string x, string y);

        //Compare strings with natural sort
        public static int Compare(string x, string y)
        {
            return StrCmpLogicalW(x, y);
        }
    }

    // Similar to the filename comparer, except that decimals are understood
    // e.g. for the filename comparer, "123.4056" comes after "123.456" because 4056 > 456 -
    // it doesn't seem them as two parts of a decimal value
    public class NaturalStringComparer
    {
        // Regular expression pattern to match decimal parts - note it accepts both . and , decimal separator
        private static readonly Regex REGEX = new Regex(@"(\d+(?:[.,]\d+)?|\D+)", RegexOptions.Compiled);

        public static int Compare(string x, string y)
        {
            return Comparer<object>.Default.Compare(MakeCompareKey(x), MakeCompareKey(y));
        }

        private static decimal? ParseLocalizedDecimal(string value)
        {
            // Attempt to parse the decimal value using invariant culture
            if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result))
            {
                return result;
            }
            // If parsing fails, attempt to parse  using  the current culture's settings
            else if (decimal.TryParse(value, NumberStyles.Any, CultureInfo.CurrentCulture , out result))
            {
                return result;
            }
            return null; // Didn't parse
        }

        public static IStructuralComparable MakeCompareKey(string s)
        {
            if (s == null)
            {
                return null;
            }
            var stringParts = new List<string>();
            var decimalParts = new List<decimal>();
            foreach (Match segment in REGEX.Matches(s))
            {
                var stringPart = segment.Groups[1].Value;
                decimalParts.Add(ParseLocalizedDecimal(stringPart) ?? decimal.MaxValue);
                stringParts.Add(stringPart);
            }

            int segmentCount = stringParts.Count;
            if (segmentCount == 0)
            {
                return Tuple.Create(decimal.MinValue, string.Empty);
            }
            IStructuralComparable tuple = Tuple.Create(decimalParts[segmentCount - 1], stringParts[segmentCount - 1]);
            for (int i = segmentCount - 2; i >= 0; i--)
            {
                tuple = Tuple.Create(decimalParts[i], stringParts[i], tuple);
            }

            return tuple;
        }
    }
}