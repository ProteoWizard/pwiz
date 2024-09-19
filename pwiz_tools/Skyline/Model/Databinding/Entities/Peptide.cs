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
using System.IO;
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.peptide)]
    [ProteomicDisplayName("Peptide")]
    [InvariantDisplayName("Molecule")]
    public class Peptide : SkylineDocNode<PeptideDocNode>
    {
        private readonly CachedValues _cachedValues = new CachedValues();
        public Peptide(SkylineDataSchema dataSchema, IdentityPath identityPath)
            : base(dataSchema, identityPath)
        {
        }

        [OneToMany(ForeignKey = "Peptide")]
        [HideWhen(AncestorOfType = typeof(FoldChangeBindingSource.FoldChangeRow))]
        public IList<Precursor> Precursors
        {
            get
            {
                return _cachedValues.GetValue1(this);
            }
        }

        [ProteomicDisplayName("PeptideResults")]
        [InvariantDisplayName("MoleculeResults")]
        [OneToMany(ForeignKey = "Peptide")]
        [HideWhen(AncestorOfType = typeof(FoldChangeBindingSource.FoldChangeRow))]
        public IDictionary<ResultKey, PeptideResult> Results
        {
            get { return _cachedValues.GetValue2(this); }
        }

        private IDictionary<ResultKey, PeptideResult> MakeResults()
        {
            return MakeChromInfoResultsMap(DocNode.Results, file => new PeptideResult(this, file));
        }

        public bool IsSmallMolecule()
        {
            return DocNode.Peptide.IsCustomMolecule;
        }

        protected override PeptideDocNode CreateEmptyNode()
        {
            return new PeptideDocNode(new Model.Peptide(null, @"X", null, null, 0));
        }

        [HideWhen(AncestorsOfAnyOfTheseTypes = new []{typeof(Protein),typeof(FoldChangeBindingSource.FoldChangeRow)})]
        [InvariantDisplayName("MoleculeList", ExceptInUiMode = UiModes.PROTEOMIC)]
        public Protein Protein
        {
            get { return new Protein(DataSchema, IdentityPath.Parent); }
        }

        [InvariantDisplayName("PeptideSequence")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public string Sequence
        {
            get {   return IsSmallMolecule()
                    ? TextUtil.EXCEL_NA
                    : ToString(); } 
        }

        [InvariantDisplayName("PeptideSequenceLength")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? SequenceLength
        {
            get
            {
                if (IsSmallMolecule())
                    return null;
                return Sequence.Replace(@"-", string.Empty).Length;
            }
        }

        [InvariantDisplayName("PeptideModifiedSequence")]
        [ChildDisplayName("PeptideModifiedSequence{0}")]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public ProteomicSequence ModifiedSequence
        {
            get
            {
                return ProteomicSequence.GetProteomicSequence(SrmDocument.Settings, DocNode, IsotopeLabelType.light);
            }
        }

        [Obsolete("use MoleculeName instead")]  // It's a molecule, not an ion
        public string IonName 
        {
            get
            {
                return MoleculeName;
            }
        }

        [Obsolete("use MoleculeFormula instead")]  // It's a molecule, not an ion
        public string IonFormula
        {
            get
            {
                return MoleculeFormula;
            }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string MoleculeName
        {
            get
            {
                if (IsSmallMolecule())
                {
                    return DocNode.CustomMolecule.Name ?? string.Empty;
                }
                else
                {
                    var molecule = RefinementSettings.ConvertToSmallMolecule(RefinementSettings.ConvertToSmallMoleculesMode.formulas, SrmDocument, DocNode);
                    return molecule.InvariantName ?? string.Empty;
                }
            }
        }

        public string MoleculeFormula
        {
            get
            {
                if (IsSmallMolecule())
                {
                    return DocNode.CustomMolecule.HasChemicalFormula ? DocNode.CustomMolecule.Formula : string.Empty;
                }
                else
                {
                    var crosslinkBuilder = new CrosslinkBuilder(SrmDocument.Settings, DocNode.Peptide,
                        DocNode.ExplicitMods, IsotopeLabelType.light);
                    return crosslinkBuilder.GetPrecursorFormula().Molecule.ToString();
                }
            }
        }

        [DataGridViewColumnType(typeof(StandardTypeDataGridViewColumn))]
        [Importable(Formatter = typeof(StandardType.PropertyFormatter))]
        public StandardType StandardType
        {
            get
            {
                return DocNode.GlobalStandardType;
            }
            set
            {
                if (StandardType == value)
                {
                    return;
                }
                if (StandardType == StandardType.IRT || value == StandardType.IRT)
                {
                    throw new InvalidOperationException(EntitiesResources.Peptide_StandardType_iRT_standards_can_only_be_changed_by_modifying_the_iRT_calculator);
                }
                ModifyDocument(EditColumnDescription(nameof(StandardType), value).ChangeElementRef(GetElementRef()),
                    doc => doc.ChangeStandardType(value, new[]{IdentityPath}));
            }
        }

        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public char PreviousAa
        {
            get { return DocNode.Peptide.PrevAA; }
        }

        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public char NextAa
        {
            get { return DocNode.Peptide.NextAA; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public int? FirstPosition
        {
            get
            {
                return DocNode.Peptide.Begin + 1;
            }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public int? LastPosition
        {
            get
            {
                return DocNode.Peptide.End;
            }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        [Obsolete]
        public int? BeginPos
        {
            get { return DocNode.Peptide.Begin; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        [Obsolete]
        public int? EndPos
        {
            get { return DocNode.Peptide.End - 1; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        [Hidden(InUiMode = UiModes.SMALL_MOLECULES)]
        public int MissedCleavages
        {
            get { return DocNode.Peptide.MissedCleavages; }
        }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? RetentionTimeCalculatorScore
        {
            get
            {
                var retentionTimeRegression = GetRetentionTimeRegression();
                if (retentionTimeRegression == null)
                {
                    return null;
                }
                return retentionTimeRegression.Calculator.ScoreSequence(SrmDocument.Settings.GetSourceTarget(DocNode));
            }
        }

        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? PredictedRetentionTime
        {
            get
            {
                var retentionTimeRegression = GetRetentionTimeRegression();
                if (null == retentionTimeRegression)
                {
                    return null;
                }
                return retentionTimeRegression.GetRetentionTime(SrmDocument.Settings.GetSourceTarget(DocNode));
            }
        }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? AverageMeasuredRetentionTime 
        {
            get { return DocNode.AverageMeasuredRetentionTime; }
        }

        [Format(Formats.RETENTION_TIME)]
        [Importable]
        public double? ExplicitRetentionTime
        {
            get
            {
                if (DocNode.ExplicitRetentionTime != null)
                   return DocNode.ExplicitRetentionTime.RetentionTime;
                return null;
            }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(ExplicitRetentionTime), value),
                    docNode=>docNode.ChangeExplicitRetentionTime(value));
            }
        }

        [Format(Formats.RETENTION_TIME)]
        [Importable]
        public double? ExplicitRetentionTimeWindow
        {
            get
            {
                if (DocNode.ExplicitRetentionTime != null)
                    return DocNode.ExplicitRetentionTime.RetentionTimeWindow;
                return null;
            }
            set
            {
                if (DocNode.ExplicitRetentionTime == null)
                {
                    // Can't set window without retention time
                    if (value.HasValue)
                        throw new InvalidDataException(Resources.Peptide_ExplicitRetentionTimeWindow_Explicit_retention_time_window_requires_an_explicit_retention_time_value_);
                }
                else
                {
                    ChangeDocNode(EditColumnDescription(nameof(ExplicitRetentionTimeWindow), value),
                        docNode=>docNode.ChangeExplicitRetentionTime(new ExplicitRetentionTimeInfo(docNode.ExplicitRetentionTime.RetentionTime, value)));
                }
            }
        }

        [DataGridViewColumnType(typeof(NormalizationMethodDataGridViewColumn))]
        [Importable(Formatter = typeof(NormalizationMethod.PropertyFormatter))]
        public NormalizationMethod NormalizationMethod
        {
            get { return DocNode.NormalizationMethod; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(NormalizationMethod), value),
                    docNode=>docNode.ChangeNormalizationMethod(value));
            }
        }


        [DataGridViewColumnType(typeof(SurrogateStandardDataGridViewColumn))]
        [Importable]
        public string SurrogateExternalStandard
        {
            get { return DocNode.SurrogateCalibrationCurve; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(SurrogateExternalStandard), value),
                    docNode => docNode.ChangeSurrogateCalibrationCurve(value));
            }
        }

        [ProteomicDisplayName("PeptideNote")]
        [InvariantDisplayName("MoleculeNote")]
        [Importable]
        public string Note
        {
            get { return DocNode.Note; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(Note), value),
                    docNode=>(PeptideDocNode) docNode.ChangeAnnotations(docNode.Annotations.ChangeNote(value)));
            }
        }

        private RetentionTimeRegression GetRetentionTimeRegression()
        {
            return SrmDocument.Settings.PeptideSettings.Prediction.RetentionTime;
        }

        public override string ToString()
        {
            if (DocNode.Peptide.IsCustomMolecule)
            {
                return DocNode.CustomMolecule.ToString();
            }

            return DocNode.GetCrosslinkedSequence();
        }

        [InvariantDisplayName("PeptideDocumentLocation")]
        [Obsolete]
        public DocumentLocation DocumentLocation
        {
            get
            {
                return new DocumentLocation(IdentityPath.ToGlobalIndexList());
            }
        }

        [ProteomicDisplayName("PeptideLocator")]
        [InvariantDisplayName("MoleculeLocator")]
        public string Locator
        {
            get { return GetLocator(); }
        }

        [Importable]
        public double? InternalStandardConcentration
        {
            get { return DocNode.InternalStandardConcentration; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(InternalStandardConcentration), value),
                    docNode=>docNode.ChangeInternalStandardConcentration(value));
            }
        }

        [Importable]
        public double? ConcentrationMultiplier
        {
            get { return DocNode.ConcentrationMultiplier; }
            set
            {
                ChangeDocNode(EditColumnDescription(nameof(ConcentrationMultiplier), value),
                    docNode => docNode.ChangeConcentrationMultiplier(value));
            }
        }

        public LinkValue<CalibrationCurveMetrics> CalibrationCurve
        {
            get
            {
                CalibrationCurveFitter curveFitter = GetCalibrationCurveFitter();
                var calibrationCurve = curveFitter.GetCalibrationCurveMetrics();
                return new LinkValue<CalibrationCurveMetrics>(calibrationCurve, (sender, args) =>
                {
                    if (null == DataSchema.SkylineWindow)
                    {
                        return;
                    }
                    DataSchema.SkylineWindow.SelectedPath = IdentityPath;
                    var calibrationForm = DataSchema.SkylineWindow.ShowCalibrationForm();
                    if (calibrationForm != null)
                    {
                        if (DocNode.HasPrecursorConcentrations &&
                            Settings.Default.CalibrationCurveOptions.SingleBatch)
                        {
                            Settings.Default.CalibrationCurveOptions = Settings.Default.CalibrationCurveOptions.ChangeSingleBatch(false);
                            calibrationForm.UpdateUI(false);
                        }
                    }
                });
            }
        }

        public FiguresOfMerit FiguresOfMerit
        {
            get
            {
                CalibrationCurveFitter calibrationCurveFitter = GetCalibrationCurveFitter();
                var calibrationCurve = calibrationCurveFitter.GetCalibrationCurve();
                return calibrationCurveFitter.GetFiguresOfMerit(calibrationCurve);
            }
        }

        public PeptideQuantifier GetPeptideQuantifier()
        {
            var quantifier = PeptideQuantifier.GetPeptideQuantifier(DataSchema.LazyNormalizationData, 
                SrmDocument.Settings, Protein.DocNode.PeptideGroup, DocNode);
            return quantifier;
        }

        public CalibrationCurveFitter GetCalibrationCurveFitter()
        {
            return _cachedValues.GetValue(this);
        }

        public override string GetDeleteConfirmation(int nodeCount)
        {
            if (nodeCount == 1)
            {
                return string.Format(DataSchema.ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic
                    ? EntitiesResources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_peptide___0___
                    : EntitiesResources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_molecule___0___, this);
            }
            return string.Format(
                DataSchema.ModeUI == SrmDocument.DOCUMENT_TYPE.proteomic
                ? Resources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__peptides_
                : EntitiesResources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__molecules_, nodeCount);
        }

        // Small molecule IDs (in PREFERRED_ACCESSION_TYPE_ORDER) - keep these at end
        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string InChiKey
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetInChiKey() ?? string.Empty : string.Empty; }
        }

        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string CAS
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetCAS() ?? string.Empty : string.Empty; }
        }

        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string HMDB
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetHMDB() ?? string.Empty : string.Empty; }
        }

        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string InChI
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetInChI() ?? string.Empty : string.Empty; }
        }

        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string SMILES
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetSMILES() ?? string.Empty : string.Empty; }
        }

        [Hidden(InUiMode = UiModes.PROTEOMIC)]
        public string KEGG
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetKEGG() ?? string.Empty : string.Empty; }
        }

        [Importable]
        public bool AutoSelectPrecursors
        {
            get { return DocNode.AutoManageChildren; }
            set
            {
                if (value == AutoSelectPrecursors)
                {
                    return;
                }
                ChangeDocNode(EditDescription.SetColumn(nameof(AutoSelectPrecursors), value), docNode =>
                {
                    docNode = (PeptideDocNode) docNode.ChangeAutoManageChildren(value);
                    if (docNode.AutoManageChildren)
                    {
                        var srmSettingsDiff = new SrmSettingsDiff(false, false, true, false, false, false);
                        docNode = docNode.ChangeSettings(SrmDocument.Settings, srmSettingsDiff);
                    }
                    return docNode;
                });
            }
        }

        [Importable]
        public string AttributeGroupId
        {
            get { return DocNode.AttributeGroupId; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(nameof(AttributeGroupId), value), docNode=>docNode.ChangeAttributeGroupId(value));
            }
        }

        protected override NodeRef NodeRefPrototype
        {
            get { return MoleculeRef.PROTOTYPE; }
        }

        protected override Type SkylineDocNodeType
        {
            get { return typeof(Peptide); }
        }

        private class CachedValues : CachedValues<Peptide, CalibrationCurveFitter, ImmutableList<Precursor>,
            IDictionary<ResultKey, PeptideResult>>
        {
            protected override SrmDocument GetDocument(Peptide owner)
            {
                return owner.SrmDocument;
            }

            protected override CalibrationCurveFitter CalculateValue(Peptide owner)
            {
                return CalibrationCurveFitter.GetCalibrationCurveFitter(owner.DataSchema.LazyNormalizationData,
                    owner.SrmDocument.Settings,
                    new IdPeptideDocNode(owner.Protein.DocNode.PeptideGroup, owner.DocNode));
            }

            protected override ImmutableList<Precursor> CalculateValue1(Peptide owner)
            {
                return ImmutableList.ValueOf(owner.DocNode.Children.Select(child =>
                    new Precursor(owner.DataSchema, new IdentityPath(owner.IdentityPath, child.Id))));
            }

            protected override IDictionary<ResultKey, PeptideResult> CalculateValue2(Peptide owner)
            {
                return owner.MakeResults();
            }
        }
    }
}
