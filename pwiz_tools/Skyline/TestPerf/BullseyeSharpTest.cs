/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net >
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

//
// Test to make sure our BullseyeSharp fork tracks with official release
// 
namespace TestPerf
{
    [TestClass]
    public class BullseyeSharpTest : AbstractUnitTestEx
    {
        private string GetDataPath(string path)
        {
            return TestFilesDir.GetTestPath(path);
        }

        [TestMethod, NoUnicodeTesting(TestExclusionReason.HARDKLOR_UNICODE_ISSUES), NoParallelTesting(TestExclusionReason.RESOURCE_INTENSIVE)]
        public void TestBullseyeSharp()
        {
            TestFilesZip = GetPerfTestDataURL(@"BullseyeSharpTest.zip"); // Files produced by the official (non-forked) BullseyeSharp
            TestFilesPersistent = new[] { "2021_0810_Eclipse_LiPExp_05_SS3.raw" }; // List of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            UnzipTestFiles();

            var processStartInfo =  new ProcessStartInfo(@"BullseyeSharp")
            {
                Arguments = $@"""{GetDataPath("2021_0810_Eclipse_LiPExp_05_SS3_MS1_3sn.hk")}"" ""{GetDataPath("2021_0810_Eclipse_LiPExp_05_SS3.raw")}"" ""{GetDataPath("testMatch.ms2")}"" ""{GetDataPath("testNoMatch.ms2")}""",
                CreateNoWindow = true,
                UseShellExecute = false
            };
            var processRunner = new ProcessRunner { OutputEncoding = Encoding.UTF8 };
            IProgressStatus status = new ProgressStatus(string.Empty);

            var textSink = new StringWriter();
            textSink.WriteLine($@"{processStartInfo.FileName} {processStartInfo.Arguments}");

            // Bullseye doesn't handle L10N, so invoke with invariant culture
            LocalizationHelper.CallWithCulture(CultureInfo.InvariantCulture, () =>
            {
                processRunner.Run(processStartInfo, null, null, ref status, textSink);
                return true;
            });

            // Now compare our output to the official output, ignoring differences like time stamps, or file paths
            AssertEx.AreEquivalentDsvFiles(GetDataPath("expected\\2021_0810_Eclipse_LiPExp_05_SS3_MS1_3sn.hk.bs.kro"), 
                GetDataPath("2021_0810_Eclipse_LiPExp_05_SS3_MS1_3sn.hk.bs.kro"), 
                true, // Has headers
                new []{0}); // Ignore differences in column 0 (file path)

            void CompareMS2Files(string expected, string actual)
            {
                using var readerExpected = new StreamReader(GetDataPath(expected));
                using var readerActual = new StreamReader(GetDataPath(actual));
                AssertEx.FieldsEqual(readerExpected,
                    readerActual, 
                    null, // Variable field count
                    null, // Ignore no columns
                    true, // Allow for rounding errors - the TIC line in particular is an issue here
                    0, // Allow no extra lines
                    null, // No overall tolerance
                    1, // Skip first line with its timestamp
                    $"comparing {expected} vs {actual}");
            }

            CompareMS2Files("expected\\Matches.ms2", "testMatch.ms2");
            CompareMS2Files("expected\\NoMatch.ms2", "testNoMatch.ms2");
        }
    }
}
