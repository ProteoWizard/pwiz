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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Represents a chain of fragment ions crosslinked together.
    /// </summary>
    public class ComplexFragmentIon : Immutable, IComparable<ComplexFragmentIon>
    {
        private PeptideStructure _peptideStructure;
        public ComplexFragmentIon(Transition primaryTransition, NeutralFragmentIon complexFragmentIon, PeptideStructure peptideStructure)
        {
            PrimaryTransition = primaryTransition;
            NeutralFragmentIon = complexFragmentIon;
            _peptideStructure = peptideStructure;
        }

        public ComplexFragmentIon(Transition primaryTransition, NeutralFragmentIon complexFragmentIon,
            ExplicitMods explicitMods) : this(primaryTransition, complexFragmentIon, new PeptideStructure(primaryTransition.Group.Peptide, explicitMods))
        {
        }
        public Transition PrimaryTransition { get; private set; }
        public NeutralFragmentIon NeutralFragmentIon { get; private set; }

        private PeptideStructure PeptideStructure
        {
            get
            {
                return _peptideStructure ?? new PeptideStructure(PrimaryTransition.Group.Peptide, null);
            }
        }

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
                return NeutralFragmentIon.Losses;
            }
        }

        public bool IsCrosslinked
        {
            get
            {
                return _peptideStructure?.Peptides?.Count > 1;
            }
        }
        public static ComplexFragmentIon Simple(Transition transition, TransitionLosses losses)
        {
            return new ComplexFragmentIon(transition,
                new NeutralFragmentIon(ImmutableList.Singleton(IonOrdinal.FromTransition(transition)), losses),
                (PeptideStructure)null);
        }
        public string GetFragmentIonName()
        {
            return NeutralFragmentIon.GetLabel(PeptideStructure, false);
        }

        public string GetTargetsTreeLabel()
        {
            return NeutralFragmentIon.GetLabel(PeptideStructure, true) + Transition.GetMassIndexText(PrimaryTransition.MassIndex);
        }

        public ComplexFragmentIon ChangeMassIndex(int massIndex)
        {
            var transition = new Transition(PrimaryTransition.Group, PrimaryTransition.IonType, PrimaryTransition.CleavageOffset, massIndex,
                PrimaryTransition.Adduct, PrimaryTransition.DecoyMassShift);
            return ChangeProp(ImClone(this), im => im.PrimaryTransition = transition);
        }

        public CrosslinkBuilder GetCrosslinkBuilder(SrmSettings settings)
        {
            return GetCrosslinkBuilder(settings, null);
        }

        private CrosslinkBuilder GetCrosslinkBuilder(SrmSettings settings, ExplicitMods explicitMods)
        {
            return new CrosslinkBuilder(settings, explicitMods?.GetPeptideStructure() ?? PeptideStructure, PrimaryTransition.Group.LabelType);
        }
        public TypedMass GetFragmentMass(SrmSettings settings, ExplicitMods explicitMods)
        {
            return GetCrosslinkBuilder(settings).GetFragmentMass(NeutralFragmentIon);
        }
        public TransitionDocNode MakeTransitionDocNode(SrmSettings settings, ExplicitMods explicitMods, IsotopeDistInfo isotopeDist)
        {
            return MakeTransitionDocNode(settings, explicitMods, isotopeDist, Annotations.EMPTY, TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY, null);
        }

        private void VerifySameExplicitMods(ExplicitMods explicitMods)
        {
            if (explicitMods == null)
            {
                return;
            }
            Assume.AreEqual(PeptideStructure.Crosslinks, explicitMods.CrosslinkStructure);
        }

        public TransitionDocNode MakeTransitionDocNode(SrmSettings settings, ExplicitMods explicitMods,
            IsotopeDistInfo isotopeDist,
            Annotations annotations,
            TransitionDocNode.TransitionQuantInfo transitionQuantInfo,
            ExplicitTransitionValues explicitTransitionValues,
            Results<TransitionChromInfo> results)
        {
            return GetCrosslinkBuilder(settings, explicitMods).MakeTransitionDocNode(
                this, isotopeDist, annotations, transitionQuantInfo, explicitTransitionValues, results);
        }

        public int CompareTo(ComplexFragmentIon other)
        {
            if (NeutralFragmentIon.IsIonTypePrecursor)
            {
                if (!other.NeutralFragmentIon.IsIonTypePrecursor)
                {
                    return -1;
                }
            }
            else if (other.NeutralFragmentIon.IsIonTypePrecursor)
            {
                return 1;
            }

            int result = TransitionGroup.CompareTransitionIds(PrimaryTransition, other.PrimaryTransition);
            if (result != 0)
            {
                return result;
            }

            return NeutralFragmentIon.CompareTo(other.NeutralFragmentIon);
        }

        public IonChain GetName()
        {
            return NeutralFragmentIon.GetName();
        }

        public bool IsMs1
        {
            get
            {
                return NeutralFragmentIon.IsMs1;
            }
        }

        public bool IsOrphan
        {
            get
            {
                return NeutralFragmentIon.IsOrphan;
            }
        }
    }
}
