using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Serialization;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIonName : Immutable
    {
        public static readonly ComplexFragmentIonName ORPHAN = new ComplexFragmentIonName()
        {
            IonType = IonType.custom,
        };

        public static readonly ComplexFragmentIonName PRECURSOR
            = new ComplexFragmentIonName(IonType.precursor, 0);

        public ComplexFragmentIonName(IonType ionType, int ordinal) : this()
        {
            IonType = ionType;
            if (IonType != IonType.precursor)
            {
                Ordinal = ordinal;
            }
        }

        private ComplexFragmentIonName()
        {
            Losses = ImmutableList<Tuple<ModificationSite, string>>.EMPTY;
            Children = ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>>.EMPTY;
        }

        public IonType IonType { get; private set; }
        public int Ordinal { get; private set; }
        public ImmutableList<Tuple<ModificationSite, string>> Losses { get; private set; }
        public ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>> Children { get; private set; }
        public bool IsOrphan
        {
            get { return IonType == IonType.custom; }
        }

        private static ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>> ToChildList(
            IEnumerable<Tuple<ModificationSite, ComplexFragmentIonName>> children)
        {
            return ImmutableList.ValueOf(children.OrderBy(tuple => tuple.Item1));
        }

        public ComplexFragmentIonName AddChild(ModificationSite modificationSite, ComplexFragmentIonName child)
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

        public ComplexFragmentIonName AddLoss(ModificationSite modificationSite, string loss)
        {
            if (IsOrphan)
            {
                throw new InvalidOperationException();
            }

            return ChangeProp(ImClone(this),
                im =>
                {
                    im.Losses = ImmutableList.ValueOf(im.Losses.Append(Tuple.Create(modificationSite, loss))
                        .OrderBy(tuple => tuple));
                });
        }

        protected bool Equals(ComplexFragmentIonName other)
        {
            return IonType == other.IonType && Ordinal == other.Ordinal && Losses.Equals(other.Losses) &&
                   Children.Equals(other.Children) && IsOrphan == other.IsOrphan;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ComplexFragmentIonName) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (int) IonType;
                hashCode = (hashCode * 397) ^ Ordinal;
                hashCode = (hashCode * 397) ^ Losses.GetHashCode();
                hashCode = (hashCode * 397) ^ Children.GetHashCode();
                hashCode = (hashCode * 397) ^ IsOrphan.GetHashCode();
                return hashCode;
            }
        }

        public ComplexFragmentIonName DisqualifyChildren()
        {
            return ChangeProp(ImClone(this),
                im =>
                {
                    im.Children = ImmutableList.ValueOf(im.Children.Select(child =>
                        new Tuple<ModificationSite, ComplexFragmentIonName>(null, child.Item2)));
                });
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

            foreach (var loss in Losses)
            {
                stringBuilder.Append($@"({loss.Item1}[{loss.Item2}])");
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

        private string ChildToString(Tuple<ModificationSite, ComplexFragmentIonName> child)
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

        public static ComplexFragmentIonName FromLinkedIonProto(SkylineDocumentProto.Types.LinkedIon linkedIon)
        {
            ComplexFragmentIonName child;
            if (linkedIon.Orphan)
            {
                child = ORPHAN;
            }
            else
            {
                child = new ComplexFragmentIonName(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal);
            }

            child = child.AddLinkedIonProtos(linkedIon.Children);
            return child;
        }

        public ComplexFragmentIonName AddLinkedIonProtos(IEnumerable<SkylineDocumentProto.Types.LinkedIon> linkedIons)
        {
            var result = this;
            foreach (var linkedIon in linkedIons)
            {
                ComplexFragmentIonName child;
                if (linkedIon.Orphan)
                {
                    child = ORPHAN;
                }
                else
                {
                    child = new ComplexFragmentIonName(DataValues.FromIonType(linkedIon.IonType), linkedIon.Ordinal);
                }

                child = child.AddLinkedIonProtos(linkedIon.Children);
                result = result.AddChild(new ModificationSite(linkedIon.ModificationIndex, linkedIon.ModificationName),
                    child);
            }

            return result;
        }
    }
}
