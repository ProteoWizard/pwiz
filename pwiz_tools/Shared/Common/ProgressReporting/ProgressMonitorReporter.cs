using System;
using System.Threading;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.ProgressReporting
{
    public class ProgressMonitorReporter : AbstractProgressReporter
    {
        protected IProgressStatus _progressStatus;
        
        public ProgressMonitorReporter(IProgressMonitor progressMonitor, CancellationToken cancellationToken,
            IProgressStatus progressStatus) : base(cancellationToken)
        {
            ProgressMonitor = progressMonitor;
            _progressStatus = progressStatus;
            progressMonitor.UpdateProgress(progressStatus);
        }

        public IProgressMonitor ProgressMonitor { get; }

        public override void SetProgressValue(double value)
        {
            ChangeProgressStatus(status=>status.ChangePercentComplete(ToPercentComplete(value)));
        }

        public override void SetProgressMessage(string message)
        {
            ChangeProgressStatus(progressStatus=>progressStatus.ChangeMessage(message));
        }

        public static int ToPercentComplete(double value)
        {
            return Math.Max(Math.Min((int)Math.Round(value), 100), 0);
        }

        protected void ChangeProgressStatus(Func<IProgressStatus, IProgressStatus> changeFunc)
        {
            IProgressStatus progressStatus;
            lock (this)
            {
                progressStatus = _progressStatus = changeFunc(_progressStatus);
            }

            ProgressMonitor.UpdateProgress(progressStatus);
        }

        public void Complete()
        {
            ChangeProgressStatus(status=>status.Complete());
        }
    }
}
