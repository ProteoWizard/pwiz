using System;
using System.Threading;
using pwiz.Common.SystemUtil;

namespace pwiz.Common.Progress
{
    public static class ProgressExtensions
    {
        public static void SetProgressValue(this IProgress progress, double value)
        {
            progress.Value = value;
        }

        public static void SetProgressValue(this IProgress progress, int value)
        {
            progress.Value = value;
        }

        public static void SetProgressMessage(this IProgress progress, string message)
        {
            progress.Message = message;
        }
        public static void SetProgressCheckCancel(this IProgress progress, int step, int totalSteps)
        {
            progress.ThrowIfCancellationRequested();
            progress.SetProgressValue(step * 100.0 / totalSteps);
        }

        public static UpdateProgressResponse UpdateProgress(this IProgress progress, IProgressStatus status)
        {
            progress.Message = status.Message;
            progress.Value = status.PercentComplete;
            if (status.IsError)
            {
                throw status.ErrorException;
            }
            return progress.IsCanceled ? UpdateProgressResponse.cancel : UpdateProgressResponse.normal;
        }

        public static void ThrowIfCancellationRequested(this IProgress progress)
        {
            if (progress is IProgressEx progressEx)
            {
                progressEx.CancellationToken.ThrowIfCancellationRequested();
            } 
            else if (progress.IsCanceled)
            {
                throw new OperationCanceledException();
            }
        }

        public static IProgressEx WithCancellationToken(this IProgress progress, CancellationToken cancellationToken)
        {
            return new ProgressEx(progress, cancellationToken);
        }

        private class ProgressEx : IProgressEx
        {
            private readonly IProgress _progress;
            public ProgressEx(IProgress progress, CancellationToken cancellationToken)
            {
                _progress = progress;
                CancellationToken = cancellationToken;
            }

            public bool IsCanceled
            {
                get { return _progress.IsCanceled; }
            }
            public double Value
            {
                set { _progress.Value = value; }
            }
            public string Message
            {
                set { _progress.Message = value; }
            }
            public CancellationToken CancellationToken { get; }
        }

    }
}
