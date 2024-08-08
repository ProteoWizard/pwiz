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
using System.Linq;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.precursor)]
    public class Precursor : SkylineDocNode<TransitionGroupDocNode>
    {
        private readonly Lazy<Peptide> _peptide;
        private readonly CachedValues _cachedValues = new CachedValues();
        public Precursor(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
            _peptide = new Lazy<Peptide>(() => new Peptide(DataSchema, IdentityPath.Parent));
        }

        [HideWhen(AncestorOfType = typeof(Peptide))]
        [InvariantDisplayName("Molecule", ExceptInUiMode = UiModes.PROTEOMIC)]
        public Peptide Peptide
        {
            get { return _peptide.Value; }
        }

        [OneToMany(ForeignKey = "Precursor")]
        public IList<Transition> Transitions
        {
            get
            {
                return _cachedValues.GetValue(this);
            }
        }

        [InvariantDisplayName("PrecursorResults")]
        [OneToMany(ForeignKey = "Precursor")]
        public IDictionary<ResultKey, PrecursorResult> Results
        {
            get { return _cachedValues.GetValue1(this); }
        }

        protected override TransitionGroupDocNode CreateEmptyNode()
        {
            return new TransitionGroupDocNode(new TransitionGroup(new Model.Peptide(null, @"X", null, null, 0), Util.Adduct.SINGLY_PROTONATED, IsotopeLabelType.light), null);
        }

        [InvariantDisplayName("PrecursorResultsSummary")]
        public PrecursorResultSummary ResultSummary
        {
            get
            {
                return new PrecursorResultSummary(this, Results.Values);
            }
        }

        [InvariantDisplayName("PrecursorCharge")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int Charge
        {
            get { return DocNode.TransitionGroup.PrecursorCharge; }
        }

        public IsotopeLabelType IsotopeLabelType
        {
            get { return DocNode.TransitionGroup.LabelType; }
        }

        [InvariantDisplayName("PrecursorNeutralMass")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double NeutralMass
        {
            get
            {
                return DocNode.GetPrecursorIonPersistentNeutralMass();
            }
        }

        public int TransitionCount
        {
            get { return DocNode.TransitionCount; }
        }

        private bool IsSmallMolecule()
        {
            return DocNode.TransitionGroup.IsCustomIon;
        }

        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        [InvariantDisplayName("PrecursorIonName")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public string IonName
        {
            get
            {
                if (IsSmallMolecule())
                {
                    return (DocNode.CustomMolecule.Name ?? string.Empty);
                }
                else
                {
                    var parent = DataSchema.Document.FindNode(IdentityPath.Parent) as PeptideDocNode;
                    var molecule = RefinementSettings.ConvertToSmallMolecule(RefinementSettings.ConvertToSmallMoleculesMode.formulas, SrmDocument, parent, out _, DocNode.TransitionGroup.PrecursorAdduct.AdductCharge, DocNode.TransitionGroup.LabelType);
                    return molecule.InvariantName ?? string.Empty;
                }
            }
        }

        // Helper function for PrecursorIonFormula and PrecursorNeutralFormula
        private void GetPrecursorFormulaAndAdduct(out Adduct adduct, out ParsedMolecule formula)
        {
            if (IsSmallMolecule())
            {
                formula = DocNode.CustomMolecule.ParsedMolecule;
                adduct = DocNode.PrecursorAdduct;
            }
            else
            {
                var crosslinkBuilder = new CrosslinkBuilder(SrmDocument.Settings, DocNode.TransitionGroup.Peptide,
                    Peptide.DocNode.ExplicitMods, DocNode.LabelType);
                adduct = Util.Adduct.FromChargeProtonated(Charge);
                formula = ParsedMolecule.Create(crosslinkBuilder.GetPrecursorFormula());
            }
        }

        [InvariantDisplayName("PrecursorIonFormula")]
        public string IonFormula
        {
            get
            {
                // Given formula C12H8O3 and adduct M3H2+H, apply label 3H2 and ionization +H to return C12H'3H6
                GetPrecursorFormulaAndAdduct(out var adduct, out var formula);
                return ParsedMolecule.IsNullOrEmpty(formula) ? string.Empty : adduct.ApplyToMolecule(formula).ToString();
            }
        }

        [InvariantDisplayName("PrecursorNeutralFormula")]
        public string NeutralFormula
        {
            get
            {
                // Given formula C12H8O3 and adduct M3H2+H, apply label 3H2 but not ionization +H to return C12H'3H5
                // Given formula C12H8O3 and adduct M(-0.234)+H, apply mass-only label (-0.234) but not ionization +H to return C12H8O3[-0.234]
                GetPrecursorFormulaAndAdduct(out var adduct, out var formula);
                return ParsedMolecule.IsNullOrEmpty(formula) ? string.Empty : adduct.ApplyIsotopeLabelsToMolecule(formula).ToString();
            }
        }

        [InvariantDisplayName("PrecursorAdduct")]
        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string Adduct
        {
            get
            {
                return DocNode.PrecursorAdduct.AsFormula();
            }
        }

        [InvariantDisplayName("PrecursorMz")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double Mz
        {
            get { return SequenceMassCalc.PersistentMZ(DocNode.PrecursorMz); }
        }

        /// <summary>
        /// Predicted collision energy, not to be confused with <see cref="ExplicitCollisionEnergy"/>.
        /// </summary>
        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? CollisionEnergy
        {
            get
            {
                var collisionEnergyRegression = SrmDocument.Settings.TransitionSettings.Prediction.CollisionEnergy;
                if (collisionEnergyRegression == null || Equals(collisionEnergyRegression, CollisionEnergyList.NONE))
                {
                    return null;
                }

                return collisionEnergyRegression.GetCollisionEnergy(DocNode.PrecursorAdduct, GetRegressionMz());
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? DeclusteringPotential
        {
            get
            {
                var declusteringPotentialRegression =
                    SrmDocument.Settings.TransitionSettings.Prediction.DeclusteringPotential;
                if (null == declusteringPotentialRegression)
                {
                    return null;
                }
                return declusteringPotentialRegression.GetDeclustringPotential(GetRegressionMz());
            }
        }
        
        [ChildDisplayName("ModifiedSequence{0}")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public ProteomicSequence ModifiedSequence
        {
            get
            {
                return ProteomicSequence.GetProteomicSequence(SrmDocument.Settings, Peptide.DocNode, IsotopeLabelType);
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Obsolete("Use PrecursorExplicitCollisionEnergy instead")]
        public double? ExplicitCollisionEnergy
        {
            get
            {
                return PrecursorExplicitCollisionEnergy;
            }
            set
            {
                PrecursorExplicitCollisionEnergy = value;
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? PrecursorExplicitCollisionEnergy
        {
            get
            {
                return DocNode.ExplicitValues.CollisionEnergy;
            }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(PrecursorExplicitCollisionEnergy), value),
                    docNode => docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeCollisionEnergy(value)));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Obsolete("Use Transition.SLens instead")]
        public double? ExplicitSLens
        {
            get
            {
                // If all transitions have the same value, show that
                return Transitions.Any() && Transitions.All(t => Equals(t.ExplicitSLens, Transitions.First().ExplicitSLens))
                    ? Transitions.First().ExplicitSLens
                    : null;
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Obsolete("Use Transition.ExplicitConeVoltage instead")]
        public double? ExplicitConeVoltage
        {
            get
            {
                // If all transitions have the same value, show that
                return Transitions.Any() && Transitions.All(t => Equals(t.ExplicitConeVoltage, Transitions.First().ExplicitConeVoltage))
                    ? Transitions.First().ExplicitConeVoltage
                    : null;
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Obsolete("Use Transition.ExplicitDeclusteringPotential instead")]
        public double? ExplicitDeclusteringPotential
        {
            get
            {
                // If all transitions have the same value, show that
                return Transitions.Any() && Transitions.All(t => Equals(t.ExplicitDeclusteringPotential, Transitions.First().ExplicitDeclusteringPotential))
                    ? Transitions.First().ExplicitDeclusteringPotential
                    : null;
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        [Importable]
        public double? ExplicitCompensationVoltage
        {
            get
            {
                return DocNode.ExplicitValues.CompensationVoltage;
            }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(ExplicitCompensationVoltage), value),
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobility(value, eIonMobilityUnits.compensation_V)));
            }
        }

        [Obsolete("use ExplicitIonMobility instead")] 
        public double? ExplicitDriftTimeMsec // Backward compatibility, the more general "ExplicitIonMobility" is preferred
        {
            get
            {
                return DocNode.ExplicitValues.IonMobilityUnits == eIonMobilityUnits.drift_time_msec ? DocNode.ExplicitValues.IonMobility : null;
            }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(ExplicitDriftTimeMsec), value),
                    docNode => docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobility(value, eIonMobilityUnits.drift_time_msec)));
            }
        }

        [Obsolete("use Transition.IonMobilityHighEnergyOffset instead")] 
        public double? ExplicitDriftTimeHighEnergyOffsetMsec  // Backward compatibility, the more general "ExplicitIonMobility" is preferred
        {
            get
            {
                return DocNode.ExplicitValues.IonMobilityUnits == eIonMobilityUnits.drift_time_msec ? ExplicitIonMobilityHighEnergyOffset : null;
            }
        }

        [Importable]
        public double? ExplicitIonMobility
        {
            get
            {
                return DocNode.ExplicitValues.IonMobility;
            }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(ExplicitIonMobility), value),
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobility(value, docNode.ExplicitValues.IonMobilityUnits)));
            }
        }

        [Importable]
        public string ExplicitIonMobilityUnits
        {
            get
            {
                return DocNode.ExplicitValues.IonMobilityUnits.ToString();
            }
            set
            {
                eIonMobilityUnits eValue;
                if (SmallMoleculeTransitionListReader.IonMobilityUnitsSynonyms.TryGetValue(string.IsNullOrEmpty(value) ? string.Empty : value.Trim(), out eValue))
                    ChangeDocNode(EditColumnDescription(nameof(ExplicitIonMobilityUnits), eValue),
                        docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobility(docNode.ExplicitValues.IonMobility, eValue)));
            }
        }

        [Obsolete("Use Transition.IonMobilityHighEnergyOffset instead")]
        public double? ExplicitIonMobilityHighEnergyOffset
        {
            get
            {
                // If all transitions have the same value, show that
                return Transitions.Any() && Transitions.All(t => Equals(t.ExplicitIonMobilityHighEnergyOffset, Transitions.First().ExplicitIonMobilityHighEnergyOffset))
                    ? Transitions.First().ExplicitIonMobilityHighEnergyOffset
                    : null;
            }
        }

        [Importable]
        public double? ExplicitCollisionalCrossSection
        {
            get
            {
                return DocNode.ExplicitValues.CollisionalCrossSectionSqA;
            }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(ExplicitCollisionalCrossSection), value),
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeCollisionalCrossSection(value)));
            }
        }

        [Importable]
        public double? PrecursorConcentration
        {
            get { return DocNode.PrecursorConcentration; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(PrecursorConcentration), value),
                    docNode=>docNode.ChangePrecursorConcentration(value));
            }
        }

        [ChildDisplayName("Library{0}")]
        public IonMobilityObject LibraryIonMobility
        {
            get
            {
                var libKey = DocNode.GetLibKey(SrmDocument.Settings, Peptide.DocNode);
                var imInfo = SrmDocument.Settings.GetIonMobilities(new[] { libKey }, null);
                var im = imInfo.GetLibraryMeasuredIonMobilityAndCCS(libKey, DocNode.PrecursorMz, null);
                if (im == null || im.IsEmpty)
                {
                    return null;
                }
                return IonMobilityObject.FromIonMobilityAndCCS(im);
            }
        }

        public string SpectrumFilter { get { return DocNode.SpectrumClassFilter.ToString(); } }

        [InvariantDisplayName("PrecursorNote")]
        [Importable]
        public string Note
        {
            get { return DocNode.Note; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(Note), value),
                    docNode => (TransitionGroupDocNode) docNode.ChangeAnnotations(docNode.Annotations
                        .ChangeNote(value)));
            }
        }

        public string LibraryName
        {
            get { return DocNode.HasLibInfo ? DocNode.LibInfo.LibraryName : null; }
        }

        public string LibraryType
        {
            get { return DocNode.LibInfo?.LibraryTypeName; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryProbabilityScore
        {
            get { return DocNode.LibInfo?.Score; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryScore1
        {
            get { return GetLibraryScore(0); }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryScore2
        {
            get { return GetLibraryScore(1); }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public double? LibraryScore3
        {
            get { return GetLibraryScore(2); }
        }

        public bool IsDecoy { get { return DocNode.IsDecoy; } }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? DecoyMzShift { get { return DocNode.TransitionGroup.DecoyMassShift; } }

        private double? GetLibraryScore(int index)
        {
            var libInfo = DocNode.LibInfo;
            if (null == libInfo)
            {
                return null;
            }
            var peptideRankId = libInfo.RankValues.Skip(index).FirstOrDefault().Key;
            if (null == peptideRankId)
            {
                return null;
            }
            return libInfo.GetRankValue(peptideRankId);
        }

        internal double GetRegressionMz()
        {
            return SrmDocument.Settings.GetRegressionMz(Peptide.DocNode, DocNode);
        }

        public override string ToString()
        {
            // Consider: maybe change TransitionGroupDocNode.ToString() to be this as well:
            return TransitionGroupTreeNode.GetLabel(DocNode.TransitionGroup, DocNode.PrecursorMz, string.Empty);
        }

        [Obsolete]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? LibraryRank { get { return null; } }

        public override string GetDeleteConfirmation(int nodeCount)
        {
            if (nodeCount == 1)
            {
                return string.Format(EntitiesResources.Precursor_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_precursor___0___, this);
            }
            return string.Format(EntitiesResources.Precursor_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__precursors_, nodeCount);
        }

        [Importable]
        public bool AutoSelectTransitions
        {
            get { return DocNode.AutoManageChildren; }
            set
            {
                if (value == AutoSelectTransitions)
                {
                    return;
                }
                ChangeDocNode(EditColumnDescription(nameof(AutoSelectTransitions), value), docNode =>
                    {
                        docNode = (TransitionGroupDocNode) docNode.ChangeAutoManageChildren(value);
                        if (docNode.AutoManageChildren)
                        {
                            var srmSettingsDiff = new SrmSettingsDiff(false, false, false, false, true, false);
                            docNode = docNode.ChangeSettings(SrmDocument.Settings, Peptide.DocNode, Peptide.DocNode.ExplicitMods,
                                srmSettingsDiff);
                        }

                        return docNode;
                    });
            }
        }

        [Format(Formats.STANDARD_RATIO)]
        public double? TargetQualitativeIonRatio
        {
            get
            {
                var calibrationCurveFitter = Peptide.GetCalibrationCurveFitter();
                return calibrationCurveFitter.GetTargetIonRatio(DocNode);
            }
        }

        [InvariantDisplayName("PrecursorLocator")]
        public string Locator { get { return GetLocator(); } }

        protected override NodeRef NodeRefPrototype
        {
            get { return PrecursorRef.PROTOTYPE; }
        }

        protected override Type SkylineDocNodeType
        {
            get { return typeof(Precursor); }
        }

        private class CachedValues : CachedValues<Precursor, ImmutableList<Transition>, IDictionary<ResultKey, PrecursorResult>> 
        {
            protected override SrmDocument GetDocument(Precursor owner)
            {
                return owner.SrmDocument;
            }

            protected override ImmutableList<Transition> CalculateValue(Precursor owner)
            {
                return ImmutableList.ValueOf(owner.DocNode.Children
                    .Select(child => new Transition(owner.DataSchema, new IdentityPath(owner.IdentityPath, child.Id))));
            }

            protected override IDictionary<ResultKey, PrecursorResult> CalculateValue1(Precursor owner)
            {
                return owner.MakeChromInfoResultsMap(owner.DocNode.Results, file => new PrecursorResult(owner, file));
            }
        }
    }

    public class PrecursorResultSummary : SkylineObject
    {
        private readonly Precursor _precursor;
        public PrecursorResultSummary(Precursor precursor, IEnumerable<PrecursorResult> results)
        {
            _precursor = precursor;
            var bestRetentionTimes = new List<double>();
            var maxFhwms = new List<double>();
            var totalAreas = new List<double>();
            var totalAreasNormalized = new List<double>();
            var totalAreasRatio = new List<double>();
            var maxHeights = new List<double>();
            var detectionQValues = new List<double>();
            foreach (var result in results)
            {
                if (result.BestRetentionTime.HasValue)
                {
                    bestRetentionTimes.Add(result.BestRetentionTime.Value);
                }
                if (result.MaxFwhm.HasValue)
                {
                    maxFhwms.Add(result.MaxFwhm.Value);
                }
                if (result.TotalArea.HasValue)
                {
                    totalAreas.Add(result.TotalArea.Value);
                }
                if (result.TotalAreaNormalized.HasValue)
                {
                    totalAreasNormalized.Add(result.TotalAreaNormalized.Value);
                }
                if (result.TotalAreaRatio.HasValue)
                {
                    totalAreasRatio.Add(result.TotalAreaRatio.Value);
                }
                if (result.MaxHeight.HasValue)
                {
                    maxHeights.Add(result.MaxHeight.Value);
                }
                if (result.DetectionQValue.HasValue)
                {
                    detectionQValues.Add(result.DetectionQValue.Value);
                }
            }
            if (bestRetentionTimes.Count > 0)
            {
                BestRetentionTime = new RetentionTimeSummary(new Statistics(bestRetentionTimes));
            }
            if (detectionQValues.Count > 0)
            {
                DetectionQValue = new DetectionQValueSummary(new Statistics(detectionQValues));
            }
            if (maxFhwms.Count > 0)
            {
                MaxFwhm = new FwhmSummary(new Statistics(maxFhwms));
            }
            if (totalAreas.Count > 0)
            {
                TotalArea = new AreaSummary(new Statistics(totalAreas));
            }
            if (totalAreasNormalized.Count > 0)
            {
                TotalAreaNormalized = new AreaNormalizedSummary(new Statistics(totalAreasNormalized));
            }
            if (totalAreasRatio.Count > 0)
            {
                TotalAreaRatio = new AreaRatioSummary(new Statistics(totalAreasRatio));
            }
            if (maxHeights.Count > 0)
            {
                MaxHeight = new AreaSummary(new Statistics(maxHeights));
            }
        }
        protected override SkylineDataSchema GetDataSchema()
        {
            return _precursor.DataSchema;
        }

        [Obsolete]
        public Precursor Precursor
        {
            get { return _precursor; }
        }

        [Obsolete]
        public string ReplicatePath { get { return @"/"; } }
        [ChildDisplayName("{0}BestRetentionTime")]
        public RetentionTimeSummary BestRetentionTime { get; private set; }
        [ChildDisplayName("{0}DetectionQValue")]
        public DetectionQValueSummary DetectionQValue { get; private set; }
        [ChildDisplayName("{0}MaxFwhm")]
        public FwhmSummary MaxFwhm { get; private set; }
        [ChildDisplayName("{0}TotalArea")]
        public AreaSummary TotalArea { get; private set; }
        [ChildDisplayName("{0}TotalAreaRatio")]
        public AreaRatioSummary TotalAreaRatio { get; private set; }
        [ChildDisplayName("{0}TotalAreaNormalized")]
        public AreaNormalizedSummary TotalAreaNormalized { get; private set; }
        [ChildDisplayName("{0}MaxHeight")]
        public AreaSummary MaxHeight { get; private set; }

        public override string ToString()
        {
            return string.Format(@"RT: {0} Area: {1}", BestRetentionTime, TotalArea); // CONSIDER: localize?
        }

    }
}
