/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2021 University of Washington - Seattle, WA
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
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// A fragment from a single peptide in a crosslinked peptide.
    /// The SimpleFragmentIon's from all of the single peptides in a crosslinked peptide are permuted together to
    /// create <see cref="NeutralFragmentIon"/> objects.
    /// </summary>
    public class SingleFragmentIon : Immutable
    {
        public static readonly SingleFragmentIon EMPTY = new SingleFragmentIon(IonOrdinal.Empty, null);

        public SingleFragmentIon(IonOrdinal ion, TransitionLosses losses)
        {
            Id = ion;
            Losses = losses;
        }

        public static SingleFragmentIon FromDocNode(TransitionDocNode docNode)
        {
            if (docNode == null)
            {
                return null;
            }
            return new SingleFragmentIon(IonOrdinal.FromTransition(docNode.Transition), docNode.Losses);
        }

        public static SingleFragmentIon FromTransition(Transition transition)
        {
            return new SingleFragmentIon(IonOrdinal.FromTransition(transition), null);
        }

        public NeutralFragmentIon Prepend(NeutralFragmentIon left)
        {
            if (left == null)
            {
                return new NeutralFragmentIon(ImmutableList.Singleton(Id), Losses);
            }

            var newLosses = left.Losses;
            if (Losses != null)
            {
                if (newLosses == null)
                {
                    newLosses = Losses;
                }
                else
                {
                    newLosses = new TransitionLosses(newLosses.Losses.Concat(Losses.Losses).ToList(),
                        newLosses.MassType);
                }
            }
            return new NeutralFragmentIon(left.IonChain.Append(Id), newLosses);
        }

        public IonOrdinal Id { get; private set; }

        public IonType? IonType
        {
            get { return Id.Type; }
        }

        public int Ordinal
        {
            get { return Id.Ordinal; }
        }

        public TransitionLosses Losses { get; private set; }

        protected bool Equals(SingleFragmentIon other)
        {
            return Equals(Id, other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SingleFragmentIon) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = IonType.GetHashCode();
                hashCode = (hashCode * 397) ^ Ordinal;
                hashCode = (hashCode * 397) ^ (Losses != null ? Losses.GetHashCode() : 0);
                return hashCode;
            }
        }
    }
}
