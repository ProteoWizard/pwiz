/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies the "Run in Background" button on <see cref="LongWaitDlg"/>: pressing it must return PerformWork
    /// to its caller with the work still running, hand that work to a job the user can see in the status bar and
    /// stop, and end the job when the work does.
    /// </summary>
    [TestClass]
    public class LongWaitDlgBackgroundTest : AbstractFunctionalTest
    {
        private const string JOB_DESCRIPTION = @"Test background job";
        private const string WORK_MESSAGE = @"Working on it";
        private const int WORK_PERCENT = 42;

        // Set when the work has reported its first progress, so the test can press the button knowing what the
        // job's message and percentage should be.
        private readonly ManualResetEventSlim _workStarted = new ManualResetEventSlim(false);
        // Set when PerformWork has returned to its caller - which the button must cause, with the work unfinished.
        private readonly ManualResetEventSlim _performWorkReturned = new ManualResetEventSlim(false);
        // Set to let the work finish when it was not cancelled. Every exit path must set it.
        private readonly ManualResetEventSlim _releaseWork = new ManualResetEventSlim(false);

        [TestMethod]
        public void TestLongWaitDlgBackground()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            try
            {
                TestButtonHiddenWithoutDescription();
                TestWorkRunsOnAsAJob();
            }
            finally
            {
                _releaseWork.Set();
            }
        }

        /// <summary>
        /// An operation may be backgrounded only if its caller says so, by naming the job. Without that the button
        /// is not there at all.
        /// </summary>
        private void TestButtonHiddenWithoutDescription()
        {
            StartLongWait(null);
            var longWaitDlg = WaitForOpenForm<LongWaitDlg>();
            AssertEx.IsFalse(GetBackgroundButtonVisible(longWaitDlg));
            _releaseWork.Set();
            WaitForClosedForm(longWaitDlg);
            WaitForPerformWorkReturned();
            ResetForNextRun();
        }

        private void TestWorkRunsOnAsAJob()
        {
            StartLongWait(JOB_DESCRIPTION);
            var longWaitDlg = WaitForOpenForm<LongWaitDlg>();
            AssertEx.IsTrue(_workStarted.Wait(WAIT_TIME));
            AssertEx.IsTrue(GetBackgroundButtonVisible(longWaitDlg));

            RunUI(longWaitDlg.RunInBackground);

            // The dialog is gone and its caller has moved on, with the work still going.
            WaitForClosedForm(longWaitDlg);
            WaitForPerformWorkReturned();

            // What the work reported to the dialog now belongs to the job, which is what the status bar shows.
            var jobs = RunningJobs.Running;
            AssertEx.AreEqual(1, jobs.Length);
            var job = jobs[0];
            AssertEx.AreEqual(JOB_DESCRIPTION, job.Description);
            AssertEx.AreEqual(WORK_MESSAGE, job.Message);
            AssertEx.AreEqual(WORK_PERCENT, job.PercentComplete);

            // Cancelling the job is what stops the work - the same call a tool's CancelJob makes.
            AssertEx.IsTrue(RunningJobs.Cancel(job.JobId));
            WaitForCondition(() => RunningJobs.Running.Length == 0);
        }

        /// <summary>
        /// Runs the work under a LongWaitDlg on the UI thread, without waiting for it: the test thread has to be
        /// free to find the dialog and press its button. <paramref name="jobDescription"/> null leaves the
        /// operation un-backgroundable.
        /// </summary>
        private void StartLongWait(string jobDescription)
        {
            SkylineWindow.BeginInvoke(new Action(() =>
            {
                using (var longWaitDlg = new LongWaitDlg())
                {
                    longWaitDlg.BackgroundJobDescription = jobDescription;
                    longWaitDlg.PerformWork(SkylineWindow, 0, broker =>
                    {
                        broker.Message = WORK_MESSAGE;
                        broker.ProgressValue = WORK_PERCENT;
                        _workStarted.Set();
                        // Held here until the job is cancelled, or the test lets it go.
                        while (!broker.IsCanceled && !_releaseWork.Wait(50))
                        {
                        }
                    });
                }
                _performWorkReturned.Set();
            }));
        }

        private static bool GetBackgroundButtonVisible(LongWaitDlg longWaitDlg)
        {
            bool visible = false;
            RunUI(() => visible = FindBackgroundButton(longWaitDlg).Visible);
            return visible;
        }

        private static System.Windows.Forms.Control FindBackgroundButton(LongWaitDlg longWaitDlg)
        {
            var button = longWaitDlg.Controls.Find(@"btnBackground", true);
            AssertEx.AreEqual(1, button.Length);
            return button[0];
        }

        private void WaitForPerformWorkReturned()
        {
            AssertEx.IsTrue(_performWorkReturned.Wait(WAIT_TIME));
        }

        private void ResetForNextRun()
        {
            _workStarted.Reset();
            _performWorkReturned.Reset();
            _releaseWork.Reset();
        }
    }
}
