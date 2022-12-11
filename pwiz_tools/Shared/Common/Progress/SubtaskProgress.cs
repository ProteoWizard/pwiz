using System;

namespace pwiz.Common.Progress
{
    public class SubtaskProgress : IProgress
    {
        private readonly IProgress _parent;
        public SubtaskProgress(IProgress parent, string messageTemplate, double minProgress,
            double maxProgress)
        {
            _parent = parent;
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

        public double Value
        {
            set
            {
                if (!double.IsNaN(value))
                {
                    _parent.Value = Math.Max(0, Math.Min(100, MinProgress + (MaxProgress - MinProgress) * value / 100));
                }
            }
            
        }

        public string Message
        {
            set
            {
                if (MessageTemplate == null)
                {
                    _parent.Message = value;
                }
                else
                {
                    _parent.Message = string.Format(MessageTemplate, value);
                }
            }
        }

        public bool IsCanceled
        {
            get { return _parent.IsCanceled; }
        }
    }
}
