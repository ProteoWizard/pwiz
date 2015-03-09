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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using ZedGraph;

namespace TestPerf // Note: tests in the "TestPerf" namespace only run when the global RunPerfTests flag is set
{
    [TestClass]
    public class PerfPeakTest : AbstractFunctionalTest
    {
        [TestMethod, Timeout(7200000)]
        public void TestPeakPerf()
        {
            TestFilesZipPaths = new[]
            {
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Overlap10mz.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Overlap20mz.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Schilling_Ack.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Schilling_Mito.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/ReiterSPRG.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/LudwigSPRG.zip",
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
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/mProphetGS.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/MikeBHigh.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/SchillingDDA.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HeldDIA.zip",
            };
            // IsPauseForScreenShots = true;
            RunFunctionalTest();
        }

        /// <summary>
        /// Do we rescore the peaks before running the test?
        /// </summary>
        public bool RescorePeaks { get { return false; } }

        public readonly static string[] MODEL_NAMES = {"Skyline w/ mProphet", "Skyline Default", "Skyline Legacy", "Skyline w/ mProphet + Decoys"};

        public const double LOW_SIG = 0.01;

        public const double HIGH_SIG = 0.05;

        protected override void DoTest()
        {
            string outDir = Path.Combine(TestContext.TestDir, "PerfPeakTest");
            string resultsTable = Path.Combine(outDir, "results.txt");
            Directory.CreateDirectory(outDir); // In case it doesn't already exists
            File.Delete(resultsTable); // In case it does already exist
            var none = new string[0];
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
                // Overlap 10 mz
                AnalyzeDirectory(i++, none, outDir, decoysAndSecond, resultsWriter);
                // Overlap 20 mz
                AnalyzeDirectory(i++, none, outDir, decoysAndSecond, resultsWriter);
                // Schilling Ack
                AnalyzeDirectory(i++, peakView, outDir, decoysAndSecond, resultsWriter, new List<int> { 1 });
                // Schilling Mito
                AnalyzeDirectory(i++, peakView, outDir, decoysAndSecond, resultsWriter, new List<int> { 1 });
                // Reiter sPRG
                AnalyzeDirectory(i++, spectronaut, outDir, decoysAndSecond, resultsWriter, new List<int> { 0 });
                // Ludwig sPRG
                AnalyzeDirectory(i++, none, outDir, decoysAndSecond, resultsWriter, new List<int> { 1 });
                // Hasmik Swath Heavy only
                AnalyzeDirectory(i++, peakViewSpectronautOpenSwath, outDir, decoysAndSecond, resultsWriter, new List<int> { 1, 1, 1 });
                // Hasmik Qe Heavy only
                AnalyzeDirectory(i++, spectronaut, outDir, decoysAndSecond, resultsWriter, new List<int> { 1 });
                // Hasmik SWATH
                AnalyzeDirectory(i++, peakViewSpectronautOpenSwath, outDir, decoysAndSecond, resultsWriter, new List<int> { 1, 1, 1 });
                // Hasmik QE
                AnalyzeDirectory(i++, spectronaut, outDir, decoysAndSecond, resultsWriter, new List<int> { 1 });
                // Hasmik Swath Light only
                AnalyzeDirectory(i++, peakViewSpectronaut, outDir, decoysAndSecond, resultsWriter, new List<int> { 0, 1 });
                // Hasmik Qe Light only
                AnalyzeDirectory(i++, spectronaut, outDir, decoysAndSecond, resultsWriter, new List<int> { 1 });
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
                // mProphet Gold
                AnalyzeDirectory(i++, none, outDir, decoysAndSecond, resultsWriter);
                // MikeB high (DDA dilution top 12 runs)
                AnalyzeDirectory(i++, none, outDir, secondOnly, resultsWriter);
                // Schilling DDA (DDA dilution 15 runs)
                AnalyzeDirectory(i++, none, outDir, secondOnly, resultsWriter);
                // Held DIA
                AnalyzeDirectory(i++, peakView, outDir, decoysAndSecond, resultsWriter, new List<int> { 4 });
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
            else if (directoryName != null)
                outDir = Path.Combine(outDir, directoryName);
            Directory.CreateDirectory(outDir);
            var sb = new StringBuilder(directoryName);
            sb.Append(".sky");
            var skylineName = sb.ToString();
            var skylineDoc = Path.Combine(directoryPath, skylineName);
            var externalsPaths = externals.Select(name => Path.Combine(directoryPath, name));
// ReSharper disable UnusedVariable
            DataSetAnalyzer dataSetAnalyzer = new DataSetAnalyzer(skylineDoc, modelNames, externalsPaths, outDir, dialogSkips, resultsWriter, RescorePeaks);
// ReSharper restore UnusedVariable
        }


