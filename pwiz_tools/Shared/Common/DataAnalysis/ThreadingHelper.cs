using System;
using System.Threading;

namespace pwiz.Common.DataAnalysis
{
    public class ThreadingHelper
    {
        public static void CheckCanceled(CancellationToken token)
        {
            if (token.IsCancellationRequested)
                throw new OperationCanceledException();
        }
    }
}
