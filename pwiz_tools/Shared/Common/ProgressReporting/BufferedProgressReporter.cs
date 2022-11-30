using System;

namespace pwiz.Common.ProgressReporting
{
    public class BufferedProgressReporter : WrappedProgressReporter
    {
        private double _lastProgressValue;

        public BufferedProgressReporter(IProgressReporter parent) : base(parent)
        {
        }

        public void UpdateIfProgress(double newProgress, string messageTemplate, params object[] args)
        {
            if (Math.Round(newProgress) > Math.Round(_lastProgressValue))
            {
                SetProgressValue(newProgress);
                SetProgressMessage(string.Format(messageTemplate, args));
                _lastProgressValue = newProgress;
            }
        }
    }
}
