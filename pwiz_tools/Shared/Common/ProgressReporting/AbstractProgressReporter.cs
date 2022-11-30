using System.Threading;

namespace pwiz.Common.ProgressReporting
{
    public abstract class AbstractProgressReporter : IProgressReporter
    {
        protected AbstractProgressReporter(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }
        public CancellationToken CancellationToken { get; }
        public bool IsCanceled
        {
            get { return CancellationToken.IsCancellationRequested; }
        }

        public abstract void SetProgressValue(double value);
        public abstract void SetProgressMessage(string message);

        public double ProgressValue
        {
            set
            {
                SetProgressValue(value);
            }
        }

        public string ProgressMessage
        {
            set
            {
                SetProgressMessage(value);
            }
        }
    }
}
