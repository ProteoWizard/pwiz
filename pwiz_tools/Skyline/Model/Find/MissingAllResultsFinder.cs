/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using System.Linq;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds all peaks that were truncated.
    /// </summary>
    public class MissingAllResultsFinder : AbstractDocNodeFinder
    {
        public override string Name
        {
            get { return @"missing_all_results"; }
        }

        public override string DisplayName
        {
            get { return FindResources.MissingAllResultsFinder_DisplayName_Missing_all_results; }
        }

        // Missing everywhere is a total of no peaks at all, which the positions say directly. There
        // is no need to work out which replicates have one and then find that none of them does -
        // that is what MissingAnyResultsFinder needs, and this does not.
        protected override bool IsMatch(PeptideDocNode nodePep)
        {
            return nodePep != null && nodePep.HasResults && nodePep.TransitionGroups.All(g => g.HasNoPeaks);
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup)
        {
            return nodeGroup != null && nodeGroup.HasAbbreviatedResults && nodeGroup.HasNoPeaks;
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            var chromFileIds = nodeGroup?.AbbreviatedResults?
                .GetTransitionChromFileIds(nodeTran.Transition);
            return chromFileIds != null && chromFileIds.ReplicatePositions.TotalCount == 0;
        }
    }
}
