using System;
using System.Threading;

namespace pwiz.Common.Progress
{
    public abstract class AbstractProgress : IProgress
    {
        protected AbstractProgress(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }
        public CancellationToken CancellationToken { get; }
        public bool IsCanceled
        {
            get { return CancellationToken.IsCancellationRequested; }
        }

        public abstract double Value
        {
            set;
        }

        public abstract string Message
        {
            set;
        }

        protected static int ConstrainProgress(double progress)
        {
            return Math.Max(0, Math.Min(100, (int) Math.Round(progress)));
        }
    }
}
