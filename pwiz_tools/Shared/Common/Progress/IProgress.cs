using System.Threading;

namespace pwiz.Common.Progress
{
    public interface IProgress
    {
        CancellationToken CancellationToken { get; }
        bool IsCanceled { get; }
        double Value { set; }
        string Message { set; }
    }
}
