/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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


using System;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.IonMobility;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf 
{
    /// <summary>
    /// Verify measured drift time operation for a small molecule document
    /// </summary>
    [TestClass]
    public class DriftTimePredictorSmallMoleculesTest : AbstractFunctionalTestEx
    {
        private const string SULFA_MIX = "Sulfa Mix 1.0ms.d";

        [TestMethod]
        public void TestDriftTimePredictorSmallMolecules()  // N.B. the term "Drift Time Predictor" is a historical curiosity, leaving it alone for test history continuity
        {
            // RunPerfTests = true; // Enables perftests to run from the IDE (you don't want to commit this line without commenting it out)

            TestFilesZipPaths = new[]
            {
                @"https://skyline.ms/perftests/DriftTimePredictorSmallMoleculesTest.zip",
            };

            TestFilesPersistent = new[] { SULFA_MIX };

            RunFunctionalTest();
        }

        private string DataPath { get { return TestFilesDirs.Last().PersistentFilesDir; } }

        private string AddSmallMolDetails(string line, params Tuple<string, string>[] additions)
        {
            string HandleL10N(string text)
            {
                var inQuotes = false;
                for (var i = 0; i < text.Length; i++)
                {
                    if (text[i] == '\"')
                    {
                        inQuotes = !inQuotes;
                    }
                    else if (!inQuotes)
                    {
                        if (text[i] == ',')
                            text = text.Substring(0, i) + TextUtil.CsvSeparator + text.Substring(i + 1);
                        else if (text[i] == '.')
                            text = text.Substring(0, i) + CultureInfo.CurrentCulture.NumberFormat.NumberDecimalSeparator + text.Substring(i + 1);
                    }
                }

                return text;
            }

            foreach (var addition in additions)
            {
                var find = HandleL10N(addition.Item1);
                var replace = HandleL10N(addition.Item2);
                line = line.Replace(find, replace);
            }
            return line;
        }

        private const string inchikeySulfamethizole = "VACCAVUAMIDAGB-UHFFFAOYSA-N";
        private const string inchiSulfamethizole = "InChI=1S/C9H10N4O2S2/c1-6-11-12-9(16-6)13-17(14,15)8-4-2-7(10)3-5-8/h2-5H,10H2,1H3,(H,12,13)";
        private const string keggSulfamethizole = "D00870";
        private const string smilesSulfamethizole = "O=S(=O)(Nc1nnc(s1)C)c2ccc(N)cc2";
        private const string casSulfadimidine = "57-68-1";


        protected override void DoTest()
        {
            TestPopulateLibraryFromClipboard();

            // Empty doc with suitable full scan settings
            RunUI(() => SkylineWindow.OpenFile(
                TestFilesDirs[0].GetTestPath(@"DriftTimePredictorSmallMoleculesTest.sky")));

            var transitionListFile = TestFilesDirs[0].GetTestPath(@"Skyline Transition List wo CCS.csv");
            var transitionList = GetCsvFileText(transitionListFile);
            // Add a bit more detail to molecules
            transitionList = AddSmallMolDetails(transitionList, 
                    new Tuple<string, string>("\r\n", ",,,,,\r\n"),
                    new Tuple<string, string>(" Time,,,,,", " Time,CAS,KEGG,SMILES,InChI,InChiKey"),
                    new Tuple<string, string>("1.103,,,,,", "1.103,,"+keggSulfamethizole+","+smilesSulfamethizole +",\""+inchiSulfamethizole+"\","+inchikeySulfamethizole),
                    new Tuple<string, string>("0.949,,,,,", "0.949,"+casSulfadimidine+",,,,"));
            // Transition list is suitably formatted with headers to just drop into the targets tree
            SetClipboardText(transitionList);
            RunUI(() => SkylineWindow.Paste());
            var document = WaitForDocumentLoaded();
            AssertEx.IsDocumentState(document, null, 1, 4, 4, 4);
            {
                var importResults = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
                RunUI(() => importResults.ImportSimultaneousIndex = 2);

                // Importing raw data

                var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResults.OkDialog);

                RunUI(() =>
                {
                    openDataSourceDialog.CurrentDirectory = new MsDataFilePath(DataPath);
                    openDataSourceDialog.SelectAllFileType(".d");
                });

                OkDialog(openDataSourceDialog, openDataSourceDialog.Open);
            }
            document = WaitForDocumentLoaded();

            var area = document.MoleculePrecursorPairs.First().NodeGroup.Results.First().First().AreaMs1;
            AssertEx.IsTrue(area > 0);

            // Locate drift peaks
            const string libName = "Sulfa";
            var transitionSettingsUI = InitializeNewIonMobilityLibrary(libName, false, out var editIonMobilityLibraryDlg);

            RunUI(() =>
            {
                editIonMobilityLibraryDlg.GetIonMobilitiesFromResults();
            });
// PauseTest(); // Uncomment for a convenient informal demo stopping point
             
            // Check that a new value was calculated for all precursors
            RunUI(() => Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroupCount, editIonMobilityLibraryDlg.LibraryMobilitiesFlatCount));
            RunUI(() =>
            {
                for (var loop = 0; loop < 2; loop++)
                {
                    var ids = new SerializableDictionary<string, string>
                    {
                        {MoleculeAccessionNumbers.TagInChiKey, loop==0 ? inchikeySulfamethizole : string.Empty},
                        {MoleculeAccessionNumbers.TagInChI, loop==0 ? inchiSulfamethizole.Replace("InChI=", string.Empty) : string.Empty},
                        {MoleculeAccessionNumbers.TagKEGG, loop==0 ? keggSulfamethizole : string.Empty},
                        {MoleculeAccessionNumbers.TagSMILES, loop==0 ? smilesSulfamethizole : string.Empty},
                        {MoleculeAccessionNumbers.TagHMDB, null}, // No HMDB values at all
                        {MoleculeAccessionNumbers.TagCAS, loop == 1 ? casSulfadimidine : string.Empty}
                    };
                    var cells = editIonMobilityLibraryDlg.CellValues(loop*2);
                    var expectedDetails = MoleculeAccessionNumbers.PREFERRED_DISPLAY_ORDER.Where(tag => ids[tag]!=null).Select(tag => ids[tag]).ToList();
                    expectedDetails.Insert(0,   loop == 0 ? "C9H10N4O2S2" : "C12H14N4O2S");
                    for (var i = 0; i < expectedDetails.Count; i++)
                    {
                        AssertEx.AreEqual(expectedDetails[i], cells[i+6]);
                    }
                }
            });
            OkDialog(editIonMobilityLibraryDlg, () => editIonMobilityLibraryDlg.OkDialog());

            RunUI(() =>
            {
                Assert.AreEqual(libName, transitionSettingsUI.IonMobilityControl.SelectedIonMobilityLibrary);
            });
            OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

            WaitForDocumentChangeLoaded(document);

            var docFiltered = SkylineWindow.Document;

            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, manageDlg =>
            {
                manageDlg.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms.Take(1);
                manageDlg.ReimportResults();
                manageDlg.OkDialog();
            });

            docFiltered = WaitForDocumentChangeLoaded(docFiltered); 

            // If drift filtering was engaged, peak area should be less
            var areaFiltered = docFiltered.MoleculePrecursorPairs.First().NodeGroup.Results.First().First().AreaMs1;
            AssertEx.IsTrue(area > areaFiltered);
            AssertEx.IsTrue(areaFiltered > 0);

        }

        private TransitionSettingsUI InitializeNewIonMobilityLibrary(string libName, bool withOffsetHE, out EditIonMobilityLibraryDlg editIonMobilityLibraryDlg)
        {
            var transitionSettingsUI = ShowDialog<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI);
            RunUI(() => transitionSettingsUI.SelectedTab = TransitionSettingsUI.TABS.IonMobility);
            RunUI(() => transitionSettingsUI.IonMobilityControl.WindowWidthType =
                IonMobilityWindowWidthCalculator.IonMobilityWindowWidthType.resolving_power);
            RunUI(() => transitionSettingsUI.IonMobilityControl.IonMobilityFilterResolvingPower = 50);
            editIonMobilityLibraryDlg =
                ShowDialog<EditIonMobilityLibraryDlg>(transitionSettingsUI.IonMobilityControl.AddIonMobilityLibrary);
            var databasePath = TestFilesDir.GetTestPath(libName + IonMobilityDb.EXT);
            var dlg = editIonMobilityLibraryDlg;
            RunUI(() =>
            {
                dlg.SetOffsetHighEnergySpectraCheckbox(withOffsetHE);
                dlg.LibraryName = libName;
                dlg.CreateDatabaseFile(databasePath); // Simulate user click on Create button
            });
            return transitionSettingsUI;
        }

        private void TestPopulateLibraryFromClipboard()
        {

            for (var mode = 0; mode < 4; mode++)
            {
                bool withOffsetHE = mode < 2;
                bool ccsOnly = mode % 2 == 0;
                bool imOnly = mode == 1;
                var pasteText = // PrecursorName  PrecursorAdduct  CCS IM IM Units HEOffset PrecursorFormula CAS KEGG HMDB InChiKey InChI SMILES
                    "Sulfamethizole\t[M+H]\t11\t1\tDrift Time (ms)\t-1\tC9H10N4O2S2\t\tD00870\t\tVACCAVUAMIDAGB-UHFFFAOYSA-N\tInChI=1S/C9H10N4O2S2/c1-6-11-12-9(16-6)13-17(14,15)8-4-2-7(10)3-5-8/h2-5H,10H2,1H3,(H,12,13)\tO=S(=O)(Nc1nnc(s1)C)c2ccc(N)cc2\n" +
                    "Sulfachloropyridazine\t[M+H]\t11\t1\tDrift Time (ms)\t-1\tC10H9ClN4O2S\t\t\t\t\t\t\n" +
                    "Sulfamethazine\t[M+H]\t11\t1\tDrift Time (ms)\t-1\tC12H14N4O2S\t57-68-1\t\t\t\t\t\n" +
                    "Sulfadimethoxine\t[M+H]\t11\t1\tDrift Time (ms)\t-1\tC12H14N4O4S\t\t\t\t\t\t";
                if (ccsOnly)
                {
                    pasteText = pasteText.Replace("\t1\tDrift Time (ms)\t", "\t\t\t");
                }
                if (imOnly)
                {
                    pasteText = pasteText.Replace("\t11\t", "\t\t");
                }
                if (!withOffsetHE)
                {
                    pasteText = pasteText.Replace("\t-1\t", "\t"); // Remove the column from the paste text
                }

                // Empty doc with suitable full scan settings
                RunUI(() => SkylineWindow.OpenFile(
                    TestFilesDirs[0].GetTestPath(@"DriftTimePredictorSmallMoleculesTest.sky")));
                RunUI(() => SkylineWindow.SetUIMode(SrmDocument.DOCUMENT_TYPE.small_molecules));

                var libName = "pasted" + mode;
                var transitionSettingsUI = InitializeNewIonMobilityLibrary(libName, withOffsetHE, out var editIonMobilityLibraryDlg);

                RunUI(() =>
                {
                    SetClipboardText(pasteText);
                    editIonMobilityLibraryDlg.DoPasteLibrary();
                });

                // Check that all precursors imported properly
                RunUI(() => Assert.AreEqual(4, editIonMobilityLibraryDlg.LibraryMobilitiesFlatCount));
                RunUI(() =>
                {
                    for (var loop = 0; loop < 2; loop++)
                    {
                        var ids = new SerializableDictionary<string, string>
                        {
                            {MoleculeAccessionNumbers.TagInChiKey, loop==0 ? inchikeySulfamethizole : string.Empty},
                            {MoleculeAccessionNumbers.TagInChI, loop==0 ? inchiSulfamethizole : string.Empty},
                            {MoleculeAccessionNumbers.TagKEGG, loop==0 ? keggSulfamethizole : string.Empty},
                            {MoleculeAccessionNumbers.TagSMILES, loop==0 ? smilesSulfamethizole : string.Empty},
                            {MoleculeAccessionNumbers.TagHMDB, string.Empty}, 
                            {MoleculeAccessionNumbers.TagCAS, loop == 1 ? casSulfadimidine : string.Empty}
                        };
                        var cells = editIonMobilityLibraryDlg.CellValues(loop * 2);
                        var expectedDetails = MoleculeAccessionNumbers.PREFERRED_DISPLAY_ORDER.Where(tag => ids[tag] != null).Select(tag => ids[tag]).ToList();
                        expectedDetails.Insert(0, loop == 0 ? "C9H10N4O2S2" : "C12H14N4O2S");
                        for (var i = 0; i < expectedDetails.Count; i++)
                        {
                            AssertEx.AreEqual(expectedDetails[i], cells[i + 6]);
                        }
                    }
                });
                OkDialog(editIonMobilityLibraryDlg, () => editIonMobilityLibraryDlg.OkDialog());

                RunUI(() =>
                {
                    Assert.AreEqual(libName, transitionSettingsUI.IonMobilityControl.SelectedIonMobilityLibrary);
                });
                OkDialog(transitionSettingsUI, transitionSettingsUI.OkDialog);

                // Clean up
                RunUI(() => { SkylineWindow.NewDocument(true); });
            }
        }
    }
}
