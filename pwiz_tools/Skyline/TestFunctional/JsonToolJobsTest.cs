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
using pwiz.Common.SystemUtil;
using pwiz.Skyline;
using pwiz.Skyline.ToolsUI;
using pwiz.Skyline.Util.Extensions;
using pwiz.SkylineTestUtil;
using SkylineTool;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies the job verbs a client uses to deal with a call it gave up on: GetRunningJobs, which reports what
    /// this service started and is still working on, and CancelJob, which stops one by id.
    ///
    /// <para>Everything runs over a real pipe connection, because these two calls only matter to a caller that is
    /// not the one that started the work -- and the wire format they answer in is part of what is being tested.</para>
    /// </summary>
    [TestClass]
    public class JsonToolJobsTest : McpConnectorTest
    {
        private const string JOB_DESCRIPTION = @"Test job";
        private const string NOT_A_JOB_MESSAGE = @"Not a job";

        // How long a wait for the test job to reach a state may take before the test gives up on it.
        private const int WAIT_MILLIS = 60 * 1000;

        [TestMethod]
        public void TestJsonToolJobs()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            StartToolService();
            using (var client = SkylineJsonToolClient.Connect(Program.MainJsonToolServer.PipeName))
            {
                TestNothingRunning(client);
                TestJobListedAndCancelled(client);
                TestJobIdNotRunning(client);
                TestJobIdNotAnId(client);
                TestFinishedVerbLeavesNoJob(client);
            }
        }

        private static void TestNothingRunning(IJsonToolService client)
        {
            AssertEx.AreEqual(0, client.GetRunningJobs().Length);
        }

        /// <summary>
        /// The main scenario: a job is running, a client that did not start it lists it and stops it by id, and the
        /// job goes away. Progress that is NOT a job runs alongside it throughout, because the point of the
        /// JobProgressStatus subclass is that only what this service started is reported and cancellable.
        /// </summary>
        private static void TestJobListedAndCancelled(IJsonToolService client)
        {
            var progressMonitor = (IProgressMonitor) Program.MainWindow;
            var testJob = new TestJob(JOB_DESCRIPTION);
            // Progress the user's own work reports -- a results import, a library build. It is in the same list the
            // jobs are read from, and must never be reported to a client or be cancellable by one.
            IProgressStatus notAJob = new ProgressStatus(NOT_A_JOB_MESSAGE);
            try
            {
                testJob.Start();
                progressMonitor.UpdateProgress(notAJob);

                var jobs = client.GetRunningJobs();
                AssertEx.AreEqual(1, jobs.Length);
                var jobInfo = jobs[0];
                AssertEx.AreEqual(testJob.JobId.ToString(), jobInfo.Id);
                AssertEx.AreEqual(JOB_DESCRIPTION, jobInfo.Description);
                AssertEx.IsFalse(jobInfo.CancelRequested);

                var cancelResult = client.CancelJob(jobInfo.Id);
                AssertEx.IsTrue(cancelResult.Completed);

                // The job stops at its next cancellation check, so wait for the job itself to say it has stopped
                // rather than for the list to change.
                testJob.WaitForStopped();
                AssertEx.AreEqual(0, client.GetRunningJobs().Length);
            }
            finally
            {
                // Every exit path, so a failed assertion above does not leave a thread running and the status bar
                // reporting progress that never ends.
                testJob.Stop();
                progressMonitor.UpdateProgress(notAJob.Complete());
            }
        }

        /// <summary>
        /// Cancelling a job that is not running is not an error - the usual reason is that it finished between
        /// being listed and being cancelled, which is the outcome the caller wanted.
        /// </summary>
        private static void TestJobIdNotRunning(IJsonToolService client)
        {
            var result = client.CancelJob(Guid.NewGuid().ToString());
            AssertEx.IsFalse(result.Completed);
            AssertEx.IsFalse(string.IsNullOrEmpty(result.Message));
        }

        private static void TestJobIdNotAnId(IJsonToolService client)
        {
            AssertEx.ThrowsException<JsonRpcException>(() => client.CancelJob(@"not-a-job-id"),
                exception => AssertEx.AreEqual(JsonToolConstants.ERROR_INVALID_PARAMS, exception.Code));
        }

        /// <summary>
        /// A verb that runs as a job (every report verb does) must take its job back out of the progress list when
        /// it finishes - otherwise it would be listed as running forever, and the status bar would say so too.
        /// </summary>
        private static void TestFinishedVerbLeavesNoJob(IJsonToolService client)
        {
            var definition = new ReportDefinition { Select = new[] { @"ProteinName", @"PrecursorMz" } };
            var rows = client.GetReportFromDefinitionRows(definition, 0, 10, false,
                JsonToolConstants.CULTURE_INVARIANT);
            AssertEx.IsNotNull(rows);
            AssertEx.AreEqual(0, client.GetRunningJobs().Length);
        }

        /// <summary>
        /// Stands in for a long call made through the connector: work run by <see cref="JsonToolServer.RunJob"/> -
        /// the very path every report verb takes - which sits there until it is cancelled. A real verb finishes far
        /// too fast on a test document to still be running when the next call arrives, which is the only state
        /// these verbs exist for.
        /// </summary>
        private class TestJob
        {
            private readonly string _description;
            private readonly ManualResetEventSlim _started = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _stopped = new ManualResetEventSlim(false);
            private readonly ManualResetEventSlim _released = new ManualResetEventSlim(false);

            public TestJob(string description)
            {
                _description = description;
            }

            /// <summary>The job's id, which is what a client lists and cancels it by.</summary>
            public Guid JobId { get; private set; }

            /// <summary>
            /// Starts the job, and returns once it is running - so a call made after this really does find it.
            /// </summary>
            public void Start()
            {
                ActionUtil.RunAsync(() =>
                {
                    JsonToolServer.RunJob(_description, (job, cancellationToken) =>
                    {
                        JobId = job.JobId;
                        _started.Set();
                        // Held here until cancelled through the service, or released by the test.
                        while (!cancellationToken.IsCancellationRequested && !_released.Wait(100))
                        {
                        }
                        return true;
                    });
                    _stopped.Set();
                });
                AssertEx.IsTrue(_started.Wait(WAIT_MILLIS));
            }

            public void WaitForStopped()
            {
                AssertEx.IsTrue(_stopped.Wait(WAIT_MILLIS));
            }

            /// <summary>Ends the job without going through the service, and waits for it to stop.</summary>
            public void Stop()
            {
                _released.Set();
                _stopped.Wait(WAIT_MILLIS);
            }
        }
    }
}
