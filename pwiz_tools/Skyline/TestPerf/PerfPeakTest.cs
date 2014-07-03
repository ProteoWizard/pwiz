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
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;

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
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/MikeBHigh.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/LudovicN14N15.zip"
            };
            // IsPauseForScreenShots = true;
            RunFunctionalTest();
        }

        public readonly static string[] MODEL_NAMES = {"Skyline w/ mProphet", "Skyline Default", "Skyline Legacy", "Skyline w/ mProphet + Decoys"};

        public const double LOW_SIG = 0.01;

        public const double HIGH_SIG = 0.05;

        protected override void DoTest()
        {
            const string outDir = @"D:\Results\PeakCompare";
            string resultsTable = Path.Combine(outDir, "results.txt");
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
            using (var fs = new FileSaver(resultsTable))
            using (var resultsWriter = new StreamWriter(fs.SafeName))
            {
                WriteHeader(resultsWriter);
                // Hasmik Swath Heavy only
                AnalyzeDirectory(i++, peakViewSpectronautOpenSwath, outDir, secondOnly, resultsWriter, new List<int> { 1, 1, 1 });
                // Hasmik Qe Heavy only
                AnalyzeDirectory(i++, spectronaut, outDir, secondOnly, resultsWriter, new List<int> { 1 });
                // Hasmik SWATH
                AnalyzeDirectory(i++, peakViewSpectronautOpenSwath, outDir, secondOnly, resultsWriter, new List<int> { 1, 1, 1 });
                // Hasmik QE
                AnalyzeDirectory(i++, spectronaut, outDir, secondOnly, resultsWriter, new List<int> { 1 });
                // Hasmik Swath Light only
                AnalyzeDirectory(i++, peakViewSpectronaut, outDir, secondOnly, resultsWriter, new List<int> { 0, 1 });
                // Hasmik Qe Light only
                AnalyzeDirectory(i++, spectronaut, outDir, secondOnly, resultsWriter, new List<int> { 1 });
                // OpenSwath_Water
                AnalyzeDirectory(i++, peakViewOpenSwath, outDir, decoysAndSecond, resultsWriter, new List<int> { 2, 1 });
                // OpenSwath_Yeast
                AnalyzeDirectory(i++, peakViewOpenSwath, outDir, decoysAndSecond, resultsWriter, new List<int> { 1, 1 });
                // OpenSwath_Human
                AnalyzeDirectory(i++, peakViewOpenSwath, outDir, decoysAndSecond, resultsWriter, new List<int> { 2, 1 });
                // Olga SRM Course vantage
                AnalyzeDirectory(i++, none, outDir, decoysAndSecond, resultsWriter);
                // Olga SRM Course
                AnalyzeDirectory(i++, none, outDir, decoysAndSecond, resultsWriter);
                // Heart Failure
                AnalyzeDirectory(i++, none, outDir, secondOnly, resultsWriter);
                // Ovarian cancer
                AnalyzeDirectory(i++, none, outDir, secondOnly, resultsWriter);
                // MikeB high (DDA dilution top 12 runs)
                AnalyzeDirectory(i++, none, outDir, secondOnly, resultsWriter);
                // Ludovic N14N15
                //AnalyzeDirectory(i++, none, outDir, secondOnly, resultsWriter);
                resultsWriter.Close();
                fs.Commit();
            }

        }

        public void WriteHeader(TextWriter writer)
        {
            const char separator = TextUtil.SEPARATOR_TSV;
            // ReSharper disable NonLocalizedString
            var namesArray = new List<string>
                {
                    "DataSet",
                    "ToolName",
                    "Peptides01",
                    "Peptides05",
                    "PeptdesAll",
                    "QValue01",
                    "QValue05"
                };
            // ReSharper restore NonLocalizedString

            bool first = true;
            foreach (var name in namesArray)
            {
                if (first)
                    first = false;
                else
                    writer.Write(separator);
                writer.WriteDsvField(name, separator);
            }
            writer.WriteLine();
        }

        public void AnalyzeDirectory(int index, IEnumerable<string> externals, string outDir, IList<string> modelNames, TextWriter resultsWriter, IList<int> dialogSkips = null)
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
            DataSetAnalyzer dataSetAnalyzer = new DataSetAnalyzer(skylineDoc, modelNames, externalsPaths, outDir, dialogSkips, resultsWriter);
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
            public DataSetAnalyzer(string skylineDocument, IList<string> modelNames, IEnumerable<string> filePaths, string outDir, IList<int> dialogSkips, TextWriter resultsWriter)
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
                foreach (var comparePeakBoundaries in comparePeakPickingDlg.ComparePeakBoundariesList)
                {
                    PointPairList rocPoints;
                    PointPairList qqPoints;
                    ComparePeakPickingDlg.MakeRocLists(comparePeakBoundaries, out rocPoints);
                    ComparePeakPickingDlg.MakeQValueLists(comparePeakBoundaries, out qqPoints);
                    double peptides01 = GetCurveThreshold(rocPoints, LOW_SIG);
                    double peptides05 = GetCurveThreshold(rocPoints, HIGH_SIG);
                    double peptidesAll = GetCurveThreshold(rocPoints, double.MaxValue);
                    double qValue01 = GetCurveThreshold(qqPoints, LOW_SIG);
                    double qValue05 = GetCurveThreshold(qqPoints, HIGH_SIG);
                    WriteLine(resultsWriter, trimmedDoc, comparePeakBoundaries.Name, peptides01, peptides05, peptidesAll, qValue01, qValue05);
                }
                var sbRoc = new StringBuilder(trimmedDoc);
                sbRoc.Append("-roc.bmp");
                comparePeakPickingDlg.ZedGraphRoc.MasterPane.GetImage().Save(Path.Combine(outDir, sbRoc.ToString()));
                var sbQq = new StringBuilder(trimmedDoc);
                sbQq.Append("-qq.bmp");
                comparePeakPickingDlg.ZedGraphQq.MasterPane.GetImage().Save(Path.Combine(outDir, sbQq.ToString()));
                // TODO: How can I copy the data using commands?
                OkDialog(comparePeakPickingDlg, comparePeakPickingDlg.OkDialog);
            }

            public double GetCurveThreshold(PointPairList points, double cutoff)
            {
                var peptidesThreshPt = points.LastOrDefault(point => point.X < cutoff);
                return peptidesThreshPt == null ? points.First().Y : peptidesThreshPt.Y;
            }

            public void WriteLine(TextWriter writer,
                          string dataset,
                          string toolName,
                          double peptides01,
                          double peptides05,
                          double peptidesAll,
                          double qValue01,
                          double qValue05)
            {
                const char separator = TextUtil.SEPARATOR_TSV;
                var fieldsArray = new List<string>
                {
                    dataset,
                    toolName,
                    Convert.ToString(peptides01),
                    Convert.ToString(peptides05),
                    Convert.ToString(peptidesAll),
                    Convert.ToString(qValue01),
                    Convert.ToString(qValue05),
                };
                bool first = true;
                foreach (var name in fieldsArray)
                {
                    if (first)
                        first = false;
                    else
                        writer.Write(separator);
                    writer.WriteDsvField(name, separator);
                }
                writer.WriteLine();
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
                    editPeakScoringModelDlg.PeakCalculatorsGrid.Items[3].IsEnabled = false;
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
