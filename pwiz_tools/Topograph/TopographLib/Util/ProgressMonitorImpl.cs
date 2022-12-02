using System;
using System.Threading;
using pwiz.Common.Progress;

namespace pwiz.Topograph.Util
{
    public class ProgressMonitorImpl : AbstractProgress
    {
        private Action<int> _updateProgressImpl;
        public ProgressMonitorImpl(CancellationToken cancellationToken, Action<int> updateProgressImpl) : base(cancellationToken)
        {
            _updateProgressImpl = updateProgressImpl;
        }

        public override double Value
        {
            set => _updateProgressImpl(Math.Max(0, Math.Min(100, (int) value)));
        }

        public override string Message
        {
            set => throw new NotImplementedException();
        }

        public static ProgressMonitorImpl NewProgressMonitorImpl(CancellationToken cancellationToken, Action<int> updateProgress)
        {
            return new ProgressMonitorImpl(cancellationToken, updateProgress);
        }
    }
}