        protected class DataSetAnalyzer
        {
            public string OutDir { get; private set; }

            public DataSetAnalyzer(string skylineDocument, 
                                   IList<string> modelNames, 
                                   IEnumerable<string> filePaths,
                                   string outDir,
                                   IList<int> dialogSkips,
                                   TextWriter resultsWriter,
                                   bool rescorePeaks)
            {
                OutDir = outDir;
                Settings.Default.PeakScoringModelList.Clear();
                RunUI(() => SkylineWindow.OpenFile(skylineDocument));
                WaitForDocumentLoaded();
                if (rescorePeaks)
                    RescorePeaks();
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
                comparePeakPickingDlg.ComboYAxis = Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Fraction_of_Manual_ID_s;
                var trimmedDoc = Path.GetFileNameWithoutExtension(skylineDocument);
                foreach (var comparePeakBoundaries in comparePeakPickingDlg.ComparePeakBoundariesList)
                {
                    PointPairList rocPoints;
                    PointPairList qqPoints;
                    ComparePeakPickingDlg.MakeRocLists(comparePeakBoundaries, ComparePeakPickingDlg.NormalizeType.frac_manual, out rocPoints);
                    ComparePeakPickingDlg.MakeQValueLists(comparePeakBoundaries, out qqPoints);
                    double peptides01 = GetCurveThreshold(rocPoints, LOW_SIG);
                    double peptides05 = GetCurveThreshold(rocPoints, HIGH_SIG);
                    double peptidesAll = GetCurveThreshold(rocPoints, Double.MaxValue);
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
                var sbFiles = new StringBuilder(trimmedDoc);
                sbFiles.Append("-files.bmp");
                comparePeakPickingDlg.ZedGraphFile.MasterPane.GetImage().Save(Path.Combine(outDir, sbFiles.ToString()));
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
                    Convert.ToString(peptides01, CultureInfo.InvariantCulture),
                    Convert.ToString(peptides05, CultureInfo.InvariantCulture),
                    Convert.ToString(peptidesAll, CultureInfo.InvariantCulture),
                    Convert.ToString(qValue01, CultureInfo.InvariantCulture),
                    Convert.ToString(qValue05, CultureInfo.InvariantCulture),
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

            public void RescorePeaks()
            {
                var peakBoundariesFile = Path.Combine(OutDir, "PeakBoundaries.csv");
                // Export the peak boundaries
                ReportToCsv(MakeReportSpec(), SkylineWindow.Document, peakBoundariesFile);

                // Do the actual rescore
                var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                var rescoreResultsDlg = ShowDialog<RescoreResultsDlg>(manageResults.Rescore);
                RunUI(() => rescoreResultsDlg.Rescore(false));
                WaitForCondition(20 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 20 minutes
                WaitForClosedForm(rescoreResultsDlg);
                WaitForClosedForm(manageResults);

                // Re-import peak boundaries
                RunUI(() => SkylineWindow.ImportPeakBoundariesFile(peakBoundariesFile));
                WaitForCondition(5 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 5 minutes
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

            private static ReportSpec MakeReportSpec()
            {
                var specList = new ReportSpecList();
                var defaults = specList.GetDefaults();
                return defaults.First(spec => spec.Name == Resources.ReportSpecList_GetDefaults_Peak_Boundaries);
            }

            public static void ReportToCsv(ReportSpec reportSpec, SrmDocument doc, string fileName)
            {
                Report report = Report.Load(reportSpec);
                using (var saver = new FileSaver(fileName))
                using (var writer = new StreamWriter(saver.SafeName))
                using (var database = new Database(doc.Settings))
                {
                    database.AddSrmDocument(doc);
                    var resultSet = report.Execute(database);
                    char separator = TextUtil.CsvSeparator;
                    ResultSet.WriteReportHelper(resultSet, separator, writer, LocalizationHelper.CurrentCulture);
                    writer.Flush();
                    writer.Close();
                    saver.Commit();
                }
            }
        }
    }
}
