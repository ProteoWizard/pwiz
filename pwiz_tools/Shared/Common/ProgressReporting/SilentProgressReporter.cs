using System.Threading;

namespace pwiz.Common.ProgressReporting
{
    public class SilentProgressReporter : AbstractProgressReporter
    {
        public static readonly SilentProgressReporter
            INSTANCE = new SilentProgressReporter(CancellationToken.None);
        public SilentProgressReporter(CancellationToken cancellationToken) : base(cancellationToken)
        {
            CancellationToken = cancellationToken;
        }

        public CancellationToken CancellationToken { get; }

        public override void SetProgressValue(double value)
        {
        }
        public override void SetProgressMessage(string message)
        {
        }
    }
}
