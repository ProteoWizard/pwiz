using System;
using pwiz.Common.SystemUtil;

namespace pwiz.Topograph.Util
{
    public class ProgressMonitorImpl : IProgressMonitor
    {
        private Func<bool> _isCanceledImpl;
        private Action<IProgressStatus> _updateProgressImpl;
        public ProgressMonitorImpl(Func<bool> isCanceledImpl, Action<IProgressStatus> updateProgressImpl)
        {
            _isCanceledImpl = isCanceledImpl;
            _updateProgressImpl = updateProgressImpl;
        }
        public bool IsCanceled
        {
            get { return _isCanceledImpl.Invoke(); }
        }

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            _updateProgressImpl.Invoke(status);
            return UpdateProgressResponse.normal;
        }

        public bool HasUI
        {
            get { return false; }
        }

        public static ProgressMonitorImpl NewProgressMonitorImpl(IProgressStatus currentStatus, Func<int, bool> updateProgress)
        {
            return new ProgressMonitorImpl(
                () => !updateProgress.Invoke(currentStatus.PercentComplete), 
                status => {
                    currentStatus = status;
                    updateProgress(status.PercentComplete);
                });
        }
    }
}
