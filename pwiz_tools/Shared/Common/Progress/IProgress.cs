using System.Threading;

namespace pwiz.Common.Progress
{

    public interface IProgress
    {
        bool IsCanceled { get; }
        double Value { set; }
        string Message { set; }
    }

    public interface IProgressEx : IProgress
    {
        CancellationToken CancellationToken { get; }
    }
}
