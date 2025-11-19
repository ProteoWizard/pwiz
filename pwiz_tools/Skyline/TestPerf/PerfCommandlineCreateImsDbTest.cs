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
                GetOptionalArg(CommandArgs.ARG_IMS_LIBRARY_RES, "30"),
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
                var expectedCSV = GetTestPath("ImsDbTest_expected.csv");
                File.WriteAllText(expectedCSV, EXPECTED_CSV);
                AssertEx.AreEquivalentDsvFiles(expectedCSV, reportFilePath, true);

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

        private const string EXPECTED_CSV =
            "Molecule,Precursor,Library Collision Cross Section\n" + 
            "\"(3,4-dimethylphenyl)(phenyl)methanone\",211.1117[M+H],145.84\n" + 
            "\"[(2,3-dimethylcyclohexyl)carbamoyl]methyl benzoate\",290.1751[M+H],172.79\n" + 
            "\"[(2-methoxy-5-methylphenyl)carbamoyl]methyl 2,3-dihydro-1,4-benzodioxine-6-carboxylate\",358.1285[M+H],268.55\n" + 
            "\"{7-bromo-4-oxopyrido[1,2-a]pyrimidin-2-yl}methyl 2-(difluoromethoxy)benzoate\",424.9943[M+H],178.5\n" + 
            "\"{cyclopropyl[(4-methoxyphenyl)methyl]carbamoyl}methyl 2,5-dimethyl-1-(5-methyl-1,2-oxazol-3-yl)pyrrole-3-carboxylate\",438.2023[M+H],213.25\n" + 
            "\"1-(2,5-dimethoxyphenyl)-2-{[5-(thiophen-2-yl)-2H-1,2,4-triazol-3-yl]sulfanyl}ethanone\",362.0628[M+H],181.4\n" + 
            "\"1-(2-{2,5-dioxo-2',3'-dihydrospiro[imidazolidine-4,1'-inden]-1-yl}acetyl)-3-(2-methylpropyl)urea\",359.1714[M+H],187.66\n" + 
            "1-(2-chlorobenzenesulfonyl)-4-cyclopropanecarbonylpiperazine,329.0721[M+H],166.2\n" + 
            "\"1-(2H-1,3-benzodioxol-5-yl)-3-(2-{2',5'-dioxo-2,3-dihydrospiro[1-benzopyran-4,4'-imidazolidin]-1'-yl}acetyl)urea\",439.1248[M+H],189.31\n" + 
            "\"1-(2H-1,3-benzodioxol-5-ylmethyl)-4-{4-[(methylsulfanyl)methyl]benzoyl}piperazine\",385.1580[M+H],202.05\n" + 
            "1-(3-chlorobenzenesulfonyl)pyrrolidine-2-carboxamide,289.0408[M+H],157.22\n" + 
            "1-[(5-chloro-3-methyl-1-phenylpyrazol-4-yl)methyl]-5-(pyrrolidine-1-sulfonyl)pyridin-2-one,433.1096[M+H],203.2\n" + 
            "\"1-[1-(2H-1,3-benzodioxol-5-ylmethyl)-2,5-dimethylpyrrol-3-yl]-2-{[4-methyl-5-(trifluoromethyl)-1,2,4-triazol-3-yl]sulfanyl}ethanone\",453.1203[M+H],205.79\n" + 
            "\"1-[2-(8-methyl-octahydro-2H-quinolin-1-yl)-2-oxoethyl]-2',3'-dihydrospiro[imidazolidine-4,1'-indene]-2,5-dione\",396.2282[M+H],193.58\n" + 
            "1-{4-[4-(azepane-1-carbonyl)piperidine-1-carbonyl]phenyl}pyrrolidin-2-one,398.2438[M+H],203.55\n" + 
            "\"1-benzyl-N,N-bis(furan-2-ylmethyl)piperidine-4-carboxamide\",379.2016[M+H],187.3\n" + 
            "\"1-methyl-4-{4-[4-(pyrrolidine-1-sulfonyl)phenyl]-1,3-thiazol-2-yl}piperazine\",393.1413[M+H],200.56\n" + 
            "\"2-(2-bromo-4,5-dimethoxyphenyl)acetonitrile\",255.9968[M+H],177.56\n" + 
            "\"2-(2-chlorophenyl)-7-(trifluoromethyl)-3,1-benzoxazin-4-one\",326.0190[M+H],163.12\n" + 
            "\"2-(4-methylphenyl)-2-oxo-N-(4-phenyl-1,3-thiazol-2-yl)acetamide\",323.0849[M+H],177.1\n" + 
            "\"2-(6-chloro-3-oxo-2,4-dihydro-1,4-benzothiazin-2-yl)-N-(3-methoxyphenyl)acetamide\",363.0565[M+H],184.68\n" + 
            "\"2,4-dichloro-N-[5-(trifluoromethyl)-4H-1,2,4-triazol-3-yl]benzamide\",324.9865[M+H],161.37\n" + 
            "\"2-[({5,7-dimethyl-[1,2,4]triazolo[1,5-a]pyrimidin-2-yl}sulfanyl)methyl]-1-ethyl-N,N-dimethyl-1,3-benzodiazole-5-sulfonamide\",446.1427[M+H],207.22\n" + 
            "2-[(4-chlorophenyl)sulfanyl]-N-[(4-methoxyphenyl)methyl]acetamide,322.0663[M+H],244.07\n" + 
            "\"2-[3-cyclopropyl-5-({[4-(trifluoromethoxy)phenyl]methyl}sulfanyl)-1,2,4-triazol-4-yl]acetamide\",373.0941[M+H],177.71\n" + 
            "\"2-{3-ethyl-5-[(2-methoxyphenyl)methylidene]-4-oxo-1,3-thiazolidin-2-ylidene}-3-(morpholin-4-yl)-3-oxopropanenitrile\",400.1326[M+H],195.45\n" + 
            "\"3-[(1,1-dioxo-1lambda6,2-benzothiazol-3-yl)amino]-N-[2-(4-isopropylphenyl)ethyl]propanamide\",400.1689[M+H],193.91\n" + 
            "3-[(4-benzoylpiperazin-1-yl)methyl]benzonitrile,306.1601[M+H],181.6\n" + 
            "\"3-[3-(2,5-dimethylphenoxy)propyl]-2H-1lambda6,2,3-benzothiadiazine-1,1,4-trione\",361.1217[M+H],183.35\n" + 
            "3-[cyclopentyl(thiophen-2-yl)methyl]-1-[2-(pyrimidin-2-ylamino)ethyl]urea,346.1696[M+H],183.83\n" + 
            "\"3-fluoro-N-{[1,2,4]triazolo[4,3-a]pyridin-3-ylmethyl}benzenesulfonamide\",307.0660[M+H],165.29\n" + 
            "\"3-methoxy-4-{[5-(4-methoxyphenyl)-1,3,4-oxadiazol-2-yl]methoxy}benzonitrile\",338.1135[M+H],183.02\n" + 
            "3-methoxy-N-(3-methylpentan-2-yl)benzamide,236.1645[M+H],157.17\n" + 
            "\"3-methyl-2-{[(3-methylphenyl)methyl]sulfanyl}thieno[3,2-d]pyrimidin-4-one\",303.0620[M+H],162.43\n" + 
            "\"4-(difluoromethoxy)-N-(2,3-dihydro-1,4-benzodioxin-6-ylmethyl)-3-methoxybenzamide\",366.1148[M+H],186.56\n" + 
            "\"4,5,6-trimethyl-2-({[3-(2-methylphenyl)-1,2,4-oxadiazol-5-yl]methyl}sulfanyl)pyrimidine\",327.1274[M+H],175.45\n" + 
            "4-ethyl-N-[2-(4-sulfamoylphenyl)ethyl]benzamide,333.1267[M+H],175.72\n" + 
            "\"7-(4-chlorophenyl)-6-(4-fluorophenyl)-4H-[1,2,4]triazolo[1,5-a]pyrimidin-5-one\",341.0600[M+H],173.8\n" + 
            "ethyl 4-[(4-ethoxy-3-methoxyphenyl)formamido]butanoate,310.1649[M+H],177.39\n" + 
            "\"N-(2,4-dibromophenyl)-3,5-dimethyl-1,2-oxazole-4-carboxamide\",372.9182[M+H],168.56\n" + 
            "\"N-(2,6-dichlorophenyl)-2-{methyl[(2-methylphenyl)methyl]amino}acetamide\",337.0869[M+H],172.7\n" + 
            "\"N-(2-chloro-4,6-dimethylphenyl)-3,5-dimethyl-1,2-oxazole-4-carboxamide\",279.0895[M+H],160.24\n" + 
            "N-[(2-methoxyphenyl)methyl]-2-(pyridin-4-yl)quinoline-4-carboxamide,370.1550[M+H],197.7\n" + 
            "N-[2-(4-methylphenoxy)phenyl]cyclopropanecarboxamide,268.1332[M+H],163.52\n" + 
            "N-[2-(morpholin-4-yl)phenyl]-2-(phenylsulfanyl)pyridine-3-carboxamide,392.1427[M+H],188.05\n" + 
            "\"N-{[2-(morpholin-4-yl)pyridin-4-yl]methyl}-5-(pyridin-3-yl)-1,2-oxazole-3-carboxamide\",366.1561[M+H],194.49\n" + 
            "\"N-{[5-(2-bromophenyl)-1,3,4-oxadiazol-2-yl]methyl}-N-cyclopropylbenzamide\",398.0499[M+H],181.97\n" + 
            "\"N1-[(tert-butylcarbamoyl)methyl]benzene-1,3-dicarboxamide\",278.1499[M+H],172.13\n" + 
            "\"N-ethyl-2-[4-methyl-4-(naphthalen-1-yl)-2,5-dioxoimidazolidin-1-yl]-N-(2-methylprop-2-en-1-yl)acetamide\",380.1969[M+H],189.41\n" + 
            "Tm_118,118.0863[M+H],119.86\n" + 
            "Tm_322,322.0481[M+H],151.97\n" + 
            "Tm_622,622.0290[M+H],200.94\n" + 
            "Tm_922,922.0098[M+H],238.93\n";
    }
}
