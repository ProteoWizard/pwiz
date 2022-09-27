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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify commandline handling for ion mobility library building
    /// Tests for:
    ///  normal operation
    ///  support for not specifying "--ionmobility-library-name"
    ///  error handling for illegal characters in imsdb filename
    ///  error handling for non-existent subdirectories in imsdb file path
    ///  error handling for specifying "--ionmobility-library-name" without "--ionmobility-library-create"
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

            var badName = @"badname";
            var badPath = @"badpath";
            var badArgs = @"badargs";
            foreach (var libName in new[]{ 
                         @"_myname",   //  normal operation
                         string.Empty, //  support for not specifying "--ionmobility-library-name"
                         badName,      //  error handling for illegal characters in imsdb filename
                         badPath,      //  error handling for non-existent subdirectories in imsdb file path
                         badArgs }     //  error handling for specifying "--ionmobility-library-name" without "--ionmobility-library-create"
                     ) 
            {
                var template = TestFilesDirs[0].GetTestPath("Scripps_IMS_Template.sky");
                var inFile = TestFilesDirs[0].GetTestPath(rawFile);
                var root = $"ImsDbTest{libName}";
                var outSky = TestFilesDirs[0].GetTestPath($"{root}.sky");
                var transitionList = TestFilesDirs[0].GetTestPath("test_run_1_transition_list.csv");
                var imsdb = TestFilesDirs[0].GetTestPath($"{(root.EndsWith(badName) ? root + @":?" : root)}.imsdb");
                if (Equals(libName, badPath))
                {
                    imsdb = imsdb.Replace(TestFilesDirs[0].GetTestPath(string.Empty), badPath + "\\" + badPath);
                }
                var outCSV = TestFilesDirs[0].GetTestPath($"{root}.csv");
                var output = RunCommand($"--in={template}",
                    $"--out={outSky}",
                    $"--import-transition-list={transitionList}",
                    $"--import-file={inFile}",
                    Equals(libName, badArgs) ? string.Empty : $"--ionmobility-library-create={imsdb}",
                    string.IsNullOrEmpty(libName) ? string.Empty : $"--ionmobility-library-name={libName}",
                    "--report-name=Precursor CCS",
                    $"--report-file={outCSV}");
                if (Equals(libName, badName))
                {
                    // Expect failure because of illegal characters in imsdb filename
                    AssertEx.Contains(output, 
                        string.Format(Resources.ValueInvalidPathException_ValueInvalidPathException_The_value___0___is_not_valid_for_the_argument__1__failed_attempting_to_convert_it_to_a_full_file_path_,
                            imsdb, "--ionmobility-library-create"));
                    continue;
                }
                else if (Equals(libName, badPath))
                {
                    // Expect failure because of nonexistent subdirectories in path
                    AssertEx.ContainsSimilar(output, Resources.CommandLine_SaveFile_Error__The_file_could_not_be_saved_to__0____Check_that_the_directory_exists_and_is_not_read_only_);
                    continue;
                }
                else if (Equals(libName, badArgs))
                {
                    // Expect failure because of missing "--ionmobility-library-create"
                    AssertEx.Contains(output, 
                        string.Format(Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                            "--ionmobility-library-name", "--ionmobility-library-create"));
                    continue;
                }
                AssertEx.FileExists(imsdb);

                // Compare to expected result - may need to localize the expected copy to match the actual copy
                var expected = File.ReadAllLines(TestFilesDirs[0].GetTestPath("ImsDbTest_expected.csv"));
                var actual = File.ReadAllLines(outCSV);
                AssertEx.AreEqual(expected.Length, actual.Length, $"Report difference - expected same line count");
                for (var i = 1; i < expected.Length; i++) // Skipping header, which may be localized
                {
                    var expectedLine = expected[i];
                    if (CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator !=
                        CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator)
                    {
                        string Localize(string s)
                        {
                            return s.Replace(TextUtil.SEPARATOR_CSV, TextUtil.SEPARATOR_CSV_INTL).
                                Replace(CultureInfo.InvariantCulture.NumberFormat.NumberDecimalSeparator, CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator);
                        }
                        if (expectedLine.StartsWith("\""))
                        {
                            var parts = expectedLine.Split('"');
                            expectedLine = parts[1] + Localize(parts[2]);
                        }
                        else
                        {
                            expectedLine = Localize(expectedLine);
                        }
                    }

                    AssertEx.AreEqual(expectedLine, actual[i], $"Difference in actual and expected report at line {i}");
                }
            }

        }

    }
}
