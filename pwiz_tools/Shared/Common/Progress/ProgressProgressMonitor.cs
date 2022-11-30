using pwiz.Common.SystemUtil;

namespace pwiz.Common.Progress
{
    public class ProgressProgressMonitor : IProgressMonitor
    {
        public ProgressProgressMonitor(IProgress progress)
        {
            Progress = progress;
        }

        public IProgress Progress { get; }

        public bool IsCanceled
        {
            get { return Progress.IsCanceled; }
        }
        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            Progress.Message = status.Message;
            Progress.Value = status.PercentComplete;
            if (status.IsError)
            {
                throw status.ErrorException;
            }

            return status.IsCanceled ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
        }

        public bool HasUI
        {
            get { return false; }
        }
    }
}
