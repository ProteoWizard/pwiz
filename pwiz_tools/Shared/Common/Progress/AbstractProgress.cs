using System.Threading;

namespace pwiz.Common.Progress
{
    public abstract class AbstractProgress : IProgressEx
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
    }
}
