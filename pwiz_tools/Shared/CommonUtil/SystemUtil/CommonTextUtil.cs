/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace pwiz.Common.SystemUtil
{
    public static class CommonTextUtil
    {
        /// <summary>
        /// This function can be used as a replacement for String.Join(" ", ...)
        /// </summary>
        /// <param name="values">A set of strings to be separated by spaces</param>
        /// <returns>A single string containing the original set separated by spaces</returns>
        public static string SpaceSeparate(params string[] values)
        {
            return SpaceSeparate(values.AsEnumerable());
        }

        public static string SpaceSeparate(IEnumerable<string> values)
        {
            var sb = new StringBuilder();
            foreach (string value in values)
            {
                if (sb.Length > 0)
                    sb.Append(@" ");
                sb.Append(value);
            }
            return sb.ToString();
        }
        /// <summary>
        /// This function can be used as a replacement for String.Join("\n", ...)
        /// </summary>
        /// <param name="lines">A set of strings to be on separate lines</param>
        /// <returns>A single string containing the original set separated by new lines</returns>
        public static string LineSeparate(IEnumerable<string> lines)
        {
            var sb = new StringBuilder();
            foreach (string line in lines)
            {
                if (sb.Length > 0)
                    sb.AppendLine();
                sb.Append(line);
            }
            return sb.ToString();
        }

        public static string LineSeparate(params string[] lines)
        {
            return LineSeparate((IEnumerable<string>) lines);
        }

        /// <summary>
        /// Encrypts a string. This encryption uses the user's (i.e. not machine) key, so it is 
        /// appropriate for strings that are marked with the [UserScopedSetting].
        /// It is not appropriate for any setting marked [ApplicationScopedSetting]
        /// </summary>
        public static string EncryptString(string str)
        {
            return Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(str), null, DataProtectionScope.CurrentUser));
        }

        public static string DecryptString(string str)
        {
            return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(str), null, DataProtectionScope.CurrentUser));
        }
        public static bool IsUtf8(byte[] bytes)
        {
            try
            {
                var encoding = new UTF8Encoding(false, true);
                _ = encoding.GetCharCount(bytes);
                return true;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        public static bool LinesEquivalentIgnoringTimeStampsAndGUIDs(string lineExpected, string lineActual,
            Dictionary<int, double> columnTolerances = null) // Per-column numerical tolerances if strings can be read as TSV, "-1" means any column
        {
            if (string.Equals(lineExpected, lineActual))
            {
                return true; // Identical
            }

            // If only difference appears to be a generated GUID, let it pass
            var regexGUID =
                new Regex(
                    @"(.*)\:[0123456789abcdef]*-[0123456789abcdef]*-[0123456789abcdef]*-[0123456789abcdef]*-[0123456789abcdef]*\:(.*)");
            var matchExpected = regexGUID.Match(lineExpected);
            var matchActual = regexGUID.Match(lineActual);
            if (matchExpected.Success && matchActual.Success
                                      && Equals(matchExpected.Groups[1].ToString(), matchActual.Groups[1].ToString())
                                      && Equals(matchExpected.Groups[2].ToString(), matchActual.Groups[2].ToString()))
            {
                return true;
            }

            // If only difference appears to be a generated ISO timestamp, let it pass
            // e.g. 2020-07-10T10:40:03Z or 2020-07-10T10:40:03-07:00 etc or just 2020-07-10T10:40:03 (no timezone)
            var regexTimestamp =
                new Regex(@"(.*"")\d\d\d\d\-\d\d\-\d\dT\d\d\:\d\d\:\d\d(?:Z|(?:[\-\+]\d\d\:\d\d))?("".*)");
            matchExpected = regexTimestamp.Match(lineExpected);
            matchActual = regexTimestamp.Match(lineActual);
            if (matchExpected.Success && matchActual.Success
                                      && Equals(matchExpected.Groups[1].ToString(), matchActual.Groups[1].ToString())
                                      && Equals(matchExpected.Groups[2].ToString(), matchActual.Groups[2].ToString()))
            {
                return true;
            }

            if (columnTolerances != null)
            {
                // ReSharper disable PossibleNullReferenceException
                var colsActual = lineActual.Split('\t');
                var colsExpected = lineExpected.Split('\t');
                // ReSharper restore PossibleNullReferenceException
                if (colsExpected.Length == colsActual.Length)
                {
                    for (var c = 0; c < colsActual.Length; c++)
                    {
                        if (colsActual[c] != colsExpected[c])
                        {
                            // See if there's a tolerance for this column, or a default tolerance (column "-1" in the dictionary)
                            if ((!columnTolerances.TryGetValue(c, out var tolerance) && !columnTolerances.TryGetValue(-1, out tolerance)) || // No tolerance given for this column
                                !(TryParseDoubleUncertainCulture(colsActual[c], out var valActual) &&
                                  TryParseDoubleUncertainCulture(colsExpected[c], out var valExpected)) || // One or both don't parse as doubles
                                (Math.Abs(valActual - valExpected) > tolerance + tolerance / 1000)) // Allow for rounding cruft
                            {
                                return false; // Can't account for difference
                            }
                        }
                    }
                    return true; // Differences accounted for
                }
            }

            return false; // Could not account for difference
        }

        public static bool TryParseDoubleUncertainCulture(string valString, out double dval)
        {
            if (!double.TryParse(valString, NumberStyles.Float, CultureInfo.InvariantCulture, out dval) &&
                !double.TryParse(valString.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out dval))
            {
                return false;
            }
            return true;
        }

        public static bool TryParseFloatUncertainCulture(string valString, out float fval)
        {
            if (!float.TryParse(valString, NumberStyles.Float, CultureInfo.InvariantCulture, out fval) &&
                !float.TryParse(valString.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out fval))
            {
                return false;
            }
            return true;
        }
    }
}
