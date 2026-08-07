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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// The long operations running with nobody waiting for them, which can therefore be listed and stopped by
    /// whoever comes along afterwards: a report export whose tool client disconnected, or one the user sent to the
    /// background from its <see cref="Controls.LongWaitDlg"/>.
    ///
    /// <para>What is running is read from the main window's progress list (only the entries that are a
    /// <see cref="JobProgressStatus"/>), so a job's message and percentage are always the ones the work itself last
    /// reported - there is no second copy of that to keep up to date. What is kept here is only what the progress
    /// list cannot hold: each job's <see cref="CancellationTokenSource"/>, which has exactly one owner (the
    /// <see cref="BackgroundJob"/> handed out by <see cref="Start"/>) and is disposed when that handle is.</para>
    /// </summary>
    public static class BackgroundJobs
    {
        // Guards every use of a source in it, so one cannot be disposed out from under a cancel.
        private static readonly Dictionary<Guid, CancellationTokenSource> _cancellations =
            new Dictionary<Guid, CancellationTokenSource>();

        /// <summary>
        /// Starts a job and reports it, which is what puts it in the status bar and in <see cref="Running"/>.
        /// DISPOSE the handle when the work is over: that reports the job's final status, which is what takes it
        /// back out again.
        /// </summary>
        /// <param name="description">What the job is, in terms the user will read in the status bar
        /// ("Exporting report 'Peak Areas'").</param>
        public static BackgroundJob Start(string description)
        {
            var job = new BackgroundJob(new JobProgressStatus(description));
            lock (_cancellations)
            {
                _cancellations.Add(job.JobId, job.CancellationTokenSource);
            }
            ReportProgress(job.Status);
            return job;
        }

        /// <summary>
        /// The jobs running now, in the order they were started. Empty before the main window exists, where there
        /// is no progress list to hold them.
        /// </summary>
        public static JobProgressStatus[] Running
        {
            get
            {
                var progressStatuses = Program.MainWindow?.ProgressStatuses;
                if (progressStatuses == null)
                    return Array.Empty<JobProgressStatus>();
                return progressStatuses.OfType<JobProgressStatus>().ToArray();
            }
        }

        /// <summary>
        /// Asks the job with this id to stop, and reports whether there was such a job to ask. It is a REQUEST:
        /// the work stops at its next cancellation check, so the job can still be <see cref="Running"/> when this
        /// returns. False usually means the job had already finished.
        /// </summary>
        public static bool Cancel(Guid jobId)
        {
            lock (_cancellations)
            {
                if (!_cancellations.TryGetValue(jobId, out var cancellation))
                    return false;
                cancellation.Cancel();
                return true;
            }
        }

        /// <summary>
        /// Asks every running job to stop. Like <see cref="Cancel"/> it is a REQUEST: the jobs stop at their next
        /// cancellation check, so they are still <see cref="Running"/> for a moment afterwards.
        /// </summary>
        public static void CancelAll()
        {
            lock (_cancellations)
            {
                foreach (var cancellation in _cancellations.Values)
                {
                    cancellation.Cancel();
                }
            }
        }

        /// <summary>
        /// True when this job has been asked to stop but has not yet stopped. False for a job that is not running.
        /// </summary>
        public static bool IsCancelRequested(Guid jobId)
        {
            lock (_cancellations)
            {
                return _cancellations.TryGetValue(jobId, out var cancellation) && cancellation.IsCancellationRequested;
            }
        }

        // Reports a job's status to the main window, which is what puts it into -- and takes it back out of -- the
        // progress the status bar shows and Running reads. Does nothing before the main window exists (only the
        // start page is up), where there is no progress list and no long operation to report to it.
        internal static void ReportProgress(IProgressStatus status)
        {
            ((IProgressMonitor) Program.MainWindow)?.UpdateProgress(status);
        }

        internal static void Release(BackgroundJob job)
        {
            lock (_cancellations)
            {
                _cancellations.Remove(job.JobId);
                job.CancellationTokenSource.Dispose();
            }
        }
    }

    /// <summary>
    /// A job while it runs: the identity the user and a tool see it by, the cancellation the work must watch, and
    /// the means to report how it is going. Handed out by <see cref="BackgroundJobs.Start"/> and disposed by whoever
    /// runs the work, once it is over.
    /// </summary>
    public sealed class BackgroundJob : IDisposable
    {
        internal BackgroundJob(JobProgressStatus status)
        {
            Status = status;
        }

        /// <summary>The job's status as it was started - its identity. What the work has reported SINCE is in the
        /// main window's progress list, under the same <see cref="JobProgressStatus.JobId"/>.</summary>
        public JobProgressStatus Status { get; }

        public Guid JobId => Status.JobId;

        internal CancellationTokenSource CancellationTokenSource { get; } = new CancellationTokenSource();

        /// <summary>Cancelled when the job is cancelled. Watching it is the only way the work can be stopped.</summary>
        public CancellationToken CancellationToken => CancellationTokenSource.Token;

        public bool IsCancellationRequested => CancellationTokenSource.IsCancellationRequested;

        /// <summary>
        /// Reports how the job is going, for work that does not report progress itself (work that does simply
        /// reports <see cref="Status"/>, or a copy of it, and lands in the same place).
        /// </summary>
        /// <param name="message">What the job is doing now, or null to leave the description showing.</param>
        /// <param name="percentComplete">How far along, or -1 when that is not known.</param>
        public void UpdateProgress(string message, int percentComplete)
        {
            // Never 100: that is a FINAL status, and reporting one takes the job out of the progress list. It is
            // Dispose's to report, when the work really is over. -1 passes through - it means "unknown".
            if (percentComplete > 99)
                percentComplete = 99;
            var status = Status.ChangeMessage(message ?? Status.Description);
            BackgroundJobs.ReportProgress(status.ChangePercentComplete(percentComplete));
        }

        /// <summary>
        /// Reports that the job failed, which is what shows the user the error: nothing else will, because a job
        /// runs with no caller left to throw to.
        /// </summary>
        public void Failed(Exception exception)
        {
            BackgroundJobs.ReportProgress(Status.ChangeErrorException(exception));
        }

        /// <summary>
        /// Ends the job: reports its final status, which takes it out of the progress list and off the status bar,
        /// and releases its cancellation. Reporting a status that is already final (the work reported 100%, or
        /// <see cref="Failed"/> did) finds nothing left to replace and does nothing.
        /// </summary>
        public void Dispose()
        {
            BackgroundJobs.ReportProgress(IsCancellationRequested ? Status.Cancel() : Status.Complete());
            BackgroundJobs.Release(this);
        }
    }
}
