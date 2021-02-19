using System;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    public class ProgressHandler
    {
        private Action<int> _updatePercentComplete;

        public ProgressHandler(CancellationToken cancellationToken) : this(cancellationToken, i => { })
        {
        }
        public ProgressHandler(CancellationToken cancellationToken, Action<int> updatePercentComplete)
        {
            CancellationToken = cancellationToken;
            _updatePercentComplete = updatePercentComplete;
        }
        public CancellationToken CancellationToken { get; }

        public void SetPercentComplete(int percentComplete)
        {
            CancellationToken.ThrowIfCancellationRequested();
            _updatePercentComplete(percentComplete);
        }
    }
}
