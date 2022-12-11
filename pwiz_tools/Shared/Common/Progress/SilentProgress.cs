using System.Threading;

namespace pwiz.Common.Progress
{
    public struct SilentProgress : IProgressEx
    {
        public static readonly SilentProgress INSTANCE = default;
        public SilentProgress(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }
        public CancellationToken CancellationToken { get; }

        public string Message
        {
            set { }
        }

        public double Value
        {
            set { }
        }

        public bool IsCanceled
        {
            get { return CancellationToken.IsCancellationRequested; }
        }
    }
}
