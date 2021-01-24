using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ComplexFragmentIon : Immutable, IComparable<ComplexFragmentIon>
    {
        public static readonly ComplexFragmentIon EMPTY = new ComplexFragmentIon(ImmutableList.Empty<Part>(), null);
        public ComplexFragmentIon(IEnumerable<Part> parts, TransitionLosses losses)
        {
            Parts = ImmutableList.ValueOf(parts);
            Losses = losses;
        }

        public static ComplexFragmentIon Simple(TransitionDocNode transitionDocNode)
        {
            return new ComplexFragmentIon(ImmutableList.Singleton(new Part(transitionDocNode.Transition, false)), transitionDocNode.Losses);
        }

        public ComplexFragmentIon Concat(ComplexFragmentIon child)
        {
            var newLosses = TransitionLosses;
            if (child.TransitionLosses != null)
            {
                if (newLosses == null)
                {
                    newLosses = child.TransitionLosses;
                }
                else
                {
                    newLosses = new TransitionLosses(newLosses.Losses.Concat(child.TransitionLosses.Losses).ToList(),
                        newLosses.MassType);
                }
            }
            return new ComplexFragmentIon(Parts.Concat(child.Parts), newLosses);
        }

        public ImmutableList<Part> Parts { get; private set; }
        public IEnumerable<Transition> Transitions
        {
            get { return Parts.Select(part => part.Transition); }
        }

        public int PartCount
        {
            get { return Parts.Count; }
        }

        public Transition PrimaryTransition
        {
            get { return Transitions.FirstOrDefault(); }
        }

        public TransitionLosses Losses { get; private set; }

        public TransitionLosses TransitionLosses
        {
            get { return Losses; }
        }

        public static ComplexFragmentIon EmptyComplexFragmentIon(TransitionGroup transitionGroup)
        {
            return new ComplexFragmentIon(ImmutableList.Singleton(EmptyPart(transitionGroup)), null);
        }

        public bool? IncludesSite(CrosslinkSite site)
        {
            if (site.PeptideIndex >= PartCount)
            {
                return null;
            }

            if (Parts[site.PeptideIndex].IsEmpty)
            {
                return false;
            }
            var transition = Parts[site.PeptideIndex].Transition;
            return transition.IncludesAaIndex(site.AaIndex);
        }

        public ComplexFragmentIon CloneTransition()
        {
            return ChangePrimaryTransition((Transition) Transitions.First().Copy());
        }

        public static ComplexFragmentIon Simple(Transition transition, TransitionLosses losses)
        {
            return new ComplexFragmentIon(ImmutableList.Singleton(new Part(transition, false)), losses);
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
                return Parts.All(part => !part.IsEmpty && part.Transition.IonType == IonType.precursor);
            }
        }

        public bool IsEmpty
        {
            get
            {
                return Parts.All(part => part.IsEmpty);
            }
        }

        public bool IsOrphan
        {
            get
            {
                return Parts.Count > 0 && Parts[0].IsEmpty;
            }
        }

        public bool IsCrosslinked
        {
            get { return Parts.Count > 1; }
        }

        public string GetFragmentIonName()
        {
            return GetLabel(false);
        }

        public string GetTargetsTreeLabel()
        {
            return GetLabel(true) + Transition.GetMassIndexText(PrimaryTransition.MassIndex);
        }

        public ComplexFragmentIonKey GetName()
        {
            return new ComplexFragmentIonKey(Transitions.Select(t=>t.IonType), Transitions.Select(t=>t.Ordinal));
        }


        /// <summary>
        /// Returns the text that should be displayed for this in the Targets tree.
        /// </summary>
        private string GetLabel(bool includeResidues)
        {
            if (IsIonTypePrecursor)
            {
                return IonTypeExtension.GetLocalizedString(IonType.precursor) + GetTransitionLossesText();
            }

            StringBuilder stringBuilder = new StringBuilder();
            // Simple case of two peptides linked together
            if (includeResidues)
            {
                var firstPart = Parts.First();
                if (!firstPart.IsEmpty && firstPart.Transition.IonType != IonType.precursor)
                {
                    stringBuilder.Append(firstPart.Transition.AA);
                    stringBuilder.Append(@" ");
                }
            }

            string strHyphen = string.Empty;
            stringBuilder.Append(@"[");
            foreach (var part in Parts)
            {
                stringBuilder.Append(strHyphen);
                strHyphen = @"-";
                if (part.IsEmpty)
                {
                    stringBuilder.Append(@"*");
                }
                else if (part.Transition.IonType == IonType.precursor)
                {
                    stringBuilder.Append(@"p");
                }
                else
                {
                    stringBuilder.Append(part.Transition.IonType);
                    stringBuilder.Append(part.Transition.Ordinal);
                }
            }
            stringBuilder.Append(GetTransitionLossesText());

            stringBuilder.Append(@"]");
            if (includeResidues && Parts.Count > 1)
            {
                var lastPart = Parts[Parts.Count - 1];
                if (!lastPart.IsEmpty)
                {
                    stringBuilder.Append(@" ");
                    stringBuilder.Append(lastPart.Transition.AA);
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

            foreach (var transition in Transitions)
            {
                switch (transition.IonType)
                {
                    case IonType.precursor:
                    case IonType.custom:
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
                if (!ContainsCrosslink(crosslink.Sites).HasValue)
                {
                    return false;
                }
            }
            return true;
        }

        public bool? ContainsCrosslink(IEnumerable<CrosslinkSite> crosslinkSites)
        {
            int countIncluded = 0;
            int countExcluded = 0;
            foreach (var site in crosslinkSites)
            {
                switch (IncludesSite(site))
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

        public ComplexFragmentIon ChangeMassIndex(int massIndex)
        {
            var transition = new Transition(PrimaryTransition.Group, PrimaryTransition.IonType, PrimaryTransition.CleavageOffset, massIndex,
                PrimaryTransition.Adduct, PrimaryTransition.DecoyMassShift);
            return ChangePrimaryTransition(transition);
        }

        public ComplexFragmentIon ChangeAdduct(Adduct adduct)
        {
            return ChangePrimaryTransition(new Transition(PrimaryTransition.Group, PrimaryTransition.IonType,
                PrimaryTransition.CleavageOffset,
                PrimaryTransition.MassIndex, adduct, PrimaryTransition.DecoyMassShift));
        }

        private ComplexFragmentIon ChangePrimaryTransition(Transition transition)
        {
            return ChangeProp(ImClone(this), im => 
                im.Parts = im.Parts.ReplaceAt(0, new Part(transition, im.Parts[0].IsEmpty)));
        }

        public CrosslinkBuilder GetCrosslinkBuilder(SrmSettings settings, ExplicitMods explicitMods)
        {
            return new CrosslinkBuilder(settings, PrimaryTransition.Group.Peptide, explicitMods, PrimaryTransition.Group.LabelType);
        }


        public TypedMass GetFragmentMass(SrmSettings settings, ExplicitMods explicitMods)
        {
            return GetCrosslinkBuilder(settings, explicitMods).GetFragmentMass(this);
        }
        public TransitionDocNode MakeTransitionDocNode(SrmSettings settings, ExplicitMods explicitMods, IsotopeDistInfo isotopeDist)
        {
            return MakeTransitionDocNode(settings, explicitMods, isotopeDist, Annotations.EMPTY, TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY, null);
        }

        public TransitionDocNode MakeTransitionDocNode(SrmSettings settings, ExplicitMods explicitMods,
            IsotopeDistInfo isotopeDist,
            Annotations annotations,
            TransitionDocNode.TransitionQuantInfo transitionQuantInfo,
            ExplicitTransitionValues explicitTransitionValues,
            Results<TransitionChromInfo> results)
        {
            return GetCrosslinkBuilder(settings, explicitMods).MakeTransitionDocNode(this, isotopeDist, annotations, transitionQuantInfo, explicitTransitionValues, results);
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
                int result = Parts[i].IsEmpty.CompareTo(other.Parts[i].IsEmpty);
                if (result == 0)
                {
                    result = TransitionGroup.CompareTransitionIds(Parts[i].Transition, other.Parts[i].Transition);
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

        public class Part
        {
            public Part(Transition transition, bool isEmpty)
            {
                Transition = transition;
                IsEmpty = isEmpty;
            }
            public Transition Transition { get; private set; }
            public bool IsEmpty { get; private set; }
        }

        public static Part EmptyPart(TransitionGroup group)
        {
            return new Part(new Transition(group, IonType.precursor, group.Peptide.Sequence.Length - 1, 0, Adduct.SINGLY_PROTONATED), true);
        }

        public static Part TransitionPart(Transition transition)
        {
            return new Part(transition, false);
        }
    }
}
