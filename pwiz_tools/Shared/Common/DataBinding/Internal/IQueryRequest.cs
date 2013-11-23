using System;
using System.Threading;
using System.Threading.Tasks;

namespace pwiz.Common.DataBinding.Internal
{
    internal interface IQueryRequest
    {
        TaskScheduler EventTaskScheduler { get; }
        CancellationToken CancellationToken { get; }
        QueryParameters QueryParameters { get; }
        QueryResults InitialQueryResults { get; }
        void SetFinalQueryResults(QueryResults newResults);
        void OnUnhandledException(Exception exception);
    }
}
