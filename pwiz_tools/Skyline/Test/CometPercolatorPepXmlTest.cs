/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.IO;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies that the Comet -> Percolator pepXML rewrite annotates EVERY search hit with a
    /// percolator_qvalue. Hits that Percolator dropped from its output tables must get a failing
    /// q-value (1) rather than none: BiblioSpec's PepXMLreader treats a percolator hit with no
    /// q-value as q-value 0, which would admit every unmatched PSM to the library unfiltered.
    /// Also verifies the output is well-formed XML when Comet has written a path containing a
    /// bare '&amp;' into the pepXML.
    /// </summary>
    [TestClass]
    public class CometPercolatorPepXmlTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestCometPercolatorQValueAnnotation()
        {
            string pepXml = string.Join(Environment.NewLine, new[]
            {
                @"<?xml version=""1.0"" encoding=""UTF-8""?>",
                // Comet writes file paths into attributes without escaping them; a folder named
                // with '&' must not break BiblioSpec's XML parse. An already-escaped entity must
                // survive unchanged (not become &amp;amp;).
                @"<msms_pipeline_analysis summary_xml=""c:\Skyline T&est ^Data\run.pep.xml"">",
                @"<msms_run_summary base_name=""c:\Already T&amp;Escaped\run"">",
                @"<search_summary search_engine=""Comet"" search_engine_version=""2024.01"">",
                @"</search_summary>",
                // Hit A: matched, passing q-value
                @"<spectrum_query spectrum=""run.00010.00010.2"" start_scan=""10"" end_scan=""10"" assumed_charge=""2"" index=""1"">",
                @"<search_result>",
                @"<search_hit hit_rank=""1"" peptide=""PEPTIDEA"" num_tot_proteins=""1"">",
                @"<search_score name=""expect"" value=""0.01""/>",
                @"</search_hit>",
                @"</search_result>",
                @"</spectrum_query>",
                // Hit B: matched, failing q-value (still annotated; BiblioSpec does the cutoff)
                @"<spectrum_query spectrum=""run.00020.00020.2"" start_scan=""20"" end_scan=""20"" assumed_charge=""2"" index=""2"">",
                @"<search_result>",
                @"<search_hit hit_rank=""1"" peptide=""PEPTIDEB"" num_tot_proteins=""1"">",
                @"<search_score name=""expect"" value=""0.02""/>",
                @"</search_hit>",
                @"</search_result>",
                @"</spectrum_query>",
                // Hit C: Percolator dropped it -> no entry in the q-value map
                @"<spectrum_query spectrum=""run.00030.00030.2"" start_scan=""30"" end_scan=""30"" assumed_charge=""2"" index=""3"">",
                @"<search_result>",
                @"<search_hit hit_rank=""1"" peptide=""PEPTIDEC"" num_tot_proteins=""1"">",
                @"<search_score name=""expect"" value=""0.03""/>",
                @"</search_hit>",
                @"</search_result>",
                @"</spectrum_query>",
                @"</msms_run_summary>",
                @"</msms_pipeline_analysis>",
            });

            var qvalueByPsmId = new Dictionary<string, double>
            {
                { @"run_10_2_1", 0.001 }, // Hit A
                { @"run_20_2_1", 0.5 },   // Hit B
                // run_30_2_1 (Hit C) deliberately absent -> a Percolator-dropped PSM
            };

            string output;
            using (var reader = new StringReader(pepXml))
            using (var writer = new StringWriter())
            {
                CometSearchEngine.FixPercolatorPepXml(reader, writer, qvalueByPsmId);
                output = writer.ToString();
            }

            // Every one of the three hits must carry a percolator_qvalue, so none defaults to
            // q-value 0 in BiblioSpec's reader (the bug that left the library unfiltered).
            int qvalueCount = Regex.Matches(output, @"name=""percolator_qvalue""").Count;
            AssertEx.AreEqual(3, qvalueCount);

            // Matched hits keep their real q-value...
            AssertEx.Contains(output, @"<search_score name=""percolator_qvalue"" value=""0.001"" />");
            AssertEx.Contains(output, @"<search_score name=""percolator_qvalue"" value=""0.5"" />");
            // ...and the Percolator-dropped hit gets a failing q-value (1) so it is excluded.
            AssertEx.Contains(output, @"<search_score name=""percolator_qvalue"" value=""1"" />");

            // The marker BiblioSpec keys on to read percolator q-values must still be emitted.
            AssertEx.Contains(output, @"<parameter name=""post-processor"" value=""percolator"" />");

            // The output must be well-formed XML, with the raw '&' in the path escaped and the
            // already-escaped one left alone.
            var doc = XDocument.Parse(output);
            AssertEx.AreEqual(@"c:\Skyline T&est ^Data\run.pep.xml", (string) doc.Root?.Attribute(@"summary_xml"));
            AssertEx.AreEqual(@"c:\Already T&Escaped\run",
                (string) doc.Root?.Element(@"msms_run_summary")?.Attribute(@"base_name"));
        }
    }
}
