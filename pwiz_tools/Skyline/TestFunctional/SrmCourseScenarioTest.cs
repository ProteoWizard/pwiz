/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.FileUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.Results.Scoring;
using pwiz.Skyline.SettingsUI;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Test handling of a difficult scenario from the SRMCourse, where
    /// SRM data was imported without its heavy isotope standards, and then
    /// the standard precursors were added after import.
    /// </summary>
    [TestClass]
    public class SrmCourseScenarioTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSrmCourseScenario()
        {
            TestFilesZip = @"TestFunctional\SrmCourseScenarioTest.zip";
            RunFunctionalTest();
        }

        private readonly string[] EXPECTED_REPLICATES =
        {
            "A1 (2)", "B1 (2)", "A2 (2)", "olgas_S130501_008_StC-DosR_A4"
        };

        protected override void DoTest()
        {
            int countRep = EXPECTED_REPLICATES.Length;

            // Open the .sky file
            string documentPath1 = TestFilesDir.GetTestPath("SRMCourse_DosR-hDP__20130501.sky");
            RunUI(() => SkylineWindow.OpenFile(documentPath1));
            WaitForDocumentLoaded();

            // Check expected initial state
            const int protCount = 13, pepCount = 71, tranCount = 312;
            var document = SkylineWindow.Document;
            AssertEx.IsDocumentState(document, null, protCount, pepCount, pepCount, tranCount);
            int[] missing = {1, 0, 0, 0};
            for (int i = 0; i < EXPECTED_REPLICATES.Length; i++)
            {
                string replicateName = EXPECTED_REPLICATES[i];
                AssertResult.IsDocumentResultsState(document, replicateName,
                    pepCount, pepCount, 0, tranCount - missing[i], 0);
            }
            VerifyUserSets(document,
                new[] { new UserSetCount(UserSet.FALSE, countRep * pepCount) },
                new[] { new UserSetCount(UserSet.FALSE, countRep * tranCount) });

            // Add heavy precursors
            RunDlg<RefineDlg>(SkylineWindow.ShowRefineDlg, refineDlg =>
            {
                refineDlg.RefineLabelType = IsotopeLabelType.heavy;
                refineDlg.AddLabelType = true;
                refineDlg.OkDialog();
            });

            // Check changed result state
            const int pepStandardCount = 13, tranStandardCount = 34;
            document = SkylineWindow.Document;
            AssertEx.IsDocumentState(document, null, protCount, pepCount, pepCount*2, tranCount*2);
            int[] missingHeavy = {1, 1, 2, 2};
            for (int i = 0; i < EXPECTED_REPLICATES.Length; i++)
            {
                string replicateName = EXPECTED_REPLICATES[i];
                AssertResult.IsDocumentResultsState(document, replicateName,
                    pepCount, pepCount, pepCount-pepStandardCount,
                    tranCount - missing[i],
                    tranCount-tranStandardCount - missingHeavy[i]);
            }
            const int pepRandomCount = 8;   // Randomly need no changes from default to match
            // TODO: Figure out what is up with the 6
            var matchedUserSetGroups = new[]
            {
                new UserSetCount(UserSet.FALSE, 4*pepStandardCount + pepRandomCount),
                new UserSetCount(UserSet.MATCHED, 8*(pepCount - pepStandardCount) - pepRandomCount),
            };
            var matchedUserSetTrans = new[]
            {
                new UserSetCount(UserSet.FALSE, countRep*tranStandardCount + countRep*pepRandomCount + 6),
                new UserSetCount(UserSet.MATCHED, 2*countRep*(tranCount - tranStandardCount) - countRep*pepRandomCount - 6),
            };

            VerifyUserSets(document, matchedUserSetGroups, matchedUserSetTrans);

            // Remove heavies from standards
            RunUI(() => SkylineWindow.RemoveMissingResults());
            document = WaitForDocumentChange(document);

            AssertEx.IsDocumentState(document, null, protCount, pepCount,
                pepCount * 2 - pepStandardCount, tranCount * 2 - tranStandardCount);

            VerifyMatchingPeakBoundaries(document, true);   // CONSIDER: Strange that these peak times match exactly

            // Test limited scoring model
            {
                var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                RunDlg<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel, scoringModelDlg =>
                {
                    scoringModelDlg.TrainModelClick();
                    Assert.IsNull(scoringModelDlg.GetCalculatorWeight(typeof(MQuestWeightedReferenceShapeCalc)));
                    Assert.IsNull(scoringModelDlg.GetCalculatorWeight(typeof(MQuestWeightedReferenceCoElutionCalc)));
                    Assert.IsNotNull(scoringModelDlg.GetCalculatorWeight(typeof(MQuestReferenceCorrelationCalc)));
                    Assert.IsNotNull(scoringModelDlg.GetCalculatorWeight(typeof(LegacyUnforcedCountScoreStandardCalc)));
                    Assert.AreEqual(16, scoringModelDlg.GetTargetCount());
                    Assert.AreEqual(4, scoringModelDlg.GetDecoyCount());
                    scoringModelDlg.CancelDialog();
                });
                OkDialog(reintegrateDlg, reintegrateDlg.CancelDialog);
            }

            // Rescore
            var manageResults = ShowDialog<ManageResultsDlg>(SkylineWindow.ManageResults);
            var rescoreResultsDlg = ShowDialog<RescoreResultsDlg>(manageResults.Rescore);
            RunUI(() => rescoreResultsDlg.Rescore(false));
            WaitForCondition(5 * 60 * 1000, () => SkylineWindow.Document.Settings.MeasuredResults.IsLoaded);    // 5 minutes
            WaitForClosedForm(rescoreResultsDlg);
            WaitForClosedForm(manageResults);
            WaitForConditionUI(() => FindOpenForm<AllChromatogramsGraph>() == null);

            var documentRescore = SkylineWindow.Document;
            VerifyUserSets(documentRescore, matchedUserSetGroups, matchedUserSetTrans);

            // Test corrected scoring model
            {
                const string peakScoringModelName = "Test Model";
                var reintegrateDlg = ShowDialog<ReintegrateDlg>(SkylineWindow.ShowReintegrateDialog);
                RunDlg<EditPeakScoringModelDlg>(reintegrateDlg.AddPeakScoringModel, scoringModelDlg =>
                {
                    scoringModelDlg.PeakScoringModelName = peakScoringModelName;
                    scoringModelDlg.TrainModelClick();
                    Assert.IsNotNull(scoringModelDlg.GetCalculatorWeight(typeof(MQuestWeightedReferenceShapeCalc)));
                    Assert.IsNotNull(scoringModelDlg.GetCalculatorWeight(typeof(MQuestWeightedReferenceCoElutionCalc)));
                    Assert.IsNotNull(scoringModelDlg.GetCalculatorWeight(typeof(MQuestReferenceCorrelationCalc)));
                    Assert.IsNotNull(scoringModelDlg.GetCalculatorWeight(typeof(LegacyUnforcedCountScoreStandardCalc)));
                    const int pepTargetCount = (pepCount - pepStandardCount)/2;
                    Assert.AreEqual(pepTargetCount * countRep, scoringModelDlg.GetTargetCount());
                    Assert.AreEqual(pepTargetCount * countRep, scoringModelDlg.GetDecoyCount());
                    scoringModelDlg.OkDialog();
                });
                RunUI(() =>
                {
                    reintegrateDlg.OverwriteManual = true;
                    reintegrateDlg.ComboPeakScoringModelSelected = peakScoringModelName;
                });
                OkDialog(reintegrateDlg, reintegrateDlg.OkDialog);
            }
            documentRescore = WaitForDocumentChange(documentRescore);

            matchedUserSetGroups = new[]
            {
                new UserSetCount(UserSet.FALSE, 276),
                new UserSetCount(UserSet.REINTEGRATED, 8),
            };
            matchedUserSetTrans = new[]
            {
                new UserSetCount(UserSet.FALSE, 1212),
                new UserSetCount(UserSet.REINTEGRATED, 36),
            };

            VerifyUserSets(documentRescore, matchedUserSetGroups, matchedUserSetTrans, true);
            VerifyMatchingPeakBoundaries(documentRescore, true);

            int changeCount = CountChangedPeaks(document, documentRescore);
            Assert.IsTrue(changeCount > 200);
        }

        private void VerifyUserSets(SrmDocument document, UserSetCount[] userSetGroups, UserSetCount[] userSetTrans, bool ignoreDecoys = false)
        {
            var counts = new int[userSetGroups.Length];
            foreach (var chromInfo in document.PeptideTransitionGroups
                .Where(nodeGroup => !ignoreDecoys || !nodeGroup.IsDecoy)
                .SelectMany(nodeGroup => nodeGroup.ChromInfos))
            {
                var userSet = chromInfo.UserSet;
                int userSetIndex = userSetGroups.IndexOf(us => Equals(us.UserSet, userSet));
                Assert.AreNotEqual(-1, userSetIndex, string.Format("Unexpected User Set value {0}", userSet));
                counts[userSetIndex]++;
            }
            for (int i = 0; i < userSetGroups.Length; i++)
            {
                Assert.AreEqual(userSetGroups[i].Count, counts[i]);
            }
            counts = new int[userSetTrans.Length];
            foreach (var chromInfo in document.PeptideTransitions
                .Where(nodeTran => !ignoreDecoys || !nodeTran.IsDecoy)
                .SelectMany(nodeTran => nodeTran.ChromInfos))
            {
                var userSet = chromInfo.UserSet;
                int userSetIndex = userSetTrans.IndexOf(us => Equals(us.UserSet, userSet));
                Assert.AreNotEqual(-1, userSetIndex, string.Format("Unexpected User Set value {0}", userSet));
                counts[userSetIndex]++;
            }
            for (int i = 0; i < userSetTrans.Length; i++)
            {
                Assert.AreEqual(userSetTrans[i].Count, counts[i]);
            }
        }

        private class UserSetCount
        {
            public UserSetCount(UserSet userSet, int count)
            {
                UserSet = userSet;
                Count = count;
            }

            public UserSet UserSet { get; private set; }
            public int Count { get; private set; }
        }

        private void VerifyMatchingPeakBoundaries(SrmDocument document, bool exactMatch)
        {
            foreach (var nodePep in document.Peptides)
            {
                if (nodePep.TransitionGroupCount < 2)
                    continue;

                VerifyMatchingPeakBoundaries(nodePep.TransitionGroups.First(),
                    nodePep.TransitionGroups.Skip(1).First(), exactMatch);
            }
        }

        private void VerifyMatchingPeakBoundaries(TransitionGroupDocNode nodeGroupLight, TransitionGroupDocNode nodeGroupHeavy, bool exactMatch)
        {
            for (int i = 0; i < EXPECTED_REPLICATES.Length; i++)
            {
                var peakLight = nodeGroupLight.Results[i][0];
                var peakHeavy = nodeGroupHeavy.Results[i][0];
                if (exactMatch)
                    Assert.AreEqual(peakLight.StartRetentionTime.Value, peakHeavy.StartRetentionTime.Value);
                else
                    Assert.AreEqual(peakLight.StartRetentionTime.Value, peakHeavy.StartRetentionTime.Value, 0.01);
                if (exactMatch)
                    Assert.AreEqual(peakLight.EndRetentionTime.Value, peakHeavy.EndRetentionTime.Value);
                else
                    Assert.AreEqual(peakLight.EndRetentionTime.Value, peakHeavy.EndRetentionTime.Value, 0.01);
            }
        }

        private int CountChangedPeaks(SrmDocument document, SrmDocument docNew)
        {
            var peptides = document.Peptides.ToArray();
            var pepNew = docNew.Peptides.ToArray();
            Assert.AreEqual(peptides.Length, pepNew.Length);
            int changedCount = 0;
            for (int i = 0; i < peptides.Length; i++)
            {
                var tranGroups = peptides[i].TransitionGroups.ToArray();
                var tranGroupsNew = pepNew[i].TransitionGroups.ToArray();
                changedCount += CountChangedPeaks(tranGroups, tranGroupsNew);
            }
            return changedCount;
        }

        private int CountChangedPeaks(TransitionGroupDocNode[] tranGroups, TransitionGroupDocNode[] tranGroupsNew)
        {
            Assert.AreEqual(tranGroups.Length, tranGroupsNew.Length);
            int changeCount = 0;
            for (int i = 0; i < tranGroups.Length; i++)
            {
                var nodeGroup = tranGroups[i];
                var nodeGroupNew = tranGroupsNew[i];

                changeCount += CountChangedPeaks(nodeGroup, nodeGroupNew);
            }
            return changeCount;
        }

        private int CountChangedPeaks(TransitionGroupDocNode nodeGroup, TransitionGroupDocNode nodeGroupNew)
        {
            int changeCount = 0;
            for (int i = 0; i < EXPECTED_REPLICATES.Length; i++)
            {
                var peak = nodeGroup.Results[i][0];
                var peakNew = nodeGroupNew.Results[i][0];
                if (peak.StartRetentionTime != peakNew.StartRetentionTime ||
                    peak.EndRetentionTime != peakNew.EndRetentionTime)
                {
                    changeCount++;
                }
            }
            return changeCount;
        }
    }
}