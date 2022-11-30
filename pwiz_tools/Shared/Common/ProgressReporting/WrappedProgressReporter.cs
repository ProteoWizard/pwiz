using System.Threading;

namespace pwiz.Common.ProgressReporting
{
    public class WrappedProgressReporter : AbstractProgressReporter
    {
        public WrappedProgressReporter(IProgressReporter parent) : this(parent, parent.CancellationToken)
        {
}

        public WrappedProgressReporter(IProgressReporter parent, CancellationToken newCancellationToken) : base(newCancellationToken)
        {
            Parent = parent;
        }

        protected IProgressReporter Parent { get; }

        public override void SetProgressMessage(string message)
        {
            Parent.SetProgressMessage(message);
        }

        public override void SetProgressValue(double value)
        {
            Parent.SetProgressValue(value);
        }
    }
}
