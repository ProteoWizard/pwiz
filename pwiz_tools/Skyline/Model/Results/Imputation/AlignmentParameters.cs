using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.Caching;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class AlignmentParameters : Immutable
    {
        public static readonly Producer<AlignmentParameters, AlignmentResults> ALIGNMENT_PRODUCER =
            new AlignmentProducer();
        public AlignmentParameters(SrmDocument document, RtValueType rtValueType, AlignmentType alignmentType)
        {
            Document = document;
            RtValueType = rtValueType;
            AlignmentType = alignmentType;
        }
        public SrmDocument Document { get; private set; }
        public RtValueType RtValueType { get; private set; }

        public AlignmentType AlignmentType { get; private set; }

        protected bool Equals(AlignmentParameters other)
        {
            return ReferenceEquals(Document, other.Document) && RtValueType.Equals(other.RtValueType) &&
                   AlignmentType.Equals(other.AlignmentType);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((AlignmentParameters)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = RuntimeHelpers.GetHashCode(Document);
                hashCode = (hashCode * 397) ^ RtValueType.GetHashCode();
                hashCode = (hashCode * 397) ^ AlignmentType.GetHashCode();
                return hashCode;
            }
        }

        public WorkOrder MakeWorkOrder()
        {
            return ALIGNMENT_PRODUCER.MakeWorkOrder(this);
        }

        private class AlignmentProducer : Producer<AlignmentParameters, AlignmentResults>
        {
            public override AlignmentResults ProduceResult(ProductionMonitor productionMonitor, AlignmentParameters parameter, IDictionary<WorkOrder, object> inputs)
            {
                return parameter.GetResults(productionMonitor);
            }

            public override string GetDescription(object workParameter)
            {
                return ImputationResources.AlignmentProducer_GetDescription_Retention_time_alignment;
            }
        }

        public AlignmentResults GetResults(ProductionMonitor productionMonitor)
        {
            var document = Document;
            var times = ReplicateFileId.List(document.MeasuredResults).ToDictionary(
                replicateFileId => replicateFileId,
                replicateFileId => RtValueType.GetRetentionTimes(document, replicateFileId));
            return new AlignmentResults(AlignmentType.PerformAlignment(productionMonitor, times));
        }
    }
}
