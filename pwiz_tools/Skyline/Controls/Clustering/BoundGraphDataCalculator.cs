using System.Threading;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.Graphs;
using ZedGraph;

namespace pwiz.Skyline.Controls.Clustering
{
    public abstract class BoundGraphDataCalculator<TInput, TResults> : GraphDataCalculator<TInput, TResults> where TInput : DataSchemaInput
    {
        protected BoundGraphDataCalculator(CancellationToken parentCancellationToken, ZedGraphControl zedGraphControl) : base(parentCancellationToken, zedGraphControl)
        {
        }

        protected sealed override TResults CalculateResults(TInput input, CancellationToken cancellationToken)
        {
            using (input.QueryLock.GetReadLock())
            {
                var cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
                    cancellationToken, input.QueryLock.CancellationToken);
                {
                    return CalculateDataBoundResults(input, cancellationTokenSource.Token);
                }
            }
        }

        protected abstract TResults CalculateDataBoundResults(TInput input, CancellationToken cancellationToken);
    }

    public class DataSchemaInput
    {
        public DataSchemaInput(DataSchema dataSchema)
        {
            DataSchema = dataSchema;
        }

        public DataSchema DataSchema { get; }

        public QueryLock QueryLock
        {
            get { return DataSchema.QueryLock; }
        }

        public DataSchemaLocalizer DataSchemaLocalizer
        {
            get { return DataSchema.DataSchemaLocalizer; }
        }
    }
}
