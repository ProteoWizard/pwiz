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
using pwiz.Common.DataBinding.Attributes;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.precursor)]
    public class Precursor : SkylineDocNode<TransitionGroupDocNode>
    {
        private readonly Lazy<Peptide> _peptide;
        private readonly CachedValue<Transition[]> _transitions;
        private readonly CachedValue<IDictionary<ResultKey, PrecursorResult>> _results;
        public Precursor(SkylineDataSchema dataSchema, IdentityPath identityPath) : base(dataSchema, identityPath)
        {
            _peptide = new Lazy<Peptide>(() => new Peptide(DataSchema, IdentityPath.Parent));
            _transitions = CachedValue.Create(dataSchema, () => DocNode.Children
                .Select(child => new Transition(DataSchema, new IdentityPath(IdentityPath, child.Id))).ToArray());
            _results = CachedValue.Create(dataSchema, MakeResults);
        }

        [HideWhen(AncestorOfType = typeof(Peptide))]
        public Peptide Peptide
        {
            get { return _peptide.Value; }
        }

        [OneToMany(ForeignKey = "Precursor")]
        public IList<Transition> Transitions
        {
            get
            {
                return _transitions.Value;
            }
        }

        [InvariantDisplayName("PrecursorResults")]
        [OneToMany(ForeignKey = "Precursor", ItemDisplayName = "PrecursorResult")]
        public IDictionary<ResultKey, PrecursorResult> Results
        {
            get { return _results.Value; }
        }

        private IDictionary<ResultKey, PrecursorResult> MakeResults()
        {
            return MakeChromInfoResultsMap(DocNode.Results, file => new PrecursorResult(this, file));
        }

        protected override TransitionGroupDocNode CreateEmptyNode()
        {
            return new TransitionGroupDocNode(new TransitionGroup(new Model.Peptide(null, "X", null, null, 0), Util.Adduct.SINGLY_PROTONATED, IsotopeLabelType.light), null); // Not L10N
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
                    Adduct adduct;
                    var molecule = RefinementSettings.ConvertToSmallMolecule(RefinementSettings.ConvertToSmallMoleculesMode.formulas, SrmDocument, parent, out adduct, DocNode.TransitionGroup.PrecursorAdduct.AdductCharge, DocNode.TransitionGroup.LabelType);
                    return molecule.InvariantName ?? string.Empty;
                }
            }
        }

        [InvariantDisplayName("PrecursorIonFormula")]
        public string IonFormula
        {
            get
            {
                if (IsSmallMolecule())
                {
                    return (DocNode.CustomMolecule.Formula ?? string.Empty);
                }
                else
                {
                    PeptideDocNode parent = DataSchema.Document.FindNode(IdentityPath.Parent) as PeptideDocNode;
                    if (parent == null)
                    {
                        return string.Empty;
                    }
                    Adduct adduct;
                    var molecule = RefinementSettings.ConvertToSmallMolecule(RefinementSettings.ConvertToSmallMoleculesMode.formulas, SrmDocument, parent, out adduct, DocNode.TransitionGroup.PrecursorAdduct.AdductCharge, DocNode.TransitionGroup.LabelType);
                    return molecule.Formula ?? string.Empty;
                }
            }
        }

        [InvariantDisplayName("PrecursorNeutralFormula")]
        public string NeutralFormula
        {
            get
            {
                if (IsSmallMolecule())
                {
                    return DocNode.CustomMolecule.Formula ?? string.Empty;
                }
                else
                {
                    var parent = DataSchema.Document.FindNode(IdentityPath.Parent) as PeptideDocNode;
                    var molecule = RefinementSettings.ConvertToSmallMolecule(RefinementSettings.ConvertToSmallMoleculesMode.formulas, SrmDocument, parent);
                    return molecule.Formula ?? string.Empty;
                }
            }
        }

        [InvariantDisplayName("PrecursorAdduct")]
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

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double CollisionEnergy
        {
            get
            {
                // Note this is the predicited CE, explicit CE has its own display column
                return SrmDocument.Settings.TransitionSettings.Prediction.CollisionEnergy
                                  .GetCollisionEnergy(DocNode.PrecursorAdduct, GetRegressionMz());
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
        public ModifiedSequence ModifiedSequence
        {
            get
            {
                return ModifiedSequence.GetModifiedSequence(SrmDocument.Settings, Peptide.DocNode, IsotopeLabelType);
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? ExplicitCollisionEnergy
        {
            get
            {
                return DocNode.ExplicitValues.CollisionEnergy;
            }
            set
            {
                var values = DocNode.ExplicitValues.ChangeCollisionEnergy(value);
                ChangeDocNode(EditDescription.SetColumn("ExplicitCollisionEnergy", value), // Not L10N
                    docNode=>docNode.ChangeExplicitValues(values));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? ExplicitSLens
        {
            get
            {
                return DocNode.ExplicitValues.SLens;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitSLens", value), // Not L10N
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeSLens(value)));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? ExplicitConeVoltage
        {
            get
            {
                return DocNode.ExplicitValues.ConeVoltage;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitConeVoltage", value), // Not L10N
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeConeVoltage(value)));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? ExplicitDeclusteringPotential
        {
            get
            {
                return DocNode.ExplicitValues.DeclusteringPotential;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitDeclusteringPotential", value), // Not L10N
                    docNode => docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeDeclusteringPotential(value)));
            }
        }

        [Format(Formats.OPT_PARAMETER, NullValue = TextUtil.EXCEL_NA)]
        public double? ExplicitCompensationVoltage
        {
            get
            {
                return DocNode.ExplicitValues.CompensationVoltage;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitCompensationVoltage", value), // Not L10N
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeCompensationVoltage(value)));
            }
        }

        [Obsolete("use ExplicitIonMobility instead")] 
        public double? ExplicitDriftTimeMsec // Backward compatibility, the more general "ExplicitIonMobility" is preferred
        {
            get
            {
                return DocNode.ExplicitValues.IonMobilityUnits == MsDataFileImpl.eIonMobilityUnits.drift_time_msec ? DocNode.ExplicitValues.IonMobility : null;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitDriftTimeMsec", value), // Not L10N
                    docNode => docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobility(value, MsDataFileImpl.eIonMobilityUnits.drift_time_msec)));
            }
        }

        [Obsolete("use IonMobilityHighEnergyOffset instead")] 
        public double? ExplicitDriftTimeHighEnergyOffsetMsec  // Backward compatibility, the more general "ExplicitIonMobility" is preferred
        {
            get
            {
                return DocNode.ExplicitValues.IonMobilityUnits == MsDataFileImpl.eIonMobilityUnits.drift_time_msec ? DocNode.ExplicitValues.IonMobilityHighEnergyOffset : null;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitDriftTimeHighEnergyOffsetMsec", value), // Not L10N
                    docNode => docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobilityHighEnergyOffset(value)));
            }
        }

        public double? ExplicitIonMobility
        {
            get
            {
                return DocNode.ExplicitValues.IonMobility;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitIonMobility", value), // Not L10N
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobility(value, docNode.ExplicitValues.IonMobilityUnits)));
            }
        }

        public string ExplicitIonMobilityUnits
        {
            get
            {
                return DocNode.ExplicitValues.IonMobilityUnits.ToString();
            }
            set
            {
                MsDataFileImpl.eIonMobilityUnits eValue;
                if (SmallMoleculeTransitionListReader.IonMobilityUnitsSynonyms.TryGetValue(value.Trim(), out eValue))
                    ChangeDocNode(EditDescription.SetColumn("ExplicitIonMobilityUnits", eValue), // Not L10N
                        docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobility(docNode.ExplicitValues.IonMobility, eValue)));
            }
        }

        public double? ExplicitIonMobilityHighEnergyOffset
        {
            get
            {
                return DocNode.ExplicitValues.IonMobilityHighEnergyOffset;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitIonMobilityHighEnergyOffset", value), // Not L10N
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeIonMobilityHighEnergyOffset(value)));
            }
        }
        
        public double? ExplicitCollisionalCrossSection
        {
            get
            {
                return DocNode.ExplicitValues.CollisionalCrossSectionSqA;
            }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("ExplicitCollisionalCrossSection", value), // Not L10N
                    docNode=>docNode.ChangeExplicitValues(docNode.ExplicitValues.ChangeCollisionalCrossSection(value)));
            }
        }

        [InvariantDisplayName("PrecursorNote")]
        public string Note
        {
            get { return DocNode.Note; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn("PrecursorNote", value), // Not L10N
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
            get
            {
                if (DocNode.LibInfo is NistSpectrumHeaderInfo)
                {
                    return "NIST"; // Not L10N
                }
                if (DocNode.LibInfo is XHunterSpectrumHeaderInfo)
                {
                    return "GPM"; // Not L10N
                }
                if (DocNode.LibInfo is BiblioSpecSpectrumHeaderInfo)
                {
                    return "BiblioSpec"; // Not L10N
                }
                return null;
            }
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
                return string.Format(Resources.Precursor_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_precursor___0___, this);
            }
            return string.Format(Resources.Precursor_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__precursors_, nodeCount);
        }

        [InvariantDisplayName("PrecursorLocator")]
        public string Locator { get { return GetLocator(); } }

        protected override NodeRef NodeRefPrototype
        {
            get { return PrecursorRef.PROTOTYPE; }
        }
    }

    public class PrecursorResultSummary : SkylineObject
    {
        public PrecursorResultSummary(Precursor precursor, IEnumerable<PrecursorResult> results)
            : base(precursor.DataSchema)
        {
#pragma warning disable 612
            Precursor = precursor;
#pragma warning restore 612
            var bestRetentionTimes = new List<double>();
            var maxFhwms = new List<double>();
            var totalAreas = new List<double>();
            var totalAreasNormalized = new List<double>();
            var totalAreasRatio = new List<double>();
            var maxHeights = new List<double>();
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
            }
            if (bestRetentionTimes.Count > 0)
            {
                BestRetentionTime = new RetentionTimeSummary(new Statistics(bestRetentionTimes));
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

        [Obsolete]
        public Precursor Precursor { get; private set; }
        [Obsolete]
        public string ReplicatePath { get { return "/"; } } // Not L10N
        [ChildDisplayName("{0}BestRetentionTime")]
        public RetentionTimeSummary BestRetentionTime { get; private set; }
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
            return string.Format("RT: {0} Area: {1}", BestRetentionTime, TotalArea); // Not L10N?
        }

    }
}
