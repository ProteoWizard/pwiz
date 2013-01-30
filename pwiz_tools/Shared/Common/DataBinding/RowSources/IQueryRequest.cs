using System.Threading;
using System.Threading.Tasks;

namespace pwiz.Common.DataBinding.RowSources
{
    public interface IQueryRequest
    {
        TaskScheduler EventTaskScheduler { get; }
        CancellationToken CancellationToken { get; }
        QueryParameters QueryParameters { get; }
        QueryResults InitialQueryResults { get; }
        void SetFinalQueryResults(QueryResults newResults);
    }
}
