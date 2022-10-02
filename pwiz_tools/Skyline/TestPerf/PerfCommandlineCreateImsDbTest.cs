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


using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
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

        private const string RAW_FILE = "010521_Enamine_U6601911_A1_f100_pos_1_1_1086.d";

        private string GetTestPath(string relativePath)
        {
            return TestFilesDirs[0].GetTestPath(relativePath);
        }

        private string GetImsDbFileName(string testMode)
        {
            return $"ImsDbTest{testMode}.imsdb";
        }

        private string GetImsDbFilePath(string testMode)
        {
            return GetTestPath(GetImsDbFileName(testMode));
        }

        [TestMethod]
        public void CommandlineCreateImsDbPerfTest()
        {
            TestFilesZip = GetPerfTestDataURL(@"PerfCommandlineCreateImsDbTest.zip");
            TestFilesPersistent = new[] {  RAW_FILE }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, ".", TestFilesPersistent);

            // Normal use
            const string normal = @"normal";
            TestCreateImsdb(normal, GetImsDbFilePath(normal));

            // Support for  optionally not specifying "--ionmobility-library-name"
            TestCreateImsdb(null, GetImsDbFilePath(@"implied-name"));

            // Expect failure because of illegal characters in imsdb filename
            string imsdbPathBadName = GetImsDbFilePath("bad-name:?");
            string output = TestCreateImsdb(@"bad-name", imsdbPathBadName, true);
            AssertEx.Contains(output,
                string.Format(
                    Resources.ValueInvalidPathException_ValueInvalidPathException_The_value___0___is_not_valid_for_the_argument__1__failed_attempting_to_convert_it_to_a_full_file_path_,
                    imsdbPathBadName, CommandArgs.ARG_IMSDB_CREATE.ArgumentText));

            // Expect failure because of nonexistent subdirectories in path
            const string badPath = @"bad-path";
            output = TestCreateImsdb(badPath, Path.Combine(badPath, GetImsDbFileName(badPath)), true);
            AssertEx.AreComparableStrings(
                Resources.CommandLine_SaveFile_Error__The_file_could_not_be_saved_to__0____Check_that_the_directory_exists_and_is_not_read_only_,
                output);

            // Expect failure because of missing "--ionmobility-library-create"
            output = TestCreateImsdb(@"bad-args", null, true);
            AssertEx.Contains(output,
                string.Format(
                    Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                    CommandArgs.ARG_IMSDB_NAME.ArgumentText, CommandArgs.ARG_IMSDB_CREATE.ArgumentText));
        }

        private string TestCreateImsdb(string imsdbName, string imsdbPath, bool errorExpected = false)
        {
            string reportFilePath = GetTestPath("Scripps_CCS_report.csv");
            var output = RunCommand(GetPathArg(CommandArgs.ARG_IN, "Scripps_IMS_Template.sky"),
                GetPathArg(CommandArgs.ARG_OUT, "Scripps_IMS_DB.sky"),
                GetPathArg(CommandArgs.ARG_IMPORT_TRANSITION_LIST, "test_run_1_transition_list.csv"),
                GetPathArg(CommandArgs.ARG_IMPORT_FILE, RAW_FILE),
                GetOptionalArg(CommandArgs.ARG_IMSDB_CREATE, imsdbPath),
                GetOptionalArg(CommandArgs.ARG_IMSDB_NAME, imsdbName),
                GetArg(CommandArgs.ARG_REPORT_NAME, "Precursor CCS"),
                GetArg(CommandArgs.ARG_REPORT_FILE, reportFilePath));

            if (!errorExpected)
            {
                AssertEx.FileExists(imsdbPath);

                // Compare to expected report - may need to localize the expected copy to match the actual copy
                AssertEx.AreEquivalentDsvFiles(GetTestPath("ImsDbTest_expected.csv"), reportFilePath, true);
            }

            return output;
        }

        private string GetArg(CommandArgs.Argument arg, string value)
        {
            return arg.GetArgumentTextWithValue(value);
        }

        private string GetOptionalArg(CommandArgs.Argument arg, string value)
        {
            return string.IsNullOrEmpty(value) ? string.Empty : GetArg(arg, value);
        }

        private string GetPathArg(CommandArgs.Argument arg, string value)
        {
            return GetArg(arg, GetTestPath(value));
        }
    }
}
