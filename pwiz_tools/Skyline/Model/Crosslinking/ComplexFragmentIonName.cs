/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Serialization;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Represents the parts of a <see cref="LegacyComplexFragmentIon"/> separated from the actual Transition and TransitionGroup objects.
    /// </summary>
    public class LegacyComplexFragmentIonName : Immutable
    {
        public static readonly LegacyComplexFragmentIonName ORPHAN = new LegacyComplexFragmentIonName()
        {
            IonType = IonType.custom,
        };

        public static readonly LegacyComplexFragmentIonName PRECURSOR
            = new LegacyComplexFragmentIonName(IonType.precursor, 0);

        public LegacyComplexFragmentIonName(IonType ionType, int ordinal) : this()
        {
            IonType = ionType;
            if (IonType != IonType.precursor)
            {
                Ordinal = ordinal;
            }
        }

        private LegacyComplexFragmentIonName()
        {
            Children = ImmutableList<Tuple<ModificationSite, LegacyComplexFragmentIonName>>.EMPTY;
        }

        public IonType IonType { get; private set; }
        public int Ordinal { get; private set; }
        public ImmutableList<Tuple<ModificationSite, LegacyComplexFragmentIonName>> Children { get; private set; }
        public bool IsOrphan
        {
            get { return IonType == IonType.custom; }
        }

        private static ImmutableList<Tuple<ModificationSite, LegacyComplexFragmentIonName>> ToChildList(
            IEnumerable<Tuple<ModificationSite, LegacyComplexFragmentIonName>> children)
        {
            return ImmutableList.ValueOf(children.OrderBy(tuple => tuple.Item1));
        }

        public LegacyComplexFragmentIonName AddChild(ModificationSite modificationSite, LegacyComplexFragmentIonName child)
        {
            if (IsOrphan)
            {
                if (Children.Count > 0)
                {
                    throw new InvalidOperationException();
                }
            }

            return ChangeProp(ImClone(this),
                im => { im.Children = ToChildList(Children.Append(Tuple.Create(modificationSite, child))); });
        }

        protected bool Equals(LegacyComplexFragmentIonName other)
        {
            return IonType == other.IonType && Ordinal == other.Ordinal && Children.Equals(other.Children) && IsOrphan == other.IsOrphan;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((LegacyComplexFragmentIonName) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) IonType;
                hashCode = (hashCode * 397) ^ Ordinal;
                hashCode = (hashCode * 397) ^ Children.GetHashCode();
                hashCode = (hashCode * 397) ^ IsOrphan.GetHashCode();
                return hashCode;
            }
        }

        public override string ToString()
        {
            if (IsOrphan && Children.Count == 0)
            {
                return @"-";
            }

            StringBuilder stringBuilder = new StringBuilder();
            if (!IsOrphan)
            {
                if (IonType == IonType.precursor)
                {
                    stringBuilder.Append(@"p");
                }
                else
                {
                    stringBuilder.Append(IonType);
                    stringBuilder.Append(Ordinal);
                }
            }

            if (Children.Count == 1 && Children[0].Item1 == null)
            {
                stringBuilder.Append(@"-");
                stringBuilder.Append(Children[0].Item2);
            }
            else if (Children.Count != 0)
            {
                stringBuilder.Append(@"-");
                if (Children.Count != 1)
                {
                    stringBuilder.Append(@"[");
                }

                stringBuilder.Append(string.Join(@",", Children.Select(ChildToString)));
                if (Children.Count != 1)
                {
                    stringBuilder.Append(@"]");
                }
            }

            return stringBuilder.ToString();
        }

        private string ChildToString(Tuple<ModificationSite, LegacyComplexFragmentIonName> child)
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append(@"{");
            if (child.Item1 != null)
            {
                stringBuilder.Append(child.Item1);
                stringBuilder.Append(@":");
            }

            stringBuilder.Append(child.Item2);
            stringBuilder.Append(@"}");
            return stringBuilder.ToString();
        }

        public IEnumerable<SkylineDocumentProto.Types.LinkedIon> GetLinkedIonProtos()
        {
            foreach (var child in Children)
            {
                var proto = new SkylineDocumentProto.Types.LinkedIon()
                {
                    ModificationIndex = child.Item1.IndexAa,
                    ModificationName = child.Item1.ModName
                };

                if (child.Item2.IsOrphan)
                {
                    proto.Orphan = true;
                }
                else
                {
                    proto.IonType = DataValues.ToIonType(child.Item2.IonType);
                    proto.Ordinal = child.Item2.Ordinal;
                }
                proto.Children.AddRange(child.Item2.GetLinkedIonProtos());
                yield return proto;
            }
        }

        public static LegacyComplexFragmentIonName FromLinkedIonProto(SkylineDocumentProto.Types.LinkedIon linkedIon)
        {
            LegacyComplexFragmentIonName child;
            if (linkedIon.Orphan)
            {
                child = ORPHAN;
            }
            else
            {
                child = new LegacyComplexFragmentIonName(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal);
            }

            child = child.AddLinkedIonProtos(linkedIon.Children);
            return child;
        }

        public LegacyComplexFragmentIonName AddLinkedIonProtos(IEnumerable<SkylineDocumentProto.Types.LinkedIon> linkedIons)
        {
            var result = this;
            foreach (var linkedIon in linkedIons)
            {
                LegacyComplexFragmentIonName child;
                if (linkedIon.Orphan)
                {
                    child = ORPHAN;
                }
                else
                {
                    child = new LegacyComplexFragmentIonName(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal);
                }

                child = child.AddLinkedIonProtos(linkedIon.Children);
                result = result.AddChild(new ModificationSite(linkedIon.ModificationIndex, linkedIon.ModificationName),
                    child);
            }

            return result;
        }


        public IEnumerable<IonType> EnumerateIonTypes()
        {
            return Children.SelectMany(child => child.Item2.EnumerateIonTypes()).Prepend(IonType);
        }
    }
}
