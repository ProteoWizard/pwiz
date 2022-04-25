using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SynchronizedIntegrationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSynchronizedIntegration()
        {
            TestFilesZip = @"TestFunctional\SynchronizedIntegrationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("test1.sky")));

            var doc = WaitForDocumentLoaded();
            CheckSettings(doc, null, null, false);

            SelectNode(SrmDocument.Level.Molecules, 0);
            WaitForGraphs();

            var dlg = ShowDialog<SynchronizedIntegrationDlg>(SkylineWindow.EditMenu.ShowSynchronizedIntegrationDialog);

            string[] toSync = null;

            RunUI(() =>
            {
                // Check dialog options
                Assert.IsTrue(ArrayUtil.EqualsDeep(dlg.TargetOptions.ToArray(), doc.MeasuredResults.Chromatograms.Select(c => c.Name).ToArray()));
                Assert.IsTrue(dlg.Targets.Count() == dlg.TargetOptions.Count()); // everything should be checked initially
                Assert.IsTrue(ArrayUtil.EqualsDeep(dlg.GroupByOptions.ToArray(), new[] { Resources.GroupByItem_ToString_Replicates, "Condition", "BioReplicate" }));

                // Pick a few replicates to synchronize
                Assert.AreEqual(Resources.GroupByItem_ToString_Replicates, dlg.GroupBy);
                toSync = new[] { "2_SW-B", "4_SW-B", "6_SW-B" };
                dlg.Targets = toSync;
                Assert.AreEqual(dlg.Targets.Count(), toSync.Length);
            });
            OkDialog(dlg, dlg.OkDialog);

            doc = WaitForDocumentChange(doc);
            CheckSettings(doc, null, toSync, false);

            // Select 1st precursor
            var precursorIdx = 0;
            SelectNode(SrmDocument.Level.TransitionGroups, precursorIdx);

            // Get current peak boundaries before we change anything
            var originalTimes = CollectPeakBoundaries(doc, doc.PeptideTransitionGroups.First().TransitionGroup);

            // Change peak bounds and check
            var targetChromName = "1_SW-A";
            var tranGroup = doc.PeptideTransitionGroups.ElementAt(precursorIdx).TransitionGroup;
            doc = ChangePeakBoundsSynchronized(doc, tranGroup, originalTimes, targetChromName, toSync, 12.78, 13.25);

            // Remove peak and check
            doc = RemovePeakSynchronized(doc, tranGroup, originalTimes, targetChromName, toSync);

            // Try grouping by annotation
            dlg = ShowDialog<SynchronizedIntegrationDlg>(SkylineWindow.EditMenu.ShowSynchronizedIntegrationDialog);
            string groupByPersistedString = null;
            var targetConditions = new[] { "A" };
            RunUI(() =>
            {
                dlg.GroupBy = "Condition";
                dlg.Targets = targetConditions;
                groupByPersistedString = dlg.GroupByPersistedString;
            });
            OkDialog(dlg, dlg.OkDialog);

            doc = WaitForDocumentChange(doc);
            CheckSettings(doc, groupByPersistedString, targetConditions, false);

            var replicateValue = ReplicateValue.FromPersistedString(doc.Settings, groupByPersistedString);
            var annotationCalculator = new AnnotationCalculator(doc);
            toSync = (
                from chromSet in doc.MeasuredResults.Chromatograms
                where targetConditions.Contains(replicateValue.GetValue(annotationCalculator, chromSet).ToString())
                select chromSet.Name).ToArray();
            Assert.IsTrue(0 < toSync.Length && toSync.Length < doc.MeasuredResults.Chromatograms.Count);

            doc = ChangePeakBoundsSynchronized(doc, tranGroup, originalTimes, targetChromName, toSync, 15.0, 16.0);

            // Sync all
            dlg = ShowDialog<SynchronizedIntegrationDlg>(SkylineWindow.EditMenu.ShowSynchronizedIntegrationDialog);
            RunUI(() =>
            {
                dlg.GroupBy = Resources.GroupByItem_ToString_Replicates;
                toSync = dlg.TargetOptions.ToArray();
                dlg.Targets = toSync;

                // Test alignment to RT prediction
                Assert.IsFalse(SkylineWindow.AlignToRtPrediction);
                Assert.IsTrue(dlg.SelectedAlignItem.IsNone);
                Assert.IsTrue(dlg.SelectAlignRt());
                Assert.IsTrue(SkylineWindow.AlignToRtPrediction);
                // Reset alignment
                Assert.IsTrue(dlg.SelectNone());
                Assert.IsFalse(SkylineWindow.AlignToRtPrediction);
            });
            OkDialog(dlg, dlg.OkDialog);

            doc = WaitForDocumentChange(doc);
            CheckSettings(doc, null, null, true);

            // Test synchronized integration when "Show {Prediction} Score" is enabled (auto-calculated RT predictions)
            RunUI(() => SkylineWindow.AlignToRtPrediction = true);
            WaitForGraphs();
            ChangePeakBoundsSynchronized(doc, tranGroup, originalTimes, targetChromName, toSync, -28, -26);

            // Save and open other file
            RunUI(() =>
            {
                SkylineWindow.SaveDocument();
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("test2.sky"));
            });

            doc = WaitForDocumentLoaded();
            CheckSettings(doc, null, null, false);

            SelectNode(SrmDocument.Level.Molecules, 3);
            WaitForGraphs();

            // Sync all
            dlg = ShowDialog<SynchronizedIntegrationDlg>(SkylineWindow.EditMenu.ShowSynchronizedIntegrationDialog);
            targetChromName = "S_3";
            RunUI(() =>
            {
                dlg.GroupBy = Resources.GroupByItem_ToString_Replicates;
                toSync = dlg.TargetOptions.ToArray();
                dlg.Targets = toSync;

                // Test alignment to file
                Assert.IsNull(SkylineWindow.AlignToFile);
                var alignId = doc.MeasuredResults.Chromatograms.First(c => c.Name.Equals(targetChromName)).MSDataFileInfos[0].FileId;
                Assert.IsTrue(dlg.SelectAlignFile(alignId));
                Assert.IsTrue(ReferenceEquals(SkylineWindow.AlignToFile, alignId));
            });
            OkDialog(dlg, dlg.OkDialog);

            doc = WaitForDocumentChange(doc);
            CheckSettings(doc, null, null, true);

            // Test synchronized integration when times are aligned to a file
            WaitForGraphs();
            tranGroup = doc.PeptideTransitionGroups.ElementAt(3).TransitionGroup;
            originalTimes = CollectPeakBoundaries(doc, tranGroup);
            doc = ChangePeakBoundsSynchronized(doc, tranGroup, originalTimes, targetChromName, toSync, 35, 36);
            // Try changing integration on a different file than the one being aligned to
            targetChromName = "S_5";
            ChangePeakBoundsSynchronized(doc, tranGroup, originalTimes, targetChromName, toSync, 35, 36);
        }

        private static void CheckSettings(SrmDocument doc, string groupBy, IEnumerable<string> targets, bool all)
        {
            var docIntegration = doc.Settings.TransitionSettings.Integration;
            Assert.AreEqual(groupBy ?? string.Empty, docIntegration.SynchronizedIntegrationGroupBy ?? string.Empty);
            var targetSet = (targets ?? Array.Empty<string>()).ToHashSet();
            var docTargets = docIntegration.SynchronizedIntegrationTargets ?? Array.Empty<string>();
            Assert.IsTrue(docTargets.Length == targetSet.Count && docTargets.All(t => targetSet.Contains(t)));
            Assert.AreEqual(all, docIntegration.SynchronizedIntegrationAll);
        }

        private static Dictionary<string, Tuple<float, float>> CollectPeakBoundaries(SrmDocument doc, TransitionGroup tranGroup)
        {
            var originalTimes = new Dictionary<string, Tuple<float, float>>();
            var chromatograms = doc.MeasuredResults.Chromatograms;
            for (var i = 0; i < chromatograms.Count; i++)
            {
                var chromSet = chromatograms[i];
                Assert.AreEqual(1, chromSet.FileCount);
                var results = doc.PeptideTransitionGroups.First(t => ReferenceEquals(t.TransitionGroup, tranGroup)).Results[i];
                Assert.AreEqual(1, results.Count);
                var chromInfo = results[0];
                originalTimes[chromSet.Name] = Tuple.Create(chromInfo.StartRetentionTime.Value, chromInfo.EndRetentionTime.Value);
            }
            return originalTimes;
        }

        private SrmDocument ChangePeakBoundsSynchronized(SrmDocument doc, TransitionGroup tranGroup,
            IReadOnlyDictionary<string, Tuple<float, float>> originalTimes, string targetChromName, IEnumerable<string> syncChromSets,
            double startTime, double endTime, bool undo = true)
        {
            ChangePeakBounds(targetChromName, startTime, endTime);
            doc = WaitForDocumentChange(doc);

            var nodeTranGroup = doc.PeptideTransitionGroups.First(n => ReferenceEquals(n.TransitionGroup, tranGroup));

            var syncTargets = (syncChromSets ?? Array.Empty<string>()).ToHashSet();
            if (!syncTargets.Contains(targetChromName))
                syncTargets.Clear();

            for (var i = 0; i < doc.MeasuredResults.Chromatograms.Count; i++)
            {
                var name = doc.MeasuredResults.Chromatograms[i].Name;
                var chromInfo = nodeTranGroup.Results[i][0];
                var (originalStart, originalEnd) = originalTimes[name];
                var newStart = chromInfo.StartRetentionTime;
                var newEnd = chromInfo.EndRetentionTime;
                if (!name.Equals(targetChromName) && !syncTargets.Contains(name))
                {
                    Assert.AreEqual(originalStart, newStart,
                        string.Format("{0}: expected start RT to be unchanged ({1}), but was {2}", name, originalStart, newStart));
                    Assert.AreEqual(originalEnd, newEnd,
                        string.Format("{0}: expected end RT to be unchanged ({1}), but was {2}", name, originalEnd, newEnd));
                }
                else
                {
                    Assert.IsTrue(!newStart.HasValue || Math.Abs(startTime - newStart.Value) < Math.Abs(startTime - originalStart),
                        string.Format("{0}: old start RT = {1}, new start RT = {2}, target start RT = {3}", name, originalStart, newStart, startTime));
                    Assert.IsTrue(!newEnd.HasValue || Math.Abs(endTime - newEnd.Value) < Math.Abs(endTime - originalEnd),
                        string.Format("{0}: old end RT = {1}, new end RT = {2}, target end RT = {3}", name, originalEnd, newEnd, endTime));
                }
            }

            if (undo)
            {
                RunUI(SkylineWindow.Undo);
                doc = WaitForDocumentChange(doc);
            }

            return doc;
        }

        private static SrmDocument RemovePeakSynchronized(SrmDocument doc, TransitionGroup tranGroup,
            IReadOnlyDictionary<string, Tuple<float, float>> originalTimes, string targetChromName, IEnumerable<string> syncChromSets,
            bool undo = true)
        {
            ActivateReplicate(targetChromName);
            WaitForGraphs();

            RunUI(() => SkylineWindow.EditMenu.RemovePeak(false));
            doc = WaitForDocumentChange(doc);

            var nodeTranGroup = doc.PeptideTransitionGroups.First(n => ReferenceEquals(n.TransitionGroup, tranGroup));
            var syncTargets = (syncChromSets ?? Array.Empty<string>()).ToHashSet();

            for (var i = 0; i<doc.MeasuredResults.Chromatograms.Count; i++)
            {
                var name = doc.MeasuredResults.Chromatograms[i].Name;
                var chromInfo = nodeTranGroup.Results[i][0];
                var (originalStart, originalEnd) = originalTimes[name];
                var newStart = chromInfo.StartRetentionTime;
                var newEnd = chromInfo.EndRetentionTime;
                if (!name.Equals(targetChromName) && !syncTargets.Contains(name))
                {
                    Assert.AreEqual(originalStart, newStart);
                    Assert.AreEqual(originalEnd, newEnd);
                }
                else
                {
                    Assert.IsNull(newStart);
                    Assert.IsNull(newEnd);
                }
            }

            if (undo)
            {
                RunUI(SkylineWindow.Undo);
                doc = WaitForDocumentChange(doc);
            }

            return doc;
        }
    }
}
