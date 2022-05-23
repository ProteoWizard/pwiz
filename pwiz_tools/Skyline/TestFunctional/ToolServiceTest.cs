/*
 * Original author: Don Marsh <donmarsh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;
using SkylineTool;
using Version = SkylineTool.Version;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class ToolServiceTest : AbstractFunctionalTestEx
    {
        private const string TOOL_NAME = "Test Interactive Tool";
        private const string FILE_NAME = "TestToolAPI.sky";
        private TestToolClient _testToolClient;

        [TestMethod]
        public void TestToolService()
        {
            Run(@"TestFunctional\ToolServiceTest.zip"); 
        }

        protected override void DoTest()
        {
            OpenDocument(FILE_NAME);

            // Make extra sure that the test tool isn't around before or after the
            // test starts. Either will cause problems
            using (new ProcessKiller("TestInteractiveTool"))
            // Install and run the test tool.
            using (var tool = new Tool(
                TestFilesDir.GetTestPath(""),
                TOOL_NAME,
                "$(ToolDir)\\TestInteractiveTool.exe",
                "$(SkylineConnection)",
                string.Empty,
                true,
                "Peak Area"))
            {
                tool.Run();

                _testToolClient = new TestToolClient(ToolConnection + "-test");

                // Check inter-process communication.
                CheckCommunication();

                // Check version and path.
                CheckVersion();
                CheckPath(FILE_NAME);

                // Select peptides.
                SelectPeptide(219, "VAQLPLSLK", "5600TT13-1070");
                SelectPeptide(330, "ELSELSLLSLYGIHK", "5600TT13-1070");

                // Select peptides in specific replicates.
                SelectPeptideReplicate(83, "TDFGIFR", "5600TT13-1076");
                SelectPeptideReplicate(313, "SAPLPNDSQAR", "5600TT13-1073");

                // Check document changes.
                CheckDocumentChanges();

                // Select insert node.
                SelectInsertNode();
                // Test FASTA import.
                AssertEx.IsDocumentState(SkylineWindow.Document, 1, 1, 20, 46, 138);
                ImportFasta(TEXT_FASTA);
                WaitForProteinMetadataBackgroundLoaderCompletedUI();
                AssertEx.IsDocumentState(SkylineWindow.Document, 3, 2, 82, 46, 138);

                // Test addition of spectral library.
                var docPreLibrary = SkylineWindow.Document;
                var libraryPath = TestFilesDir.GetTestPath("ABRF_sPRG_HCD.blib");
                _testToolClient.TestAddSpectralLibrary("Test library", libraryPath);

                // Test small molecule transition list insert
                _testToolClient.InsertSmallMoleculeTransitionList(GetSmallMoleculeTransitionsText());

                // Exit the test tool.
                _testToolClient.Exit();

                // Make sure we got all the library info we expect.
                Assert.IsTrue(docPreLibrary.Peptides.All(nodePep => !nodePep.HasLibInfo));
                var docWithLibrary = WaitForDocumentChangeLoaded(docPreLibrary);
                Assert.IsTrue(docWithLibrary.Peptides.All(nodePep => nodePep.HasLibInfo));
            }

            // There is a race condition where undoing a change occasionally leaves the document in a dirty state.
            SkylineWindow.DiscardChanges = true;
        }

        private const string TEXT_FASTA = @"
>YAL001C TFC3 SGDID:S000000001, Chr I from 151168-151099,151008-147596, reverse complement, Verified ORF, ""Largest of six subunits of the RNA polymerase III transcription initiation factor complex (TFIIIC); part of the TauB domain of TFIIIC that binds DNA at the BoxB promoter sites of tRNA and similar genes; cooperates with Tfc6p in DNA binding""
MVLTIYPDELVQIVSDKIASNKGKITLNQLWDISGKYFDLSDKKVKQFVLSCVILKKDIE
VYCDGAITTKNVTDIIGDANHSYSVGITEDSLWTLLTGYTKKESTIGNSAFELLLEVAKS
GEKGINTMDLAQVTGQDPRSVTGRIKKINHLLTSSQLIYKGHVVKQLKLKKFSHDGVDSN
PYINIRDHLATIVEVVKRSKNGIRQIIDLKRELKFDKEKRLSKAFIAAIAWLDEKEYLKK
VLVVSPKNPAIKIRCVKYVKDIPDSKGSPSFEYDSNSADEDSVSDSKAAFEDEDLVEGLD
NFNATDLLQNQGLVMEEKEDAVKNEVLLNRFYPLQNQTYDIADKSGLKGISTMDVVNRIT
GKEFQRAFTKSSEYYLESVDKQKENTGGYRLFRIYDFEGKKKFFRLFTAQNFQKLTNAED
EISVPKGFDELGKSRTDLKTLNEDNFVALNNTVRFTTDSDGQDIFFWHGELKIPPNSKKT
PNKNKRKRQVKNSTNASVAGNISNPKRIKLEQHVSTAQEPKSAEDSPSSNGGTVVKGKVV
NFGGFSARSLRSLQRQRAILKVMNTIGGVAYLREQFYESVSKYMGSTTTLDKKTVRGDVD
LMVESEKLGARTEPVSGRKIIFLPTVGEDAIQRYILKEKDSKKATFTDVIHDTEIYFFDQ
TEKNRFHRGKKSVERIRKFQNRQKNAKIKASDDAISKKSTSVNVSDGKIKRRDKKVSAGR
TTVVVENTKEDKTVYHAGTKDGVQALIRAVVVTKSIKNEIMWDKITKLFPNNSLDNLKKK
WTARRVRMGHSGWRAYVDKWKKMLVLAIKSEKISLRDVEELDLIKLLDIWTSFDEKEIKR
PLFLYKNYEENRKKFTLVRDDTLTHSGNDLAMSSMIQREISSLKKTYTRKISASTKDLSK
SQSDDYIRTVIRSILIESPSTTRNEIEALKNVGNESIDNVIMDMAKEKQIYLHGSKLECT
DTLPDILENRGNYKDFGVAFQYRCKVNELLEAGNAIVINQEPSDISSWVLIDLISGELLN
MDVIPMVRNVRPLTYTSRRFEIRTLTPPLIIYANSQTKLNTARKSAVKVPLGKPFSRLWV
NGSGSIRPNIWKQVVTMVVNEIIFHPGITLSRLQSRCREVLSLHEISEICKWLLERQVLI
TTDFDGYWVNHNWYSIYEST*
";

        private string GetSmallMoleculeTransitionsText()
        {
            var header = string.Join(",", new string[]
            {
                SmallMoleculeTransitionListColumnHeaders.moleculeGroup,
                SmallMoleculeTransitionListColumnHeaders.namePrecursor,
                SmallMoleculeTransitionListColumnHeaders.nameProduct,
                SmallMoleculeTransitionListColumnHeaders.labelType,
                SmallMoleculeTransitionListColumnHeaders.formulaPrecursor,
                SmallMoleculeTransitionListColumnHeaders.formulaProduct,
                SmallMoleculeTransitionListColumnHeaders.mzPrecursor,
                SmallMoleculeTransitionListColumnHeaders.mzProduct,
                SmallMoleculeTransitionListColumnHeaders.chargePrecursor,
                SmallMoleculeTransitionListColumnHeaders.chargeProduct,
                SmallMoleculeTransitionListColumnHeaders.rtPrecursor,
           });
           return header + "\n" +
               "Amino Acids B,AlaB,,light,,,225,44,-1,-1,3\n" +
                "Amino Acids B,ArgB,,light,,,310,217,-1,-1,19\n" +
                "Amino Acids,Ala,,light,,,225,44,1,1,3\n" +
                "Amino Acids,Ala,,heavy,,,229,48,1,1,4\n" + // NB we ignore RT conflicts
                "Amino Acids,Arg,,light,,,310,217,1,1,19\n" +
                "Amino Acids,Arg,,heavy,,,312,219,1,1,19\n" +
                "Amino Acids B,AlaB,,light,,,225,45,-1,-1,3\n" +
                "Amino Acids B,AlaB,,heavy,,,229,48,-1,-1,4\n" + // NB we ignore RT conflicts
                "Amino Acids B,AlaB,,heavy,,,229,49,-1,-1,4\n" + // NB we ignore RT conflicts
                "Amino Acids B,ArgB,,light,,,310,218,-1,-1,19\n" +
                "Amino Acids B,ArgB,,heavy,,,312,219,-1,-1,19\n" +
                "Amino Acids B,ArgB,,heavy,,,312,220,-1,-1,19\n";
            }

        private void CheckCommunication()
        {
            _testToolClient.Timeout = -1;
            Assert.AreEqual(4.0f, _testToolClient.TestFloat(2.0f));
            var floatArray = _testToolClient.TestFloatArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(floatArray, new[] { 1.0f, 2.0f }));
            var stringArray = _testToolClient.TestStringArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(stringArray, new[] { "two", "strings" }));
            var versionArray = _testToolClient.TestVersionArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(versionArray, new[]
                {
                    new Version {Major = 1},
                    new Version {Minor = 2}
                }));
            var chromatogramArray = _testToolClient.TestChromatogramArray();
            Assert.IsTrue(ArrayUtil.EqualsDeep(chromatogramArray, new[]
                {
                    new Chromatogram
                    {
                        PrecursorMz = 1.0,
                        ProductMz = 2.0,
                        Times = new[] {1.0f, 2.0f},
                        Intensities = new[] {10.0f, 20.0f}
                    },
                    new Chromatogram
                    {
                        PrecursorMz = 2.0,
                        ProductMz = 4.0,
                        Times = new[] {3.0f, 4.0f, 5.0f},
                        Intensities = new[] {30.0f, 40.0f, 50.0f}
                    }
                }));
        }

        private void SelectInsertNode()
        {
            _testToolClient.TestSelect(string.Empty);
        }

        private void SelectPeptide(int index, string peptideSequence, string replicate)
        {
            _testToolClient.TestSelect(index.ToString("D"));
            RunUI(() =>
            {
                Assert.AreEqual(peptideSequence, SkylineWindow.SelectedPeptideSequence);
                Assert.AreEqual(replicate, SkylineWindow.ResultNameCurrent);
            });
        }

        private void SelectPeptideReplicate(int index, string peptideSequence, string replicate)
        {
            _testToolClient.TestSelectReplicate(index.ToString("D"));
            RunUI(() =>
            {
                Assert.AreEqual(peptideSequence, SkylineWindow.SelectedPeptideSequence);
                Assert.AreEqual(replicate, SkylineWindow.ResultNameCurrent);
            });
        }

        private void ImportFasta(string textFasta)
        {
            _testToolClient.ImportFasta(textFasta);
        }

        private void CheckDocumentChanges()
        {
            Assert.AreEqual(0, DocumentChangeCount);
            RunUI(SkylineWindow.EditDelete);
            const int GRACE_PERIOD_MSEC = 5 * 1000; // Normally this takes less than 1/2 second, but not always, esp. under debugger
            TryWaitForCondition(GRACE_PERIOD_MSEC, () => 1 == DocumentChangeCount);
            AssertEx.AreEqual(1, DocumentChangeCount, "timed out waiting for DocumentChangeCount==1");
            RunUI(SkylineWindow.Undo);
            TryWaitForCondition(GRACE_PERIOD_MSEC, () => 2 == DocumentChangeCount);
            AssertEx.AreEqual(2, DocumentChangeCount, "timed out waiting for DocumentChangeCount==2");
        }

        private int DocumentChangeCount
        {
            get { return _testToolClient.GetDocumentChangeCount(); }
        }

        private void CheckVersion()
        {
            var version = _testToolClient.TestVersion();
            Assert.IsTrue(version.Major >= 0);
            Assert.IsTrue(version.Minor >= 0);
            Assert.IsTrue(version.Build >= 0);
            Assert.IsTrue(version.Revision >= 0);
        }

        private void CheckPath(string fileName)
        {
            var path = _testToolClient.TestDocumentPath();
            Assert.IsTrue(path.Contains(fileName));
        }

        private static string ToolConnection
        {
            get { return ToolMacros.GetSkylineConnection(); }
        }
    }
}
