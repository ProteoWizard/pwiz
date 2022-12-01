using System;
using System.Threading;
using pwiz.Common.Progress;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util.Extensions
{
    public static class ProgressMonitorExtensions
    {
        public static T SafeCallWithProgress<T>(this IProgressMonitor progressMonitor, CancellationToken cancellationToken,
            ref IProgressStatus status, Func<IProgress, T> function)
        {
            T returnValue = default(T);
            try
            {
                returnValue = CallWithProgress(progressMonitor, cancellationToken, ref status, function);
            }
            catch (Exception e)
            {
                if (!progressMonitor.IsCanceled)
                {
                    if (!status.IsFinal)
                    {
                        progressMonitor.UpdateProgress(status = status.ChangeErrorException(e));
                    }
                }
            }
            return returnValue;
        }
        public static T CallWithProgress<T>(this IProgressMonitor progressMonitor, CancellationToken cancellationToken,
            ref IProgressStatus status, Func<IProgress, T> function)
        {
            var progressMonitorProgress = new ProgressMonitorProgress(progressMonitor, cancellationToken, status);
            try
            {
                var returnValue = function(progressMonitorProgress);
                status = progressMonitorProgress.ProgressStatus;
                if (progressMonitor.IsCanceled && !status.IsFinal)
                {
                    progressMonitor.UpdateProgress(status);
                }
                return returnValue;
            }
            finally
            {
                status = progressMonitorProgress.ProgressStatus;
            }
        }

        public static T CallWithNewProgress<T>(this IProgressMonitor progressMonitor, CancellationToken cancellationToken,
            Func<IProgress, T> function)
        {
            using (var progress = ProgressMonitorProgress.ForNewTask(progressMonitor, cancellationToken))
            {
                return function((IProgress)progress ?? SilentProgress.INSTANCE);
            }
        }
    }
}