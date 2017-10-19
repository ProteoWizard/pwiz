using System;
using System.Threading;

namespace pwiz.Common.DataBinding.Internal
{
    internal interface IQueryRequest
    {
        QueryLock QueryLock { get; }
        CancellationToken CancellationToken { get; }
        QueryParameters QueryParameters { get; }
        void SetFinalQueryResults(QueryResults newResults);
        void OnUnhandledException(Exception exception);
    }
}
