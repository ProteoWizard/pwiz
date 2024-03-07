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
            if (x == null)
            {
                return y == null ? 0 : -1;
            }
            else if (y == null)
            {
                return 1;
            }

            // Match decimal parts of both strings
            var matchesX = REGEX.Matches(x);
            var matchesY = REGEX.Matches(y);

            // Compare segments iteratively
            var segmentCount = Math.Min(matchesX.Count, matchesY.Count);

            for (var i = 0; i < segmentCount; i++)
            {
                // Compare non-decimal segments
                var partX = matchesX[i].Groups[1].Value;
                var partY = matchesY[i].Groups[1].Value;
                var comparison = string.Compare(partX, partY, StringComparison.OrdinalIgnoreCase);
                if (comparison != 0)
                {
                    // Compare as decimals
                    var decimalX = ParseLocalizedDecimal(partX);
                    var decimalY = ParseLocalizedDecimal(partY);

                    if (decimalX == null || decimalY == null)
                    {
                        return comparison; // One or both could not be understood as a decimal, return the string comparison
                    }

                    comparison = decimal.Compare(decimalX.Value, decimalY.Value);
                    if (comparison != 0)
                    {
                        return comparison;
                    }
                }
            }

            // If one string has more segments, it should come after
            return matchesX.Count.CompareTo(matchesY.Count);
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
    }
}