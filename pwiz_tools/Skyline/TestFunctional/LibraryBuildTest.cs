/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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

using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for LibraryBuildTest
    /// </summary>
    [TestClass]
    public class LibraryBuildTest : AbstractFunctionalTest
    {
        private string _libraryName;
        
        public LibraryBuildTest()
        {
            _libraryName = "library_test";
        }

        private PeptideSettingsUI PeptideSettingsUI { get; set; }

        [TestMethod]
        public void TestLibraryBuild()
        {
            TestFilesZip = @"TestFunctional\LibraryBuildTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Clean-up before running the test
            RunUI(() => SkylineWindow.ModifyDocument("Set default settings",
                            doc => doc.ChangeSettings(SrmSettingsList.GetDefault())));

            // Test error conditions
            BuildLibraryError("missing_charge.pep.XML", TestFilesDir.FullPath, "uw.edu");
            BuildLibraryError("non_int_charge.pep.XML");
            BuildLibraryError("zero_charge.pep.XML");
            BuildLibraryError("truncated.pep.XML");
            BuildLibraryError("no_such_file.pep.XML", "Failed to open");
            BuildLibraryError("missing_mzxml.pep.XML", "Could not find spectrum file");
            // Barbara added code to ProteoWizard to rebuild a missing or invalid mzXML index
            // BuildLibraryError("bad_mzxml.pep.XML", "<index> not found");
            BuildLibraryValid(TestFilesDir.GetTestPath("library_errors"), new[] { "bad_mzxml.pep.XML" }, false, false, false, 1);

            string libraryBaseName = _libraryName;

            // Test mascot parser
            _libraryName = libraryBaseName + "mascot";
            string libraryMascot = _libraryName + BiblioSpecLiteSpec.EXT;
            BuildLibraryValid(TestFilesDir.GetTestPath("mascot"), new[] { "F027319.dat" },
                true, false, false, 121, 4);
            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryMascot)));

            // Test successful builds
            _libraryName = libraryBaseName + "a";
            string libraryA = _libraryName + BiblioSpecLiteSpec.EXT;
            string libraryARedundant = _libraryName + BiblioSpecLiteSpec.EXT_REDUNDANT;
            BuildLibraryValid("CPTAC_Set4_725_091509.pep.XML", true, false, false, 1);
            BuildLibraryValid("CPTAC_Set4_610_080509.pep.XML", true, false, true, 2);
            _libraryName = libraryBaseName + "b";
            string libraryB = _libraryName + BiblioSpecLiteSpec.EXT;
            BuildLibraryValid("CPTAC_Set4_624_072409.pep.XML", false, false, false, 6);
            _libraryName = libraryBaseName + "c";
            string libraryC = _libraryName + BiblioSpecLiteSpec.EXT;
            BuildLibraryValid(TestFilesDir.FullPath, new[] {libraryA, libraryB},
                false, false, false, 8);

            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryA)));
            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryARedundant)));
            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryB)));
            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryC)));

            // Test peptide filter
            const string filterList = "ACARPIISVYSEK\n" +
                                      // TODO: Having the modified sequence as the first line causes an error with European number formats
                                      "ADRDESSPYAAM[+{0:F01}]IAAQDVAQR\n" +
                                      "ADAIQAGASQFETSAAK";

            PastePeptideList(string.Format(filterList, 16.0), true, 0, 3, true);

            _libraryName = libraryBaseName + "filter";
            string libraryFilter = _libraryName + BiblioSpecLiteSpec.EXT;
            BuildLibraryValid(TestFilesDir.GetTestPath("maxquant"), new[] { "test.msms.txt" },
                false, true, false, 2);

            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryFilter)));
            RunUI(SkylineWindow.Undo);
            RunUI(SkylineWindow.Undo);

            // Test AddPathsDlg (file not found)
            EnsurePeptideSettings();
            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(PeptideSettingsUI.ShowBuildLibraryDlg);
            string[] invalidPaths =
            {
                Path.Combine(TestFilesDir.GetTestPath("maxquant"), "test.msms.xml"),
                Path.Combine(TestFilesDir.GetTestPath("library_valid"), "CPTAC_Set4_624_072409.pep.XML")
            };
            TestAddPaths(buildLibraryDlg, invalidPaths, true);

            // Test AddPathsDlg (file invalid type)
            string[] invalidTypes =
            {
                Path.Combine(TestFilesDir.GetTestPath("maxquant"), "test.msms.txt"),
                Path.Combine(TestFilesDir.GetTestPath("maxquant"), "mqpar.xml")
            };
            TestAddPaths(buildLibraryDlg, invalidTypes, true);

            // Test AddPathsDlg (valid files)
            string[] goodPaths =
            {
                Path.Combine(TestFilesDir.GetTestPath("maxquant"), "test.msms.txt"),
                Path.Combine(TestFilesDir.GetTestPath("library_valid"), "CPTAC_Set4_624_072409.pep.XML")
            };
            TestAddPaths(buildLibraryDlg, goodPaths, false);
            OkDialog(buildLibraryDlg, buildLibraryDlg.CancelDialog);

            const string heavyRPeptide = "TPAQFDADELR";
            const string oxidizedMPeptide = "LVGNMHGDETVSR";
            const string peptideList = heavyRPeptide + "\n" +
                                       oxidizedMPeptide + "\n" +
                                        "ALSIGFETCR\n" +
                                       "GNMHGDETVSR\n" +
                                       "VGNMHGDETVSR";

            PastePeptideList(peptideList, true, 0, 1);

            // Set modifications on peptides to verify they connect with library spectra.
            const LabelAtoms labelAtoms = LabelAtoms.C13 | LabelAtoms.N15;
            const string heavyR = "Heavy R";
            Settings.Default.HeavyModList.Add(new StaticMod(heavyR, "R", ModTerminus.C, null, labelAtoms, null, null));
            const string oMeth = "Oxidized Methionine";
            Settings.Default.StaticModList.Add(new StaticMod(oMeth, "M", null, "O"));

            var sequenceTree = SkylineWindow.SequenceTree;
            var docCurrent = SkylineWindow.Document;
            // Select the heavyR peptide
            PeptideTreeNode nodePepTree = null;
            IdentityPath pathPep = docCurrent.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            RunUI(() =>
                {
                    sequenceTree.SelectedPath = pathPep;
                    nodePepTree = sequenceTree.SelectedNode as PeptideTreeNode;
                });
            Assert.IsNotNull(nodePepTree);
            Assert.AreEqual(heavyRPeptide, nodePepTree.DocNode.Peptide.Sequence);
            // Set the Heavy R modification explicitly
            var editPepModsDlg = ShowDialog<EditPepModsDlg>(SkylineWindow.ModifyPeptide);
            RunUI(() =>
            {
                editPepModsDlg.SetModification(heavyRPeptide.Length - 1, IsotopeLabelType.heavy, heavyR);
                editPepModsDlg.OkDialog();
            });
            WaitForCondition(() => (SkylineWindow.Document.Molecules.First().TransitionGroupCount == 2));

            // The peptide should now match the spectrum in the library, and have
            // both heavy and light precursors, with ranked transitions
            PeptideDocNode nodePep = null;
            RunUI(() => nodePep = nodePepTree.DocNode);
            Assert.IsNotNull(nodePep);
            Debug.Assert(nodePep != null);
            Assert.AreEqual(2, nodePep.Children.Count, "Missing precursor for heavy R peptide.");
            docCurrent = SkylineWindow.Document;
            foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                AssertLibInfo(docCurrent, nodeGroup);
            // Which means all transition groups should now have spectrum info
            foreach (var nodeGroup in docCurrent.PeptideTransitionGroups)
                AssertLibInfo(docCurrent, nodeGroup);

            // New document
            var docNew = new SrmDocument(SrmSettingsList.GetDefault());
            RunUI(() => SkylineWindow.SwitchDocument(docNew, null));

            const string idpList3 = "FHYKTDQGIK\n" +
                                    "WCAIGHQER\n" +
                                    "WCTISTHEANK";
            int idpCount3 = idpList3.Split('\n').Length; 

            const string idpList = "ADVTLGGGAK\n" +
                                   "AGFAGDDAPR\n" +
                                   "ALEFAKK\n" +
                                   "CCTESLVNR\n" +
                                   "DSYVGDEAQSK\n" +
                                   "YICDNQDTISSK\n" +
                                   // charge 3 peptides all have 2 also
                                   idpList3;
            int idpCount = idpList.Split('\n').Length;

            _libraryName = libraryBaseName + "_idp";
            string libraryIdp = _libraryName + BiblioSpecLiteSpec.EXT;
            BuildLibraryValid(TestFilesDir.GetTestPath("idp_xml"), new[] { "orbi-small-eg.idpXML" },
                false, false, false, idpCount + idpCount3);

            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryIdp)));

            // Add peptides expected to have charge 2 spectra in the library
            PastePeptideList(idpList, true, 0, 0);

            // Undo the paste
            RunUI(SkylineWindow.Undo);

            // Try filtering for only charge 3 spectra
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(
                SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
                {
                    transitionSettingsUI.PrecursorCharges = "3";
                    transitionSettingsUI.OkDialog();
                });

            PastePeptideList(idpList, false, idpCount - idpCount3 + 1 /* missing cleavage*/, 0);

            // New document
            RunUI(() => SkylineWindow.SwitchDocument(docNew, null));

            _libraryName = libraryBaseName + "_cpas1";
            string libraryCpas1 = _libraryName + BiblioSpecLiteSpec.EXT;
            BuildLibraryValid(TestFilesDir.GetTestPath("cpas"), null,
                false, false, false, 3);

            Assert.IsTrue(File.Exists(TestFilesDir.GetTestPath(libraryCpas1)));

            // These are very poor searches, so repeat with no filter
            Settings.Default.LibraryResultCutOff = 0;

            _libraryName = libraryBaseName + "_cpas2";
            BuildLibraryValid(TestFilesDir.GetTestPath("cpas"), null,
                false, false, false, 100, 100);

            // And, since the spectra are really poor, allow lots of
            // possibilities for fragment ions.
            var transitionSettingsCpas = ShowDialog<TransitionSettingsUI>(
                SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() =>
            {
                transitionSettingsCpas.PrecursorCharges =
                transitionSettingsCpas.ProductCharges = "1,2,3";
                transitionSettingsCpas.FragmentTypes = "y,b";
                transitionSettingsCpas.InstrumentMaxMz = 2000;
                transitionSettingsCpas.OkDialog();
            });

            EnsurePeptideSettings();

            RunUI(() =>
                {
                    // Turn off carbamidomethyl cys, since not in these searches
                    PeptideSettingsUI.PickedStaticMods = new string[0];
                    PeptideSettingsUI.OkDialog();
                });

            // Get the set of peptides to paste from the library, since there
            // are a lot.
            HashSet<string> setPeptides = new HashSet<string>();
            var library = SkylineWindow.Document.Settings.PeptideSettings.Libraries.Libraries[0];
            foreach (var libKey in library.Keys)
            {
                if (!libKey.IsModified)
                    setPeptides.Add(libKey.Sequence);                
            }

            string cpasPeptides = string.Join("\n", setPeptides.ToArray());
            
            var pasteFilteredPeptideDlg = ShowDialog<PasteFilteredPeptidesDlg>(
                () => SkylineWindow.Paste(cpasPeptides));
            RunUI(pasteFilteredPeptideDlg.NoDialog);
            Assert.IsTrue(WaitForCondition(() => SkylineWindow.Document.PeptideCount == setPeptides.Count),
                string.Format("Expecting {0} peptides, found {1}.", setPeptides.Count, SkylineWindow.Document.PeptideCount));
            Assert.AreEqual(setPeptides.Count, SkylineWindow.Document.PeptideTransitionGroupCount,
                "Expecting precursors for peptides matched to library spectrum.");
        }

        private static void PastePeptideList(string peptideList, bool keep,
            int filteredPeptideCount, int missingSpectraCount, bool expectMessage = false)
        {
            int peptideCount = peptideList.Split('\n').Length;

            var pasteFilteredPeptideDlg = ShowDialog<PasteFilteredPeptidesDlg>(
                () => SkylineWindow.Paste(peptideList));

            if (keep)
            {
                OkDialog(pasteFilteredPeptideDlg, pasteFilteredPeptideDlg.NoDialog);
            }
            else
            {
                OkDialog(pasteFilteredPeptideDlg, pasteFilteredPeptideDlg.YesDialog);
                peptideCount -= filteredPeptideCount;
            }

            if (expectMessage)
            {
                var messageDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                OkDialog(messageDlg, messageDlg.Btn1Click);
            }

            Assert.IsTrue(WaitForCondition(() => SkylineWindow.Document.PeptideCount == peptideCount),
                string.Format("Expecting {0} peptides, found {1}.", peptideCount, SkylineWindow.Document.PeptideCount));
            if (peptideCount - missingSpectraCount != SkylineWindow.Document.PeptideTransitionGroupCount)
            {
                string peptideSeqs = string.Join(", ", (from nodeGroup in SkylineWindow.Document.PeptideTransitionGroups
                                                       select nodeGroup.TransitionGroup.Peptide.Sequence).ToArray());
                Assert.AreEqual(peptideCount - missingSpectraCount, SkylineWindow.Document.PeptideTransitionGroupCount,
                    string.Format("Expecting precursors for peptides matched to library spectrum. Found precursors for {0}.", peptideSeqs));                
            }

            var docCurrent = SkylineWindow.Document;
            foreach (var nodeGroup in docCurrent.PeptideTransitionGroups)
                AssertLibInfo(docCurrent, nodeGroup);
        }

        private static void AssertLibInfo(SrmDocument docCurrent, TransitionGroupDocNode nodeGroup)
        {
            Assert.IsTrue(nodeGroup.HasLibInfo,
                string.Format("Precursor {0} found without library info", nodeGroup.TransitionGroup));
            int ionCount = docCurrent.Settings.TransitionSettings.Libraries.IonCount;
            Assert.AreEqual(ionCount, nodeGroup.Children.Count);
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                Assert.IsTrue(nodeTran.HasLibInfo,
                    string.Format("Transition {0} found without library info", nodeTran.Transition));
                Assert.IsTrue(nodeTran.LibInfo.Rank <= ionCount);
            }
        }

        private void TestAddPaths(BuildLibraryDlg buildLibraryDlg, string[] paths, bool error)
        {
            RunDlg<AddPathsDlg>(buildLibraryDlg.ShowAddPathsDlg, addPathsDlg =>
                {
                    addPathsDlg.FileNames = paths;
                    if (error)
                    {
                        string errorMsg = addPathsDlg.CheckForError();
                        Assert.AreNotEqual(string.Empty, errorMsg);
                    }
                    addPathsDlg.CancelDialog();
                });
        }

        private void BuildLibraryValid(string inputFile,
            bool keepRedundant, bool filterPeptides, bool append, int expectedSpectra)
        {
            BuildLibraryValid(TestFilesDir.GetTestPath("library_valid"), new[] { inputFile },
                keepRedundant, filterPeptides, append, expectedSpectra);
        }

        private void BuildLibraryValid(string inputDir, IEnumerable<string> inputFiles,
            bool keepRedundant, bool filterPeptides, bool append, int expectedSpectra, int expectedAmbiguous = 0)
        {
            BuildLibrary(inputDir, inputFiles,
                null, null, keepRedundant, filterPeptides, append);

            if (expectedAmbiguous > 0)
            {
                var ambiguousDlg = WaitForOpenForm<MessageDlg>();
                RunUI(() =>
                {
                    Assert.AreEqual(expectedAmbiguous, ambiguousDlg.Message.Split('\n').Count() - 1);
                    ambiguousDlg.OkDialog();
                });
            }

            Assert.IsTrue(WaitForCondition(() =>
                PeptideSettingsUI.AvailableLibraries.Contains(_libraryName)));
            RunUI(() => PeptideSettingsUI.PickedLibraries = new[] { _libraryName });
            OkDialog(PeptideSettingsUI, PeptideSettingsUI.OkDialog);

            // Wait for the library to load
            AbstractFunctionalTestEx.WaitForLibrary(expectedSpectra);
            var librarySettings = SkylineWindow.Document.Settings.PeptideSettings.Libraries;
            Assert.IsTrue(librarySettings.IsLoaded);
            Assert.AreEqual(1, librarySettings.Libraries.Count);
            Assert.AreEqual(_libraryName, librarySettings.Libraries[0].Name);
            Assert.AreEqual(expectedSpectra, librarySettings.Libraries[0].Keys.Count());
        }

        private void BuildLibraryError(string inputFile, params string[] messageParts)
        {
            BuildLibraryError(inputFile, null, null, messageParts);
        }

        private void BuildLibraryError(string inputFile, string libraryPath, string libraryAuth,
            params string[] messageParts)
        {
            string redundantBuildPath = TestFilesDir.GetTestPath(_libraryName + BiblioSpecLiteSpec.EXT_REDUNDANT);
            FileEx.SafeDelete(redundantBuildPath);
            string nonredundantBuildPath = TestFilesDir.GetTestPath(_libraryName + BiblioSpecLiteSpec.EXT);
            FileEx.SafeDelete(nonredundantBuildPath);

            BuildLibrary(TestFilesDir.GetTestPath("library_errors"), new[] { inputFile },
                libraryPath, libraryAuth, false, false, false);

            var messageDlg = WaitForOpenForm<MessageDlg>();
            Assert.IsNotNull(messageDlg, "No message box shown");
            AssertEx.Contains(messageDlg.Message, "ERROR");
            if (messageParts.Length == 0)
                AssertEx.Contains(messageDlg.Message, inputFile, "line");
            else
                AssertEx.Contains(messageDlg.Message, messageParts);
            OkDialog(messageDlg, messageDlg.OkDialog);           
            CheckLibraryExistence(redundantBuildPath, false);
            CheckLibraryExistence(nonredundantBuildPath, false);
        }

        private void EnsurePeptideSettings()
        {
            PeptideSettingsUI = FindOpenForm<PeptideSettingsUI>() ??
                                ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
        }

        private void BuildLibrary(string inputDir, IEnumerable<string> inputFiles,
            string libraryPath, string libraryAuth, bool keepRedundant, bool filterPeptides, bool append)
        {
            EnsurePeptideSettings();

            var buildLibraryDlg = ShowDialog<BuildLibraryDlg>(PeptideSettingsUI.ShowBuildLibraryDlg);
            List<string> inputPaths = null;
            if (inputFiles != null)
                inputPaths = new List<string>(inputFiles).ConvertAll(f => Path.Combine(inputDir, f));
            string autoLibPath = null;
            string autoLibId = null;
            RunUI(() =>
            {
                if (libraryPath != null)
                    buildLibraryDlg.LibraryPath = libraryPath;
                buildLibraryDlg.LibraryName = _libraryName;
                autoLibPath = buildLibraryDlg.LibraryPath;
                autoLibId = buildLibraryDlg.LibraryId;
                buildLibraryDlg.LibraryKeepRedundant = keepRedundant;
                buildLibraryDlg.LibraryFilterPeptides = filterPeptides;
                buildLibraryDlg.LibraryBuildAction = (append ?
                    LibraryBuildAction.Append : LibraryBuildAction.Create);
                if (libraryAuth != null)
                    buildLibraryDlg.LibraryAuthority = libraryAuth;
                buildLibraryDlg.OkWizardPage();
                if (inputPaths != null)
                    buildLibraryDlg.AddInputFiles(inputPaths);
                else
                    buildLibraryDlg.AddDirectory(inputDir);
            });
            OkDialog(buildLibraryDlg, buildLibraryDlg.OkWizardPage);
            Assert.AreEqual(TestFilesDir.GetTestPath(_libraryName + BiblioSpecLiteSpec.EXT),
                autoLibPath);
            Assert.AreEqual(_libraryName, autoLibId);
        }

        private static void CheckLibraryExistence(string libPath, bool libExist)
        {
            // Wait for journal to be removed
            string libJournalPath = libPath + BlibBuild.EXT_SQLITE_JOURNAL;
            WaitForCondition(() => !File.Exists(libJournalPath));
            Assert.IsFalse(File.Exists(libJournalPath),
                string.Format("Unexpected library journal {0} found", libJournalPath));            

            if (libExist)
            {
                Assert.IsTrue(File.Exists(libPath),
                    string.Format("Expected library {0} not found", libPath));                                
            }
            else
            {
                Assert.IsFalse(File.Exists(libPath),
                    string.Format("Unexpected library {0} found", libPath));                
            }
        }
    }
}