/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford University
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfPeakTest : AbstractFunctionalTest, IPerfTest  // IPerfTests only run when the global "allow perf tests" flag is set
    {
        [TestMethod]
        public void TestPeakPerf()
        {
            TestFilesZipPaths = new[]
            {
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikSwathHeavy.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikQeHeavy.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikSwath.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikQe.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikSwathLight.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikQeLight.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Water.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Yeast.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Human.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course_vantage.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HeartFailure.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OvarianCancer.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/LudovicN14N15.zip"
            };
            IsPauseForScreenShots = true;
            RunFunctionalTest();
        }

        public readonly static string[] MODEL_NAMES = {"Skyline w/ mProphet", "Skyline Default", "Skyline Legacy", "Skyline w/ mProphet + Decoys"};

        protected override void DoTest()
        {
            const string outDir = @"D:\Results\PeakCompare";
            var none = new List<string>();
            var openSwath = new List<string> { "OpenSwath.csv" };
            var spectronaut = new List<string> { "Spectronaut.csv" };
            var peakView = new List<string> { "PeakView.txt" };
            var peakViewSpectronaut = spectronaut.Concat(peakView).ToList();
            var peakViewOpenSwath = openSwath.Concat(peakView).ToList();
            var peakViewSpectronautOpenSwath = peakViewSpectronaut.Concat(openSwath).ToList();
            var decoysAndSecond = MODEL_NAMES;
            var secondOnly = MODEL_NAMES.Take(3).ToList();

            int i = 0;
            // Hasmik Swath Heavy only
            AnalyzeDirectory(i++, peakViewSpectronautOpenSwath, outDir, secondOnly, new List<int> { 1, 1, 1 });
            // Hasmik Qe Heavy only
            AnalyzeDirectory(i++, spectronaut, outDir, secondOnly, new List<int> { 1 });
            // Hasmik SWATH
            AnalyzeDirectory(i++, peakViewSpectronautOpenSwath, outDir, secondOnly, new List<int> { 1, 1, 1 });
            // Hasmik QE
            AnalyzeDirectory(i++, spectronaut, outDir, secondOnly, new List<int> { 1 });
            // Hasmik Swath Light only
            AnalyzeDirectory(i++, peakViewSpectronaut, outDir, secondOnly, new List<int> { 0, 1 });
            // Hasmik Qe Light only
            AnalyzeDirectory(i++, spectronaut, outDir, secondOnly, new List<int> { 1 });
            // OpenSwath_Water
            AnalyzeDirectory(i++, peakViewOpenSwath, outDir, decoysAndSecond, new List<int> { 2, 1 });
            // OpenSwath_Yeast
            AnalyzeDirectory(i++, peakViewOpenSwath, outDir, decoysAndSecond, new List<int> { 1, 1 });
            // OpenSwath_Human
            AnalyzeDirectory(i++, peakViewOpenSwath, outDir, decoysAndSecond, new List<int> { 2, 1 });
            // Olga SRM Course vantage
            AnalyzeDirectory(i++, none, outDir, decoysAndSecond);
            // Olga SRM Course
            AnalyzeDirectory(i++, none, outDir, decoysAndSecond);
            // Heart Failure
            AnalyzeDirectory(i++, none, outDir, secondOnly);
            // Ovarian cancer
            AnalyzeDirectory(i++, none, outDir, secondOnly);
            // Ludovic N14N15
            AnalyzeDirectory(i++, none, outDir, secondOnly);
        }

        public void AnalyzeDirectory(int index, IEnumerable<string> externals, string outDir, IList<string> modelNames, IList<int> dialogSkips = null)
        {
            if(dialogSkips == null)
                dialogSkips = new List<int>();
            var directoryName = Path.GetFileNameWithoutExtension(TestFilesZipPaths[index]);
            TestFilesPersistent = new [] {directoryName};
            var testFilesDir = new TestFilesDir(TestContext, TestFilesZipPaths[index], null, TestFilesPersistent);
            var directoryPath = testFilesDir.GetTestPath(directoryName);
            if (outDir == null)
                outDir = directoryPath;
            var sb = new StringBuilder(directoryName);
            sb.Append(".sky");
            var skylineName = sb.ToString();
            var skylineDoc = Path.Combine(directoryPath, skylineName);
            var externalsPaths = externals.Select(name => Path.Combine(directoryPath, name));
// ReSharper disable UnusedVariable
            DataSetAnalyzer dataSetAnalyzer = new DataSetAnalyzer(skylineDoc, modelNames, externalsPaths, outDir, dialogSkips);
// ReSharper restore UnusedVariable
        }

        protected static void RunEditPeakScoringDlg(string editName, bool cancelReintegrate, Action<EditPeakScoringModelDlg> act)
        {
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            if (editName != null)
            {
                var editList = ShowDialog<EditListDlg<SettingsListBase<PeakScoringModelSpec>, PeakScoringModelSpec>>(
                    reintegrateDlg.EditPeakScoringModel);
                RunUI(() => editList.SelectItem(editName)); // Not L10N
                RunDlg(editList.EditItem, act);
                OkDialog(editList, editList.OkDialog);
            }
            else
            {
                RunDlg(reintegrateDlg.AddPeakScoringModel, act);
            }
            if (cancelReintegrate)
            {
                OkDialog(reintegrateDlg, reintegrateDlg.CancelDialog);
            }
            else
            {
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            }
        }

        protected class DataSetAnalyzer
        {
            public DataSetAnalyzer(string skylineDocument, IList<string> modelNames, IEnumerable<string> filePaths, string outDir, IList<int> dialogSkips)
            {
                Settings.Default.PeakScoringModelList.Clear();
                RunUI(() => SkylineWindow.OpenFile(skylineDocument));
                WaitForDocumentLoaded();
                CreateModels();
                foreach (var modelName in modelNames)
                {
                    TrainModel(modelName);
                }
                var comparePeakPickingDlg = ShowDialog<ComparePeakPickingDlg>(SkylineWindow.ShowCompareModelsDlg);
                foreach (var modelName in modelNames)
                {
                    AddModel(comparePeakPickingDlg, modelName);
                }
                int i = 0;
                foreach (var filePath in filePaths)
                {
                    var fileName = Path.GetFileNameWithoutExtension(filePath);
                    Assert.IsNotNull(fileName);
                    AddFile(comparePeakPickingDlg, fileName, filePath, dialogSkips[i]);
                    ++i;
                }
                var trimmedDoc = Path.GetFileNameWithoutExtension(skylineDocument);
                var sbRoc = new StringBuilder(trimmedDoc);
                sbRoc.Append("-roc.bmp");
                comparePeakPickingDlg.ZedGraphRoc.MasterPane.GetImage().Save(Path.Combine(outDir, sbRoc.ToString()));
                var sbQq = new StringBuilder(trimmedDoc);
                sbQq.Append("-qq.bmp");
                comparePeakPickingDlg.ZedGraphQq.MasterPane.GetImage().Save(Path.Combine(outDir, sbQq.ToString()));
                // TODO: How can I copy the data using commands?
                OkDialog(comparePeakPickingDlg, comparePeakPickingDlg.OkDialog);
            }

            public void CreateModels()
            {
                RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.PeakScoringModelName = MODEL_NAMES[0];
                    editPeakScoringModelDlg.SelectedModelItem = "mProphet";
                    editPeakScoringModelDlg.UsesDecoys = false;
                    editPeakScoringModelDlg.UsesSecondBest = true;
                    editPeakScoringModelDlg.TrainModel(true);
                    editPeakScoringModelDlg.OkDialog();
                });
                RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.PeakScoringModelName = MODEL_NAMES[1];
                    editPeakScoringModelDlg.SelectedModelItem = "Default";
                    editPeakScoringModelDlg.UsesDecoys = false;
                    editPeakScoringModelDlg.UsesSecondBest = true;
                    editPeakScoringModelDlg.TrainModel(true);
                    editPeakScoringModelDlg.OkDialog();
                });
                RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.PeakScoringModelName = MODEL_NAMES[2];
                    editPeakScoringModelDlg.SelectedModelItem = "Default";
                    editPeakScoringModelDlg.UsesDecoys = false;
                    editPeakScoringModelDlg.UsesSecondBest = true;
                    editPeakScoringModelDlg.PeakCalculatorsGrid.Items[6].IsEnabled = false;
                    editPeakScoringModelDlg.PeakCalculatorsGrid.Items[5].IsEnabled = false;
                    editPeakScoringModelDlg.PeakCalculatorsGrid.Items[4].IsEnabled = false;
                    editPeakScoringModelDlg.TrainModel(true);
                    editPeakScoringModelDlg.OkDialog();
                });
                if (SkylineWindow.Document.Peptides.Any(pep => pep.IsDecoy))
                {
                    RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
                    {
                        editPeakScoringModelDlg.PeakScoringModelName = MODEL_NAMES[3];
                        editPeakScoringModelDlg.SelectedModelItem = "mProphet";
                        editPeakScoringModelDlg.UsesDecoys = true;
                        editPeakScoringModelDlg.UsesSecondBest = false;
                        editPeakScoringModelDlg.TrainModel(true);
                        editPeakScoringModelDlg.OkDialog();
                    });
                }
            }

            public static void AddModel(ComparePeakPickingDlg dlg, string modelName)
            {
                var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(dlg.Add);
                RunUI(() =>
                {
                    addPeakCompareDlg.IsModel = true;
                    addPeakCompareDlg.PeakScoringModelSelected = modelName;
                });
                OkDialog(addPeakCompareDlg, addPeakCompareDlg.OkDialog);
            }

            private static void AddFile(ComparePeakPickingDlg dlg, string fileName, string filePath, int dialogSkip = 0)
            {
                var addPeakCompareDlg = ShowDialog<AddPeakCompareDlg>(dlg.Add);
                RunUI(() =>
                {
                    addPeakCompareDlg.IsModel = false;
                    addPeakCompareDlg.FileName = fileName;
                    addPeakCompareDlg.FilePath = filePath;
                });
                if (dialogSkip == 0)
                {
                    RunUI(addPeakCompareDlg.OkDialog);
                }
                else
                {
                    var messageDlg = ShowDialog<MultiButtonMsgDlg>(addPeakCompareDlg.OkDialog);
                    for (int i = 0; i < dialogSkip; ++i)
                    {
                        if (i > 0)
                        {
                            messageDlg = WaitForOpenForm<MultiButtonMsgDlg>();
                        }
                        RunUI(messageDlg.Btn1Click);
                        WaitForClosedForm<MultiButtonMsgDlg>();
                    }
                }
                WaitForClosedForm<AddPeakCompareDlg>();
            }

            public static void TrainModel(string modelName)
            {
                RunEditPeakScoringDlg(modelName, true, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.TrainModel(true);
                    editPeakScoringModelDlg.OkDialog();
                });
            }
        }
    }
}
