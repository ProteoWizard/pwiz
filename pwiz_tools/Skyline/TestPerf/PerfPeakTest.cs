﻿/*
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
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
        private struct DataSetParams
        {
            public DataSetParams(string url, IList<string> externals, IList<string> modelNames, IList<int> dialogSkips = null) : this()
            {
                Url = url;
                Externals = externals;
                ModelNames = modelNames;
                DialogSkips = dialogSkips;
            }

            public string Url { get; private set; }
            public IList<string> Externals { get; private set; }
            public IList<string> ModelNames { get; private set; }
            public IList<int> DialogSkips { get; private set; }
        }

        public static readonly string[] MODEL_NAMES = {"Skyline w/ mProphet", "Skyline Default", "Skyline Legacy", "Skyline w/ mProphet + Decoys"};

        private static readonly string[] none = new string[0];
        private static readonly string[] openSwath = { "OpenSwath.csv" };
        private static readonly string[] spectronaut = { "Spectronaut.csv" };
        private static readonly string[] peakView = { "PeakView.txt" };
        private static readonly string[] peakViewSpectronaut = spectronaut.Concat(peakView).ToArray();
        private static readonly string[] peakViewOpenSwath = openSwath.Concat(peakView).ToArray();
        private static readonly string[] peakViewSpectronautOpenSwath = peakViewSpectronaut.Concat(openSwath).ToArray();
        private static readonly string[] spectronautOpenSwath = spectronaut.Concat(openSwath).ToArray();
        private static readonly string[] decoysAndSecond = MODEL_NAMES;
        private static readonly string[] secondOnly = MODEL_NAMES.Take(3).ToArray();

        private static readonly DataSetParams[] DataSets = 
        {
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/Overlap10mz.zip",
                none, decoysAndSecond), 
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/Overlap20mz.zip",
                none, decoysAndSecond), 
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/Schilling_Ack.zip",
                peakView, secondOnly, new[] { 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/Schilling_Mito.zip",
                peakView, secondOnly, new[] { 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/ReiterSPRG.zip",
                spectronaut, decoysAndSecond, new[] { 0 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/LudwigSPRG.zip",
                none, decoysAndSecond, new[] { 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikSwathHeavy.zip",
                peakViewSpectronautOpenSwath, decoysAndSecond, new[] { 1, 1, 2 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikQeHeavy.zip",
                spectronautOpenSwath, decoysAndSecond, new[] { 1, 2 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikSwath.zip",
                peakViewSpectronautOpenSwath, decoysAndSecond, new[] { 1, 1, 2 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikQe.zip",
                spectronaut, decoysAndSecond, new[] { 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikSwathLight.zip",
                peakViewSpectronaut, decoysAndSecond, new[] { 0, 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HasmikQeLight.zip",
                spectronaut, decoysAndSecond, new[] { 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Water.zip",
                peakViewOpenSwath, decoysAndSecond, new List<int> { 2, 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Yeast.zip",
                peakViewOpenSwath, decoysAndSecond, new List<int> { 1, 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Human.zip",
                peakViewOpenSwath, decoysAndSecond, new List<int> { 2, 1 }),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course_vantage.zip",
                none, decoysAndSecond),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course.zip",
                none, decoysAndSecond),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HeartFailure.zip",
                none, secondOnly),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/OvarianCancer.zip",
                none, secondOnly),
//            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/mProphetGS.zip",
//                none, decoysAndSecond),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/MikeBHigh.zip",
                none, secondOnly),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/SchillingDDA.zip",
                none, secondOnly),
            new DataSetParams(@"http://proteome.gs.washington.edu/software/test/skyline-perf/HeldDIA.zip",
                peakView, decoysAndSecond, new List<int> { 4 })
        };

        private string REPORT_DIRECTORY
        {
            get
            {
                return null;     // To allow plots to be written with test files and deleted after test run
                // return @"C:\proj\pwiz\pwiz_tools\Skyline\TestResults";
            }
        }

        [TestMethod, Timeout(7200000)]
        public void TestPeakPerf()
        {
            // RunPerfTests = true;

            TestFilesPersistent = new[] {"."};  // All persistent. No saving
            TestFilesZipPaths = DataSets.Select(p => p.Url).ToArray();

            RunFunctionalTest();
        }

        /// <summary>
        /// Do we rescore the peaks before running the test?
        /// </summary>
        public bool RescorePeaks { get { return false; } }

        public const double LOW_SIG = 0.01;

        public const double HIGH_SIG = 0.05;

        protected override void DoTest()
        {
            string outDir = Path.Combine(REPORT_DIRECTORY ?? TestContext.TestDir, GetType().Name);
            string resultsTable = Path.Combine(outDir, "results.txt");
            Directory.CreateDirectory(outDir); // In case it doesn't already exists
            File.Delete(resultsTable); // In case it does already exist

            using (var fs = new FileSaver(resultsTable))
            using (var resultsWriter = new StreamWriter(fs.SafeName))
            {
                WriteHeader(resultsWriter);
                for (int i = 0; i < DataSets.Length; i++)
                {
                    var p = DataSets[i];
                    try
                    {
                        AnalyzeDirectory(i, p.Externals, outDir, p.ModelNames, resultsWriter, p.DialogSkips);
                    }
                    catch (Exception e)
                    {
                        Assert.Fail("Failed analyzing {0}\r\n{1}", p.Url, e);
                    }
                }

                resultsWriter.Close();
                fs.Commit();
            }
        }

        public void WriteHeader(TextWriter writer)
        {
            const char separator = TextUtil.SEPARATOR_TSV;
            // ReSharper disable LocalizableElement
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
            // ReSharper restore LocalizableElement

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
            if (index >= TestFilesZipPaths.Length)
                return;

            if(dialogSkips == null)
                dialogSkips = new List<int>();
            var fileUrl = TestFilesZipPaths[index];
            var directoryName = Path.GetFileNameWithoutExtension(fileUrl);
            var testFilesDir = new TestFilesDir(TestContext, fileUrl, null, new [] {directoryName});
            var directoryPath = testFilesDir.GetTestPath(directoryName);
            var skylineName = directoryName + SrmDocument.EXT;
            var skylineDoc = Path.Combine(directoryPath, skylineName);
            var externalsPaths = externals.Select(name => Path.Combine(directoryPath, name));
// ReSharper disable UnusedVariable
            var dataSetAnalyzer = new DataSetAnalyzer(index, skylineDoc, modelNames, externalsPaths, outDir,
                dialogSkips, resultsWriter, RescorePeaks);
// ReSharper restore UnusedVariable
        }


        protected class DataSetAnalyzer
        {
            public string OutDir { get; private set; }

            public DataSetAnalyzer(int index,
                                   string skylineDocument, 
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
                    AddFile(comparePeakPickingDlg, fileName, filePath, outDir, dialogSkips[i]);
                    ++i;
                }
                RunUI(() => comparePeakPickingDlg.ComboYAxis = Resources.ComparePeakPickingDlg_ComparePeakPickingDlg_Fraction_of_Manual_ID_s);
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
                var baseName = (index+1) + "-" + trimmedDoc;
                var rocPath = Path.Combine(outDir, baseName + "-roc.bmp");
                RunUI(() => comparePeakPickingDlg.ZedGraphRoc.MasterPane.GetImage().Save(rocPath));
                var qqPath = Path.Combine(outDir, baseName + "-qq.bmp");
                RunUI(() => comparePeakPickingDlg.ZedGraphQq.MasterPane.GetImage().Save(qqPath));
                var filesPath = Path.Combine(outDir, baseName + "-files.bmp");
                RunUI(() => comparePeakPickingDlg.ZedGraphFile.MasterPane.GetImage().Save(filesPath));
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
                    editPeakScoringModelDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
                    editPeakScoringModelDlg.UsesDecoys = false;
                    editPeakScoringModelDlg.UsesSecondBest = true;
                    editPeakScoringModelDlg.TrainModel(true);
                    editPeakScoringModelDlg.OkDialog();
                });
                RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.PeakScoringModelName = MODEL_NAMES[1];
                    editPeakScoringModelDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
                    editPeakScoringModelDlg.UsesDecoys = false;
                    editPeakScoringModelDlg.UsesSecondBest = true;
                    editPeakScoringModelDlg.TrainModel(true);
                    editPeakScoringModelDlg.OkDialog();
                });
                RunEditPeakScoringDlg(null, true, editPeakScoringModelDlg =>
                {
                    editPeakScoringModelDlg.PeakScoringModelName = MODEL_NAMES[2];
                    editPeakScoringModelDlg.SelectedModelItem = LegacyScoringModel.DEFAULT_NAME;
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
                        editPeakScoringModelDlg.SelectedModelItem = MProphetPeakScoringModel.NAME;
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

            private static void AddFile(ComparePeakPickingDlg dlg, string fileName, string filePath, string outDir, int dialogSkip = 0)
            {
                string intlPath = Path.Combine(outDir, fileName + "Intl" + Path.GetExtension(filePath));
                if (TextUtil.WriteDsvToCsvLocal(filePath, intlPath, true))
                    filePath = intlPath;

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
                        OkDialog(messageDlg, messageDlg.Btn1Click);
                    }
                }
                var messageDlgExtra = TryWaitForOpenForm<MultiButtonMsgDlg>(100);
                if (messageDlgExtra != null)
                {
                    string message = messageDlgExtra.Message;
                    OkDialog(messageDlgExtra, messageDlgExtra.BtnCancelClick);
                    Assert.Fail("Unexpected message dialog after {0}: {1}", dialogSkip, message);
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
                CheckReportCompatibility.ReportToCsv(reportSpec, doc, fileName, CultureInfo.CurrentCulture);
            }
        }
    }
}
