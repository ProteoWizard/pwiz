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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Chemistry;
using pwiz.Skyline;
using pwiz.Skyline.Model;
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

            if (IsPerfTest && !RunPerfTests)
            {
                return; // Don't want to run this test with its large download right now
            }

            TestFilesZip = GetPerfTestDataURL(@"PerfCommandlineCreateImsDbTest.zip");
            TestFilesPersistent = new[] {  RAW_FILE }; // list of files that we'd like to unzip alongside parent zipFile, and (re)use in place
            TestDirectoryName = "ImsDbTest";
            TestFilesDir = new TestFilesDir(TestContext, TestFilesZip, TestDirectoryName, TestFilesPersistent);

            // Normal use
            const string normal = @"normal";
            TestCreateImsdb(normal, GetImsDbFilePath(normal));

            // Support for  optionally not specifying "--ionmobility-library-name"
            TestCreateImsdb(null, GetImsDbFilePath(@"implied-name"));

            // Expect failure because of illegal characters in imsdb filename
            string imsdbPathBadName = GetImsDbFilePath("bad-name:?");
            string output = TestCreateImsdb(@"bad-name", imsdbPathBadName, ExpectedResult.error);
            AssertEx.Contains(output,
                string.Format(
                    Resources.ValueInvalidPathException_ValueInvalidPathException_The_value___0___is_not_valid_for_the_argument__1__failed_attempting_to_convert_it_to_a_full_file_path_,
                    imsdbPathBadName, CommandArgs.ARG_IMSDB_CREATE.ArgumentText));

            // Expect failure because of nonexistent subdirectories in path
            const string badPath = @"bad-path";
            output = TestCreateImsdb(badPath, Path.Combine(badPath, GetImsDbFileName(badPath)), ExpectedResult.error);
            AssertEx.AreComparableStrings(
                Resources.CommandLine_SaveFile_Error__The_file_could_not_be_saved_to__0____Check_that_the_directory_exists_and_is_not_read_only_,
                output);

            // Expect failure because of missing "--ionmobility-library-create"
            output = TestCreateImsdb(@"bad-args", null, ExpectedResult.warning);
            AssertEx.Contains(output,
                string.Format(
                    Resources.CommandArgs_WarnArgRequirment_Warning__Use_of_the_argument__0__requires_the_argument__1_,
                    CommandArgs.ARG_IMSDB_NAME.ArgumentText, CommandArgs.ARG_IMSDB_CREATE.ArgumentText));
        }

        private enum ExpectedResult { success, warning, error }

        private string TestCreateImsdb(string imsdbName, string imsdbPath, ExpectedResult expectedResult = ExpectedResult.success)
        {
            // Clean-up after possible prior runs - also ensures they are not locked
            string outputPath = GetTestPath("Scripps_IMS_DB.sky");
            File.Delete(outputPath);
            string reportFilePath = GetTestPath("Scripps_CCS_report.csv");
            File.Delete(reportFilePath);
            if (!string.IsNullOrEmpty(imsdbPath) && File.Exists(imsdbPath))
                File.Delete(imsdbPath);

            var output = RunCommand(expectedResult != ExpectedResult.error,
                GetPathArg(CommandArgs.ARG_IN, "Scripps_IMS_Template.sky"),
                GetArg(CommandArgs.ARG_OUT, outputPath),
                GetPathArg(CommandArgs.ARG_IMPORT_TRANSITION_LIST, "test_run_1_transition_list.csv"),
                GetPathArg(CommandArgs.ARG_IMPORT_FILE, RAW_FILE),
                GetOptionalArg(CommandArgs.ARG_IMSDB_CREATE, imsdbPath),
                GetOptionalArg(CommandArgs.ARG_IMSDB_NAME, imsdbName),
                GetArg(CommandArgs.ARG_REPORT_NAME, "Precursor CCS"),
                GetArg(CommandArgs.ARG_REPORT_FILE, reportFilePath));

            if (expectedResult == ExpectedResult.error)
            {
                // These files get created in the case of a warning, even if that
                // may seem a bit undesirable.
                Assert.IsFalse(File.Exists(outputPath));
                Assert.IsFalse(File.Exists(reportFilePath));
            }
            else if (expectedResult == ExpectedResult.success)
            {
                AssertEx.FileExists(imsdbPath);

                // Compare to expected report - may need to localize the expected copy to match the actual copy
                AssertEx.AreEquivalentDsvFiles(GetTestPath("ImsDbTest_expected.csv"), reportFilePath, true);

                // Finally, check the persisted document to make sure it loads the IMS library
                // information that was just added.
                var doc = ResultsUtil.DeserializeDocument(outputPath);

                AssertEx.IsDocumentState(doc, 0, 1, 53, 53);
                
                using var docContainer = new ResultsTestDocumentContainer(null, outputPath, true);
                docContainer.SetDocument(doc, null, true);
                docContainer.AssertComplete();

                doc = docContainer.Document;

                AssertResult.IsDocumentResultsState(doc, Path.GetFileNameWithoutExtension(RAW_FILE), 53, 53, 0, 53, 0);

                var imFiltering = doc.Settings.TransitionSettings.IonMobilityFiltering;
                Assert.IsNotNull(imFiltering);
                Assert.IsTrue(imFiltering.IonMobilityLibrary != null && !imFiltering.IonMobilityLibrary.IsNone);

                foreach (var ppp in doc.MoleculePrecursorPairs)
                {
                    AssertEx.AreEqual(ExplicitTransitionGroupValues.EMPTY, ppp.NodeGroup.ExplicitValues,
                        "Expected no explicit values to be set, should all be in library");
                    var libKey = ppp.NodeGroup.GetLibKey(doc.Settings, ppp.NodePep);
                    var libEntries = imFiltering.GetIonMobilityInfoFromLibrary(libKey);
                    Assert.IsNotNull(libEntries);
                    Assert.AreEqual(1, libEntries.Count);
                    var libInfo = libEntries.First();
                    AssertEx.AreEqual(eIonMobilityUnits.inverse_K0_Vsec_per_cm2, libInfo.IonMobility.Units);
                    Assert.IsNotNull(libInfo.CollisionalCrossSectionSqA);
                }
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
