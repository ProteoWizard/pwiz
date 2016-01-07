using System;
using pwiz.Common.SystemUtil;

namespace pwiz.Topograph.Util
{
    public class ProgressMonitorImpl : IProgressMonitor
    {
        private Func<bool> _isCanceledImpl;
        private Action<ProgressStatus> _updateProgressImpl;
        public ProgressMonitorImpl(Func<bool> isCanceledImpl, Action<ProgressStatus> updateProgressImpl)
        {
            _isCanceledImpl = isCanceledImpl;
            _updateProgressImpl = updateProgressImpl;
        }
        public bool IsCanceled
        {
            get { return _isCanceledImpl.Invoke(); }
        }

        public UpdateProgressResponse UpdateProgress(ProgressStatus status)
        {
            _updateProgressImpl.Invoke(status);
            return UpdateProgressResponse.normal;
        }

        public bool HasUI
        {
            get { return false; }
        }

        public static ProgressMonitorImpl NewProgressMonitorImpl(ProgressStatus currentStatus, Func<int, bool> updateProgress)
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
