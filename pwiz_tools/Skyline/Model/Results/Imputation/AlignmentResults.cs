using System.Collections.Generic;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class AlignmentResults : Immutable, GraphValues.IRetentionTimeTransformOp
    {
        private Dictionary<ReplicateFileId, AlignmentFunction> _alignmentFunctions;
        private Dictionary<ReferenceValue<ChromFileInfoId>, ReplicateFileId> _replicateFileIds;
        public AlignmentResults(Dictionary<ReplicateFileId, AlignmentFunction> alignmentFunctions)
        {
            _alignmentFunctions = alignmentFunctions;
            _replicateFileIds = new Dictionary<ReferenceValue<ChromFileInfoId>, ReplicateFileId>();
            foreach (var entry in _alignmentFunctions)
            {
                _replicateFileIds[entry.Key.FileId] = entry.Key;
            }
        }

        public string Name { get; private set; }

        public AlignmentResults ChangeName(string name)
        {
            return ChangeProp(ImClone(this), im => im.Name = name);
        }
        public string GetAxisTitle(RTPeptideValue rtPeptideValue)
        {
            return string.Format(GraphsResources.RtAlignment_AxisTitleAlignedTo,
                GraphValues.ToLocalizedString(rtPeptideValue), Name);
        }

        public bool TryGetRegressionFunction(ChromFileInfoId chromFileInfoId, out AlignmentFunction regressionFunction)
        {
            if (_replicateFileIds.TryGetValue(chromFileInfoId, out var replicateFileId))
            {
                regressionFunction = GetAlignment(replicateFileId);
                return true;
            }

            regressionFunction = null;
            return false;
        }

        public AlignmentFunction GetAlignment(ReplicateFileId replicateFileId)
        {
            _alignmentFunctions.TryGetValue(replicateFileId, out var alignmentFunction);
            return alignmentFunction;
        }
    }
}