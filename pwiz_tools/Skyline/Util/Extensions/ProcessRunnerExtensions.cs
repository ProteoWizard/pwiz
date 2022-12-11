using System.Diagnostics;
using System.IO;
using pwiz.Common.SystemUtil;

namespace pwiz.Skyline.Util.Extensions
{
    public static class ProcessRunnerExtensions
    {
        public static void Run(this ProcessRunner processRunner, ProcessStartInfo psi, string stdin, IProgressMonitor progress, ref IProgressStatus status,
            ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal)
        {
            Run(processRunner, psi, stdin, progress, ref status, null, priorityClass);
        }


        public static void Run(this ProcessRunner processRunner, ProcessStartInfo psi, string stdin, IProgressMonitor progressMonitor, ref IProgressStatus status,
            TextWriter writer,
            ProcessPriorityClass priorityClass = ProcessPriorityClass.Normal)
        {
            var progressMonitorProgress = new ProgressMonitorProgress(progressMonitor, status);
            try
            {
                processRunner.Run(psi, stdin, progressMonitorProgress, writer, priorityClass);
            }
            finally
            {
                status = progressMonitorProgress.ProgressStatus;
            }
        }
    }
}
