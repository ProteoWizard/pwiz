/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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
using System.IO;
using System.IO.Compression;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DdaSearch;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// MS Amanda runs Percolator itself, and only the PSMs Percolator scored carry a q-value; the
    /// rest carry just Amanda:AmandaScore, which no library build can use. When Percolator fails to
    /// start - it needs a vcruntime140_1.dll its own package does not ship - MS Amanda still writes
    /// results, just unscored ones, and the only symptom used to be BlibBuild rejecting the file two
    /// steps later. <see cref="MSAmandaSearchWrapper.HasPercolatorQValues"/> is what turns that into
    /// an error at the search, so it has to be right about an absence, not just a presence.
    /// </summary>
    [TestClass]
    public class MSAmandaSearchResultsTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestPercolatorQValueDetection()
        {
            // Padding long enough to push the accession in the "straddles a read" case past the
            // reader's 64K chunk, which is the one way a chunk-at-a-time scan can miss a match.
            string padding = new string('x', 70 * 1024);

            AssertQValuesFound(true, "percolator Q value",
                @"<cvParam accession=""MS:1001491"" name=""percolator:Q value"" value=""0.001""/>");
            AssertQValuesFound(true, "generic PSM-level q-value",
                @"<cvParam accession=""MS:1002354"" name=""PSM-level q-value"" value=""0.004""/>");
            AssertQValuesFound(false, "only the raw Amanda score",
                @"<cvParam accession=""MS:1002319"" name=""Amanda:AmandaScore"" value=""123.4""/>");
            AssertQValuesFound(false, "no scores at all", string.Empty);
            AssertQValuesFound(true, "q-value beyond the first read",
                padding + @"<cvParam accession=""MS:1001491"" name=""percolator:Q value"" value=""0.001""/>");
            AssertQValuesFound(true, "q-value straddling a read boundary",
                new string('y', 64 * 1024 - 5) + @"<cvParam accession=""MS:1001491"" name=""percolator:Q value""/>");
        }

        private void AssertQValuesFound(bool expected, string label, string body)
        {
            string path = Path.Combine(TestContext.TestRunDirectory ?? Path.GetTempPath(),
                @"MSAmandaSearchResultsTest.mzid.gz");
            WriteGzippedMzid(path, body);
            try
            {
                Assert.AreEqual(expected, MSAmandaSearchWrapper.HasPercolatorQValues(path), label);
            }
            finally
            {
                FileEx.SafeDelete(path, true);
            }
        }

        private static void WriteGzippedMzid(string path, string body)
        {
            using var file = File.Create(path);
            using var gzip = new GZipStream(file, CompressionMode.Compress);
            using var writer = new StreamWriter(gzip, new UTF8Encoding(false));
            writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?><MzIdentML>");
            writer.Write(body);
            writer.Write(@"</MzIdentML>");
        }
    }
}
