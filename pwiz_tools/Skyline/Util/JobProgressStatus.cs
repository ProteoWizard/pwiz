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
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// The progress status of a JOB: a long operation that goes on running with nobody waiting for it, which can
    /// be listed and stopped afterwards by whoever comes along - a tool through
    /// <see cref="SkylineTool.IJsonToolService"/>, once the call that started it has been abandoned, or the user,
    /// once they have sent a <see cref="Controls.LongWaitDlg"/> to the background.
    ///
    /// <para>Being a status of this type is what makes an operation controllable that way. The main window's
    /// progress list holds the status of everything that reports progress - a results import, a library build, a
    /// background loader - and most of that has an owner already. Only a <see cref="JobProgressStatus"/> is
    /// reported as a job, and only by its <see cref="JobId"/> can one be cancelled.</para>
    ///
    /// <para><see cref="Description"/> says what the job IS ("Exporting report 'Peak Areas'") and does not change,
    /// unlike the inherited Message, which the work rewrites as it advances ("Writing row 5,000 / 20,000").</para>
    ///
    /// <para>This is an identity only - what can be told about a job, and what names it to cancel one. The
    /// cancellation itself is NOT here: a status is immutable and freely copied (the work reports a new copy for
    /// every progress update), which leaves no one place to own a CancellationTokenSource or to dispose it.
    /// <see cref="BackgroundJobs"/> keeps those, keyed by <see cref="JobId"/>, for as long as the job runs.</para>
    /// </summary>
    public class JobProgressStatus : ProgressStatus
    {
        public JobProgressStatus(string description) : base(description)
        {
            JobId = Guid.NewGuid();
            Description = description;
        }

        /// <summary>
        /// Identifies this job. It is what a cancel names, and it survives every immutable copy the work makes as
        /// it reports progress, so the copy in the progress list names the same job.
        /// </summary>
        public Guid JobId { get; }

        /// <summary>
        /// What the job is, in the terms of the call that started it. Fixed for the job's lifetime.
        /// </summary>
        public string Description { get; }
    }
}
