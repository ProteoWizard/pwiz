using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class SimpleFragmentIon : Immutable
    {
        public static readonly SimpleFragmentIon EMPTY = new SimpleFragmentIon(FragmentIonType.Empty, null);
        public static readonly SimpleFragmentIon PRECURSOR = new SimpleFragmentIon(FragmentIonType.Precursor, null);

        public SimpleFragmentIon(FragmentIonType ion, TransitionLosses losses)
        {
            Id = ion;
            Losses = losses;
        }

        public static SimpleFragmentIon FromDocNode(TransitionDocNode docNode)
        {
            if (docNode == null)
            {
                return null;
            }
            return new SimpleFragmentIon(FragmentIonType.FromTransition(docNode.Transition), docNode.Losses);
        }

        public static SimpleFragmentIon FromTransition(Transition transition)
        {
            return new SimpleFragmentIon(FragmentIonType.FromTransition(transition), null);
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

        public FragmentIonType Id { get; private set; }

        public IonType? IonType
        {
            get { return Id.Type; }
        }

        public int Ordinal
        {
            get { return Id.Ordinal; }
        }

        public TransitionLosses Losses { get; private set; }

        public SimpleFragmentIon ChangeLosses(TransitionLosses losses)
        {
            if (ReferenceEquals(Losses, losses))
            {
                return this;
            }
            return ChangeProp(ImClone(this), im => im.Losses = losses);
        }

        protected bool Equals(SimpleFragmentIon other)
        {
            return Equals(Id, other.Id);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((SimpleFragmentIon) obj);
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
