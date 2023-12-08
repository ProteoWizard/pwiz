/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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

using System.Runtime.CompilerServices;

namespace pwiz.Skyline.Model
{
    /// <summary>
    /// A PeptideDocNode combined with the ID of the Protein that it belongs to.
    /// </summary>
    public class IdPeptideDocNode
    {
        public IdPeptideDocNode(PeptideGroup peptideGroup, PeptideDocNode peptideDocNode)
        {
            PeptideGroup = peptideGroup;
            PeptideDocNode = peptideDocNode;
        }

        public PeptideGroup PeptideGroup { get; }
        public PeptideDocNode PeptideDocNode { get; }

        protected bool Equals(IdPeptideDocNode other)
        {
            return ReferenceEquals(PeptideGroup, other.PeptideGroup) && ReferenceEquals(PeptideDocNode, other.PeptideDocNode);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((IdPeptideDocNode)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (RuntimeHelpers.GetHashCode(PeptideGroup) * 397) ^ RuntimeHelpers.GetHashCode(PeptideDocNode);
            }
        }

        public IdentityPath IdentityPath
        {
            get { return new IdentityPath(PeptideGroup, PeptideDocNode.Peptide); }
        }
    }
}
