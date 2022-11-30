using System.Threading;

namespace pwiz.Common.ProgressReporting
{
    public interface IProgressReporter
    {
        CancellationToken CancellationToken { get; }
        bool IsCanceled { get; }
        double ProgressValue { set; }
        string ProgressMessage { set; }
        void SetProgressValue(double value);
        void SetProgressMessage(string message);
    }
}
