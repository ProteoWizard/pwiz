using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.V01;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIon : Immutable, IComparable<ComplexFragmentIon>
    {
        public ComplexFragmentIon(IEnumerable<IonFragment?> parts, TransitionLosses losses)
        {
            Parts = ImmutableList.ValueOf(parts);
            Losses = losses;
        }

        public static ComplexFragmentIon Simple(TransitionDocNode transitionDocNode)
        {
            return new ComplexFragmentIon(ImmutableList.Singleton(IonFragment.FromTransition(transitionDocNode.Transition)), transitionDocNode.Losses);
        }

        private ComplexFragmentIon Append(SimpleFragmentIon child)
        {
            var newLosses = TransitionLosses;
            if (child?.Losses != null)
            {
                if (newLosses == null)
                {
                    newLosses = child.Losses;
                }
                else
                {
                    newLosses = new TransitionLosses(newLosses.Losses.Concat(child.Losses.Losses).ToList(),
                        newLosses.MassType);
                }
            }
            return new ComplexFragmentIon(Parts.Append(child?.Id), newLosses);
        }

        public static ComplexFragmentIon Append(ComplexFragmentIon left, SimpleFragmentIon right)
        {
            if (left == null)
            {
                return new ComplexFragmentIon(ImmutableList<IonFragment?>.Singleton(right?.Id), right?.Losses);
            }

            return left.Append(right);
        }

        public Adduct Adduct { get; private set; }

        public ImmutableList<IonFragment?> Parts { get; private set; }

        public int PartCount
        {
            get { return Parts.Count; }
        }

        public TransitionLosses Losses { get; private set; }

        public TransitionLosses TransitionLosses
        {
            get { return Losses; }
        }

        public bool? IncludesSite(PeptideStructure peptideStructure, CrosslinkSite site)
        {
            if (site.PeptideIndex >= PartCount)
            {
                return null;
            }

            return Parts[site.PeptideIndex]?
                       .IncludesAaIndex(peptideStructure.Peptides[site.PeptideIndex], site.AaIndex)
                   ?? false;
        }

        public static ComplexFragmentIon Simple(Transition transition, TransitionLosses losses)
        {
            return new ComplexFragmentIon(ImmutableList.Singleton(IonFragment.FromTransition(transition)), losses);
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
                return Parts.All(part => part?.IonType == IonType.precursor);
            }
        }

        public bool IsEmpty
        {
            get
            {
                return Parts.All(part => part == null);
            }
        }

        public bool IsOrphan
        {
            get
            {
                return Parts.Count > 0 && Parts[0] == null;
            }
        }

        public bool IsCrosslinked
        {
            get { return Parts.Count > 1; }
        }

        public ComplexFragmentIonKey GetName()
        {
            return new ComplexFragmentIonKey(Parts);
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
                var aminoAcid = Parts.First()?.GetAminoAcid(peptideStructure.Peptides[0].Sequence);
                if (aminoAcid.HasValue)
                {
                    stringBuilder.Append(aminoAcid);
                    stringBuilder.Append(@" ");
                }
            }

            string strHyphen = string.Empty;
            stringBuilder.Append(@"[");
            foreach (var part in Parts)
            {
                stringBuilder.Append(strHyphen);
                strHyphen = @"-";
                if (part == null)
                {
                    stringBuilder.Append(@"*");
                }
                else if (part.Value.IonType == IonType.precursor)
                {
                    stringBuilder.Append(@"p");
                }
                else
                {
                    stringBuilder.Append(part.Value.IonType);
                    stringBuilder.Append(part.Value.Ordinal);
                }
            }
            stringBuilder.Append(GetTransitionLossesText());

            stringBuilder.Append(@"]");
            if (includeResidues && Parts.Count > 1)
            {
                var lastAminoAcid = Parts[Parts.Count - 1]
                    ?.GetAminoAcid(peptideStructure.Peptides[peptideStructure.Peptides.Count - 1].Sequence);
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
            if (TransitionLosses == null)
            {
                return string.Empty;
            }

            return @" -" + Math.Round(TransitionLosses.Mass, 1);
        }

        public int CountFragmentationEvents()
        {
            int result = 0;
            if (Losses != null)
            {
                result += Losses.Losses.Count;
            }

            foreach (var part in Parts)
            {
                switch (part?.IonType)
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

        public int CompareTo(ComplexFragmentIon other)
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

            for (int i = 0; i < Math.Min(Parts.Count, other.Parts.Count); i++)
            {
                int result = Parts[i].HasValue.CompareTo(other.Parts[i].HasValue);
                if (result == 0)
                {
                    result = Parts[i]?.CompareTo(other.Parts[i].Value) ?? 0;
                }

                if (result == 0 && i == 0)
                {
                    result = Comparer<double?>.Default.Compare(TransitionLosses?.Mass, other.TransitionLosses?.Mass);
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return Parts.Count.CompareTo(other.Parts.Count);
        }

        public Transition MakeTransition(TransitionGroup group, Adduct adduct)
        {
            var firstPart = Parts[0];
            IonType ionType = firstPart?.IonType ?? IonType.precursor;
            int cleavageOffset;
            if (ionType == IonType.precursor)
            {
                cleavageOffset = group.Peptide.Sequence.Length - 1;
            }
            else
            {
                cleavageOffset =
                    Transition.OrdinalToOffset(ionType, firstPart.Value.Ordinal, group.Peptide.Sequence.Length);
            }
            return new Transition(group, ionType, cleavageOffset, 0, adduct, null);
        }

        public ChargedIon MakeChargedIon(TransitionGroup group, Adduct adduct, ExplicitMods explicitMods)
        {
            return new ChargedIon(MakeTransition(group, adduct), this, explicitMods);
        }
    }
}
