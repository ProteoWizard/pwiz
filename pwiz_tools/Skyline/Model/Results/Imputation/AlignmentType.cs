using System.Collections.Generic;
using pwiz.Common.SystemUtil.Caching;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class AlignmentType
    {
        delegate ConsensusAlignmentResults PerformAlignmentImpl(ProductionMonitor productionMonitor,
            IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries);

        private readonly PerformAlignmentImpl _impl;

        private AlignmentType(PerformAlignmentImpl impl)
        {
            _impl = impl;
        }
        public ConsensusAlignmentResults PerformAlignment(ProductionMonitor productionMonitor,
            IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries)
        {
            return _impl(productionMonitor, fileTimesDictionaries);
        }

        public static readonly AlignmentType KDE = new AlignmentType(KdeAlignmentType.PerformAlignment);
        public static readonly AlignmentType CONSENSUS = new AlignmentType(ConsensusAlignment.PerformAlignment);
    }
}
