/*
 * Original author: Dario Amodei <damodei .at. stanford.edu>,
 *                  Mallick Lab, Department of Radiology, Stanford
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.Properties;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    //[TestClass]
    public class PeakScoreCompareTest : AbstractFunctionalTest
    {

        //[TestMethod]
        public void TestPeakScoreCompare()
        {
            IsPauseForScreenShots = true;
            TestFilesZipPaths = new[]
            {
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Water.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Yeast.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OpenSWATH_Human.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course_vantage.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/Olga_srm_course.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/HeartFailure.zip",
                 @"http://proteome.gs.washington.edu/software/test/skyline-perf/OvarianCancer.zip",
            };
            RunFunctionalTest();
        }

        public readonly List<string> _datasetNames = new List<string> { "OpenSWATH_Water", "OpenSWATH_Yeast", "OpenSWATH_Human", "Olga_srm_course_vantage", "Olga_srm_course", "HeartFailure", "OvarianCancer" };

        public string GetTestPath(string path, string dataSetName)
        {
            int dataSetIndex = _datasetNames.IndexOf(dataSetName);
            if(dataSetIndex == -1)
                throw new InvalidDataException("Dataset name not found.");
            return GetTestPath(path, dataSetIndex);
        }

        public string GetTestPath(string path, int dataSetIndex)
        {
            return TestFilesDirs[dataSetIndex].GetTestPath(Path.Combine(_datasetNames[dataSetIndex], path));
        }

        protected override void DoTest()
        {
            const bool rescore = false;
            // OpenSWATH dataset (water background)
            var dataSetComparer = new DataSetComparer(GetTestPath("AQUA4_Water_picked_hroest.sky", "OpenSWATH_Water"), "Manual", true);
            dataSetComparer.Add(GetTestPath("OpenSWATH_SM3_GoldStandardAutomatedResults_water_peakgroups.txt", "OpenSWATH_Water"), "OpenSWATH", false);
            dataSetComparer.RunFullTestDataSet(GetTestPath("AQUA4_Water_picked_hroest_full_rescore_iRT_plus.sky", "OpenSWATH_Water"), rescore, true, false, "_iRT_plus");
            dataSetComparer.ExportQValues(GetTestPath("AQUA4_Water_picked_hroest", "OpenSWATH_Water"));

            // OpenSWATH dataset (yeast background)
            var dataSetComparerYeast = new DataSetComparer(GetTestPath("AQUA4_Yeast_picked_georger_2.sky", "OpenSWATH_Yeast"), "Manual", true);
            dataSetComparerYeast.Add(GetTestPath("OpenSWATH_SM3_GoldStandardAutomatedResults_yeast_peakgroups.txt", "OpenSWATH_Yeast"), "OpenSWATH", false);
            dataSetComparerYeast.RunFullTestDataSet(GetTestPath("AQUA4_Yeast_picked_georger_2_full_rescore_iRT_plus.sky", "OpenSWATH_Yeast"), rescore, true, false, "_iRT_plus");
            dataSetComparerYeast.ExportQValues(GetTestPath("AQUA4_Yeast_picked_georger_2", "OpenSWATH_Yeast"));

            // OpenSWATH dataset (human background)
            var dataSetComparerHuman = new DataSetComparer(GetTestPath("AQUA4_Human_picked_napedro2.sky", "OpenSWATH_Human"), "Manual", true);
            dataSetComparerHuman.Add(GetTestPath("OpenSWATH_SM3_GoldStandardAutomatedResults_human_peakgroups.txt", "OpenSWATH_Human"), "OpenSWATH", false);
            dataSetComparerHuman.RunFullTestDataSet(GetTestPath("AQUA4_Human_picked_napedro2_full_rescore_iRT_plus.sky", "OpenSWATH_Human"), rescore, true, false, "_iRT_plus");
            dataSetComparerHuman.ExportQValues(GetTestPath("AQUA4_Human_picked_napedro2", "OpenSWATH_Human"));

            // Olga_srm_vantage
            var dataSetComparerOlgaSV = new DataSetComparer(GetTestPath("SRMCourse_DosR-hDP__TSQv_true.sky", "Olga_srm_course_vantage"), "Manual", true);
            dataSetComparerOlgaSV.RunFullTestDataSet(GetTestPath("SRMCourse_DosR-hDP__TSQv_base.sky", "Olga_srm_course_vantage"), false, true, false, null, true);
            dataSetComparerOlgaSV.ExportQValues(GetTestPath("Olga_SRM_course_vantage", "Olga_srm_course_vantage"));

            // Olga_srm
            var dataSetComparerOlgaS = new DataSetComparer(GetTestPath("SRMCourse_DosR-hDP__20130501_true.sky", "Olga_srm_course"), "Manual", true);
            dataSetComparerOlgaS.RunFullTestDataSet(GetTestPath("SRMCourse_DosR-hDP__20130501_base.sky", "Olga_srm_course"), false, true, false, null, true);
            dataSetComparerOlgaS.ExportQValues(GetTestPath("Olga_SRM_course", "Olga_srm_course"));

            // Heart Failure
            var dataSetComparerHeartFailure = new DataSetComparer(GetTestPath("Rat_plasma_true.sky", "HeartFailure"), "Manual", true);
            dataSetComparerHeartFailure.RunFullTestDataSet(GetTestPath("Rat_plasma_base.sky", "HeartFailure"), false, false, true, null, true);
            dataSetComparerHeartFailure.ExportQValues(GetTestPath("HeartFailure", "HeartFailure"));

            // Ovarian Cancer
            var dataSetComparerOvarianCancer = new DataSetComparer(GetTestPath("Human_plasma_base.sky", "OvarianCancer"), "Manual", true);
            dataSetComparerOvarianCancer.RunFullTestDataSet(GetTestPath("Human_plasma_no_heavy.sky", "OvarianCancer"), false, false, true, null, true);
            dataSetComparerOvarianCancer.ExportQValues(GetTestPath("OvarianCancer", "OvarianCancer"));

            // mProphet Gold Standard

            // Demux dataset (10 mz)

            // Demux dataset (20 mz)

            // Demux dataset (demux)

            PauseForScreenShot("All tasks completed.");
        }

        public class DataSetComparer
        {
            public IList<PeakBoundsComparer> Comparers { get; private set; }
            public PeakBoundsSource SourceTrue { get; private set; }
            public IList<string> Names { get; private set; }
            public IList<double?> ScoreValues { get; private set; }

            public DataSetComparer(string fileName, string name, bool isSrmDocument) : this(new PeakBoundsSource(fileName, name, isSrmDocument))
            {
            }

            public DataSetComparer(PeakBoundsSource sourceTrue, IEnumerable<PeakBoundsSource> sourcesPicked = null, IList<double?> scoreValues = null)
            {
                SourceTrue = sourceTrue;
                ScoreValues = scoreValues;
                Names = new List<string>();
                Comparers = new List<PeakBoundsComparer>();
                if (sourcesPicked == null)
                    return;
                foreach (var sourcePicked in sourcesPicked)
                    Add(sourcePicked);
            }

            public void Add(string fileName, string name, bool isSrmDocument)
            {
                Add(new PeakBoundsSource(fileName, name, isSrmDocument));
            }

            public void Add(PeakBoundsSource sourcePicked)
            {
                var comparer = new PeakBoundsComparer(sourcePicked, SourceTrue);
                comparer.ComputeScoreStatistics(ScoreValues);
                Comparers.Add(comparer);
                Names.Add(sourcePicked.Name);
            }

            public void ExportQValues(string baseName)
            {
                foreach (var comparer in Comparers)
                {
                    comparer.ExportRocPlots(baseName + comparer.Name + ".txt");
                }
            }

            public void RunFullTestDataSet(string documentFile, 
                                           bool rescore = false,
                                           bool usesDecoys = true,
                                           bool usesSecondBest = false,
                                           string appendText = null,
                                           bool reimport = false)
            {
                Settings.Default.PeakScoringModelList.Clear();
                // Open the document
                RunUI(() => SkylineWindow.OpenFile(documentFile));
                WaitForDocumentLoaded();

                if (appendText == null)
                    appendText = "";

                if (reimport)
                {
                    var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                    RunUI(() =>
                    {
                        manageResults.SelectedChromatograms = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms;
                        manageResults.ReimportResults();
                        manageResults.OkDialog();
                    });
                    WaitForCondition(10 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 10 minutes
                    WaitForDocumentLoaded(400000);
                }

                // Rescore the document
                if (rescore)
                {
                    var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
                    var rescoreResultsDlg = ShowDialog<RescoreResultsDlg>(manageResults.Rescore);

                    RunUI(() => rescoreResultsDlg.Rescore(false));
                    WaitForCondition(10 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 10 minutes
                    WaitForClosedForm(rescoreResultsDlg);
                    WaitForClosedForm(manageResults);
                }

                // Reintegrate default
                var reintegrateDlgDefault = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                var editDlgDefault = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlgDefault.AddPeakScoringModel);
                RunUI(() =>
                {
                    editDlgDefault.SelectedModelItem = "Default";
                    editDlgDefault.UsesDecoys = usesDecoys;
                    editDlgDefault.UsesSecondBest = usesSecondBest;
                    editDlgDefault.PeakCalculatorsGrid.Items[6].IsEnabled = false;
                    editDlgDefault.PeakCalculatorsGrid.Items[5].IsEnabled = false;
                    editDlgDefault.PeakCalculatorsGrid.Items[4].IsEnabled = false;
                    editDlgDefault.PeakScoringModelName = "default";
                    editDlgDefault.TrainModel(true);
                });
                OkDialog(editDlgDefault, editDlgDefault.OkDialog);
                RunUI(() =>
                {
                    reintegrateDlgDefault.ScoreAnnotation = true;
                    reintegrateDlgDefault.ReintegrateAll = true;
                    reintegrateDlgDefault.AddAnnotation = true;
                    reintegrateDlgDefault.OverwriteManual = true;
                });
                OkDialog(reintegrateDlgDefault, reintegrateDlgDefault.OkDialog);
                WaitForDocumentLoaded();

                RunUI(() => Add(new PeakBoundsSource(SkylineWindow.Document, "SkylineDefault" + appendText)));

                // Reintegrate default plus
                var reintegrateDlgDefaultPlus = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                var editDlgDefaultPlus = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlgDefaultPlus.AddPeakScoringModel);
                RunUI(() =>
                {
                    editDlgDefaultPlus.SelectedModelItem = "Default";
                    editDlgDefaultPlus.UsesDecoys = usesDecoys;
                    editDlgDefaultPlus.UsesSecondBest = usesSecondBest;
                    editDlgDefault.PeakCalculatorsGrid.Items[6].IsEnabled = true;
                    editDlgDefault.PeakCalculatorsGrid.Items[5].IsEnabled = true;
                    editDlgDefault.PeakCalculatorsGrid.Items[4].IsEnabled = true;
                    editDlgDefaultPlus.PeakScoringModelName = "default_plus";
                    editDlgDefaultPlus.TrainModel(true);
                });
                OkDialog(editDlgDefaultPlus, editDlgDefaultPlus.OkDialog);
                RunUI(() =>
                {
                    reintegrateDlgDefault.ScoreAnnotation = true;
                    reintegrateDlgDefaultPlus.ReintegrateAll = true;
                    reintegrateDlgDefaultPlus.AddAnnotation = true;
                    reintegrateDlgDefaultPlus.OverwriteManual = true;
                });
                OkDialog(reintegrateDlgDefaultPlus, reintegrateDlgDefaultPlus.OkDialog);
                WaitForDocumentLoaded();

                RunUI(() => Add(new PeakBoundsSource(SkylineWindow.Document, "SkylineDefaultPlus" + appendText)));

                // Reintegrate
                var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                var editDlg = ShowDialog<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel);
                RunUI(() =>
                {
                    editDlg.UsesDecoys = usesDecoys;
                    editDlg.UsesSecondBest = usesSecondBest;
                    editDlg.PeakScoringModelName = "mProphet";
                    editDlg.TrainModel();
                });
                OkDialog(editDlg, editDlg.OkDialog);
                RunUI(() =>
                {
                    reintegrateDlgDefault.ScoreAnnotation = true;
                    reintegrateDlg.ReintegrateAll = true;
                    reintegrateDlg.AddAnnotation = true;
                    reintegrateDlg.OverwriteManual = true;
                });
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
                WaitForDocumentLoaded();

                RunUI(() => Add(new PeakBoundsSource(SkylineWindow.Document, "SkylineModelReintegrateAll" + appendText)));

                Settings.Default.PeakScoringModelList.Clear();
            }
        }

        public class PeakBoundsComparer
        {
            private readonly IList<PeakBoundsMatch> _peakBoundsMatchList;
            const char SEPARATOR = TextUtil.SEPARATOR_TSV;
            private const double PERCENTILE_INCREMENT = 0.01;

            public IList<PeakDataKey> Unmatched { get; private set; }

            public string Name { get; private set; }

            public Dictionary<double?, MatchStatistics> QValueData { get; private set; }

            public PeakBoundsComparer(PeakBoundsSource pickedSource, PeakBoundsSource trueSource)
            {
                Name = pickedSource.Name;
                Unmatched = new List<PeakDataKey>();
                QValueData = new Dictionary<double?, MatchStatistics>();
                var pickedPeaks = pickedSource.DictPeakBoundaries;
                var truePeaks = trueSource.DictPeakBoundaries;
                _peakBoundsMatchList = new List<PeakBoundsMatch>();
                foreach (var peakDataKey in truePeaks.Keys)
                {
                    if (!pickedPeaks.ContainsKey(peakDataKey))
                    {
                        pickedPeaks.Add(peakDataKey, new PeakBounds(double.NaN, double.NaN, double.NaN));
                    }
                    _peakBoundsMatchList.Add(new PeakBoundsMatch(peakDataKey, pickedPeaks[peakDataKey], truePeaks[peakDataKey]));
                }
                foreach (var peakDataKey in pickedPeaks.Keys)
                {
                    if (!truePeaks.ContainsKey(peakDataKey))
                    {
                        Unmatched.Add(peakDataKey);
                    }
                }
            }

            public IList<double> GenerateScoreValues()
            {
                var scoreValuesAll = _peakBoundsMatchList.Select(match => match.Score ?? double.MinValue).ToList();
                var scoreValuesStats = new Statistics(scoreValuesAll);
                var percentiles = new List<double>();
                for (int i = 0; i * PERCENTILE_INCREMENT < 1.0; ++i)
                {
                    percentiles.Add(i * PERCENTILE_INCREMENT);
                }
                return percentiles.Select(scoreValuesStats.Percentile).ToList();
            }

            public double TotalPeaks { get { return _peakBoundsMatchList.Count; } }

            public void ComputeScoreStatistics(IList<double?> scoreValues)
            {
                if (scoreValues == null)
                {
                    var qValuesTemp = GenerateScoreValues().Distinct().ToList();
                    qValuesTemp.Sort();
                    scoreValues = qValuesTemp.Select(value => (double?)value).ToList();
                }
                QValueData.Clear();
                foreach (var qValue in scoreValues)
                {
                    QValueData.Add(qValue, ComputeMatchStatistics(qValue));
                }
            }

            public MatchStatistics ComputeMatchStatistics(double? cutoff)
            {
                int correctCalls = _peakBoundsMatchList.Count(match => match.IsCorrectCall(cutoff));
                int incorrectCalls = _peakBoundsMatchList.Count(match => match.IsIncorrectCall(cutoff));
                int falseNegatives = _peakBoundsMatchList.Count(match => match.IsFalseNegative(cutoff));
                int falsePositives = _peakBoundsMatchList.Count(match => match.IsUnsupportedPositive(cutoff));
                int trueNegatives = _peakBoundsMatchList.Count(match => match.IsMutualMissing(cutoff));
                int totalAnnotated = _peakBoundsMatchList.Count(match => !match.IsMissingTrue);
                int totalCalls = _peakBoundsMatchList.Count(match => !match.IsNoPredictionPicked(cutoff));
                double fdr = totalCalls == 0 ? 0 : (double)(incorrectCalls + falsePositives) / totalCalls;
                var minQList = _peakBoundsMatchList.Where(match => !match.IsNoPredictionPicked(cutoff)).Select(match => match.QValue ?? 0).ToList();
                var minQ = minQList.Any() ? minQList.Max() : 0;
                var maxScoreList = _peakBoundsMatchList.Where(match => !match.IsNoPredictionPicked(cutoff)).Select(match => match.Score ?? double.MaxValue).ToList();
                var maxScore = maxScoreList.Any() ? maxScoreList.Min() : double.MaxValue;
                return new MatchStatistics(correctCalls, incorrectCalls, falseNegatives, falsePositives, trueNegatives, totalAnnotated, totalCalls, fdr, minQ, maxScore);
            }

            public void ExportResults(string fileName)
            {
                using (var saver = new FileSaver(fileName))
                using (var writer = new StreamWriter(saver.SafeName))
                {
                    ExportResults(writer);
                    writer.Flush();
                    writer.Close();
                    saver.Commit();
                }
            }

            public void ExportResults(TextWriter writer)
            {
                WriteHeader(writer);
                foreach (var peakBoundsMatch in _peakBoundsMatchList)
                {
                    WriteLine(writer, peakBoundsMatch);
                }
            }

            public static void WriteHeader(TextWriter writer)
            {
                var namesArray = new List<string>
                {
                    "PeptideModifiedSequence",
                    "PrecursorCharge",
                    "FileName",
                    "MinStartPicked",
                    "MaxEndPicked",
                    "MinStartTrue",
                    "MaxEndTrue",
                    "IsCorrectCall"
                };
                foreach (var name in namesArray)
                {
                    writer.WriteDsvField(name, SEPARATOR);
                    writer.Write(SEPARATOR);
                }
                writer.WriteLine();
            }

            public static void WriteLine(TextWriter writer, PeakBoundsMatch match)
            {
                var cultureInfo = CultureInfo.CurrentCulture;
                var fieldsArray = new List<string>
                {
                    Convert.ToString(match.Key.PeptideModifiedSequence, cultureInfo),
                    Convert.ToString(match.Key.PrecursorCharge, cultureInfo),
                    Convert.ToString(match.Key.FileName, cultureInfo),
                    Convert.ToString(match.PickedBounds.Start, cultureInfo),
                    Convert.ToString(match.PickedBounds.End, cultureInfo),
                    Convert.ToString(match.TrueBounds.Start, cultureInfo),
                    Convert.ToString(match.TrueBounds.End, cultureInfo),
                    Convert.ToString(match.IsCorrectCall(), cultureInfo),
                };
                foreach (var name in fieldsArray)
                {
                    writer.WriteDsvField(name, SEPARATOR);
                    writer.Write(SEPARATOR);
                }
                writer.WriteLine();
            }

            public void ExportRocPlots(string fileName)
            {
                using (var saver = new FileSaver(fileName))
                using (var writer = new StreamWriter(saver.SafeName))
                {
                    ExportRocPlots(writer);
                    writer.Flush();
                    writer.Close();
                    saver.Commit();
                }
            }

            public void ExportRocPlots(TextWriter writer)
            {
                WriteRocHeader(writer);
                foreach (var qValueStats in QValueData)
                {
                    WriteRocLine(writer, qValueStats);
                }
            }

            public static void WriteRocHeader(TextWriter writer)
            {
                var namesArray = new List<string>
                {
                    "QValue",
                    "TotalPeptides",
                    "TotalAnnotated",
                    "Correct",
                    "Incorrect",
                    "FalseNegative",
                    "FalsePositive",
                    "TrueNegative",
                    "FDR",
                    "Score"
                };
                foreach (var name in namesArray)
                {
                    writer.WriteDsvField(name, SEPARATOR);
                    writer.Write(SEPARATOR);
                }
                writer.WriteLine();
            }

            public void WriteRocLine(TextWriter writer, KeyValuePair<double?, MatchStatistics> qValueStats)
            {
                var cultureInfo = CultureInfo.CurrentCulture;
                var fieldsArray = new List<string>
                {
                    Convert.ToString(qValueStats.Value.MinimumQ, cultureInfo),
                    Convert.ToString(TotalPeaks, cultureInfo),
                    Convert.ToString(qValueStats.Value.TotalAnnotated, cultureInfo),
                    Convert.ToString(qValueStats.Value.CorrectCalls, cultureInfo),
                    Convert.ToString(qValueStats.Value.IncorrectCalls, cultureInfo),
                    Convert.ToString(qValueStats.Value.FalseNegatives, cultureInfo),
                    Convert.ToString(qValueStats.Value.FalsePositives, cultureInfo),
                    Convert.ToString(qValueStats.Value.TrueNegatives, cultureInfo),
                    Convert.ToString(qValueStats.Value.FDR, cultureInfo),
                    Convert.ToString(qValueStats.Value.MaximumScore, cultureInfo),
                };
                foreach (var name in fieldsArray)
                {
                    writer.WriteDsvField(name, SEPARATOR);
                    writer.Write(SEPARATOR);
                }
                writer.WriteLine();
            }
        }

        public struct MatchStatistics
        {
            public MatchStatistics(double correctCalls,
                                   double incorrectCalls,
                                   double falseNegatives,
                                   double falsePositives,
                                   double trueNegatives,
                                   double totalAnnotated,
                                   double totalCalls,
                                   double fdr,
                                   double minimumQ,
                                   double maximumScore)
                : this()
            {
                CorrectCalls = correctCalls;
                IncorrectCalls = incorrectCalls;
                FalseNegatives = falseNegatives;
                FalsePositives = falsePositives;
                TrueNegatives = trueNegatives;
                TotalAnnotated = totalAnnotated;
                TotalCalls = totalCalls;
                FDR = fdr;
                MinimumQ = minimumQ;
                MaximumScore = maximumScore;
            }

            public double CorrectCalls { get; private set; }
            public double IncorrectCalls { get; private set; }
            public double FalseNegatives { get; private set; }
            public double FalsePositives { get; private set; }
            public double TrueNegatives { get; private set; }
            public double TotalAnnotated { get; private set; }
            public double TotalCalls { get; private set; }
            public double FDR { get; private set; }
            public double MinimumQ { get; private set; }
            public double MaximumScore { get; private set; }
        }

        public class PeakBoundsSource
        {
            public string SourceFile { get; private set; }
            public string Name { get; private set; }
            public SrmDocument Document { get; private set; }

            public static readonly string[] FIELD_NAMES =
            {
            "PeptideModifiedSequence",
            "FileName",
            "MinStartTime",
            "MaxEndTime",
            "PrecursorCharge",
            "m_score",
            "RT",
            "d_score"
            };

            public enum PeakField { modified_peptide, filename, start_time, end_time, charge, q_value, rt, score }

            public static readonly int[] REQUIRED_FIELDS =
            {
                (int) PeakField.modified_peptide, 
                (int) PeakField.filename, 
                (int) PeakField.start_time,
                (int) PeakField.end_time,
                (int) PeakField.charge,
                (int) PeakField.q_value,
                (int) PeakField.rt,
                (int) PeakField.score
            };

            public Dictionary<PeakDataKey, PeakBounds> DictPeakBoundaries { get; private set; }

            public PeakBoundsSource(SrmDocument doc, string name)
            {
                Document = doc;
                Initialize(name);
            }

            public PeakBoundsSource(string sourceFile, string name, bool isSrmDocument)
            {
                SourceFile = sourceFile;
                Document = isSrmDocument ? LoadDocument(sourceFile) : null;
                Initialize(name);
            }

            private void Initialize(string name)
            {
                Name = name;
                DictPeakBoundaries = Document != null ? PeaksFromDoc(Document) : PeaksFromFile(SourceFile);
            }

            private static SrmDocument LoadDocument(string documentLocation)
            {
                SrmDocument docTrue = null;
                RunUI(() => SkylineWindow.OpenFile(documentLocation));
                WaitForDocumentLoaded();
                RunUI(() => docTrue = SkylineWindow.Document);
                return docTrue;
            }

            private static Dictionary<PeakDataKey, PeakBounds> PeaksFromFile(string sourceFile)
            {
                var dictPeakBoundaries = new Dictionary<PeakDataKey, PeakBounds>();
                var separators = new[]
                {
                    TextUtil.CsvSeparator,
                    TextUtil.SEPARATOR_TSV,
                    TextUtil.SEPARATOR_SPACE
                };
                foreach (var separator in separators)
                {
                    using (var sourceReader = new StreamReader(sourceFile))
                    {
                        var dsvReader = new DsvFileReader(sourceReader, separator);
                        if (!FIELD_NAMES.All(field => dsvReader.FieldDict.ContainsKey(field)))
                            continue;
                        int linesRead = 0;
                        while (dsvReader.ReadLine() != null)
                        {
                            linesRead++;
                            string modifiedPeptide = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.modified_peptide]);
                            string fileName = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.filename]);
                            string precursorCharge = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.charge]);
                            string minStartTime = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.start_time]);
                            string maxEndTime = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.end_time]);
                            string qValueString = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.q_value]);
                            string rtString = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.rt]);
                            string scoreString = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.score]);
                            if (modifiedPeptide == null ||
                                fileName == null ||
                                precursorCharge == null ||
                                minStartTime == null ||
                                maxEndTime == null ||
                                qValueString == null ||
                                rtString == null)
                            {
                                return null;
                            }
                            int charge;
                            double startTime = GetTime(minStartTime, linesRead);
                            double endTime = GetTime(maxEndTime, linesRead);
                            double rt = GetTime(rtString, linesRead) / 60.0; // TODO: Get rid of this HACK
                            if (!Int32.TryParse(precursorCharge, out charge))
                            {
                                throw new InvalidDataException(String.Format("{0} is not a valid charge state on line {1}", precursorCharge, linesRead));
                            }
                            double qValue;
                            if (!Double.TryParse(qValueString, out qValue))
                            {
                                throw new InvalidDataException(String.Format("{0} is not a valid q value on line {1}", qValueString, linesRead));
                            }
                            double score;
                            if (!Double.TryParse(scoreString, out score))
                            {
                                throw new InvalidDataException(String.Format("{0} is not a valid score on line {1}", scoreString, linesRead));
                            }
                            var peakBounds = new PeakBounds(startTime, endTime, rt, qValue, score);
                            // TODO: allow sample names to be imported
                            var peakDataKey = new PeakDataKey(modifiedPeptide, fileName, charge, null);
                            dictPeakBoundaries.Add(peakDataKey, peakBounds);
                        }
                        return dictPeakBoundaries;
                    }
                }
                return null;
            }

            public static double GetTime(string timeData, long linesRead)
            {
                double startTime;
                double startTimeTemp;
                if (Double.TryParse(timeData, out startTimeTemp))
                    startTime = startTimeTemp;
                else if (timeData.Equals(TextUtil.EXCEL_NA))
                    startTime = Double.NaN;
                else
                    throw new IOException(String.Format("Bad time {0} on line {1}", timeData, linesRead));
                return startTime;
            }

            public static Dictionary<PeakDataKey, PeakBounds> PeaksFromDoc(SrmDocument document)
            {
                var peakBoundsDictionary = new Dictionary<PeakDataKey, PeakBounds>();
                var chromatogramSets = document.Settings.MeasuredResults.Chromatograms;
                foreach (var nodePep in document.Peptides)
                {
                    var peptideModifiedSequence = nodePep.ModifiedSequence;
                    if (nodePep.IsDecoy)
                        continue;
                    foreach (var nodeGroups in PeakFeatureEnumerator.ComparableGroups(nodePep))
                    {
                        var nodeGroup = nodeGroups.FirstOrDefault();
                        if (nodeGroup == null)
                            continue;
                        var precursorCharge = nodeGroup.TransitionGroup.PrecursorCharge;
                        int i = 0;
                        foreach (var chromatogramSet in chromatogramSets)
                        {
                            if (nodeGroup.Results[i] == null)
                                continue;
                            foreach (var chromInfo in nodeGroup.Results[i])
                            {
                                double minStartTime = chromInfo.StartRetentionTime ?? Double.NaN;
                                double maxEndTime = chromInfo.EndRetentionTime ?? Double.NaN;
                                double apexTime = chromInfo.RetentionTime ?? Double.NaN;
                                double peakArea = chromInfo.Area ?? Double.NaN;
                                var fileId = chromInfo.FileId;
                                var fileInfo = chromatogramSet.GetFileInfo(fileId);
                                var filePath = fileInfo.FilePath;
                                var fileName = Path.GetFileNameWithoutExtension(SampleHelp.GetPathFilePart(filePath));
                                string qValueString = chromInfo.Annotations.GetAnnotation(MProphetResultsHandler.AnnotationName);
                                string scoreString = chromInfo.Annotations.GetAnnotation(MProphetResultsHandler.MAnnotationName);
                                var qValue = GetDoubleAnnotation(qValueString);
                                var score = GetDoubleAnnotation(scoreString);
                                string sampleName = SampleHelp.GetPathSampleNamePart(filePath);
                                var peakDataKey = new PeakDataKey(peptideModifiedSequence, fileName, precursorCharge, sampleName);
                                var peakBounds = new PeakBounds(minStartTime, maxEndTime, apexTime, qValue, score, peakArea);
                                if (peakBoundsDictionary.ContainsKey(peakDataKey))
                                {
                                    continue;
                                    //throw new InvalidDataException(string.Format("Duplicate node at peptide sequence {0} for file {1}", peptideModifiedSequence, filePath));
                                }
                                peakBoundsDictionary.Add(peakDataKey, peakBounds);
                            }
                            ++i;
                        }
                    }
                }
                return peakBoundsDictionary;
            }

            public static double? GetDoubleAnnotation(string annotationString)
            {
                double annotation;
                if (annotationString == null || !Double.TryParse(annotationString, out annotation))
                {
                    return null;
                }
                else
                {
                    return annotation;
                }
            }
        }

        public class PeakBoundsMatch
        {
            private const double MIN_PEAK_SEP = 0.5;

            public PeakDataKey Key { get; private set; }
            public PeakBounds PickedBounds { get; private set; }
            public PeakBounds TrueBounds { get; private set; }
            public double PeakDistance { get; private set; }
            public double NormPeakDistance { get; private set; }

            public PeakBoundsMatch(PeakDataKey key, PeakBounds pickedBounds, PeakBounds trueBounds)
            {
                Key = key;
                PickedBounds = pickedBounds;
                TrueBounds = trueBounds;
                //PeakDistance = PickedBounds.Apex - TrueBounds.Apex;
                PeakDistance = PickedBounds.Apex - TrueBounds.Center;
                NormPeakDistance = PeakDistance / TrueBounds.Width;
            }

            //public bool ArePeaksSame { get { return !double.IsNaN(PeakDistance) && PeakDistance < MIN_PEAK_SEP; } }
            public bool ArePeaksSame { get { return !double.IsNaN(NormPeakDistance) && NormPeakDistance < MIN_PEAK_SEP; } }
            public bool IsMissingTrue { get { return TrueBounds.IsMissing; } }
            public bool IsMissingPicked { get { return PickedBounds.IsMissing; } }

            public bool IsCutoffPicked(double? cutoff = null)
            {
                return cutoff != null && (Score == null || Score < cutoff);
            }

            public bool IsNoPredictionPicked(double? cutoff = null)
            {
                return IsMissingPicked || IsCutoffPicked(cutoff);
            }

            public bool IsCorrectCall(double? cutoff = null)
            {
                return !IsMissingTrue && !IsNoPredictionPicked(cutoff) && ArePeaksSame;
            }

            public bool IsIncorrectCall(double? cutoff = null)
            {
                return !IsMissingTrue && !IsNoPredictionPicked(cutoff) && !ArePeaksSame;
            }

            public bool IsFalseNegative(double? cutoff = null, bool useMScore = false)
            {
                return !IsMissingTrue && IsNoPredictionPicked(cutoff);
            }

            public bool IsUnsupportedPositive(double? cutoff = null, bool useMScore = false)
            {
                return IsMissingTrue && !IsNoPredictionPicked(cutoff);
            }

            public bool IsMutualMissing(double? cutoff = null, bool useMScore = false)
            {
                return IsMissingTrue && IsNoPredictionPicked(cutoff);
            }

            public double? QValue { get { return PickedBounds.Qvalue; } }

            public double? Score { get { return PickedBounds.Score; } }
        }

        public class PeakDataKey
        {
            public string PeptideModifiedSequence { get; private set; }
            public string FileName { get; private set; }
            public int PrecursorCharge { get; private set; }
            public string SampleName { get; private set; }

            public PeakDataKey(string peptideModifiedSequence, string fileName, int precursorCharge, string sampleName)
            {
                SampleName = sampleName;
                PeptideModifiedSequence = peptideModifiedSequence;
                FileName = fileName;
                PrecursorCharge = precursorCharge;
            }

            #region object overrides
            protected bool Equals(PeakDataKey other)
            {
                return string.Equals(PeptideModifiedSequence, other.PeptideModifiedSequence) &&
                    string.Equals(FileName, other.FileName) &&
                    PrecursorCharge == other.PrecursorCharge &&
                    SampleName == other.SampleName;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((PeakDataKey)obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    int hashCode = (PeptideModifiedSequence != null ? PeptideModifiedSequence.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ (FileName != null ? FileName.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ PrecursorCharge;
                    return hashCode;
                }
            }
            #endregion
        }

        public class PeakBounds
        {
            public double Start { get; private set; }
            public double End { get; private set; }
            public double Apex { get; private set; }
            public double? Qvalue { get; private set; }
            public double? Score { get; private set; }
            public double? PeakArea { get; private set; }

            public PeakBounds(double start, double end, double apex, double? qValue = null, double? score = null, double? peakArea = null)
            {
                Start = start;
                End = end;
                Apex = apex;
                Qvalue = qValue;
                Score = score;
                PeakArea = peakArea;
            }

            public double Center { get { return (Start + End) / 2; } }
            public double Width { get { return (End - Start) / 2; } }

            public bool IsMissing { get { return double.IsNaN(Start) || double.IsNaN(End) || Width <= 0; } }
        }
    }
}
