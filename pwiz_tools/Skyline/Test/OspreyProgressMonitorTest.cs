/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// The Osprey Search wizard's progress bar, fed Osprey's console lines in the shapes its logs
    /// print them: one segment per [TASK], the per-file tasks advanced by their "file N/M:" lines
    /// (in any language) and the others by Osprey's own percentages, never backwards.
    /// </summary>
    [TestClass]
    public class OspreyProgressMonitorTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestOspreyProgressMonitor()
        {
            TestExpectedTaskCount();
            TestPlainSearch();
            TestTuningSearchSkipsAndGrowth();
            TestCancelPassesThrough();
            TestTimeStampedLines();
        }

        private static void TestExpectedTaskCount()
        {
            var config = new OspreyConfig { FastaPath = @"C:\proteome.fasta" };
            AssertEx.AreEqual(5, OspreyProgressMonitor.ExpectedTaskCount(config));
            config.TrainingInputs.Add(@"C:\runs\tuning.mzML");
            AssertEx.AreEqual(11, OspreyProgressMonitor.ExpectedTaskCount(config));
            config = new OspreyConfig { LibraryPath = @"C:\library.blib" };
            AssertEx.AreEqual(4, OspreyProgressMonitor.ExpectedTaskCount(config));
        }

        /// <summary>A FASTA search: the predicted library, then the four search tasks.</summary>
        private static void TestPlainSearch()
        {
            var wizard = new RecordingMonitor();
            var monitor = new OspreyProgressMonitor(wizard, 5);

            // Osprey's banner, before any task: no segments, the bar at zero
            var status = Feed(monitor, wizard, @"Osprey 1.0.0 (CPU, 32 threads)");
            AssertEx.AreEqual(0, status.SegmentCount);
            AssertEx.AreEqual(0, status.PercentComplete);

            // The first task opens the first of five segments; the bar names it, the log keeps Osprey's line
            status = Feed(monitor, wizard, @"[TASK] PredictLibrary:starting");
            AssertSegment(status, 0, 5, 0);
            AssertEx.AreEqual(@"[TASK] PredictLibrary:starting", status.Message);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_Predicting_the_library, status.SegmentName);

            // Osprey's own percentages advance a prediction task, and show on the bar
            Feed(monitor, wizard, @"Predicting spectra...");
            status = Feed(monitor, wizard, @"  30%");
            AssertSegment(status, 0, 5, 30);
            AssertEx.Contains(status.SegmentName, LibResources.OspreyProgressMonitor_GetTaskName_Predicting_the_library, @"30%");
            AssertEx.AreEqual(@"  30%", status.Message); // The wizard's log box drops a bare percentage itself
            status = Feed(monitor, wizard, @"  97%");
            AssertSegment(status, 0, 5, 97);
            // A Batch N/M line is not a file line outside the per-file tasks
            status = Feed(monitor, wizard, @"Batch 1/1: peptide forms 1-195646, 385206 precursors predicted in 23.9 s");
            AssertSegment(status, 0, 5, 97);
            status = Feed(monitor, wizard, @"  100%");
            AssertSegment(status, 0, 5, 100);
            AssertEx.AreEqual(20, status.PercentComplete);
            // A later, lower percentage (the writer's own progress) never moves the bar back
            status = Feed(monitor, wizard, @"  56%");
            AssertSegment(status, 0, 5, 100);
            status = Feed(monitor, wizard, @"[TASK] PredictLibrary:done (32.9s)");
            AssertSegment(status, 0, 5, 100);
            AssertEx.AreEqual(@"[TASK] PredictLibrary:done (32.9s)", status.Message);

            // A per-file task ignores the percentages and follows the file N/M lines
            status = Feed(monitor, wizard, @"[TASK] PerFileScoring:starting");
            AssertSegment(status, 1, 5, 0);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_Scoring_files, status.SegmentName);
            Feed(monitor, wizard, @"Reading 385,206 library spectra...");
            status = Feed(monitor, wizard, @"  100%");
            AssertSegment(status, 1, 5, 0);
            status = Feed(monitor, wizard, @"Unique library strings: 255,645 / 1,273,500 total (79.9% reduced)");
            AssertSegment(status, 1, 5, 0);
            status = Feed(monitor, wizard, @"Scoring file 1/3: D:\Perftests\stellar\Ste-2024-12-02_HeLa_4mz_sDIA_400-900_20.mzML");
            AssertSegment(status, 1, 5, 0);
            AssertEx.Contains(status.SegmentName, LibResources.OspreyProgressMonitor_GetTaskName_Scoring_files, @"1/3");
            status = Feed(monitor, wizard, @"  61%");
            AssertSegment(status, 1, 5, 0);
            status = Feed(monitor, wizard, @"Scoring file 2/3: D:\Perftests\stellar\Ste-2024-12-02_HeLa_4mz_sDIA_400-900_21.mzML");
            AssertSegment(status, 1, 5, 33);
            AssertEx.Contains(status.SegmentName, @"2/3");
            // An N/M with another count is not the file counter
            status = Feed(monitor, wizard, @"Calibrated 7/12 windows");
            AssertSegment(status, 1, 5, 33);
            AssertEx.Contains(status.SegmentName, @"2/3");
            status = Feed(monitor, wizard, @"Scoring file 3/3: D:\Perftests\stellar\Ste-2024-12-02_HeLa_4mz_sDIA_400-900_22.mzML");
            AssertSegment(status, 1, 5, 66);
            AssertEx.AreEqual(20 + 66 / 5, status.PercentComplete);
            status = Feed(monitor, wizard, @"[TASK] PerFileScoring:done (71.8s)");
            AssertSegment(status, 1, 5, 100);
            AssertEx.AreEqual(40, status.PercentComplete);

            // The FDR tasks: Percolator's lines end in a parenthesis, not a percentage; the folds are indented
            status = Feed(monitor, wizard, @"[TASK] FirstPassFDR:starting");
            AssertSegment(status, 2, 5, 0);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_First_pass_FDR, status.SegmentName);
            status = Feed(monitor, wizard, @"  Percolator iteration 1 of 10 (15.2% of training targets at 1% FDR)");
            AssertSegment(status, 2, 5, 0);
            status = Feed(monitor, wizard, @"    fold 1/3: C = 0.001");
            AssertSegment(status, 2, 5, 0);
            status = Feed(monitor, wizard, @"  66%");
            AssertSegment(status, 2, 5, 66);
            Feed(monitor, wizard, @"[TASK] FirstPassFDR:done (64.1s)");

            // Re-scoring in Japanese keeps the N/M; the training exports after the last file do not move it back
            status = Feed(monitor, wizard, @"[TASK] PerFileRescoring:starting");
            AssertSegment(status, 3, 5, 0);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_Re_scoring_files, status.SegmentName);
            status = Feed(monitor, wizard, "\u30d5\u30a1\u30a4\u30eb2/3\u3092\u518d\u30b9\u30b3\u30a2\u30ea\u30f3\u30b0\u4e2d\uff1aSte-2024-12-02_HeLa_4mz_sDIA_400-900_21");
            AssertSegment(status, 3, 5, 33);
            status = Feed(monitor, wizard, @"Re-scoring file 3/3: Ste-2024-12-02_HeLa_4mz_sDIA_400-900_22");
            AssertSegment(status, 3, 5, 66);
            status = Feed(monitor, wizard, @"Training export 1/3: Ste-2024-12-02_HeLa_4mz_sDIA_400-900_20");
            AssertSegment(status, 3, 5, 66);
            AssertEx.Contains(status.SegmentName, @"3/3");
            Feed(monitor, wizard, @"[TASK] PerFileRescoring:done (15.7s)");

            status = Feed(monitor, wizard, @"[TASK] SecondPassFDR:starting");
            AssertSegment(status, 4, 5, 0);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_Second_pass_FDR, status.SegmentName);
            AssertEx.AreEqual(80, status.PercentComplete);
            status = Feed(monitor, wizard, @"[TASK] SecondPassFDR:done (5.4s)");
            AssertSegment(status, 4, 5, 100);
            AssertEx.AreEqual(100, status.PercentComplete);
            status = Feed(monitor, wizard, @"Analysis complete in 189.9 seconds");
            AssertEx.AreEqual(100, status.PercentComplete);
        }

        /// <summary>
        /// A tuning run resumed: tasks skipped for their valid outputs are complete segments, the
        /// fine-tune's epochs do not move its segment, and a task beyond the expected count widens
        /// the bar instead of overflowing it.
        /// </summary>
        private static void TestTuningSearchSkipsAndGrowth()
        {
            var wizard = new RecordingMonitor();
            var monitor = new OspreyProgressMonitor(wizard, 11);

            var status = Feed(monitor, wizard, @"[TASK] PredictTrainingLibrary:skipping (outputs valid)");
            AssertSegment(status, 0, 11, 100);
            AssertEx.AreEqual(@"[TASK] PredictTrainingLibrary:skipping (outputs valid)", status.Message);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_Predicting_the_training_library, status.SegmentName);
            foreach (string task in new[] { OspreyProgressMonitor.TASK_PER_FILE_SCORING, OspreyProgressMonitor.TASK_FIRST_PASS_FDR,
                         OspreyProgressMonitor.TASK_PER_FILE_RESCORING, OspreyProgressMonitor.TASK_SECOND_PASS_FDR })
            {
                status = Feed(monitor, wizard, string.Format(@"[TASK] {0}:skipping (outputs valid)", task));
            }
            AssertSegment(status, 4, 11, 100);

            status = Feed(monitor, wizard, @"[TASK] TrainModel:starting");
            AssertSegment(status, 5, 11, 0);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_Fine_tuning_the_model, status.SegmentName);
            AssertEx.AreEqual(45, status.PercentComplete);
            status = Feed(monitor, wizard, @"  epoch 12/40: lr 9.89E-005, train loss 0.008240, test loss 0.013074");
            AssertSegment(status, 5, 11, 0);
            AssertEx.AreEqual(LibResources.OspreyProgressMonitor_GetTaskName_Fine_tuning_the_model, status.SegmentName);
            status = Feed(monitor, wizard, @"[TASK] TrainModel:done (580.2s)");
            AssertSegment(status, 5, 11, 100);

            foreach (string task in new[] { OspreyProgressMonitor.TASK_PREDICT_LIBRARY, OspreyProgressMonitor.TASK_PER_FILE_SCORING,
                         OspreyProgressMonitor.TASK_FIRST_PASS_FDR, OspreyProgressMonitor.TASK_PER_FILE_RESCORING,
                         OspreyProgressMonitor.TASK_SECOND_PASS_FDR })
            {
                Feed(monitor, wizard, string.Format(@"[TASK] {0}:starting", task));
                status = Feed(monitor, wizard, string.Format(@"[TASK] {0}:done (1.0s)", task));
            }
            AssertSegment(status, 10, 11, 100);
            AssertEx.AreEqual(100, status.PercentComplete);

            // A task the wizard did not expect is named on the bar as Osprey names it
            status = Feed(monitor, wizard, @"[TASK] ModelDiagnostics:starting");
            AssertSegment(status, 11, 12, 0);
            AssertEx.AreEqual(@"[TASK] ModelDiagnostics:starting", status.Message);
            AssertEx.AreEqual(@"ModelDiagnostics", status.SegmentName);
        }

        /// <summary>The cancel ProcessRunner reports reaches the wizard as a cancel, segments or not.</summary>
        private static void TestCancelPassesThrough()
        {
            var wizard = new RecordingMonitor();
            var monitor = new OspreyProgressMonitor(wizard, 5);
            Feed(monitor, wizard, @"[TASK] PredictLibrary:starting");
            var status = Feed(monitor, wizard, @"  30%");
            AssertSegment(status, 0, 5, 30);
            monitor.UpdateProgress(new ProgressStatus().ChangeMessage(@"  40%").Cancel());
            AssertEx.IsTrue(wizard.Last.IsCanceled);
            AssertEx.AreEqual(0, wizard.Last.SegmentCount);
        }

        /// <summary>
        /// Osprey runs with --timestamp: the stamp comes off every line the wizard shows, and is
        /// neither a file counter (its 2026/10) nor in the way of the percentage or the task line.
        /// </summary>
        private static void TestTimeStampedLines()
        {
            var wizard = new RecordingMonitor();
            var monitor = new OspreyProgressMonitor(wizard, 5);
            var status = Feed(monitor, wizard, "[2026/10/25 16:41:02]\tOsprey 1.0.0 (CPU, 32 threads)");
            AssertEx.AreEqual(@"Osprey 1.0.0 (CPU, 32 threads)", status.Message);
            status = Feed(monitor, wizard, "[2026/10/25 16:41:03]\t[TASK] PredictLibrary:starting");
            AssertSegment(status, 0, 5, 0);
            AssertEx.AreEqual(@"[TASK] PredictLibrary:starting", status.Message);
            status = Feed(monitor, wizard, "[2026/10/25 16:41:09]\t  30%");
            AssertSegment(status, 0, 5, 30);
            AssertEx.AreEqual(@"  30%", status.Message);
            Feed(monitor, wizard, "[2026/10/25 16:41:40]\t[TASK] PredictLibrary:done (37.2s)");
            status = Feed(monitor, wizard, "[2026/10/25 16:41:40]\t[TASK] PerFileScoring:starting");
            AssertSegment(status, 1, 5, 0);
            status = Feed(monitor, wizard, "[2026/10/25 16:41:44]\tScoring file 2/3: D:\\Perftests\\stellar\\Ste-2024-12-02_HeLa_4mz_sDIA_400-900_21.mzML");
            AssertSegment(status, 1, 5, 33);
            AssertEx.Contains(status.SegmentName, @"2/3");
            // A line that merely starts with a bracket keeps its text
            status = Feed(monitor, wizard, @"[PATH] PerFileScoring: 12 blocks read");
            AssertEx.AreEqual(@"[PATH] PerFileScoring: 12 blocks read", status.Message);
        }

        /// <summary>Feeds one line as ProcessRunner reports it, a status with no segments, and returns what the wizard received.</summary>
        private static IProgressStatus Feed(OspreyProgressMonitor monitor, RecordingMonitor wizard, string line)
        {
            int countBefore = wizard.Updates.Count;
            monitor.UpdateProgress(new ProgressStatus().ChangeMessage(line));
            AssertEx.AreEqual(countBefore + 1, wizard.Updates.Count, @"Every line reaches the wizard");
            return wizard.Last;
        }

        private static void AssertSegment(IProgressStatus status, int segment, int segmentCount, int percentOfSegment)
        {
            AssertEx.AreEqual(segmentCount, status.SegmentCount, @"segment count");
            AssertEx.AreEqual(segment, status.Segment, @"segment");
            AssertEx.AreEqual(percentOfSegment, status.ZoomedPercentComplete, @"percent of segment");
        }

        private class RecordingMonitor : IProgressMonitor
        {
            public List<IProgressStatus> Updates { get; } = new List<IProgressStatus>();
            public IProgressStatus Last => Updates[Updates.Count - 1];

            public bool IsCanceled => false;
            public bool HasUI => false;

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                Updates.Add(status);
                return UpdateProgressResponse.normal;
            }
        }
    }
}
