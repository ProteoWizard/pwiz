/*
 * Original author: Brian Pratt <bspratt .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.SkylineTestUtil;

namespace TestPerf // This would be in tutorial tests if it didn't take about 10 minutes to run
{
    /// <summary>
    /// Verify measured drift time tutorial operation
    /// </summary>
    [TestClass]
    public class MeasuredDriftTimesTutorialTest : AbstractFunctionalTestEx
    {
        const string BSA_Frag = "trained_dt_tutorial_BSA_Frag_100nM_18May15_Fir_15-04-02.d";
        const string Yeast_BSA = "trained_dt_tutorial_Yeast_0pt1ug_BSA_50nM_18May15_Fir_15-04-01.d";

        [TestMethod]
        [Timeout(int.MaxValue)]  // These can take a long time
        public void MeasuredDriftValuesTutorialTest()
        {
            LinkPdf = "https://skyline.gs.washington.edu/labkey/_webdav/home/software/Skyline/%40files/tutorials/TrainedDriftTimePredictionTutorial.pdf";
            var downloadsFolder = PathEx.GetDownloadsPath();

            TestFilesZipPaths = new [] { @"https://skyline.gs.washington.edu/tutorials/TrainedDriftTimePredictionTutorial.zip", 
                @"https://skyline.gs.washington.edu/perftests/Trained_Drift_Times_Tutorial.zip", 
                Path.Combine(downloadsFolder ,@"perftests\Trained_Drift_Times_Tutorial\"+BSA_Frag+".zip"), // Contained in the chorus download zip
                Path.Combine(downloadsFolder, @"perftests\Trained_Drift_Times_Tutorial\"+Yeast_BSA+".zip") // Contained in the chorus download zip
            };

            TestFilesPersistent = new[] { BSA_Frag, Yeast_BSA, "AcqData" };

            RunFunctionalTest();
            
        }

        protected override void DoTest()
        {
            // IsPauseForScreenShots = true;
            string skyFile = TestFilesDirs[0].GetTestPath("TrainedDriftTimePredictionTutorial/TrainedDriftTimePredictionTutorial.sky");
            RunUI(() => SkylineWindow.OpenFile(skyFile));

            var document = WaitForDocumentLoaded(240000);  
            PauseForScreenShot("Doc open");
            AssertEx.IsDocumentState(document, null, 1, 34, 38, 398);

            // Importing raw data from a sample which is a mixture of yeast and BSA

            var mixturePath = TestFilesDirs[1].GetTestPath(Yeast_BSA);
            ImportResultsDlg importResultsDlg = ShowDialog<ImportResultsDlg>(SkylineWindow.ImportResults);
            PauseForScreenShot("Options");
            var openDataSourceDialog = ShowDialog<OpenDataSourceDialog>(importResultsDlg.OkDialog);
            RunUI(() =>
            {
                openDataSourceDialog.CurrentDirectory = new MsDataFilePath(Path.GetDirectoryName(mixturePath)); 
                openDataSourceDialog.SelectAllFileType("d");
            });
            PauseForScreenShot("Picker");
            var removePrefix = ShowDialog<ImportResultsNameDlg>(openDataSourceDialog.Open);
            PauseForScreenShot("Prefix check");
            RunUI(removePrefix.YesDialog);


            var document2 = WaitForDocumentChangeLoaded(document, 1000*60*60*10); // 10 minutes

            // Arrange graphs tiled
            RunUI(() =>
            {
                SkylineWindow.AutoZoomBestPeak();
                SkylineWindow.ArrangeGraphsTiled();
            });
            FindNode("R.HPEYAVSVLLR.L"); // R.HPEYAVSVLLR.L [360, 370]
            PauseForScreenShot("Data imported - note interference in the yeast-BSA mixture");

            // Remove the messy data
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                var chromatograms = document2.Settings.MeasuredResults.Chromatograms;
                var chromMixture = chromatograms[1];
                var chroms = new[] { chromMixture };
                dlg.SelectedChromatograms = chroms; 
                dlg.RemoveReplicates();
                dlg.OkDialog();
            });
            PauseForScreenShot("Remove the mixture replicate so we can train on the pure run");

            // Get the observed drift times
            var precursors = document.MoleculePrecursorPairs.Select(p => p.NodePep.ModifiedTarget.GetLibKey(p.NodeGroup.PrecursorAdduct)).ToArray();
            // Verify ability to extract predictions from raw data
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(
                () => SkylineWindow.ShowPeptideSettingsUI(PeptideSettingsUI.TABS.Prediction));
            PauseForScreenShot("Peptide Prediction dialog");

            // Simulate user picking Add from the Drift Time Predictor combo control
            var driftTimePredictorDlg = ShowDialog<EditDriftTimePredictorDlg>(peptideSettingsDlg.AddDriftTimePredictor);
            PauseForScreenShot("Drift time Prediction dialog");
            RunUI(() =>
            {
                driftTimePredictorDlg.SetPredictorName("FromTrainingSet");
                driftTimePredictorDlg.SetResolvingPower(50.0);
            });
            PauseForScreenShot("Ready to scan loaded results for drift dimension peaks");
            RunUI(() =>
            {
                driftTimePredictorDlg.GetDriftTimesFromResults();
            });
            PauseForScreenShot("Results inspected for drift dimension peaks");
            RunUI(() =>
            {
                driftTimePredictorDlg.OkDialog();
            });
            WaitForClosedForm(driftTimePredictorDlg);
            RunUI(() =>
            {
                peptideSettingsDlg.OkDialog();
            });
            WaitForClosedForm(peptideSettingsDlg);
                
            document = SkylineWindow.Document;
            var measuredDTs = document.Settings.PeptideSettings.Prediction.IonMobilityPredictor.MeasuredMobilityIons;
            var count = 0;
            foreach (var key in precursors)
            {
                if (!measuredDTs.ContainsKey(key))
                    continue;
                count++;
            }
            Assert.AreEqual(document.MoleculeTransitionGroupCount, count, "did not find drift times for all precursors"); // Expect to find a value for each precursor
            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestPath("trainingset.sky")));  // Make sure no skyd file

            PauseForScreenShot("Future runs can be done without the training set loaded, since we now know the drift peaks for each peptide.  Let's reload the mixture data using the newly acquired drift time filtering information.");
            // Now load the mixture using the filters
            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestPath("filtered.sky")));  // Make sure no skyd file
            ImportResults(mixturePath, null, 30 * 60);
            PauseForScreenShot("Mixture results loaded using learned drift time filter values - interference is reduced");
            RunUI(() => SkylineWindow.SaveDocument(TestContext.GetTestPath("filtered.sky")));  // Make sure no skyd file

            Assert.AreEqual(1, document.Settings.MeasuredResults.Chromatograms.Count);

        }  
    }
}
