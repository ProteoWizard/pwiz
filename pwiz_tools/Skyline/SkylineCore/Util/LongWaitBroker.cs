/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Broker communication between a long operation and the user,
    /// usually used with the Skyline UI's <c>LongWaitDlg</c>.
    /// </summary>
    public interface ILongWaitBroker
    {
        /// <summary>
        /// True if the use has canceled the long operation
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// Percent complete in the progress indicator shown to the user
        /// </summary>
        int ProgressValue { get; set; }

        /// <summary>
        /// Message shown to the user
        /// </summary>
        string Message { set; }

        /// <summary>
        /// Returns true if the given original document is no longer the active
        /// document for this operation.
        /// </summary>
        bool IsDocumentChanged(SrmDocument docOrig);

        /// <summary>
        /// Convenience function which calculates progress percentage and throws
        /// an OperationCanceledOperation if the user has canceled.
        /// </summary>
        void SetProgressCheckCancel(int step, int totalSteps);

        CancellationToken CancellationToken { get; }
    }

    /// <summary>
    /// Exposes <see cref="IProgressMonitorWithCancellationToken"/> for an action that requires the
    /// <see cref="IProgressMonitor" /> interface but may benefit from having access to a CancellationToken,
    /// given a <see cref="ILongWaitBroker"/>.
    /// </summary>
    public sealed class ProgressWaitBroker : IProgressMonitorWithCancellationToken
    {
        private readonly Action<IProgressMonitor> _performWork;
        private ILongWaitBroker _broker;

        public ProgressWaitBroker(Action<IProgressMonitor> performWork)
        {
            _performWork = performWork;
            Status = new ProgressStatus(string.Empty);
        }

        public void PerformWork(ILongWaitBroker broker)
        {
            _broker = broker;
            _performWork(this);
        }

        public IProgressStatus Status { get; private set; }

        public bool IsCanceled
        {
            get { return _broker.IsCanceled; }
        }

        public CancellationToken CancellationToken
        {
            get { return _broker.CancellationToken;  }
        }

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            _broker.ProgressValue = status.PercentComplete;
            _broker.Message = status.Message;
            Status = status;
            return UpdateProgressResponse.normal;
        }

        public bool HasUI { get { return false; } }
    }

    public class ProgressUpdateEventArgs : EventArgs
    {
        public ProgressUpdateEventArgs(IProgressStatus progress)
        {
            Progress = progress;
        }

        public IProgressStatus Progress { get; private set; }

        public UpdateProgressResponse Response { get; set; }
    }

    /// <summary>
    /// An <see cref="ILongWaitBroker"/> that shows nothing, for a long operation run where there is
    /// no user interface. See <see cref="ProgramContext.RunLongOperation"/>.
    /// </summary>
    public sealed class SilentLongWaitBroker : ILongWaitBroker
    {
        public SilentLongWaitBroker(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public bool IsCanceled
        {
            get { return CancellationToken.IsCancellationRequested; }
        }

        public int ProgressValue { get; set; }

        public string Message { set { } }

        public bool IsDocumentChanged(SrmDocument docOrig)
        {
            return false;
        }

        public void SetProgressCheckCancel(int step, int totalSteps)
        {
            if (IsCanceled)
                throw new OperationCanceledException();
            ProgressValue = (int)(step * 100.0 / totalSteps);
        }
    }

    /// <summary>
    /// This interface was created to enable swapping for a fake WebHelpers in testing.  
    /// </summary>
    public interface IWebHelpers
    {
        void OpenLink(string link);
        void PostToLink(string link, string postData);
    }
}
