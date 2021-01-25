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
    public class NeutralFragmentIon : Immutable, IComparable<NeutralFragmentIon>
    {
        public NeutralFragmentIon(IEnumerable<IonOrdinal> parts, TransitionLosses losses)
        {
            IonChain = IonChain.FromIons(parts);
            Losses = losses;
        }

        public static NeutralFragmentIon Simple(TransitionDocNode transitionDocNode)
        {
            return new NeutralFragmentIon(ImmutableList.Singleton(IonOrdinal.FromTransition(transitionDocNode.Transition)), transitionDocNode.Losses);
        }

        public IonChain IonChain { get; private set; }

        public TransitionLosses Losses { get; private set; }

        public bool? IncludesSite(PeptideStructure peptideStructure, CrosslinkSite site)
        {
            if (site.PeptideIndex >= IonChain.Count)
            {
                return null;
            }

            return IonChain[site.PeptideIndex]
                .IncludesAaIndex(peptideStructure.Peptides[site.PeptideIndex], site.AaIndex);
        }

        public static NeutralFragmentIon Simple(Transition transition, TransitionLosses losses)
        {
            return new NeutralFragmentIon(ImmutableList.Singleton(IonOrdinal.FromTransition(transition)), losses);
        }

        public bool IsMs1
        {
            get
            {
                return IsIonTypePrecursor && Losses == null;
            }
        }

        public bool IsIonTypePrecursor
        {
            get
            {
                return IonChain.All(part => part.Type == IonType.precursor);
            }
        }

        public bool IsEmpty
        {
            get
            {
                return IonChain.All(part => part.IsEmpty);
            }
        }

        public bool IsOrphan
        {
            get
            {
                return IonChain.Count > 0 && IonChain[0].IsEmpty;
            }
        }

        public IonChain GetName()
        {
            return IonChain;
        }

        /// <summary>
        /// Returns the text that should be displayed for this in the Targets tree.
        /// </summary>
        public string GetLabel(PeptideStructure peptideStructure, bool includeResidues)
        {
            if (IsIonTypePrecursor)
            {
                return IonTypeExtension.GetLocalizedString(IonType.precursor) + GetTransitionLossesText();
            }

            StringBuilder stringBuilder = new StringBuilder();
            // Simple case of two peptides linked together
            if (includeResidues)
            {
                var aminoAcid = IonChain.First().GetAminoAcid(peptideStructure.Peptides[0].Sequence);
                if (aminoAcid.HasValue)
                {
                    stringBuilder.Append(aminoAcid);
                    stringBuilder.Append(@" ");
                }
            }

            stringBuilder.Append(@"[");
            stringBuilder.Append(IonChain);
            stringBuilder.Append(GetTransitionLossesText());
            stringBuilder.Append(@"]");
            if (includeResidues && IonChain.Count > 1)
            {
                var lastAminoAcid = IonChain[IonChain.Count - 1]
                    .GetAminoAcid(peptideStructure.Peptides[peptideStructure.Peptides.Count - 1].Sequence);
                if (lastAminoAcid.HasValue)
                {
                    stringBuilder.Append(@" ");
                    stringBuilder.Append(lastAminoAcid);
                }
            }
            return stringBuilder.ToString();
        }
        private string GetTransitionLossesText()
        {
            if (Losses == null)
            {
                return string.Empty;
            }

            return @" -" + Math.Round(Losses.Mass, 1);
        }

        public int CountFragmentationEvents()
        {
            int result = 0;
            if (Losses != null)
            {
                result += Losses.Losses.Count;
            }

            foreach (var part in IonChain)
            {
                switch (part.Type)
                {
                    case IonType.precursor:
                    case null:
                        break;
                    default:
                        result++;
                        break;
                }
            }

            return result;
        }

        public bool IsAllowed(PeptideStructure peptideStructure)
        {
            foreach (var crosslink in peptideStructure.Crosslinks)
            {
                if (!ContainsCrosslink(peptideStructure, crosslink.Sites).HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        public bool? ContainsCrosslink(PeptideStructure peptideStructure, IEnumerable<CrosslinkSite> crosslinkSites)
        {
            int countIncluded = 0;
            int countExcluded = 0;
            foreach (var site in crosslinkSites)
            {
                switch (IncludesSite(peptideStructure, site))
                {
                    case true:
                        countIncluded++;
                        break;
                    case false:
                        countExcluded++;
                        break;
                }
            }

            if (countIncluded == 0)
            {
                return false;
            }

            if (countExcluded == 0)
            {
                return true;
            }

            return null;
        }

        public int CompareTo(NeutralFragmentIon other)
        {
            if (IsIonTypePrecursor)
            {
                if (!other.IsIonTypePrecursor)
                {
                    return -1;
                }
            }
            else if (other.IsIonTypePrecursor)
            {
                return 1;
            }

            for (int i = 0; i < Math.Min(IonChain.Count, other.IonChain.Count); i++)
            {
                int result = IonChain[i].CompareTo(other.IonChain[i]);
                if (result == 0)
                {
                    result = Comparer<double?>.Default.Compare(Losses?.Mass, other.Losses?.Mass);
                }
                if (result != 0)
                {
                    return result;
                }
            }

            return IonChain.Count.CompareTo(other.IonChain.Count);
        }

        public Transition MakeTransition(TransitionGroup group, Adduct adduct)
        {
            var firstPart = IonChain[0];
            IonType ionType = firstPart.Type ?? IonType.precursor;
            int cleavageOffset;
            if (ionType == IonType.precursor)
            {
                cleavageOffset = group.Peptide.Sequence.Length - 1;
            }
            else
            {
                cleavageOffset = Transition.OrdinalToOffset(ionType, firstPart.Ordinal, group.Peptide.Sequence.Length);
            }
            return new Transition(group, ionType, cleavageOffset, 0, adduct, null);
        }

        public ComplexFragmentIon MakeChargedIon(TransitionGroup group, Adduct adduct, ExplicitMods explicitMods)
        {
            return new ComplexFragmentIon(MakeTransition(group, adduct), this, explicitMods);
        }
    }
}
