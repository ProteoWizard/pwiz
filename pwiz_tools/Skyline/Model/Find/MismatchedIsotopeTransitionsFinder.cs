/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Model.Find
{
    /// <summary>
    /// Finds tranisitions and precursors missing library data.
    /// </summary>
    public class MismatchedIsotopeTransitionsFinder : AbstractDocNodeFinder
    {
        public override string Name
        {
            get { return "mismatched_isotope_transitions"; } // Not L10N
        }

        public override string DisplayName
        {
            get { return Resources.MismatchedIsotopeTransitionsFinder_DisplayName_Mismatched_transitions; }
        }

        private Dictionary<int, TransitionMatchInfo> _dictChargeToMatchInfo;

        protected override bool IsMatch(PeptideDocNode nodePep)
        {
            // Populate look-ups for later to be able to determine if transitions
            // are fully matched or not.
            _dictChargeToMatchInfo = new Dictionary<int, TransitionMatchInfo>();
            foreach (var nodeGroup in nodePep.TransitionGroups)
            {
                TransitionMatchInfo matchInfo;
                int charge = nodeGroup.TransitionGroup.PrecursorCharge;
                if (!_dictChargeToMatchInfo.TryGetValue(charge, out matchInfo))
                {
                    matchInfo = new TransitionMatchInfo();
                    _dictChargeToMatchInfo.Add(charge, matchInfo);
                }
                matchInfo.AddTransitionGroup(nodeGroup);

                foreach (var nodeTran in nodeGroup.Transitions)
                {
                    matchInfo.AddTransition(nodeTran, nodeGroup);
                }
            }
            // Not actually looking for peptides
            return false;
        }

        protected override bool IsMatch(TransitionGroupDocNode nodeGroup, TransitionDocNode nodeTran)
        {
            TransitionMatchInfo matchInfo;
            if (!_dictChargeToMatchInfo.TryGetValue(nodeGroup.TransitionGroup.PrecursorCharge, out matchInfo))
                return true;    // Unexpected missing charge state
            return !matchInfo.IsFullyMatched(nodeTran, nodeGroup);
        }

        private class TransitionMatchInfo
        {
            private readonly HashSet<IsotopeLabelType> _labelTypeSet;
            private readonly Dictionary<TransitionLossEquivalentKey, int> _dictTransitionCount;

            public TransitionMatchInfo()
            {
                _labelTypeSet = new HashSet<IsotopeLabelType>();
                _dictTransitionCount = new Dictionary<TransitionLossEquivalentKey, int>();
            }

            public void AddTransitionGroup(TransitionGroupDocNode nodeGroup)
            {
                _labelTypeSet.Add(nodeGroup.TransitionGroup.LabelType);
            }

            /// <summary>
            /// In the case of small molecule transitions specified by mass only, position within 
            /// the parent's list of transitions is the only meaningful key.  So we need to know our parent.
            /// </summary>
            public void AddTransition(TransitionDocNode nodeTran, TransitionGroupDocNode parent)
            {
                var tranKey = nodeTran.EquivalentKey(parent);
                if (!_dictTransitionCount.ContainsKey(tranKey))
                    _dictTransitionCount.Add(tranKey, 0);
                _dictTransitionCount[tranKey]++;
            }

            public bool IsFullyMatched(TransitionDocNode nodeTran, TransitionGroupDocNode parent)
            {
                int count;
                if (!_dictTransitionCount.TryGetValue(nodeTran.EquivalentKey(parent), out count))
                    return true;
                return count == _labelTypeSet.Count;
            }
        }
    }
}