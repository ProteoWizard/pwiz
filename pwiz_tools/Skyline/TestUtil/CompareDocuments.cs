/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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

using System.IO;
using System.Linq;
using System.Xml;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;

namespace pwiz.SkylineTestUtil
{
    public class DocumentComparer
    {
        /// <summary>
        /// Compares documents, returns null if equal, or a text diff if not
        /// </summary>
        public static string CompareDocuments(SrmDocument expected, SrmDocument actual)
        {
            if (ReferenceEquals(null, expected))
            {
                return ReferenceEquals(null, actual) ? null : @"expected a null document";
            }
            if (ReferenceEquals(null, actual))
            {
                return @"expected a non-null document";
            }
            if (expected.Equals(actual))
            {
                return null;
            }

            string textExpected;
            using (var stringWriterExpected = new StringWriter())
            using (var xmlWriterExpected = new XmlTextWriter(stringWriterExpected))
            {
                xmlWriterExpected.Formatting = Formatting.Indented;
                expected.Serialize(xmlWriterExpected, null, SkylineVersion.CURRENT, null);
                textExpected = stringWriterExpected.ToString();
            }
            string textActual;
            using (var stringWriterActual = new StringWriter())
            using (var xmlWriterActual = new XmlTextWriter(stringWriterActual))
            {
                xmlWriterActual.Formatting = Formatting.Indented;
                actual.Serialize(xmlWriterActual, null, SkylineVersion.CURRENT, null);
                textActual = stringWriterActual.ToString();
            }

            var linesExpected = textExpected.Split('\n');
            var linesActual = textActual.Split('\n');
            int lineNumber;
            for (lineNumber = 0; lineNumber < linesExpected.Length && lineNumber < linesActual.Length; lineNumber++)
            {
                var lineExpected = linesExpected[lineNumber];
                var lineActual = linesActual[lineNumber];
                if (!CommonTextUtil.LinesEquivalentIgnoringTimeStampsAndGUIDs(lineExpected, lineActual))
                {
                    return $@"Expected XML representation of document does not match actual at line {lineNumber}\n" +
                           $@"Expected line:\n{lineExpected}\n" +
                           $@"Actual line:\n{lineActual}\n" +
                           $@"Expected full document:\n{textExpected}\n" +
                           $@"Actual full document:\n{textActual}\n";
                }
            }
            if (lineNumber < linesExpected.Length || lineNumber < linesActual.Length)
            {
                return @"Expected XML representation of document is not the same length as actual\n"+
                       $@"Expected full document:\n{textExpected}\n"+
                       $@"Actual full document:\n{textActual}\n";
            }

            if (expected.Settings.PeptideSettings.Libraries.Libraries.Count != actual.Settings.PeptideSettings.Libraries.Libraries.Count)
            {
                return @"Expected document does not match actual, but the difference does not appear in the XML representation. Library count differs, though.";
            }

            for (var i = 0; i < expected.Settings.PeptideSettings.Libraries.Libraries.Count; i++)
            {
                var libE = expected.Settings.PeptideSettings.Libraries.Libraries[i];
                var libA = actual.Settings.PeptideSettings.Libraries.Libraries[i];
                var result = string.Empty;
                foreach (var key in libE.Keys)
                {
                    if (!libA.Keys.Contains(key))
                    {
                        result += $@"expected to find key {key} in {libA.FileNameHint}\n";
                    }
                }
                foreach (var key in libA.Keys)
                {
                    if (!libE.Keys.Contains(key))
                    {
                        result += $@"unexpected key {key} in {libA.FileNameHint}\n";
                    }
                }

                if (!string.IsNullOrEmpty(result))
                {
                    return result;
                }
            }

            return @"Expected document does not match actual, but the difference does not appear in the XML representation.";
        }
    }
}
