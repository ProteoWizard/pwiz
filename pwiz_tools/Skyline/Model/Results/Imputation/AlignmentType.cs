using System;
using System.Collections.Generic;
using pwiz.Common.SystemUtil.Caching;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class AlignmentType  
    {
        delegate ConsensusAlignmentResults PerformAlignmentImpl(ProductionMonitor productionMonitor,
            IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries);

        private readonly PerformAlignmentImpl _impl;
        private readonly Func<string> _getLabelFunc;

        private AlignmentType(PerformAlignmentImpl impl, Func<string> getLabelFunc)
        {
            _impl = impl;
            _getLabelFunc = getLabelFunc;
        }
        public ConsensusAlignmentResults PerformAlignment(ProductionMonitor productionMonitor,
            IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries)
        {
            return _impl(productionMonitor, fileTimesDictionaries);
        }

        public override string ToString()
        {
            return _getLabelFunc();
        }

        public static readonly AlignmentType KDE = new AlignmentType(KdeAlignmentType.PerformAlignment, ()=>Resources.KdeAlignerFactory_ToString_KDE_Aligner);
        public static readonly AlignmentType CONSENSUS = new AlignmentType(ConsensusAlignment.PerformAlignment, ()=>"Consensus (experimental)");
    }
}
