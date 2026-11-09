/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Verifies the Comet, Tide and MSFragger pepXML rewrites that add Percolator q-values. Each
    /// engine is given four hits:
    /// A and B are matched by Percolator and keep their real q-values.
    /// C was dropped by Percolator and must get a failing q-value (1) rather than none: BiblioSpec's
    /// PepXMLreader treats a percolator hit with no q-value as q-value 0, which would admit every
    /// unmatched PSM to the library unfiltered.
    /// D has a bare '&amp;' in its spectrum ID, as the engines write for a folder or file name
    /// containing '&amp;'. Its Percolator PSM ID keeps the bare '&amp;', so the lookup must use the
    /// raw attribute text, while the output must still be well-formed XML.
    /// </summary>
    [TestClass]
    public class PercolatorPepXmlTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPercolatorQValueAnnotation()
        {
            VerifyCometAnnotation();
            VerifyTideAnnotation();
            VerifyMsFraggerAnnotation();
        }

        private static void VerifyCometAnnotation()
        {
            var hits = new[]
            {
                new TestHit(@"run.00010.00010.2", @"run_10_2_1", 0.001),
                new TestHit(@"run.00020.00020.2", @"run_20_2_1", 0.5),
                new TestHit(@"run.00030.00030.2", null, 1),
                new TestHit(@"R&D Runs/comet.run.00040.00040.2", @"R&D Runs/comet.run_40_2_1", 0.002),
            };
            VerifyQValueAnnotation(CometSearchEngine.PERCOLATOR_PEPXML_ANNOTATOR, hits,
                spectrum => $@"<spectrum_query spectrum=""{spectrum}"" start_scan=""1"" end_scan=""1"" assumed_charge=""2"" index=""1"">",
                @"<search_score name=""expect"" value=""0.01""/>",
                qvalueScore => AssertEx.AreEqual(@"expect", ScoreName(qvalueScore.ElementsBeforeSelf().Last())));
        }

        private static void VerifyTideAnnotation()
        {
            var hits = new[]
            {
                new TestHit(@"run.00010.00010.2", @"run.10.10.2.1", 0.001),
                new TestHit(@"run.00020.00020.2", @"run.20.20.2.1", 0.5),
                new TestHit(@"run.00030.00030.2", null, 1),
                new TestHit(@"Q&A_run.00040.00040.2", @"Q&A_run.40.40.2.1", 0.002),
            };
            VerifyQValueAnnotation(TideSearchEngine.PERCOLATOR_PEPXML_ANNOTATOR, hits,
                spectrum => $@"<spectrum_query spectrum=""{spectrum}"" start_scan=""1"" end_scan=""1"" assumed_charge=""2"" index=""1"">",
                @"<search_score name=""xcorr_score"" value=""2.5""/>",
                qvalueScore => Assert.IsFalse(qvalueScore.ElementsAfterSelf().Any()));
        }

        private static void VerifyMsFraggerAnnotation()
        {
            var hits = new[]
            {
                new TestHit(@"run.00010.00010.2", @"run.00010.00010.2_1", 0.001),
                new TestHit(@"run.00020.00020.2", @"run.00020.00020.2_1", 0.5),
                new TestHit(@"run.00030.00030.2", null, 1),
                new TestHit(@"R&D_run.00040.00040.2", @"R&D_run.00040.00040.2_1", 0.002),
            };
            Func<string, string> spectrumQuery = spectrum =>
                $@"<spectrum_query start_scan=""1"" end_scan=""1"" assumed_charge=""2"" spectrum=""{spectrum}"" index=""1"">";
            VerifyQValueAnnotation(MsFraggerSearchEngine.PERCOLATOR_PEPXML_ANNOTATOR, hits, spectrumQuery,
                @"<search_score name=""hyperscore"" value=""20""/>",
                qvalueScore => AssertEx.AreEqual(@"hyperscore", ScoreName(qvalueScore.ElementsAfterSelf().First())));

            // A canceled search stops writing
            string pepXml = BuildPepXml(hits, spectrumQuery, @"<search_score name=""hyperscore"" value=""20""/>");
            using var reader = new StringReader(pepXml);
            using var writer = new StringWriter();
            MsFraggerSearchEngine.PERCOLATOR_PEPXML_ANNOTATOR.AddQValues(reader, writer,
                new Dictionary<string, double>(), () => true);
            AssertEx.AreEqual(string.Empty, writer.ToString());
        }

        private class TestHit
        {
            public TestHit(string spectrum, string percolatorPsmId, double expectedQValue)
            {
                Spectrum = spectrum;
                PercolatorPsmId = percolatorPsmId;
                ExpectedQValue = expectedQValue;
            }

            public string Spectrum { get; }
            public string PercolatorPsmId { get; } // null when Percolator dropped the PSM
            public double ExpectedQValue { get; }
        }

        private static void VerifyQValueAnnotation(PercolatorPepXmlAnnotator annotator, TestHit[] hits,
            Func<string, string> spectrumQuery, string engineScore, Action<XElement> verifyQValuePosition)
        {
            var qvalueByPsmId = hits.Where(hit => hit.PercolatorPsmId != null)
                .ToDictionary(hit => hit.PercolatorPsmId, hit => hit.ExpectedQValue);

            string output;
            using (var reader = new StringReader(BuildPepXml(hits, spectrumQuery, engineScore)))
            using (var writer = new StringWriter())
            {
                annotator.AddQValues(reader, writer, qvalueByPsmId);
                output = writer.ToString();
            }

            // The output must be well-formed XML, with raw '&' escaped and the already-escaped one left alone
            var doc = XDocument.Parse(output);
            AssertEx.AreEqual(@"c:\Skyline T&est ^Data\run.pep.xml", (string) doc.Root?.Attribute(@"summary_xml"));
            var runSummary = doc.Root?.Element(@"msms_run_summary");
            Assert.IsNotNull(runSummary);
            AssertEx.AreEqual(@"c:\Already T&Escaped\run", (string) runSummary.Attribute(@"base_name"));

            // The marker BiblioSpec keys on to read percolator q-values must be emitted
            var searchSummary = runSummary.Element(@"search_summary");
            Assert.IsNotNull(searchSummary);
            Assert.IsTrue(searchSummary.Elements(@"parameter").Any(p =>
                (string) p.Attribute(@"name") == @"post-processor" && (string) p.Attribute(@"value") == @"percolator"));

            // Every hit carries exactly one percolator_qvalue, so none defaults to q-value 0 in BiblioSpec
            var spectrumQueries = runSummary.Elements(@"spectrum_query").ToArray();
            AssertEx.AreEqual(hits.Length, spectrumQueries.Length);
            for (int i = 0; i < hits.Length; i++)
            {
                AssertEx.AreEqual(hits[i].Spectrum, (string) spectrumQueries[i].Attribute(@"spectrum"));
                var searchHit = spectrumQueries[i].Descendants(@"search_hit").Single();
                var qvalueScore = searchHit.Elements(@"search_score").Single(s => ScoreName(s) == @"percolator_qvalue");
                AssertEx.AreEqual(hits[i].ExpectedQValue, (double) qvalueScore.Attribute(@"value"));
                verifyQValuePosition(qvalueScore);
            }
        }

        private static string BuildPepXml(IEnumerable<TestHit> hits, Func<string, string> spectrumQuery, string engineScore)
        {
            var lines = new List<string>
            {
                @"<?xml version=""1.0"" encoding=""UTF-8""?>",
                // Search engines write file paths into attributes without escaping them
                @"<msms_pipeline_analysis summary_xml=""c:\Skyline T&est ^Data\run.pep.xml"">",
                @"<msms_run_summary base_name=""c:\Already T&amp;Escaped\run"">",
                @"<search_summary search_engine=""Test"">",
                @"</search_summary>",
            };
            foreach (var hit in hits)
            {
                lines.Add(spectrumQuery(hit.Spectrum));
                lines.Add(@"<search_result>");
                lines.Add(@"<search_hit hit_rank=""1"" peptide=""PEPTIDE"" num_tot_proteins=""1"">");
                lines.Add(engineScore);
                lines.Add(@"</search_hit>");
                lines.Add(@"</search_result>");
                lines.Add(@"</spectrum_query>");
            }
            lines.Add(@"</msms_run_summary>");
            lines.Add(@"</msms_pipeline_analysis>");
            return string.Join(Environment.NewLine, lines);
        }

        private static string ScoreName(XElement searchScore)
        {
            return (string) searchScore.Attribute(@"name");
        }
    }
}
