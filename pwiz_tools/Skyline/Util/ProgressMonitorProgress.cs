using System;
using JetBrains.Annotations;
using pwiz.Common.Progress;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util
{
    public class ProgressMonitorProgress : IProgress
    {
        protected IProgressStatus _progressStatus;
        
        public ProgressMonitorProgress(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            ProgressMonitor = progressMonitor;
            _progressStatus = progressStatus;
            progressMonitor.UpdateProgress(progressStatus);
        }

        public static IProgress ForProgressMonitor(IProgressMonitor progressMonitor, IProgressStatus progressStatus)
        {
            if (progressMonitor == null)
            {
                return SilentProgress.INSTANCE;
            }

            return new ProgressMonitorProgress(progressMonitor, progressStatus);
        }

        [CanBeNull]
        public static Disposable ForNewTask(IProgressMonitor progressMonitor)
        {
            if (progressMonitor == null)
            {
                return null;
            }

            return new Disposable(progressMonitor);
        }

        public IProgressMonitor ProgressMonitor { get; }
        public IProgressStatus ProgressStatus
        {
            get { return _progressStatus; }
        }

        public double Value 
        {
            set
            {
                ChangeProgressStatus(status => status.ChangePercentComplete(ToPercentComplete(value)));
            }
        }

        public string Message
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
            private bool _disposed;
            public Disposable(IProgressMonitor progressMonitor) : base(
                progressMonitor, new ProgressStatus())
            {
            }

            ~Disposable()
            {
                if (!_disposed)
                {
                    Program.ReportException(new ApplicationException(@"Disposable progress monitor was not disposed"));
                }
            }

            public void Dispose()
            {
                _disposed = true;
                ChangeProgressStatus(status=>status.IsFinal ? status : status.Complete());
            }
        }

        public bool IsCanceled
        {
            get { return ProgressMonitor.IsCanceled; }
        }
    }
}
