/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.GroupComparison;

namespace pwiz.Skyline.Model.GroupComparison
{
    public class GroupComparisonSelector
    {
        public GroupComparisonSelector(PeptideGroupDocNode protein, PeptideDocNode peptide, IsotopeLabelType labelType, 
            int? msLevel, GroupIdentifier groupIdentifier)
        {
            Protein = protein;
            Peptide = peptide;
            LabelType = labelType;
            MsLevel = msLevel;
            GroupIdentifier = groupIdentifier;
        }
        public PeptideGroupDocNode Protein { get; private set; }
        public PeptideDocNode Peptide { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public int? MsLevel { get; private set; }
        public GroupIdentifier GroupIdentifier { get; private set; }

        public IEnumerable<PeptideDocNode> ListPeptides()
        {
            if (null == Peptide)
            {
                return Protein.Peptides;
            }
            return new[] {Peptide};
        }

        public bool IncludePrecursor(TransitionGroupDocNode transitionGroupDocNode)
        {
            if (null != LabelType)
            {
                return LabelType.Equals(transitionGroupDocNode.TransitionGroup.LabelType);
            }
            return true;
        }

        public bool IncludeTransition(TransitionDocNode transitionDocNode)
        {
            if (MsLevel.HasValue)
            {
                if (MsLevel == 1)
                {
                    return transitionDocNode.IsMs1;
                }
                return !transitionDocNode.IsMs1;
            }
            return true;
        }

        protected bool Equals(GroupComparisonSelector other)
        {
            return Equals(Peptide, other.Peptide) 
                && Equals(Protein, other.Protein) 
                && Equals(LabelType, other.LabelType) 
                && MsLevel == other.MsLevel;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((GroupComparisonSelector) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Peptide != null ? Peptide.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Protein != null ? Protein.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (LabelType != null ? LabelType.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ MsLevel.GetHashCode();
                return hashCode;
            }
        }
    }
}
