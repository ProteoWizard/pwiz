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
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Lib;
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
            //IsPauseForScreenShots = true;
            TestFilesZip = @"http://proteome.gs.washington.edu/software/test/skyline-perf/PeakScoreCompare.zip";
            RunFunctionalTest();
        }

        public string GetTestPath(string path)
        {
            return TestFilesDir.GetTestPath(Path.Combine("PeakScoreCompare", path));
        }

        protected override void DoTest()
        {
            const bool rescore = false;
            var filter01 = new PeakSourceFilter("m_score", 0.01);
            var filter05 = new PeakSourceFilter("m_score", 0.05);
            // OpenSWATH dataset (water background)
            var dataSetComparer = new DataSetComparer(MakePeakBoundarySource("AQUA4_Water_picked_hroest.sky", true, "Manual"));
            dataSetComparer.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_water_peakgroups.txt", false, "OpenSWATH"));
            dataSetComparer.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_water_peakgroups.txt", false, "OpenSWATH0.01", filter01));
            dataSetComparer.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_water_peakgroups.txt", false, "OpenSWATH0.05", filter05));
            dataSetComparer = RunFullTestDataSet(dataSetComparer, "AQUA4_Water_picked_hroest_full_rescore_iRT_plus.sky", rescore, true, false, "iRT_plus");
            dataSetComparer = RunFullTestDataSet(dataSetComparer, "AQUA4_Water_picked_hroest_full_rescore_iRT.sky", rescore, true, false, "_iRT");
            dataSetComparer.ExportResults(GetTestPath("AQUA4_Water_picked_hroest.txt"));

            // OpenSWATH dataset (yeast background)
            var dataSetComparerYeast = new DataSetComparer(MakePeakBoundarySource("AQUA4_Yeast_picked_georger_2.sky", true, "Manual"));
            dataSetComparerYeast.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_yeast_peakgroups.txt", false, "OpenSWATH"));
            dataSetComparerYeast.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_yeast_peakgroups.txt", false, "OpenSWATH0.01", filter01));
            dataSetComparerYeast.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_yeast_peakgroups.txt", false, "OpenSWATH0.05", filter05));
            dataSetComparerYeast = RunFullTestDataSet(dataSetComparerYeast, "AQUA4_Yeast_picked_georger_2_full_rescore_iRT_plus.sky", rescore, true, false, "_iRT_plus");
            dataSetComparerYeast = RunFullTestDataSet(dataSetComparerYeast, "AQUA4_Yeast_picked_georger_2_full_rescore_iRT.sky", rescore, true, false, "_iRT");
            dataSetComparerYeast.ExportResults(GetTestPath("AQUA4_Yeast_picked_georger_2.txt"));

            // OpenSWATH dataset (human background)
            var dataSetComparerHuman = new DataSetComparer(MakePeakBoundarySource("AQUA4_Human_picked_napedro2.sky", true, "Manual"));
            dataSetComparerHuman.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_human_peakgroups.txt", false, "OpenSWATH"));
            dataSetComparerHuman.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_human_peakgroups.txt", false, "OpenSWATH0.01", filter01));
            dataSetComparerHuman.Add(MakePeakBoundarySource("OpenSWATH_SM3_GoldStandardAutomatedResults_human_peakgroups.txt", false, "OpenSWATH0.05", filter05));
            dataSetComparerHuman = RunFullTestDataSet(dataSetComparerHuman, "AQUA4_Human_picked_napedro2_full_rescore_iRT_plus.sky", rescore, true, false, "_iRT_plus");
            dataSetComparerHuman = RunFullTestDataSet(dataSetComparerHuman, "AQUA4_Human_picked_napedro2_full_rescore_iRT.sky", rescore, true, false, "_iRT");
            dataSetComparerHuman.ExportResults(GetTestPath("AQUA4_Human_picked_napedro2.txt"));

            // Olga_srm_vantage
            var dataSetComparerOlgaSV = new DataSetComparer(new PeakBoundsSource(QuickLoadDocument("SRMCourse_DosR-hDP__TSQv_true.sky"), "Manual"));
            dataSetComparerOlgaSV = RunFullTestDataSet(dataSetComparerOlgaSV, "SRMCourse_DosR-hDP__TSQv_base.sky", false, true, false, null, true);
            dataSetComparerOlgaSV.ExportResults(GetTestPath("Olga_SRM_course_vantage.txt"));

            // Olga_srm
            var dataSetComparerOlgaS = new DataSetComparer(new PeakBoundsSource(QuickLoadDocument("SRMCourse_DosR-hDP__20130501_true.sky"), "Manual"));
            dataSetComparerOlgaS = RunFullTestDataSet(dataSetComparerOlgaS, "SRMCourse_DosR-hDP__20130501_base.sky", false, true, false, null, true);
            dataSetComparerOlgaS.ExportResults(GetTestPath("Olga_SRM_course.txt"));

            // Heart Failure
            var dataSetComparerHeartFailure = new DataSetComparer(new PeakBoundsSource(QuickLoadDocument("Rat_plasma_true.sky"), "Manual"));
            dataSetComparerHeartFailure = RunFullTestDataSet(dataSetComparerHeartFailure, "Rat_plasma_base.sky", false, false, true, null, true);
            dataSetComparerHeartFailure.ExportResults(GetTestPath("HeartFailure.txt"));

            // Ovarian Cancer
            var dataSetComparerOvarianCancer = new DataSetComparer(new PeakBoundsSource(QuickLoadDocument("Human_plasma_base.sky"), "Manual"));
            dataSetComparerOvarianCancer = RunFullTestDataSet(dataSetComparerOvarianCancer, "Human_plasma_no_heavy.sky", false, false, true, null, true);
            dataSetComparerOvarianCancer.ExportResults(GetTestPath("OvarianCancer.txt"));

            // mProphet Gold Standard

            // Demux dataset (10 mz)

            // Demux dataset (20 mz)

            // Demux dataset (demux)

            PauseForScreenShot("All tasks completed.");
        }

        private SrmDocument QuickLoadDocument(string documentLocation)
        {
            SrmDocument docTrue = null;
            RunUI(() => SkylineWindow.OpenFile(GetTestPath(documentLocation)));
            WaitForDocumentLoaded();
            RunUI(() => docTrue = SkylineWindow.Document);
            return docTrue;
        }

        private PeakBoundsSource MakePeakBoundarySource(string file, bool isSrmDocument, string name, PeakSourceFilter filter = null)
        {
            return new PeakBoundsSource(GetTestPath(file), isSrmDocument, name, filter);
        }

        public DataSetComparer RunFullTestDataSet(PeakBoundsSource sourceTrue, IEnumerable<PeakBoundsSource> sourcesOther, string documentFile)
        {
            var dataSetComparer = new DataSetComparer(sourceTrue, sourcesOther);
            return RunFullTestDataSet(dataSetComparer, documentFile);
        }

        public DataSetComparer RunFullTestDataSet(DataSetComparer dataSetComparer, string documentFile, bool rescore = false, bool usesDecoys = true, bool usesSecondBest = false, string appendText = null,
            bool reimport=false)
        {
            Settings.Default.PeakScoringModelList.Clear();
            var document = GetTestPath(documentFile);
            // Open the document
            RunUI(() => SkylineWindow.OpenFile(document));
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
                WaitForCondition(10*60*1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded); // 10 minutes
                WaitForClosedForm(rescoreResultsDlg);
                WaitForClosedForm(manageResults);
            }
            RunUI(() => dataSetComparer.Add(new PeakBoundsSource(SkylineWindow.Document, "SkylineDefault" + appendText)));

            // Train a model
            var peptideSettingsDlg = ShowDialog<PeptideSettingsUI>(SkylineWindow.ShowPeptideSettingsUI);
            RunUI(() => peptideSettingsDlg.SelectedTab = PeptideSettingsUI.TABS.Integration);
            var editDlg = ShowDialog<EditPeakScoringModelDlg>(peptideSettingsDlg.AddPeakScoringModel);
            RunUI(() =>
            {
                editDlg.UsesDecoys = usesDecoys;
                editDlg.UsesSecondBest = usesSecondBest;
                editDlg.PeakScoringModelName = "functional test";
                editDlg.TrainModel();
            });
            OkDialog(editDlg, editDlg.OkDialog);
            OkDialog(peptideSettingsDlg, peptideSettingsDlg.OkDialog);
            WaitForDocumentLoaded();

            // Reintegrate
            var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlg.ReintegrateAll = true);
            OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);

            RunUI(() => dataSetComparer.Add(new PeakBoundsSource(SkylineWindow.Document, "SkylineModelReintegrateAll" + appendText)));

            // Reintegrate with q=0.01
            var reintegrateDlgQ = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlgQ.ReintegrateAll = false);
            RunUI(() => reintegrateDlgQ.Cutoff = 0.01);
            OkDialog(reintegrateDlgQ, reintegrateDlgQ.OkDialog);

            RunUI(() => dataSetComparer.Add(new PeakBoundsSource(SkylineWindow.Document, "SkylineModelReintegrate0.01" + appendText)));

            // Reintegrate with q=0.05
            var reintegrateDlgQQ = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
            RunUI(() => reintegrateDlgQQ.ReintegrateAll = false);
            RunUI(() => reintegrateDlgQQ.Cutoff = 0.05);
            OkDialog(reintegrateDlgQQ, reintegrateDlgQQ.OkDialog);

            RunUI(() => dataSetComparer.Add(new PeakBoundsSource(SkylineWindow.Document, "SkylineModelReintegrate0.05" + appendText)));

            Settings.Default.PeakScoringModelList.Clear();
            return dataSetComparer;
        }
    }


    public class DataSetComparer
    {
        const char SEPARATOR = TextUtil.SEPARATOR_TSV;

        public IList<PeakBoundsComparer> Comparers { get; private set; }
        public PeakBoundsSource SourceTrue { get; private set; }
        public IList<string> Names { get; private set; } 

        public DataSetComparer(PeakBoundsSource sourceTrue, IEnumerable<PeakBoundsSource> sourcesPicked = null)
        {
            SourceTrue = sourceTrue;
            Names = new List<string>();
            Comparers = new List<PeakBoundsComparer>();
            if (sourcesPicked == null)
                return;
            foreach (var sourcePicked in sourcesPicked)
                Add(sourcePicked);
        }

        public void Add(PeakBoundsSource sourcePicked)
        {
            var comparer = new PeakBoundsComparer(sourcePicked, SourceTrue);
            comparer.ComputeMatchStatistics();
            Comparers.Add(comparer);
            Names.Add(sourcePicked.Name);
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

        private void ExportResults(TextWriter writer)
        {
            WriteHeader(writer);
            foreach (var comparer in Comparers)
                WriteLine(writer, comparer);
        }

        private static void WriteHeader(TextWriter writer)
        {
            var namesArray = new List<string>
                {
                    "DataSet",
                    "TotalPeptides",
                    "TotalAnnotated",
                    "Correct",
                    "Incorrect",
                    "FalseNegative",
                    "FalsePositive",
                    "TrueNegative",
                    "FDR"
                };
            foreach (var name in namesArray)
            {
                writer.WriteDsvField(name, SEPARATOR);
                writer.Write(SEPARATOR);
            }
            writer.WriteLine();
        }

        private static void WriteLine(TextWriter writer, PeakBoundsComparer comparer)
        {
            var cultureInfo = CultureInfo.CurrentCulture;
            var fieldsArray = new List<string>
                {
                    Convert.ToString(comparer.Name, cultureInfo),
                    Convert.ToString(comparer.TotalPeaks, cultureInfo),
                    Convert.ToString(comparer.TotalAnnotated, cultureInfo),
                    Convert.ToString(comparer.CorrectCalls, cultureInfo),
                    Convert.ToString(comparer.IncorrectCalls, cultureInfo),
                    Convert.ToString(comparer.FalseNegatives, cultureInfo),
                    Convert.ToString(comparer.FalsePositives, cultureInfo),
                    Convert.ToString(comparer.TrueNegatives, cultureInfo),
                    Convert.ToString(comparer.FDR, cultureInfo)
                };
            foreach (var name in fieldsArray)
            {
                writer.WriteDsvField(name, SEPARATOR);
                writer.Write(SEPARATOR);
            }
            writer.WriteLine();
        }
    }

    public class PeakBoundsComparer
    {
        private readonly IList<PeakBoundsMatch> _peakBoundsMatchList;
        const char SEPARATOR = TextUtil.SEPARATOR_TSV;

        public IList<PeakDataKey> Unmatched { get; private set; }

        public string Name { get; private set; }

        public double CorrectCalls { get; private set; }
        public double IncorrectCalls { get; private set; }
        public double FalseNegatives { get; private set; }
        public double FalsePositives { get; private set; }
        public double TrueNegatives { get; private set; }
        public double TotalAnnotated { get; private set; }
        public double TotalCalls{ get; private set; }
        public double FDR { get; private set; }


        public PeakBoundsComparer(PeakBoundsSource pickedSource, PeakBoundsSource trueSource)
        {
            Name = pickedSource.Name;
            Unmatched = new List<PeakDataKey>();
            var pickedPeaks = pickedSource.DictPeakBoundaries;
            var truePeaks = trueSource.DictPeakBoundaries;
            _peakBoundsMatchList = new List<PeakBoundsMatch>();
            foreach (var peakDataKey in truePeaks.Keys)
            {
                if (!pickedPeaks.ContainsKey(peakDataKey))
                {
                    pickedPeaks.Add(peakDataKey, new PeakBounds(double.NaN, double.NaN));
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

        public double TotalPeaks { get { return _peakBoundsMatchList.Count; } }

        public void ComputeMatchStatistics()
        {
            CorrectCalls = _peakBoundsMatchList.Count(match => match.IsCorrectCall);
            IncorrectCalls = _peakBoundsMatchList.Count(match => match.IsIncorrectCall);
            FalseNegatives = _peakBoundsMatchList.Count(match => match.IsFalseNegative);
            FalsePositives = _peakBoundsMatchList.Count(match => match.IsUnsupportedPositive);
            TrueNegatives = _peakBoundsMatchList.Count(match => match.IsMutualMissing);
            TotalAnnotated = _peakBoundsMatchList.Count(match => !match.IsMissingTrue);
            TotalCalls = _peakBoundsMatchList.Count(match => !match.IsMissingPicked);
            FDR = (IncorrectCalls + FalsePositives)/TotalCalls;
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
                    Convert.ToString(match.IsCorrectCall, cultureInfo),
                };
            foreach (var name in fieldsArray)
            {
                writer.WriteDsvField(name, SEPARATOR);
                writer.Write(SEPARATOR);
            }
            writer.WriteLine();
        }
    }

    public class PeakBoundsSource
    {
        public string SourceFile { get; private set; }
        public string Name { get; private set; }
        public PeakSourceFilter Filter { get; private set; }
        public SrmDocument Document { get; private set; }

        public static readonly string[] FIELD_NAMES =
        {
            "PeptideModifiedSequence",
            "FileName",
            "MinStartTime",
            "MaxEndTime",
            "PrecursorCharge",
        };

        public enum PeakField { modified_peptide, filename, start_time, end_time, charge }

        public static readonly int[] REQUIRED_FIELDS =
            {
                (int) PeakField.modified_peptide, 
                (int) PeakField.filename, 
                (int) PeakField.start_time,
                (int) PeakField.end_time,
                (int) PeakField.charge,
            };

        public Dictionary<PeakDataKey, PeakBounds> DictPeakBoundaries { get; private set; }

        public PeakBoundsSource(SrmDocument doc, string name, PeakSourceFilter filter = null)
        {
            Document = doc;
            Initialize(name, filter);
        }

        public PeakBoundsSource(string sourceFile, bool isSrmDocument, string name, PeakSourceFilter filter = null)
        {
            SourceFile = sourceFile;
            Document = isSrmDocument ? LoadDocument(SourceFile) : null;
            Initialize(name, filter);
        }

        private void Initialize(string name, PeakSourceFilter filter)
        {
            Name = name;
            Filter = filter;
            DictPeakBoundaries = Document != null ? PeaksFromDoc(Document) : PeaksFromFile(SourceFile, Filter);
        }

        private static Dictionary<PeakDataKey, PeakBounds> PeaksFromFile(string sourceFile, PeakSourceFilter sourceFilter = null)
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
                        string modifiedPeptide = dsvReader.GetFieldByName(FIELD_NAMES[(int) PeakField.modified_peptide]);
                        string fileName = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.filename]);
                        string precursorCharge = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.charge]);
                        string minStartTime = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.start_time]);
                        string maxEndTime = dsvReader.GetFieldByName(FIELD_NAMES[(int)PeakField.end_time]);
                        if (modifiedPeptide == null ||
                            fileName == null ||
                            precursorCharge == null ||
                            minStartTime == null ||
                            maxEndTime == null)
                        {
                            return null;
                        }
                        int charge;
                        double startTime = GetTime(minStartTime, linesRead);
                        double endTime = GetTime(maxEndTime, linesRead);
                        if (!int.TryParse(precursorCharge, out charge))
                        {
                            throw new InvalidDataException(string.Format("{0} is not a valid charge state on line {1}", precursorCharge, linesRead));
                        }
                        var peakBounds = new PeakBounds(startTime, endTime);
                        // TODO: allow sample names to be imported
                        var peakDataKey = new PeakDataKey(modifiedPeptide, fileName, charge, null);
                        // If there is a source filter specified, don't guess peaks for any chrom group above the cutoff q-value
                        if (sourceFilter != null)
                        {
                            string qValueString = dsvReader.GetFieldByName(sourceFilter.ColumnName);
                            double qValue;
                            if (!double.TryParse(qValueString, out qValue))
                            {
                                throw new InvalidDataException(string.Format("{0} is not a valid q value on line {1}", qValueString, linesRead));
                            }
                            if (qValue > sourceFilter.Cutoff)
                            {
                                dictPeakBoundaries.Add(peakDataKey, new PeakBounds(double.NaN, double.NaN));
                                continue;
                            } 
                        }
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
            if (double.TryParse(timeData, out startTimeTemp))
                startTime = startTimeTemp;
            else if (timeData.Equals(TextUtil.EXCEL_NA))
                startTime = double.NaN;
            else
                throw new IOException(string.Format("Bad time {0} on line {1}", timeData, linesRead));
            return startTime;
        }

        private static SrmDocument LoadDocument(string documentLocation)
        {
            var directoryInfo = new FileInfo(documentLocation).Directory;
            if (directoryInfo == null)
                return null;
            string dirName = directoryInfo.FullName;
            SrmDocument doc = ResultsUtil.DeserializeDocument(documentLocation);
            doc = doc.ChangeSettings(doc.Settings.ChangePeptideLibraries(libraries =>
            {
                LibrarySpec[] specList = libraries.Libraries.Select( lib => new SpectrastSpec(lib.Name, Path.Combine(dirName, lib.FileNameHint)) as LibrarySpec).ToArray();
                return libraries.ChangeLibrarySpecs(specList);
            }));

            var docContainer = new ResultsTestDocumentContainer(null, documentLocation);
            docContainer.SetDocument(doc, null, true);
            docContainer.AssertComplete();
            var document = docContainer.Document;
            // Release open streams
            docContainer.SetDocument(new SrmDocument(SrmSettingsList.GetDefault()), document, false);
            return document;
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
                            double minStartTime = chromInfo.StartRetentionTime ?? double.NaN;
                            double maxEndTime = chromInfo.EndRetentionTime ?? double.NaN;
                            var fileId = chromInfo.FileId;
                            var fileInfo = chromatogramSet.GetFileInfo(fileId);
                            var filePath = fileInfo.FilePath;
                            var fileName = Path.GetFileNameWithoutExtension(SampleHelp.GetPathFilePart(filePath));
                            string sampleName = SampleHelp.GetPathSampleNamePart(filePath);
                            var peakDataKey = new PeakDataKey(peptideModifiedSequence, fileName, precursorCharge, sampleName);
                            var peakBounds = new PeakBounds(minStartTime, maxEndTime);
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
    }

    public class PeakSourceFilter
    {
        public string ColumnName { get; private set; }
        public double Cutoff { get; private set; }

        public PeakSourceFilter(string columnName, double cutoff)
        {
            ColumnName = columnName;
            Cutoff = cutoff;
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
            PeakDistance = PickedBounds.Center - TrueBounds.Center;
            NormPeakDistance = PeakDistance/TrueBounds.Width;
        }

        public bool ArePeaksSame { get { return !double.IsNaN(NormPeakDistance) && NormPeakDistance < MIN_PEAK_SEP; } }
        public bool IsMissingTrue { get { return TrueBounds.IsMissing; } }
        public bool IsMissingPicked { get { return PickedBounds.IsMissing; } }

        public bool IsCorrectCall { get { return !IsMissingTrue && !IsMissingPicked && ArePeaksSame; } }
        public bool IsIncorrectCall { get { return !IsMissingTrue && !IsMissingPicked && !ArePeaksSame; } }
        public bool IsFalseNegative { get { return !IsMissingTrue && IsMissingPicked; } }
        public bool IsUnsupportedPositive { get { return IsMissingTrue && !IsMissingPicked; } }
        public bool IsMutualMissing { get { return IsMissingTrue && IsMissingPicked; } }
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
            return Equals((PeakDataKey) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = (PeptideModifiedSequence != null ? PeptideModifiedSequence.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (FileName != null ? FileName.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ PrecursorCharge;
                return hashCode;
            }
        }
        #endregion
    }

    public class PeakBounds
    {
        public double Start { get; private set; }
        public double End { get; private set; }

        public PeakBounds(double start, double end)
        {
            Start = start;
            End = end;
        }

        public double Center { get { return (Start + End) / 2; } }
        public double Width { get { return (End - Start) / 2; } }

        public bool IsMissing { get { return double.IsNaN(Start) || double.IsNaN(End) || Width <= 0; } }
    }
}
