/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that when the FullScanAcquisitionMethod is DDA, the peak scoring is only based on the MS1 chromatograms, and therefore the MS2 chromatograms
    /// do not affect the mProphet feature scores.
    /// </summary>
    [TestClass]
    public class DdaScoringTest : AbstractFunctionalTest
    {
        private FeatureCalculators _calculators;
        private IDictionary<Type, int> _calculatorIndexes; 

        public DdaScoringTest()
        {
            _calculators = FeatureCalculators.ALL;
            _calculatorIndexes = Enumerable.Range(0, _calculators.Count).ToDictionary(i => _calculators[i].GetType(), i=>i);
        }

        [TestMethod]
        public void TestDdaScoring()
        {
            TestFilesZip = @"TestFunctional\DdaScoringTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {

            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("DdaScoringTest.sky")));

            // Import the result file with the acquisition method set to "DDA"
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi=>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.DDA;
                transitionSettingsUi.OkDialog();
            });
            ImportResultsFile(TestFilesDir.GetTestPath("ddascoring.mzml"));
            var ddaScores = CalculateMProphetFeatureScores(SkylineWindow.Document);
            VerifyScoreValues(SkylineWindow.Document, ddaScores);

            // Reimport the result file with the acquisition method set to "None"
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.None;
                transitionSettingsUi.OkDialog();
            });
            RemoveAllResults();
            ImportResultsFile(TestFilesDir.GetTestPath("ddascoring.mzml"));
            var noMs2Scores = CalculateMProphetFeatureScores(SkylineWindow.Document);
            VerifyScoreValues(SkylineWindow.Document, noMs2Scores);

            // The DDA scores and the None scores should be identical
            Assert.IsTrue(AreEqualPeakTransitionGroupFeatureSet(ddaScores, noMs2Scores));

            // Reimport the results with the acquisition method set to "PRM"
            RunDlg<TransitionSettingsUI>(SkylineWindow.ShowTransitionSettingsUI, transitionSettingsUi =>
            {
                transitionSettingsUi.AcquisitionMethod = FullScanAcquisitionMethod.PRM;
                transitionSettingsUi.OkDialog();
            });
            RemoveAllResults();
            ImportResultsFile(TestFilesDir.GetTestPath("ddascoring.mzml"));
            var prmScores = CalculateMProphetFeatureScores(SkylineWindow.Document);
            Assert.IsFalse(AreEqualPeakTransitionGroupFeatureSet(ddaScores, prmScores));
            VerifyScoreValues(SkylineWindow.Document, prmScores);
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

        public PeakTransitionGroupFeatureSet CalculateMProphetFeatureScores(SrmDocument document)
        {
            return PeakFeatureEnumerator.GetPeakFeatures(document, _calculators, null, true);
        }

        public bool AreEqualPeakTransitionGroupFeatureSet(PeakTransitionGroupFeatureSet ptgfs1,
            PeakTransitionGroupFeatureSet ptgfs2)
        {
            if (ptgfs1.TargetCount != ptgfs2.TargetCount || ptgfs1.DecoyCount != ptgfs2.DecoyCount)
            {
                return false;
            }

            if (ptgfs1.Features.Length != ptgfs2.Features.Length)
            {
                return false;
            }

            for (int iTransitionGroup = 0; iTransitionGroup < ptgfs1.Features.Length; iTransitionGroup++)
            {
                var tgFeatures1 = ptgfs1.Features[iTransitionGroup];
                var tgFeatures2 = ptgfs2.Features[iTransitionGroup];
                var id1 = tgFeatures1.Id;
                var id2 = tgFeatures2.Id;
                if (id1.RawTextId != id2.RawTextId)
                {
                    return false;
                }

                if (!Equals(id1.FilePath, id2.FilePath))
                {
                    return false;
                }

                if (!ReferenceEquals(id1.NodePepGroup.Id, id2.NodePepGroup.Id))
                {
                    return false;
                }

                if (tgFeatures1.PeakGroupFeatures.Count != tgFeatures2.PeakGroupFeatures.Count)
                {
                    return false;
                }

                for (int iPeakGroup = 0; iPeakGroup < tgFeatures1.PeakGroupFeatures.Count; iPeakGroup++)
                {
                    if (!AreEqualPeakGroupFeatures(tgFeatures1.PeakGroupFeatures[iPeakGroup],
                        tgFeatures2.PeakGroupFeatures[iPeakGroup]))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        public bool AreEqualPeakGroupFeatures(PeakGroupFeatures pf1, PeakGroupFeatures pf2)
        {
            return Equals(pf1.StartTime, pf2.StartTime) &&
                   Equals(pf1.EndTime, pf2.EndTime) &&
                   pf1.Features.SequenceEqual(pf2.Features);
        }

        /// <summary>
        /// Verify that the mProphet feature scores are what we expect them to be.
        /// The only score that we currently verify is <see cref="MQuestDefaultIntensityCalc"/>.
        /// </summary>
        private void VerifyScoreValues(SrmDocument document, PeakTransitionGroupFeatureSet featureSet)
        {
            foreach (var moleculeGroup in document.MoleculeGroups)
            {
                foreach (var molecule in document.Molecules)
                {
                    var transitionGroup = molecule.TransitionGroups.Single();
                    for (int replicateIndex = 0;
                        replicateIndex < document.MeasuredResults.Chromatograms.Count;
                        replicateIndex++)
                    {
                        var chromatogramSet = document.MeasuredResults.Chromatograms[replicateIndex];
                        var filePath = chromatogramSet.MSDataFilePaths.Single();
                        var tgFeatures = featureSet.Features.Single(tgf =>
                        {
                            var id = tgf.Id;
                            return Equals(id.FilePath, filePath) && ReferenceEquals(id.NodePepGroup.Id,
                                                                     moleculeGroup.Id)
                                                                 && Equals(id.RawTextId,
                                                                     molecule.ModifiedTarget.ToString());
                        });
                        var transitionGroupChromInfo = transitionGroup.Results[replicateIndex].Single();
                        Assert.IsNotNull(transitionGroupChromInfo.RetentionTime);
                        var peakGroup = tgFeatures.PeakGroupFeatures.Single(pg =>
                            pg.StartTime <= transitionGroupChromInfo.RetentionTime &&
                            pg.EndTime >= transitionGroupChromInfo.RetentionTime);
                        var expectedIntensityScore = CalculateIntensityScore(transitionGroup, replicateIndex,
                            document.Settings.TransitionSettings.FullScan.AcquisitionMethod);
                        var actualIntensityScore = GetScore<MQuestDefaultIntensityCalc>(peakGroup);
                        AssertEx.AreEqual(expectedIntensityScore, actualIntensityScore);
                    }
                }
            }
        }

        /// <summary>
        /// Implementation of <see cref="AbstractMQuestIntensityCalc.Calculate(PeakScoringContext,IPeptidePeakData{ISummaryPeakData})"/>
        /// </summary>
        private double CalculateIntensityScore(TransitionGroupDocNode transitionGroupDocNode, int replicateIndex,
            FullScanAcquisitionMethod fullScanAcquisitionMethod)
        {
            IList<TransitionDocNode> transitions;
            if (fullScanAcquisitionMethod == FullScanAcquisitionMethod.DDA || fullScanAcquisitionMethod == FullScanAcquisitionMethod.None)
            {
                transitions = transitionGroupDocNode.Transitions.Where(t => t.IsMs1).ToList();
            }
            else
            {
                transitions = transitionGroupDocNode.Transitions.Where(t => !t.IsMs1).ToList();
            }

            double totalArea = 0;
            foreach (var transition in transitions)
            {
                var chromInfo = transition.Results[replicateIndex].FirstOrDefault();
                if (chromInfo != null && !chromInfo.IsEmpty)
                {
                    totalArea += chromInfo.Area;
                }
            }

            return (float) Math.Max(0, Math.Log10(totalArea));
        }

        private float GetScore<T>(PeakGroupFeatures peakGroupFeatures) where T : IPeakFeatureCalculator
        {
            int index = _calculatorIndexes[typeof(T)];
            return peakGroupFeatures.Features[index];
        }
    }
}
