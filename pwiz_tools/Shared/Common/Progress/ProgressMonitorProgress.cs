using System;
using System.Threading;
using JetBrains.Annotations;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Progress
{
    public class ProgressMonitorProgress : AbstractProgress
    {
        protected IProgressStatus _progressStatus;
        
        public ProgressMonitorProgress(IProgressMonitor progressMonitor, CancellationToken cancellationToken,
            IProgressStatus progressStatus) : base(cancellationToken)
        {
            ProgressMonitor = progressMonitor;
            _progressStatus = progressStatus;
            progressMonitor.UpdateProgress(progressStatus);
        }

        public static IProgress ForProgressMonitor(IProgressMonitor progressMonitor,
            CancellationToken cancellationToken, IProgressStatus progressStatus)
        {
            if (progressMonitor == null)
            {
                return SilentProgress.INSTANCE;
            }

            return new ProgressMonitorProgress(progressMonitor, cancellationToken, progressStatus);
        }

        [CanBeNull]
        public static Disposable ForNewTask(IProgressMonitor progressMonitor, CancellationToken cancellationToken)
        {
            if (progressMonitor == null)
            {
                return null;
            }

            return new Disposable(progressMonitor, cancellationToken);
        }

        public IProgressMonitor ProgressMonitor { get; }
        public IProgressStatus ProgressStatus
        {
            get { return _progressStatus; }
        }

        public override double Value 
        {
            set
            {
                ChangeProgressStatus(status => status.ChangePercentComplete(ToPercentComplete(value)));
            }
        }

        public override string Message
        {
            set
            {
                ChangeProgressStatus(progressStatus => progressStatus.ChangeMessage(value));
            }
        }

        public static int ToPercentComplete(double value)
        {
            return Math.Max(Math.Min((int)Math.Floor(value), 100), 0);
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

        public class Disposable : ProgressMonitorProgress, IDisposable
        {
            public Disposable(IProgressMonitor progressMonitor, CancellationToken cancellationToken) : base(
                progressMonitor, cancellationToken, new ProgressStatus())
            {
            }

            public void Dispose()
            {
                ChangeProgressStatus(status=>status.IsFinal ? status : status.Complete());
            }
        }
    }
}
