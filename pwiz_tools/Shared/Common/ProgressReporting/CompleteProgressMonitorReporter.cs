using System;
using System.Threading;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.ProgressReporting
{
    public class CompleteProgressMonitorReporter : ProgressMonitorReporter, IDisposable
    {
        public CompleteProgressMonitorReporter(IProgressMonitor progressMonitor, CancellationToken cancellationToken,
            string message) : base(progressMonitor, cancellationToken, new ProgressStatus(message))
        {

        }

        public void Dispose()
        {
            if (!IsCanceled)
            {
                ChangeProgressStatus(status=>status.Complete());
            }
        }
    }
}
