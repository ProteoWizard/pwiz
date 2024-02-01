using System;
using System.Threading;

namespace pwiz.Common.SystemUtil.Caching
{
    public class ProductionMonitor
    {
        public ProductionMonitor(CancellationToken cancellationToken)
        {
            CancellationToken = cancellationToken;
        }
        public CancellationToken CancellationToken { get; }
        public event Action<int> ProgressChange;

        public void SetProgress(int progress)
        {
            ProgressChange?.Invoke(progress);
        }
    }
}
