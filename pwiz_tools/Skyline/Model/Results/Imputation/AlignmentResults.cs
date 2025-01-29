/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.RetentionTimes;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class AlignmentResults : Immutable
    {
        private Dictionary<ReplicateFileId, AlignmentFunction> _alignmentFunctions;
        private Dictionary<ReferenceValue<ChromFileInfoId>, ReplicateFileId> _replicateFileIds;
        private ConsensusAlignmentResults _consensusAlignmentResults;
        public AlignmentResults(ConsensusAlignmentResults consensusAlignmentResults)
        {
            _alignmentFunctions = consensusAlignmentResults.AlignmentFunctions.ToDictionary(kvp=>kvp.Key, kvp=>kvp.Value);
            _replicateFileIds = new Dictionary<ReferenceValue<ChromFileInfoId>, ReplicateFileId>();
            _consensusAlignmentResults = consensusAlignmentResults;
            foreach (var entry in _alignmentFunctions)
            {
                _replicateFileIds[entry.Key.FileId] = entry.Key;
            }
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

        public ImmutableList<KeyValuePair<Target, double>> StandardTimes
        {
            get { return _consensusAlignmentResults.StandardTimes; }
        }
    }
}