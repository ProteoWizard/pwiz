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
using JetBrains.Annotations;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Crosslinking
{
    /// <summary>
    /// Represents a set of Transitions from different peptides linked together by crosslinked modifications.
    /// </summary>
    public class LegacyComplexFragmentIon : Immutable, IComparable<LegacyComplexFragmentIon>
    {
        public LegacyComplexFragmentIon(Transition transition, TransitionLosses transitionLosses, ImmutableSortedList<ModificationSite, LinkedPeptide> crosslinkStructure, bool isOrphan = false)
        {
            Transition = transition;
            Children = ImmutableSortedList<ModificationSite, LegacyComplexFragmentIon>.EMPTY;
            TransitionLosses = transitionLosses;
            IsOrphan = isOrphan;
            CrosslinkStructure = crosslinkStructure ?? LinkedPeptide.EMPTY_CROSSLINK_STRUCTURE;
        }

        /// <summary>
        /// Creates a ComplexFragmentIon representing something which has no amino acids from the parent peptide.
        /// </summary>
        public static LegacyComplexFragmentIon NewOrphanFragmentIon(TransitionGroup transitionGroup, ExplicitMods explicitMods, Adduct adduct)
        {
            throw new NotImplementedException();
            // var transition = new Transition(transitionGroup, IonType.precursor,
            //     transitionGroup.Peptide.Sequence.Length - 1, 0, adduct);
            // return new LegacyComplexFragmentIon(transition, null, explicitMods?.Crosslinks, true);
        }

        public Transition Transition { get; private set; }

        /// <summary>
        /// Returns a new ComplexFragmentIon where the Transition has been cloned (i.e. the Transition has a different GlobalIndex value).
        /// </summary>
        /// <returns></returns>
        public LegacyComplexFragmentIon CloneTransition()
        {
            return ChangeProp(ImClone(this), im => im.Transition = (Transition) Transition.Copy());
        }

        /// <summary>
        /// If true, this ion includes no amino acids from the parent peptide.
        /// </summary>
        public bool IsOrphan { get; private set; }

        /// <summary>
        /// Whether this ion has no amino acids from the parent peptide, or any of the children either.
        /// </summary>
        public bool IsEmptyOrphan
        {
            get { return IsOrphan && Children.Count == 0; }
        }

        [CanBeNull]
        public TransitionLosses TransitionLosses { get; private set; }
        public ImmutableSortedList<ModificationSite, LegacyComplexFragmentIon> Children { get; private set; }

        public ImmutableSortedList<ModificationSite, LinkedPeptide> CrosslinkStructure { get; private set; }

        public LegacyComplexFragmentIon ChangeCrosslinkStructure(
            ImmutableSortedList<ModificationSite, LinkedPeptide> crosslinkStructure)
        {
            return ChangeProp(ImClone(this), im => im.CrosslinkStructure = crosslinkStructure);
        }

        public LegacyComplexFragmentIon ChangeLosses(TransitionLosses transitionLosses)
        {
            return ChangeProp(ImClone(this), im => im.TransitionLosses = transitionLosses);
        }

        public IsotopeLabelType LabelType
        {
            get { return Transition.Group.LabelType; }
        }

        public LegacyComplexFragmentIon AddChild(ModificationSite modificationSite, LegacyComplexFragmentIon child)
        {
            if (IsOrphan && !IsEmptyOrphan)
            {
                throw new InvalidOperationException(string.Format(@"Cannot add {0} to {1}.", child, this));
            }

            if (child.Transition.MassIndex != 0)
            {
                throw new InvalidOperationException(string.Format(@"{0} cannot be a child fragment ion transition.", child.Transition));
            }

            var newLosses = TransitionLosses;
            if (child.TransitionLosses != null)
            {
                if (newLosses == null)
                {
                    newLosses = child.TransitionLosses;
                }
                else
                {
                    newLosses = new TransitionLosses(newLosses.Losses.Concat(child.TransitionLosses.Losses).ToList(), newLosses.MassType);
                }

                child = child.ChangeLosses(null);
            }

            return ChangeProp(ImClone(this), im =>
            {
                im.Children =
                    ImmutableSortedList.FromValues(Children.Append(
                        new KeyValuePair<ModificationSite, LegacyComplexFragmentIon>(
                            modificationSite, child)));
                im.TransitionLosses = newLosses;
            });
        }

        public LegacyComplexFragmentIon ChangeMassIndex(int massIndex)
        {
            var transition = new Transition(Transition.Group, Transition.IonType, Transition.CleavageOffset, massIndex,
                Transition.Adduct, Transition.DecoyMassShift);
            return ChangeProp(ImClone(this), im => im.Transition = transition);
        }

        /// <summary>
        /// Returns the number of peptides that need to have fragmented in order to produce this crosslinked ion.
        /// </summary>
        public int GetFragmentationEventCount()
        {
            int count = 0;
            if (!IsOrphan && !Transition.IsPrecursor())
            {
                count++;
            }

            if (null != TransitionLosses)
            {
                count += TransitionLosses.Losses.Count;
            }
            count += Children.Values.Sum(child => child.GetFragmentationEventCount());
            return count;
        }

        public bool IncludesAaIndex(int aaIndex)
        {
            switch (Transition.IonType)
            {
                case IonType.precursor:
                    return true;
                case IonType.a:
                case IonType.b:
                case IonType.c:
                    return Transition.CleavageOffset >= aaIndex;
                case IonType.x:
                case IonType.y:
                case IonType.z:
                    return Transition.CleavageOffset < aaIndex;
                default:
                    return true;
            }
        }

        public CrosslinkBuilder GetCrosslinkBuilder(SrmSettings settings, ExplicitMods explicitMods)
        {
            return new CrosslinkBuilder(settings, Transition.Group.Peptide, explicitMods, Transition.Group.LabelType);
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
            throw new NotImplementedException();
            // return GetCrosslinkBuilder(settings, explicitMods).MakeTransitionDocNode(this, isotopeDist, annotations, transitionQuantInfo, explicitTransitionValues, results);
        }

        public TypedMass GetFragmentMass(SrmSettings settings, ExplicitMods explicitMods)
        {
            throw new NotImplementedException();
            // return GetCrosslinkBuilder(settings, explicitMods).GetFragmentMass(this);
        }

        /// <summary>
        /// Returns a ComplexFragmentIonName object representing this ComplexFragmentIon
        /// </summary>
        public ComplexFragmentIonName GetName()
        {
            ComplexFragmentIonName name;
            if (IsOrphan)
            {
                name = ComplexFragmentIonName.ORPHAN;
            }
            else
            {
                name = new ComplexFragmentIonName(Transition.IonType, Transition.Ordinal);
            }

            foreach (var child in Children)
            {
                name = name.AddChild(child.Key, child.Value.GetName());
            }

            return name;
        }

        public override string ToString()
        {
            return GetName() + Transition.GetChargeIndicator(Transition.Adduct);
        }

        public bool IsMs1
        {
            get
            {
                if (IsOrphan)
                {
                    return false;
                }
                return Transition.IsPrecursor() && null == TransitionLosses &&
                       Children.Values.All(child => child.IsMs1);
            }
        }

        public bool IsIonTypePrecursor
        {
            get
            {
                if (IsOrphan)
                {
                    return false;
                }

                return Transition.IsPrecursor() && Children.Values.All(child => child.IsIonTypePrecursor);
            }
        }

        public int CompareTo(LegacyComplexFragmentIon other)
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
            int result = IsOrphan.CompareTo(other.IsOrphan);
            if (result == 0)
            {
                result = TransitionGroup.CompareTransitionIds(Transition, other.Transition);
            }
                
            if (result == 0)
            {
                result = Comparer<double?>.Default.Compare(TransitionLosses?.Mass, other.TransitionLosses?.Mass);
            }

            if (result != 0)
            {
                return result;
            }
            for (int i = 0; i < Children.Count && i < other.Children.Count; i++)
            {
                result = Children[i].Key.CompareTo(other.Children[i].Key);
                if (result == 0)
                {
                    result = Children[i].Value.CompareTo(other.Children[i].Value);
                }

                if (result != 0)
                {
                    return result;
                }
            }

            return Children.Count.CompareTo(other.Children.Count);
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
            if (CrosslinkStructure.Count == 1 && CrosslinkStructure.Values[0].CrosslinkStructure.Count == 0)
            {
                var child = Children.Values.FirstOrDefault();
                if (includeResidues)
                {
                    if (!IsOrphan && Transition.IonType != IonType.precursor)
                    {
                        stringBuilder.Append(Transition.AA);
                        stringBuilder.Append(@" ");
                    }
                }
                stringBuilder.Append(@"[");
                if (!IsOrphan)
                {
                    if (Transition.IonType == IonType.precursor)
                    {
                        stringBuilder.Append(TransitionFilter.PRECURSOR_ION_CHAR);
                    }
                    else
                    {
                        stringBuilder.Append(Transition.IonType);
                        stringBuilder.Append(Transition.Ordinal);
                    }
                }

                stringBuilder.Append(@"-");
                if (child != null)
                {
                    if (child.Transition.IonType == IonType.precursor)
                    {
                        stringBuilder.Append(TransitionFilter.PRECURSOR_ION_CHAR);
                    }
                    else
                    {
                        stringBuilder.Append(child.Transition.IonType);
                        stringBuilder.Append(child.Transition.Ordinal);
                    }
                }

                stringBuilder.Append(GetTransitionLossesText());

                stringBuilder.Append(@"]");
                if (includeResidues)
                {
                    if (child != null && child.Transition.IonType != IonType.precursor)
                    {
                        stringBuilder.Append(@" ");
                        stringBuilder.Append(child.Transition.AA);
                    }
                }
                return stringBuilder.ToString();
            }

            string label = @"[" + GetName() + @"]";
            return label;
        }

        public string GetFragmentIonName()
        {
            return GetLabel(false);
        }

        public string GetTargetsTreeLabel()
        {
            return GetLabel(true) + Transition.GetMassIndexText(Transition.MassIndex);
        }

        private string GetTransitionLossesText()
        {
            if (TransitionLosses == null)
            {
                return string.Empty;
            }

            return @" -" + Math.Round(TransitionLosses.Mass, 1);
        }
    }
}
