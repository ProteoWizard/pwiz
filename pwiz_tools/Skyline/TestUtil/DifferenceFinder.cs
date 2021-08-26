/*
 * Original author: Brian Pratt <bspratt .at. protein.ms>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using System.IO;
using System.Text.RegularExpressions;

namespace pwiz.SkylineTestUtil
{
    public class DifferenceFinder
    {
        /// <summary>
        /// Compare two strings (should not include newlines) ignoring differences that are due
        /// to per-run generated values such as GUIDs or timestamps, and  optionally allowing for
        /// small differences in tab separated columnar numerical values.
        /// </summary>
        /// <param name="lineExpected">expected value</param>
        /// <param name="lineActual">actual value</param>
        /// <param name="columnTolerances">optional dictionary of zero-based column indexes and allowable difference tolerances</param>
        /// <returns>true if lines are equivalent</returns>
        public static bool LinesEquivalentIgnoringTimeStampsAndGUIDs(string lineExpected, string lineActual,
            Dictionary<int, double> columnTolerances = null)
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
            // e.g. 2020-07-10T10:40:03Z or 2020-07-10T10:40:03-07:00 etc
            var regexTimestamp =
                new Regex(@"(.*"")\d\d\d\d\-\d\d\-\d\dT\d\d\:\d\d\:\d\d(?:Z|(?:[\-\+]\d\d\:\d\d))("".*)");
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
                            double valActual, valExpected;
                            if (!columnTolerances.ContainsKey(c) || // No tolerance given
                                !(double.TryParse(colsActual[c], out valActual) &&
                                  double.TryParse(colsExpected[c], out valExpected)) || // One or both don't parse as doubles
                                (Math.Abs(valActual - valExpected) >
                                 columnTolerances[c] + columnTolerances[c] / 1000)) // Allow for rounding cruft
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

        /// <summary>
        /// Compare two strings (assumed to be multi-line values separated by newlines), ignoring
        /// differences that are due to per-run generated values such as GUIDs or timestamps, and
        /// optionally allowing for small differences in tab separated columnar numerical values
        /// </summary>
        /// <param name="expected">expected value</param>
        /// <param name="actual">actual value</param>
        /// <param name="failureMessage">contains message describing the mismatch when this function return false</param>
        /// <param name="columnTolerances">optional dictionary of zero-based column indexes and allowable difference tolerances</param>
        /// <returns>false if no meaningful differences found, otherwise returns true and sets failureMessage to a string describing the difference</returns>
        public static bool DiffIgnoringTimeStampsAndGUIDs(string expected, string actual, out string failureMessage, Dictionary<int, double> columnTolerances = null)
        {
            string GetEarlyEndingMessage(string name, int count, string lineEqualLast, string lineNext, TextReader reader)
            {
                int linesRemaining = 0;
                while (reader.ReadLine() != null)
                    linesRemaining++;

                return string.Format(@"{0} stops at line {1}:\r\n{2}\r\n>\r\n+ {3}\r\n{4} more lines",
                    name, count, lineEqualLast, lineNext, linesRemaining);
            }
            using (StringReader readerExpected = new StringReader(expected))
            using (StringReader readerActual = new StringReader(actual))
            {
                int count = 1;
                string lineEqualLast = string.Empty;
                while (true)
                {
                    string lineExpected = readerExpected.ReadLine();
                    string lineActual = readerActual.ReadLine();
                    if (lineExpected == null && lineActual == null)
                    {
                        failureMessage = string.Empty;
                        return false;  // We're done
                    }
                    if (lineExpected == null)
                    {
                        failureMessage = GetEarlyEndingMessage(@"Expected", count - 1, lineEqualLast, lineActual, readerActual);
                        return true; // Found a difference
                    }
                    if (lineActual == null)
                    {
                        failureMessage = GetEarlyEndingMessage(@"Actual", count - 1, lineEqualLast, lineExpected, readerExpected);
                        return true; // Found a difference
                    }
                    if (!LinesEquivalentIgnoringTimeStampsAndGUIDs(lineExpected, lineActual, columnTolerances))
                    {
                        failureMessage = string.Format(@"Diff found at line {0}:\r\n{1}\r\n>\r\n{2}", count, lineExpected, lineActual);
                        return true; // Found a difference
                    }

                    lineEqualLast = lineExpected;
                    count++;
                }
            }
        }
    }
}
