/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.Drawing;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Regression test for issue #4240: GraphFullScan.CreateSingleScan threw an AssumptionException
    /// when its ScanProvider's transition list contained a precursor of the opposite polarity to the
    /// scan being displayed (e.g. a molecule with both [M+H]+ and [M-H]- precursors). It asserted
    /// transition.PrecursorMz.IsNegative == negativeScan for every transition instead of just skipping
    /// the non-matching ones. The original report came in via a timer-driven UpdateGraphPanes refresh.
    ///
    /// A simple chromatogram click can't recreate the condition (opposite-polarity precursors always
    /// render in separate panes, so a click only ever gathers one polarity). So this test builds the
    /// condition directly: a ScanProvider whose transition list spans both polarities, opened on a
    /// positive scan via ShowGraphFullScan. Without the fix that throws; with it the [M-H]- transition
    /// is skipped and the scan renders.
    ///
    /// The replicate (MixedPolarity.mzML) is a tiny synthetic polarity-switching MS1 file
    /// (every other scan flipped), so each precursor extracts a real chromatogram from its own
    /// polarity's scans. See GenerateMixedPolarityMzml.py in the test zip.
    /// </summary>
    [TestClass]
    public class MixedPolarityFullScanTest : AbstractFunctionalTestEx
    {
        [TestMethod]
        public void TestMixedPolarityFullScan()
        {
            TestFilesZip = @"TestFunctional\MixedPolarityFullScanTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // MS1-only full-scan filtering. Low-resolution (qit, 0.7 m/z) so the synthetic
            // peaks extract without depending on exact ppm matching.
            RunUI(() => SkylineWindow.NewDocument(true));
            RunUI(() => SkylineWindow.ModifyDocument("Set MS1 full-scan settings", doc =>
                doc.ChangeSettings(doc.Settings.ChangeTransitionFullScan(fs => fs
                    .ChangeAcquisitionMethod(FullScanAcquisitionMethod.None, null)
                    .ChangePrecursorIsotopes(FullScanPrecursorIsotopes.Count, 1, null)
                    .ChangePrecursorResolution(FullScanMassAnalyzerType.qit, 0.7, null)))));

            // ImportResultsFile requires the document to have a file path.
            RunUI(() => SkylineWindow.SaveDocument(TestFilesDir.GetTestPath("MixedPol.sky")));

            // One molecule (C20H30O2) with two precursors: charge +1 -> [M+H]+, charge -1 -> [M-H]-.
            // Columns: MoleculeGroup, PrecursorName, PrecursorFormula, PrecursorCharge
            var csv = TextUtil.LineSeparate(
                "MixedPol,TestMol,C20H30O2,1",
                "MixedPol,TestMol,C20H30O2,-1").Replace(',', TextUtil.CsvSeparator);
            var insertDlg = ShowDialog<InsertTransitionListDlg>(SkylineWindow.ShowPasteTransitionListDlg);
            // RunDlg waits for the column-select dialog to be shown before interacting, avoiding a
            // UI race (the exercise action runs on the event thread once the dialog is up).
            RunDlg<ImportTransitionListColumnSelectDlg>(() => insertDlg.TransitionListText = csv, colDlg =>
            {
                colDlg.radioMolecule.PerformClick();
                colDlg.SetSelectedColumnTypes(
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_List_Name,
                    Resources.ImportTransitionListColumnSelectDlg_ComboChanged_Molecule_Name,
                    Resources.ImportTransitionListColumnSelectDlg_headerList_Molecular_Formula,
                    Resources.ImportTransitionListColumnSelectDlg_PopulateComboBoxes_Precursor_Charge);
                colDlg.OkDialog();
            });
            WaitForDocumentLoaded();

            // Confirm the setup actually produced the mixed-polarity condition before testing the graph.
            var molecule = SkylineWindow.Document.Molecules.First();
            Assert.AreEqual(2, molecule.TransitionGroupCount, "expected two precursors (one per polarity)");
            var charges = molecule.TransitionGroups.Select(tg => tg.TransitionGroup.PrecursorCharge).ToArray();
            Assert.IsTrue(charges.Any(c => c > 0) && charges.Any(c => c < 0),
                "expected one positive and one negative precursor");

            // Import the polarity-switching replicate.
            ImportResultsFile(TestFilesDir.GetTestPath("MixedPolarity.mzML"));
            WaitForDocumentLoaded();

            // A UI click can't reproduce the bug: opposite-polarity precursors always render in
            // separate chromatogram panes, so a click only ever gathers a single polarity. Instead
            // construct the condition directly - a ScanProvider whose transition list contains BOTH
            // the [M+H]+ and [M-H]- precursors - and open the Full Scan graph on a positive scan.
            // CreateSingleScan then checks the positive scan's polarity against the [M-H]- transition;
            // before the fix that threw AssumptionException (issue #4240).
            var doc = SkylineWindow.Document;
            var mol = doc.Molecules.First();
            var measuredResults = doc.Settings.MeasuredResults;
            var dataFile = measuredResults.Chromatograms[0].MSDataFilePaths.First();
            var tolerance = (float)doc.Settings.TransitionSettings.Instrument.MzMatchTolerance;

            TransitionFullScanInfo MakeFullScanInfo(TransitionGroupDocNode group, Color color)
            {
                Assert.IsTrue(measuredResults.TryLoadChromatogram(0, mol, group, tolerance, out var infos));
                var ci = infos[0].GetTransitionInfo(0);
                return new TransitionFullScanInfo
                {
                    Name = group.TransitionGroup.ToString(),
                    Source = ci.Source,
                    TimeIntensities = ci.TimeIntensities,
                    Color = color,
                    PrecursorMz = ci.PrecursorMz,
                    ProductMz = ci.ProductMz,
                    ExtractionWidth = ci.ExtractionWidth,
                    IonMobilityInfo = ci.GetIonMobilityFilter(),
                    Id = group.Transitions.First().Id
                };
            }

            var posInfo = MakeFullScanInfo(mol.TransitionGroups.First(tg => tg.TransitionGroup.PrecursorCharge > 0), Color.Blue);
            var negInfo = MakeFullScanInfo(mol.TransitionGroups.First(tg => tg.TransitionGroup.PrecursorCharge < 0), Color.Red);

            // Apex scan of the positive precursor's chromatogram.
            var posIntensities = posInfo.TimeIntensities.Intensities;
            var apexScan = 0;
            for (var i = 1; i < posIntensities.Count; i++)
            {
                if (posIntensities[i] > posIntensities[apexScan])
                {
                    apexScan = i;
                }
            }

            // Transition list spans both polarities; display a positive scan (transition 0 = positive).
            var scanProvider = new ScanProvider(SkylineWindow.DocumentFilePath, dataFile, posInfo.Source,
                posInfo.TimeIntensities.Times, new[] { posInfo, negInfo }, measuredResults);
            RunUI(() => SkylineWindow.ShowGraphFullScan(scanProvider, 0, apexScan, null));

            var graphFullScan = WaitForOpenForm<GraphFullScan>();
            Assert.IsNotNull(graphFullScan);
            WaitForConditionUI(() => graphFullScan.IsLoaded);

            // The opposite-polarity precursor must not be rendered for a positive scan. The crash fix
            // skips it when assigning spectrum points, but the curve/label/mass-error loops in
            // CreateSingleScanInPane originally filtered by Source only (assuming the now-removed
            // same-polarity invariant), so the [M-H]- transition still produced an empty curve and a
            // stray label. Assert it produces no curve; the displayed [M+H]+ transition still does.
            RunUI(() =>
            {
                var panes = graphFullScan.ZedGraphControl.MasterPane.PaneList;
                var curveLabels = panes
                    .SelectMany(pane => pane.CurveList)
                    .Select(curve => curve.Label.Text)
                    .ToArray();
                Assert.IsTrue(curveLabels.Contains(posInfo.Name),
                    string.Format("expected a curve for the displayed-polarity transition '{0}'", posInfo.Name));
                Assert.IsFalse(curveLabels.Contains(negInfo.Name),
                    string.Format("opposite-polarity transition '{0}' must not be rendered for a positive scan", negInfo.Name));

                // Extraction boxes are drawn per in-polarity transition as well; the off-polarity
                // precursor must not contribute one (issue #4240, AddExtractionBoxes).
                var extractionBoxCount = panes.SelectMany(pane => pane.GraphObjList.OfType<BoxObj>()).Count();
                Assert.AreEqual(1, extractionBoxCount,
                    "expected a single extraction box, for the displayed-polarity transition only");
            });

            // Step to the adjacent scan and back to exercise the refresh path too.
            RunUI(() => graphFullScan.ChangeScan(1));
            WaitForGraphs();
            RunUI(() => graphFullScan.ChangeScan(-1));
            WaitForGraphs();
        }
    }
}
