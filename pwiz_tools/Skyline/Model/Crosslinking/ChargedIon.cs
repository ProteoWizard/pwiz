using System;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    public class ChargedIon : Immutable, IComparable<ChargedIon>
    {
        public ChargedIon(Transition primaryTransition, ComplexFragmentIon complexFragmentIon, PeptideStructure peptideStructure)
        {
            PrimaryTransition = primaryTransition;
            ComplexFragmentIon = complexFragmentIon;
            PeptideStructure = peptideStructure;
        }

        public ChargedIon(Transition primaryTransition, ComplexFragmentIon complexFragmentIon,
            ExplicitMods explicitMods) : this(primaryTransition, complexFragmentIon, new PeptideStructure(primaryTransition.Group.Peptide, explicitMods))
        {
        }
        public Transition PrimaryTransition { get; private set; }
        public ComplexFragmentIon ComplexFragmentIon { get; private set; }
        protected PeptideStructure PeptideStructure { get; private set; }
        public Adduct Adduct
        {
            get { return PrimaryTransition.Adduct; }
        }
        public int MassIndex
        {
            get { return PrimaryTransition.MassIndex; }
        }
        public int? DecoyMassShift
        {
            get { return PrimaryTransition.DecoyMassShift; }
        }

        public TransitionLosses Losses
        {
            get
            {
                return ComplexFragmentIon.Losses;
            }
        }

        public bool IsCrosslinked
        {
            get
            {
                return PeptideStructure?.Peptides?.Count > 1;
            }
        }

        public static ChargedIon Simple(Transition transition, TransitionLosses losses)
        {
            return new ChargedIon(transition,
                new ComplexFragmentIon(ImmutableList.Singleton(IonFragment.FromTransition(transition)), losses),
                (PeptideStructure)null);
        }
        public string GetFragmentIonName()
        {
            return ComplexFragmentIon.GetLabel(PeptideStructure, false);
        }

        public string GetTargetsTreeLabel()
        {
            return ComplexFragmentIon.GetLabel(PeptideStructure, true) + Transition.GetMassIndexText(PrimaryTransition.MassIndex);
        }

        public ChargedIon ChangeMassIndex(int massIndex)
        {
            var transition = new Transition(PrimaryTransition.Group, PrimaryTransition.IonType, PrimaryTransition.CleavageOffset, massIndex,
                PrimaryTransition.Adduct, PrimaryTransition.DecoyMassShift);
            return ChangeProp(ImClone(this), im => im.PrimaryTransition = transition);
        }

        public ChargedIon ChangeAdduct(Adduct adduct)
        {
            var transition = new Transition(PrimaryTransition.Group, PrimaryTransition.IonType,
                PrimaryTransition.CleavageOffset,
                PrimaryTransition.MassIndex, adduct, PrimaryTransition.DecoyMassShift);
            return ChangeProp(ImClone(this), im => im.PrimaryTransition = transition);
        }

        public CrosslinkBuilder GetCrosslinkBuilder(SrmSettings settings)
        {
            return new CrosslinkBuilder(settings, PeptideStructure, PrimaryTransition.Group.LabelType);
        }
        public TypedMass GetFragmentMass(SrmSettings settings, ExplicitMods explicitMods)
        {
            return GetCrosslinkBuilder(settings).GetFragmentMass(ComplexFragmentIon);
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
            return GetCrosslinkBuilder(settings).MakeTransitionDocNode(this, isotopeDist, annotations, transitionQuantInfo, explicitTransitionValues, results);
        }

        public int CompareTo(ChargedIon other)
        {
            if (ComplexFragmentIon.IsIonTypePrecursor)
            {
                if (!other.ComplexFragmentIon.IsIonTypePrecursor)
                {
                    return -1;
                }
            }
            else if (other.ComplexFragmentIon.IsIonTypePrecursor)
            {
                return 1;
            }

            int result = TransitionGroup.CompareTransitionIds(PrimaryTransition, other.PrimaryTransition);
            if (result != 0)
            {
                return result;
            }

            return ComplexFragmentIon.CompareTo(other.ComplexFragmentIon);
        }

        public ComplexFragmentIonKey GetName()
        {
            return ComplexFragmentIon.GetName();
        }

        public bool IsMs1
        {
            get
            {
                return ComplexFragmentIon.IsMs1;
            }
        }

        public bool IsOrphan
        {
            get
            {
                return ComplexFragmentIon.IsOrphan;
            }
        }
    }
}
