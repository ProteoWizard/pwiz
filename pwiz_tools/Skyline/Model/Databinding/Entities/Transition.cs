/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2012 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [InvariantDisplayName(nameof(Transition))]
    [AnnotationTarget(AnnotationDef.AnnotationTarget.transition)]
    public class Transition : SkylineDocNode<TransitionDocNode>
    {
        private readonly Lazy<Precursor> _precursor;
        private readonly CachedValue<IDictionary<ResultKey, TransitionResult>> _results;
        public Transition(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
            _precursor = new Lazy<Precursor>(() => new Precursor(DataSchema, IdentityPath.Parent));
            _results = CachedValue.Create(DataSchema, MakeResults);
        }

        [HideWhen(AncestorOfType = typeof(Precursor))]
        public Precursor Precursor
        {
            get
            {
                return _precursor.Value;
            }
        }

        [InvariantDisplayName("TransitionResults")]
        [OneToMany(ForeignKey = nameof(TransitionResult.Transition))]
        public IDictionary<ResultKey, TransitionResult> Results
        {
            get
            {
                return _results.Value;
            }
        }

        private IDictionary<ResultKey, TransitionResult> MakeResults()
        {
            return MakeChromInfoResultsMap(DocNode.Results, file => new TransitionResult(this, file));
        }

        private bool IsCustomTransition()
        {
            return DocNode.Transition.IsNonReporterCustomIon();  // As opposed to just IsCustom(), which might be a reporter ion on a peptide node
        }

        protected override TransitionDocNode CreateEmptyNode()
        {
            var transitionGroup = new TransitionGroup(new Model.Peptide(null, @"X", null, null, 0),
                Adduct.SINGLY_PROTONATED, IsotopeLabelType.light);
            var transition = new Model.Transition(transitionGroup, 0, Adduct.SINGLY_PROTONATED);
            return new TransitionDocNode(transition, Annotations.EMPTY, null, TypedMass.ZERO_MONO_MASSH,
                TransitionDocNode.TransitionQuantInfo.DEFAULT, ExplicitTransitionValues.EMPTY, null);
        }

        [InvariantDisplayName("TransitionResultsSummary")]
        public TransitionResultSummary ResultSummary
        {
            get
            {
                return new TransitionResultSummary(this, Results.Values);
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int ProductCharge { get { return DocNode.Transition.Charge; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double ProductNeutralMass
        {
            get { return DocNode.GetMoleculePersistentNeutralMass(); }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double ProductMz
        {
            get { return SequenceMassCalc.PersistentMZ(DocNode.Mz); }
        }
        public string FragmentIon
        {
            get
            {
                string fragmentIon =  DocNode.GetFragmentIonName(CultureInfo.InvariantCulture);
                if (DocNode.Transition.IonType == IonType.precursor)
                {
                    fragmentIon += Model.Transition.GetMassIndexText(DocNode.Transition.MassIndex);
                }
                return fragmentIon;
            }
        }
        public string ProductIonFormula
        {
            get
            {
                if (IsCustomTransition())
                {
                    return DocNode.Transition.CustomIon.HasChemicalFormula ? DocNode.Transition.CustomIon.Formula : String.Empty;
                }

                var neutralFormula = GetNeutralProductFormula();
                var adduct = DocNode.Transition.Adduct;
                var formulaWithAdductApplied = adduct.ApplyToMolecule(neutralFormula);
                return formulaWithAdductApplied.ToString();
            }
        }
        public string ProductNeutralFormula
        {
            get
            {
                if (IsCustomTransition())
                {
                    return DocNode.Transition.CustomIon.HasChemicalFormula ? DocNode.Transition.CustomIon.Formula : String.Empty;
                }

                return GetNeutralProductFormula().Molecule.ToString();
            }
        }

        private MoleculeMassOffset GetNeutralProductFormula()
        {
            var peptide = Precursor.Peptide;
            var crosslinkBuilder = new CrosslinkBuilder(SrmDocument.Settings, peptide.DocNode.Peptide,
                peptide.DocNode.ExplicitMods, Precursor.IsotopeLabelType);
            return crosslinkBuilder.GetNeutralFormula(DocNode.ComplexFragmentIon.NeutralFragmentIon);
        }
        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string ProductAdduct
        {
            get
            {
                return DocNode.Transition.Adduct.AsFormula();
            }
        }
        public IonType FragmentIonType
        {
            get { return DocNode.Transition.IonType; }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public int? FragmentIonOrdinal
        {
            get
            {
                if (DocNode.Transition.IsCustom())
                    return null;
                return DocNode.Transition.Ordinal;
            }
        }
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string CleavageAa
        {
            get
            {
                return IsCustomTransition()
                    ? null 
                    : DocNode.Transition.AA.ToString();
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public double? LossNeutralMass
        {
            get
            {
                if (IsCustomTransition())
                    return  null;
                return DocNode.LostMass;
            }
        }
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string Losses
        {
            get
            {
                return IsCustomTransition()
                    ? null
                    : (DocNode.HasLoss ? string.Join(@", ", DocNode.Losses.ToStrings()) : string.Empty);
            }
        }
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string LossFormulas
        {
            get
            {
                if (IsCustomTransition())
                    return null;
                return DocNode.HasLoss && DocNode.Losses.Losses.All(l => l.Loss.Formula != null)
                        ? string.Join(@", ", DocNode.Losses.Losses.Select(l => l.Loss.Formula))
                        : string.Empty;
            }
        }

        [Importable]
        public bool Quantitative
        {
            get { return DocNode.ExplicitQuantitative; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(Quantitative), value),
                    docNode=>docNode.ChangeQuantitative(value));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Importable]
        public double? ExplicitCollisionEnergy
        {
            get { return DocNode.ExplicitValues.CollisionEnergy; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"ExplicitCollisionEnergy", value),
                    docNode => DocNode.ChangeExplicitCollisionEnergy(value));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Importable]
        public double? ExplicitSLens
        {
            get { return DocNode.ExplicitValues.SLens; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"ExplicitSLens", value),
                    docNode => docNode.ChangeExplicitSLens(value));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Importable]
        public double? ExplicitConeVoltage
        {
            get { return DocNode.ExplicitValues.ConeVoltage; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"ExplicitConeVoltage", value),
                    docNode => docNode.ChangeExplicitConeVoltage(value));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Importable]
        public double? ExplicitDeclusteringPotential
        {
            get { return DocNode.ExplicitValues.DeclusteringPotential; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"ExplicitDeclusteringPotential", value),
                    docNode => docNode.ChangeExplicitDeclusteringPotential(value));
            }
        }

        [Importable]
        public double? ExplicitIonMobilityHighEnergyOffset
        {
            get { return DocNode.ExplicitValues.IonMobilityHighEnergyOffset; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"ExplicitIonMobilityHighEnergyOffset", value),
                    docNode => docNode.ChangeExplicitIonMobilityHighEnergyOffset(value));
            }
        }

        [InvariantDisplayName("TransitionNote")]
        [Importable]
        public string Note
        {
            get { return DocNode.Note; }
            set { ChangeDocNode(EditColumnDescription(nameof(Note), value),
                docNode=>(TransitionDocNode) docNode.ChangeAnnotations(docNode.Annotations.ChangeNote(value)));}
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? LibraryRank
        {
            get
            {
                return DocNode.HasLibInfo ? (int?) DocNode.LibInfo.Rank : null;
            }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryIntensity
        {
            get { return DocNode.HasLibInfo ? (double?) DocNode.LibInfo.Intensity : null; }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int IsotopeDistIndex { get { return DocNode.Transition.MassIndex; } }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? IsotopeDistRank { get { return DocNode.HasDistInfo ? (int?)DocNode.IsotopeDistInfo.Rank : null; } }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? IsotopeDistProportion {get { return DocNode.HasDistInfo ? (double?) DocNode.IsotopeDistInfo.Proportion : null; }}
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? FullScanFilterWidth
        {
            get
            {
                var fullScan = SrmDocument.Settings.TransitionSettings.FullScan;
                if (fullScan.IsEnabledMs && DocNode.Transition.IonType == IonType.precursor)
                {
                    return SequenceMassCalc.PersistentMZ(fullScan.GetPrecursorFilterWindow(ProductMz));
                }
                if (fullScan.IsEnabledMsMs &&
                    (DocNode.Transition.IonType != IonType.precursor || DocNode.Transition.MassIndex == 0))
                {
                    return SequenceMassCalc.PersistentMZ(fullScan.GetProductFilterWindow(DocNode.Mz));
                }
                return null;
            }
        }
        [InvariantDisplayName("TransitionIsDecoy")]
        public bool IsDecoy
        {
            get { return DocNode.IsDecoy; }
        }
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? ProductDecoyMzShift
        {
            get { return DocNode.Transition.DecoyMassShift; }
        }

        public override string ToString()
        {
            return DocNode.Transition.ToString();
        }

        public override string GetDeleteConfirmation(int nodeCount)
        {
            if (nodeCount == 1)
            {
                return string.Format(EntitiesResources.Transition_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_transition___0___, this);
            }
            return string.Format(EntitiesResources.Transition_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__transitions_, nodeCount);
        }

        [InvariantDisplayName("TransitionLocator")]
        public string Locator { get { return GetLocator(); } }

        protected override NodeRef NodeRefPrototype
        {
            get { return TransitionRef.PROTOTYPE; }
        }

        protected override Type SkylineDocNodeType
        {
            get { return typeof(Transition); }
        }
    }

    public class TransitionResultSummary : SkylineObject
    {
        private Transition _transition;
        public TransitionResultSummary(Transition transition, IEnumerable<TransitionResult> results)
        {
            _transition = transition;
            var retentionTimes = new List<double>();
            var fwhms = new List<double>();
            var areas = new List<double>();
            var areasRatio = new List<double>();
            var areasNormalized = new List<double>();

            foreach (var result in results)
            {
                if (result.RetentionTime.HasValue)
                {
                    retentionTimes.Add(result.RetentionTime.Value);
                }
                if (result.Fwhm.HasValue)
                {
                    fwhms.Add(result.Fwhm.Value);
                }
                if (result.Area.HasValue)
                {
                    areas.Add(result.Area.Value);
                }
                if (result.AreaNormalized.HasValue)
                {
                    areasNormalized.Add(result.AreaNormalized.Value);
                }
                if (result.AreaRatio.HasValue)
                {
                    areasRatio.Add(result.AreaRatio.Value);
                }
            }
            if (retentionTimes.Count > 0)
            {
                RetentionTime = new RetentionTimeSummary(new Statistics(retentionTimes));
            }
            if (fwhms.Count > 0)
            {
                Fwhm = new FwhmSummary(new Statistics(fwhms));
            }
            if (areas.Count > 0)
            {
                Area = new AreaSummary(new Statistics(areas));
            }
            if (areasNormalized.Count > 0)
            {
                AreaNormalized = new AreaNormalizedSummary(new Statistics(areasNormalized));
            }
            if (areasRatio.Count > 0)
            {
                AreaRatio = new AreaRatioSummary(new Statistics(areasRatio));
            }
        }

        protected override SkylineDataSchema GetDataSchema()
        {
            return _transition.DataSchema;
        }

        [Obsolete]
        public string ReplicatePath { get { return @"/"; } }
        [Obsolete]
        public Transition Transition
        {
            get { return _transition; }
        }
        [ChildDisplayName("{0}RetentionTime")]
        public RetentionTimeSummary RetentionTime { get; private set; }
        [ChildDisplayName("{0}Fwhm")]
        public FwhmSummary Fwhm { get; private set; }
        [ChildDisplayName("{0}Area")]
        public AreaSummary Area { get; private set; }
        [ChildDisplayName("{0}AreaNormalized")]
        public AreaNormalizedSummary AreaNormalized { get; private set; }
        [ChildDisplayName("{0}AreaRatio")]
        public AreaRatioSummary AreaRatio { get; private set; }

        public override string ToString()
        {
            return string.Format(@"RT: {0} Area: {1}", RetentionTime, Area); // CONSIDER: localize?
        }

    }
}
