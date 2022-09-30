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


using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    /// <summary>
    /// Verify commandline handling for ion mobility library building
    /// Tests for:
    ///  normal operation
    ///  support for optionally not specifying "--ionmobility-library-name"
    ///  error handling for illegal characters in imsdb filename
    ///  error handling for non-existent subdirectories in imsdb file path
    ///  error handling for specifying "--ionmobility-library-name" without "--ionmobility-library-create"
    /// </summary>
    [TestClass]
    public class TestCommandlineCreateImsDbPerf : AbstractUnitTestEx
    {

        private const string _rawFile = "010521_Enamine_U6601911_A1_f100_pos_1_1_1086.d";
        private const string _normal = @"_normal";
        private const string _badName = @"badname";
        private const string _impliedName = @"impliedname";
        private const string _badPath = @"badpath";
        private const string _badArgs = @"badargs";

        [TestMethod]
        public void CommandlineCreateImsDbPerfTest()
        {

            TestFilesZip = GetPerfTestDataURL(@"PerfCommandlineCreateImsDbTest.zip");
            TestFilesPersistent = new[] {  _rawFile }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, ".", TestFilesPersistent);

            TestCreateImsdb(_normal); //  normal operation
            TestCreateImsdb(_impliedName); //  support for  optionally not specifying "--ionmobility-library-name"
            TestCreateImsdb(_badName); //  error handling for illegal characters in imsdb filename
            TestCreateImsdb(_badPath); //  error handling for non-existent subdirectories in imsdb file path
            TestCreateImsdb(_badArgs); //  error handling for specifying "--ionmobility-library-name" without "--ionmobility-library-create"

        }

        private void TestCreateImsdb(string testMode)
        {
            var template = TestFilesDirs[0].GetTestPath("Scripps_IMS_Template.sky");
            var inFile = TestFilesDirs[0].GetTestPath(_rawFile);
            var root = $"ImsDbTest{testMode}";
            var outSky = TestFilesDirs[0].GetTestPath($"{root}.sky");
            var transitionList = TestFilesDirs[0].GetTestPath("test_run_1_transition_list.csv");
            var imsdb = TestFilesDirs[0].GetTestPath($"{(root.EndsWith(_badName) ? root + @":?" : root)}.imsdb");
            if (Equals(testMode, _badPath))
            {
                imsdb = imsdb.Replace(TestFilesDirs[0].GetTestPath(string.Empty), _badPath + "\\" + _badPath);
            }

            var outCSV = TestFilesDirs[0].GetTestPath($"{root}.csv");
            var output = RunCommand($"--in={template}",
                $"--out={outSky}",
                $"--import-transition-list={transitionList}",
                $"--import-file={inFile}",
                Equals(testMode, _badArgs) ? string.Empty : $"--ionmobility-library-create={imsdb}",
                Equals(testMode, _impliedName) ? string.Empty : $"--ionmobility-library-name={testMode}",
                "--report-name=Precursor CCS",
                $"--report-file={outCSV}");
            if (Equals(testMode, _badName))
            {
                // Expect failure because of illegal characters in imsdb filename
                AssertEx.Contains(output,
                    string.Format(
                        Resources
                            .ValueInvalidPathException_ValueInvalidPathException_The_value___0___is_not_valid_for_the_argument__1__failed_attempting_to_convert_it_to_a_full_file_path_,
                        imsdb, "--ionmobility-library-create"));
                return;
            }
            else if (Equals(testMode, _badPath))
            {
                // Expect failure because of nonexistent subdirectories in path
                AssertEx.AreComparableStrings(
                    Resources
                        .CommandLine_SaveFile_Error__The_file_could_not_be_saved_to__0____Check_that_the_directory_exists_and_is_not_read_only_,
                    output);
                return;
            }
            else if (Equals(testMode, _badArgs))
            {
                // Expect failure because of missing "--ionmobility-library-create"
                AssertEx.Contains(output,
                    string.Format(
                        Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                        "--ionmobility-library-name", "--ionmobility-library-create"));
                return;
            }

            AssertEx.FileExists(imsdb);

            // Compare to expected report - may need to localize the expected copy to match the actual copy
            AssertEx.AreEquivalentDsvFiles(TestFilesDirs[0].GetTestPath("ImsDbTest_expected.csv"), outCSV, true);
        }
    }
}
