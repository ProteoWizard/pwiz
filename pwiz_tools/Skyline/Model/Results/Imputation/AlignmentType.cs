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
using System;
using System.Collections.Generic;
using pwiz.Common.SystemUtil.Caching;

namespace pwiz.Skyline.Model.Results.Imputation
{
    public class AlignmentType  
    {
        delegate ConsensusAlignmentResults PerformAlignmentImpl(ProductionMonitor productionMonitor,
            IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries);

        private readonly PerformAlignmentImpl _impl;
        private readonly Func<string> _getLabelFunc;

        private AlignmentType(string name, PerformAlignmentImpl impl, Func<string> getLabelFunc)
        {
            Name = name;
            _impl = impl;
            _getLabelFunc = getLabelFunc;
        }

        public string Name { get; }
        public ConsensusAlignmentResults PerformAlignment(ProductionMonitor productionMonitor,
            IDictionary<ReplicateFileId, Dictionary<Target, double>> fileTimesDictionaries)
        {
            return _impl(productionMonitor, fileTimesDictionaries);
        }

        public override string ToString()
        {
            return _getLabelFunc();
        }

        public static readonly AlignmentType KDE = new AlignmentType(@"kde", KdeAlignmentType.PerformAlignment, ()=> ImputationResources.AlignmentType_KDE_KDE_Aligner);
        public static readonly AlignmentType CONSENSUS = new AlignmentType(@"consensus", ConsensusAlignment.PerformAlignment, ()=>ImputationResources.AlignmentType_CONSENSUS_Consensus__experimental_);

        public static AlignmentType ForName(string name)
        {
            foreach (var type in new[] { KDE, CONSENSUS })
            {
                if (type.Name == name)
                {
                    return type;
                }
            }

            return null;
        }
    }
}
