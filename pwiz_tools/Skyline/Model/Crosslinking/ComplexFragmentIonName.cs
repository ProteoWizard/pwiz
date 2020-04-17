using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIonName : Immutable
    {
        public static readonly ComplexFragmentIonName ORPHAN = new ComplexFragmentIonName()
        {
            IonType = IonType.precursor,
            IsOrphan = true,
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
            Losses = ImmutableList<Tuple<ModificationSite, int>>.EMPTY;
            Children = ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>>.EMPTY;
        }

        public IonType IonType { get; private set; }
        public int Ordinal { get; private set; }
        public ImmutableList<Tuple<ModificationSite, int>> Losses { get; private set; }
        public ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>> Children { get; private set; }
        public bool IsOrphan { get; private set; }

        private static ImmutableList<Tuple<ModificationSite, ComplexFragmentIonName>> ToChildList(
            IEnumerable<Tuple<ModificationSite, ComplexFragmentIonName>> children)
        {
            return ImmutableList.ValueOf(children.OrderBy(tuple=>tuple.Item1));
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

        public ComplexFragmentIonName AddLoss(ModificationSite modificationSite, int lossIndex)
        {
            if (IsOrphan)
            {
                throw new InvalidOperationException();
            }

            return ChangeProp(ImClone(this), im =>
            {
                im.Losses = ImmutableList.ValueOf(im.Losses.Append(Tuple.Create(modificationSite, lossIndex)).OrderBy(tuple=>tuple));
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
                stringBuilder.Append(IonType);
                if (IonType != IonType.precursor)
                {
                    stringBuilder.Append(Ordinal);
                }
            }

            foreach (var loss in Losses)
            {
                stringBuilder.Append($@"({loss.Item1}[{loss.Item2}])");
            }

            if (Children.Count != 0)
            {
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

        public ComplexFragmentIon Resolve(TransitionGroup transitionGroup, ExplicitMods explicitMods)
        {
            ComplexFragmentIon fragmentIon;
            if (IsOrphan)
            {
                fragmentIon = ComplexFragmentIon.NewOrphanFragmentIon(transitionGroup, explicitMods);
            }
            else
            {
                int offset;
                if (IonType == IonType.precursor)
                {
                    offset = transitionGroup.Peptide.Length - 1;
                }
                else
                {
                    offset = Transition.OrdinalToOffset(IonType, Ordinal, transitionGroup.Peptide.Length);
                }
                var transition = new Transition(transitionGroup, IonType, offset, 0, Adduct.SINGLY_PROTONATED);
                fragmentIon = new ComplexFragmentIon(transition, null);
            }

            var crosslinks = explicitMods.Crosslinks.ToDictionary(mod=>mod.ModificationSite);
            // TODO: losses
            foreach (var child in Children)
            {
                ExplicitMod crosslinkMod;
                if (child.Item1 == null)
                {
                    if (crosslinks.Count != 1)
                    {
                        throw new ArgumentException(@"Must have only one crosslink modification");
                    }

                    crosslinkMod = crosslinks.Values.First();
                }
                else
                {
                    if (!crosslinks.TryGetValue(child.Item1, out crosslinkMod))
                    {
                        throw new ArgumentException(string.Format(@"No such crosslink {0}", child.Item1));
                    }
                }

                var childTransitionGroup = new TransitionGroup(crosslinkMod.LinkedPeptide.Peptide,
                    transitionGroup.PrecursorAdduct, transitionGroup.LabelType);
                var resolvedChild = child.Item2.Resolve(childTransitionGroup, crosslinkMod.LinkedPeptide.ExplicitMods);
                fragmentIon = fragmentIon.AddChild(child.Item1, resolvedChild);
            }

            return fragmentIon;
        }
    }
}
