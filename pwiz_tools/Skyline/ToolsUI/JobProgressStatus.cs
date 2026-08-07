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

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// The progress status of a JOB: a long operation started through <see cref="SkylineTool.IJsonToolService"/>
    /// which a client can list (GetRunningJobs) and stop (CancelJob) on a LATER call, after the call that started
    /// it has been abandoned.
    ///
    /// <para>Being a status of this type is what makes an operation a client's to control. The main window's
    /// progress list holds the status of everything that reports progress - a results import the user started, a
    /// library build, a background loader - and none of that is a tool's business to cancel. Only a
    /// <see cref="JobProgressStatus"/> is reported to a client, and only by its <see cref="JobId"/> can anything
    /// be cancelled.</para>
    ///
    /// <para><see cref="Description"/> says what the job IS ("Exporting report 'Peak Areas'") and does not change,
    /// unlike the inherited Message, which the work rewrites as it advances ("Writing row 5,000 / 20,000").</para>
    ///
    /// <para>This is an identity only - what a client can be told about a job, and what it names to cancel one. The
    /// cancellation itself is NOT here: a status is immutable and freely copied (the work reports a new copy for
    /// every progress update), which leaves no one place to own a CancellationTokenSource or to dispose it. The
    /// server keeps those, keyed by <see cref="JobId"/>, for as long as the job runs.</para>
    /// </summary>
    public class JobProgressStatus : ProgressStatus
    {
        public JobProgressStatus(string description) : base(description)
        {
            JobId = Guid.NewGuid();
            Description = description;
        }

        /// <summary>
        /// Identifies this job to a client. It is what CancelJob takes, and it survives every immutable copy the
        /// work makes as it reports progress, so the copy in the progress list names the same job.
        /// </summary>
        public Guid JobId { get; }

        /// <summary>
        /// What the job is, in the terms of the call that started it. Fixed for the job's lifetime.
        /// </summary>
        public string Description { get; }
    }
}
