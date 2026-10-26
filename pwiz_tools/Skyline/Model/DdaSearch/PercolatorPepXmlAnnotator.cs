/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Model.DdaSearch
{
    /// <summary>
    /// Copies a search engine's pepXML through line by line, adding each search hit's Crux
    /// Percolator q-value as a &lt;search_score name="percolator_qvalue"&gt; so BiblioSpec can
    /// FDR-filter the library. The Comet, Tide and MSFragger integrations share this pass and
    /// differ only in how a pepXML spectrum_query maps to a Percolator PSM ID and where in a
    /// search hit the q-value score goes.
    /// </summary>
    public class PercolatorPepXmlAnnotator
    {
        private static readonly Regex REGEX_HIT_RANK = new Regex(@"hit_rank=""(\d+)""", RegexOptions.Compiled);

        private readonly Func<string, string> _getSpectrumId;
        private readonly string _rankSeparator;
        private readonly string _scoreAnchor;
        private readonly bool _insertBeforeAnchor;

        /// <param name="getSpectrumId">Given a raw &lt;spectrum_query line, returns the PSM ID prefix
        /// that Percolator uses for that spectrum.</param>
        /// <param name="rankSeparator">Joins the spectrum ID and the hit rank into the Percolator PSM ID.</param>
        /// <param name="scoreAnchor">Text identifying the line within a search hit that the q-value
        /// score is written next to.</param>
        /// <param name="insertBeforeAnchor">True to write the q-value score before the anchor line,
        /// false to write it after.</param>
        public PercolatorPepXmlAnnotator(Func<string, string> getSpectrumId, string rankSeparator,
            string scoreAnchor, bool insertBeforeAnchor)
        {
            _getSpectrumId = getSpectrumId;
            _rankSeparator = rankSeparator;
            _scoreAnchor = scoreAnchor;
            _insertBeforeAnchor = insertBeforeAnchor;
        }

        public void AddQValues(string pepXmlPath, string outputPath,
            IReadOnlyDictionary<string, double> qvalueByPsmId, Func<bool> isCanceled = null)
        {
            using var reader = new StreamReader(pepXmlPath);
            using var writer = new StreamWriter(outputPath);
            AddQValues(reader, writer, qvalueByPsmId, isCanceled);
        }

        public void AddQValues(TextReader pepXml, TextWriter output,
            IReadOnlyDictionary<string, double> qvalueByPsmId, Func<bool> isCanceled = null)
        {
            string line;
            string spectrumId = string.Empty;
            string rank = string.Empty;
            while ((line = pepXml.ReadLine()) != null)
            {
                if (isCanceled != null && isCanceled())
                    return;

                // Search engines write file paths into attributes without escaping '&', which
                // BiblioSpec's XML parser rejects. Escape only what is written: PSM IDs must come
                // from the raw line to match Percolator's output, which keeps the bare '&'.
                string escapedLine = PathEx.EscapePathForXML(line);
                if (line.Contains(@"<spectrum_query"))
                {
                    spectrumId = _getSpectrumId(line);
                }
                else if (line.Contains(@"<search_hit"))
                {
                    var match = REGEX_HIT_RANK.Match(line);
                    rank = match.Success ? match.Groups[1].Value : string.Empty;
                }
                else if (line.Contains(_scoreAnchor))
                {
                    if (!_insertBeforeAnchor)
                        output.WriteLine(escapedLine);
                    WriteQValue(output, qvalueByPsmId, spectrumId + _rankSeparator + rank);
                    if (_insertBeforeAnchor)
                        output.WriteLine(escapedLine);
                    continue;
                }
                else if (line.Contains(@"</search_summary>"))
                {
                    output.WriteLine(@"<parameter name=""post-processor"" value=""percolator"" />");
                }
                output.WriteLine(escapedLine);
            }
        }

        private static void WriteQValue(TextWriter output, IReadOnlyDictionary<string, double> qvalueByPsmId, string psmId)
        {
            // When Percolator has dropped a PSM from its output tables there is no q-value for it.
            // Without a percolator_qvalue, BiblioSpec's PepXMLreader treats the hit as q-value 0 and
            // admits it to the library unfiltered, so write a failing q-value (1) to exclude it.
            double qvalue = qvalueByPsmId.TryGetValue(psmId, out var found) ? found : 1;
            output.WriteLine(@"    <search_score name=""percolator_qvalue"" value=""{0}"" />",
                qvalue.ToString(CultureInfo.InvariantCulture));
        }
    }
}
