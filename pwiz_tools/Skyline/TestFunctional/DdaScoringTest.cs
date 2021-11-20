using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class DdaScoringTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestDdaScoring()
        {
            TestFilesZip = @"TestFunctional\DdaScoringTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {

            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DdaScoringTest.sky")));
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi=>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.DDA;
                transitionSettingsUi.OkDialog();
            });
            ImportResultsFile(TestFilesDir.GetTestPath("ddascoring.mzml"));
            var ddaScoresPath = TestFilesDir.GetTestPath("ddaScores.csv");
            WriteMprophetFeatures(ddaScoresPath);
            var strDdaScores = File.ReadAllText(ddaScoresPath);

            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.None;
                transitionSettingsUi.OkDialog();
            });
            RemoveAllResults();
            ImportResultsFile(TestFilesDir.GetTestPath("ddascoring.mzml"));
            var noMs2ScoresPath = TestFilesDir.GetTestPath("noMs2Scores.csv");
            WriteMprophetFeatures(noMs2ScoresPath);

            AssertEx.FileEquals(noMs2ScoresPath, ddaScoresPath);
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUi.OkDialog();
            });
            RemoveAllResults();
            ImportResultsFile(TestFilesDir.GetTestPath("ddascoring.mzml"));
            var prmScoresPath = TestFilesDir.GetTestPath("prmScores.csv");
            WriteMprophetFeatures(prmScoresPath);

            var strPrmScores = File.ReadAllText(prmScoresPath);
            Assert.AreNotEqual(strDdaScores, strPrmScores);
        }

        private void WriteMprophetFeatures(string path)
        {
            var dlg = ShowDialog<MProphetFeaturesDlg>(SkylineWindow.ShowMProphetFeaturesDialog);
            dlg.BestScoresOnly = false;
            Assert.IsTrue(dlg.WriteFeaturesToPath(path));
            OkDialog(dlg, dlg.Close);

        }

        private void RemoveAllResults()
        {
            RunDlg<ManageResultsDlg>(SkylineWindow.ManageResults, dlg =>
            {
                dlg.RemoveAllReplicates();
                dlg.OkDialog();
            });
            RunUI(() => SkylineWindow.SaveDocument());
        }

        private void VerifyScoreValues(IList<ScoreRow> scoreRows, SrmDocument document)
        {
            foreach (var peptide in document.Molecules)
            {
                for (int replicateIndex = 0;
                    replicateIndex < document.Settings.MeasuredResults.Chromatograms.Count;
                    replicateIndex++)
                {
                    foreach (var )
                }
            }
        }

        public IEnumerable<ScoreRow> ReadScores(TextReader reader)
        {
            var csvReader = new CsvFileReader(reader);
            while (null != csvReader.ReadLine())
            {
                var fileName = csvReader.GetFieldByName("FileName");
                var peptideModifiedSequence = csvReader.GetFieldByName("PeptideModifiedSequence");
                var minStartTime = Convert.ToDouble(csvReader.GetFieldByName("MinStartTime"));
                var maxEndTime = Convert.ToDouble(csvReader.GetFieldByName("MaxEndTime"));
                var scores = new List<Tuple<Type, double?>>();
                foreach (var calculator in PeakFeatureCalculator.Calculators)
                {
                    var headerName = calculator.HeaderName.Replace(' ', '_');
                    var value = csvReader.GetFieldByName("main_var_" + headerName) ??
                                csvReader.GetFieldByName("var_" + headerName);
                    if (value == null)
                    {
                        continue;
                    }

                    double? score = value == TextUtil.EXCEL_NA ? default(double?) : Convert.ToDouble(value);
                    scores.Add(Tuple.Create(calculator.GetType(), score));
                }

                yield return new ScoreRow(fileName, peptideModifiedSequence, minStartTime, maxEndTime, scores);
            }
        }

        public class ScoreRow
        {
            public ScoreRow(string fileName, string peptideModifiedSequence, double minStartTime, double maxEndTime,
                IEnumerable<Tuple<Type, double?>> featureScores)
            {
                FileName = fileName;
                PeptideModifiedSequence = peptideModifiedSequence;
                MinStartTime = minStartTime;
                MaxEndTime = maxEndTime;
                FeatureScores = ImmutableList.ValueOf(featureScores);
            }
            public string FileName { get; }
            public string PeptideModifiedSequence { get; }
            public double MinStartTime { get; }
            public double MaxEndTime { get; }
            public ImmutableList<Tuple<Type, double?>> FeatureScores { get; }

            public double? GetScore(Type type)
            {
                return FeatureScores.First(tuple => tuple.Item1 == type).Item2;
            }
        }
    }
}
