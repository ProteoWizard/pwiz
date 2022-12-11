using System;
using pwiz.Common.Progress;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util.Extensions
{
    public static class ProgressMonitorExtensions
    {
        public static T CallWithProgress<T>(this IProgressMonitor progressMonitor,
            ref IProgressStatus status, Func<IProgress, T> function)
        {
            if (progressMonitor == null)
            {
                return function(SilentProgress.INSTANCE);
            }
            var progressMonitorProgress = new ProgressMonitorProgress(progressMonitor, status);
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

        public static T CallWithNewProgress<T>(this IProgressMonitor progressMonitor, Func<IProgress, T> function)
        {
            using (var progress = ProgressMonitorProgress.ForNewTask(progressMonitor))
            {
                return function((IProgress)progress ?? SilentProgress.INSTANCE);
            }
        }
    }
}