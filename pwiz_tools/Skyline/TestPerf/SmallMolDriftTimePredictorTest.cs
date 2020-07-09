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
        public void TestDriftTimePredictorSmallMolecules()
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

        protected override void DoTest()
        {
            // Empty doc with suitable full scan settings
            RunUI(() => SkylineWindow.OpenFile(
                TestFilesDirs[0].GetTestPath(@"DriftTimePredictorSmallMoleculesTest.sky")));

            var transitionListFile = TestFilesDirs[0].GetTestPath(@"Skyline Transition List wo CCS.csv");
            var transitionList = GetCsvFileText(transitionListFile);
            // Add a bit more detail to molecules
            var inchikeySulfamethizole = "VACCAVUAMIDAGB-UHFFFAOYSA-N";
            var inchiSulfamethizole = "InChI=1S/C9H10N4O2S2/c1-6-11-12-9(16-6)13-17(14,15)8-4-2-7(10)3-5-8/h2-5H,10H2,1H3,(H,12,13)";
            var keggSulfamethizole = "D00870";
            var smilesSulfamethizole = "O=S(=O)(Nc1nnc(s1)C)c2ccc(N)cc2";
            var casSulfadimidine = "57-68-1";
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

            // Locate drift peaks
            var peptideSettingsUI = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsUI.SelectedTab = PeptideSettingsUI.TABS.Prediction);
            var driftPredictor = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsUI.AddDriftTimePredictor);
            const string predictorName = "Sulfa";
            RunUI(() =>
            {
                driftPredictor.SetPredictorName(predictorName);
                driftPredictor.SetResolvingPower(50);
                driftPredictor.GetDriftTimesFromResults();
            });
            // Check that a new value was calculated for all precursors
            RunUI(() => Assert.AreEqual(SkylineWindow.Document.MoleculeTransitionGroupCount, driftPredictor.Predictor.IonMobilityRows.Count));
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
                    var cells = driftPredictor.MeasuredDriftTimes.Rows[loop*2].Cells;
                    var expectedDetails = MoleculeAccessionNumbers.PREFERRED_DISPLAY_ORDER.Where(tag => ids[tag]!=null).Select(tag => ids[tag]).ToList();
                    expectedDetails.Insert(0,   loop == 0 ? "C9H10N4O2S2" : "C12H14N4O2S");
                    for (var i = 0; i < expectedDetails.Count; i++)
                    {
                        AssertEx.AreEqual(expectedDetails[i], cells[i+5].FormattedValue);
                    }
                }
            });
            OkDialog(driftPredictor, () => driftPredictor.OkDialog());

            RunUI(() =>
            {
                Assert.IsTrue(peptideSettingsUI.IsUseMeasuredRT);
                Assert.AreEqual(2, peptideSettingsUI.TimeWindow);
                Assert.AreEqual(predictorName, peptideSettingsUI.SelectedDriftTimePredictor);
            });
            OkDialog(peptideSettingsUI, peptideSettingsUI.OkDialog);

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
            Assume.IsTrue(area > areaFiltered);

        }

    }
}
