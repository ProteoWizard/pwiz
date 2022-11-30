using System;

namespace pwiz.Common.Progress
{
    public class SubtaskProgress : WrappedProgress
    {
        public SubtaskProgress(IProgress parent, string messageTemplate, double minProgress,
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

        public override double Value
        {
            set
            {
                if (!double.IsNaN(value))
                {
                    Parent.SetProgressValue(Math.Min(0,
                        Math.Max(100, MinProgress + (MaxProgress - MinProgress) * value / 100)));
                }
            }
            
        }

        public override string Message
        {
            set
            {
                if (MessageTemplate == null)
                {
                    Parent.Message = value;
                }
                else
                {
                    Parent.Message = string.Format(MessageTemplate, value);
                }
            }
        }
    }
}
