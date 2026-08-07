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
            const string qValue = @"<cvParam accession=""MS:1001491"" name=""percolator:Q value"" value=""0.001""/>";

            AssertQValuesFound(true, "percolator Q value", qValue);
            AssertQValuesFound(true, "generic PSM-level q-value",
                @"<cvParam accession=""MS:1002354"" name=""PSM-level q-value"" value=""0.004""/>");
            AssertQValuesFound(false, "only the raw Amanda score",
                @"<cvParam accession=""MS:1002319"" name=""Amanda:AmandaScore"" value=""123.4""/>");
            AssertQValuesFound(false, "no scores at all", string.Empty);
            AssertQValuesFound(true, "q-value beyond the first read",
                new string('x', 70 * 1024) + qValue);

            // The one way a chunk-at-a-time scan misses a match is an accession split across two
            // reads, so walk the accession through the offsets around the reader's 64K chunk. Sweep
            // rather than aim: a StreamReader is free to return a short read, so no single padding
            // length reliably lands the accession on the seam, but a run of them cannot all miss it.
            for (int offset = -32; offset <= 32; offset++)
                AssertQValuesFound(true, $"q-value at 64K{offset:+#;-#;+0}",
                    new string('y', 64 * 1024 + offset) + qValue);
        }

        private static void AssertQValuesFound(bool expected, string label, string body)
        {
            using var mzidGz = new MemoryStream(GzipMzid(body));
            Assert.AreEqual(expected, MSAmandaSearchWrapper.HasPercolatorQValues(mzidGz), label);
        }

        private static byte[] GzipMzid(string body)
        {
            using var buffer = new MemoryStream();
            using (var gzip = new GZipStream(buffer, CompressionMode.Compress, true))
            using (var writer = new StreamWriter(gzip, new UTF8Encoding(false)))
            {
                writer.Write(@"<?xml version=""1.0"" encoding=""UTF-8""?><MzIdentML>");
                writer.Write(body);
                writer.Write(@"</MzIdentML>");
            }
            return buffer.ToArray();
        }
    }
}
