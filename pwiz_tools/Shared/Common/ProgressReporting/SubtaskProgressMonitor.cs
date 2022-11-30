using System;

namespace pwiz.Common.ProgressReporting
{
    public class SubtaskProgressMonitor : WrappedProgressReporter
    {
        public SubtaskProgressMonitor(IProgressReporter parent, string messageTemplate, double minProgress,
            double maxProgress) : base(parent)
        {
            if (double.IsNaN(minProgress) || double.IsInfinity(minProgress))
            {
                throw new ArgumentOutOfRangeException(nameof(minProgress));
            }

            if (double.IsNaN(maxProgress) || double.IsInfinity(maxProgress) || maxProgress < minProgress)
            {
                throw new ArgumentOutOfRangeException(nameof(maxProgress));
            }
            MessageTemplate = messageTemplate;
            MinProgress = minProgress;
            MaxProgress = maxProgress;
        }

        public string MessageTemplate { get; }
        public double MinProgress { get; }
        public double MaxProgress { get; }

        public override void SetProgressValue(double value)
        {
            Parent.SetProgressValue(MinProgress + (MaxProgress - MinProgress) * value / 100);
        }

        public override void SetProgressMessage(string message)
        {
            Parent.SetProgressMessage(string.Format(MessageTemplate, message));
        }
    }
}
