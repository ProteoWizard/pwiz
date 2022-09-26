/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
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


using System.Globalization;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify commandline handling for ion mobility library building
    /// </summary>
    [TestClass]
    public class TestCommandlineCreateImsDbPerf : AbstractUnitTestEx
    {

        private const string rawFile = "010521_Enamine_U6601911_A1_f100_pos_1_1_1086.d";

        [TestMethod]
        public void CommandlineCreateImsDbPerfTest()
        {

            TestFilesZip = GetPerfTestDataURL(@"PerfCommandlineCreateImsDbTest.zip");
            TestFilesPersistent = new[] {  rawFile }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, ".", TestFilesPersistent);

            var badName = @"blorf";
            foreach (var libName in new[]{@"_myname", string.Empty, badName}) // Try with and without explicit library name, and with an illegal name
            {
                var template = TestFilesDirs[0].GetTestPath("Scripps_IMS_Template.sky");
                var inFile = TestFilesDirs[0].GetTestPath(rawFile);
                var root = $"ImsDbTest{libName}";
                var outSky = TestFilesDirs[0].GetTestPath($"{root}.sky");
                var transitionList = TestFilesDirs[0].GetTestPath("test_run_1_transition_list.csv");
                var imsdb = TestFilesDirs[0].GetTestPath($"{(root.EndsWith(badName) ? root + @":?" : root)}.imsdb");
                var outCSV = TestFilesDirs[0].GetTestPath($"{root}.csv");
                var output = RunCommand($"--in={template}",
                    $"--out={outSky}",
                    $"--import-transition-list={transitionList}",
                    $"--import-file={inFile}",
                    $"--ionmobility-library-create={imsdb}",
                    string.IsNullOrEmpty(libName) ? string.Empty : $"--ionmobility-library-name={libName}",
                    "--report-name=Precursor CCS",
                    $"--report-file={outCSV}");
                if (Equals(libName, badName))
                {
                    // Expect failure because of illegal filename characters
                    AssertEx.Contains(output, $"--ionmobility-library-create failed");
                    continue;
                }
                AssertEx.FileExists(imsdb);
                AssertEx.Contains(output, $"Ion mobility library changed from \"None\" to \"{(string.IsNullOrEmpty(libName) ? root : libName)}\"");

                // Compare to expected result
                var expected = File.ReadAllLines(TestFilesDirs[0].GetTestPath("ImsDbTest_expected.csv"));
                var actual = File.ReadAllLines(outCSV);
                AssertEx.AreEqual(expected.Length, actual.Length, $"Report difference - expected same line count");

                var i = 0;

                string Localize(string s)
                {
                    return s.Replace(TextUtil.SEPARATOR_CSV, TextUtil.SEPARATOR_CSV_INTL).
                        Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                }

                foreach (var line in expected)
                {
                    var expectedLine = line;
                    if (CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator !=
                        CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
                    {
                        if (line.StartsWith("\""))
                        {
                            var parts = line.Split('"');
                            expectedLine = parts[1] + Localize(parts[2]);
                        }
                        else
                        {
                            expectedLine = Localize(expectedLine);
                        }
                    }

                    AssertEx.AreEqual(expectedLine, actual[i++], $"Report difference at line {i}");
                }
            }

        }

    }
}
