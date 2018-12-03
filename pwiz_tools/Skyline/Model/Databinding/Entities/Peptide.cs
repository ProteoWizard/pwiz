﻿/*
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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.Model.Databinding.Collections;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [AnnotationTarget(AnnotationDef.AnnotationTarget.peptide)]
    public class Peptide : SkylineDocNode<PeptideDocNode>
    {
        private readonly CachedValue<CalibrationCurveFitter> _calibrationCurveFitter;
        private readonly CachedValue<Precursor[]> _precursors;
        private readonly CachedValue<IDictionary<ResultKey, PeptideResult>> _results;
        public Peptide(SkylineDataSchema dataSchema, IdentityPath identityPath)
            : base(dataSchema, identityPath)
        {
            _calibrationCurveFitter = CachedValue.Create(dataSchema,
                () => new CalibrationCurveFitter(GetPeptideQuantifier(), SrmDocument.Settings));
            _precursors = CachedValue.Create(dataSchema, ()=>DocNode.Children.Select(child=>new Precursor(DataSchema, new IdentityPath(IdentityPath, child.Id))).ToArray());
            _results = CachedValue.Create(dataSchema, MakeResults);
        }

        [OneToMany(ForeignKey = "Peptide")]
        [HideWhen(AncestorOfType = typeof(FoldChangeBindingSource.FoldChangeRow))]
        public IList<Precursor> Precursors
        {
            get
            {
                return _precursors.Value;
            }
        }

        [InvariantDisplayName("PeptideResults")]
        [OneToMany(ForeignKey = "Peptide", ItemDisplayName = "PeptideResult")]
        [HideWhen(AncestorOfType = typeof(FoldChangeBindingSource.FoldChangeRow))]
        public IDictionary<ResultKey, PeptideResult> Results
        {
            get { return _results.Value; }
        }

        private IDictionary<ResultKey, PeptideResult> MakeResults()
        {
            return MakeChromInfoResultsMap(DocNode.Results, file => new PeptideResult(this, file));
        }

        private bool IsSmallMolecule()
        {
            return DocNode.Peptide.IsCustomMolecule;
        }

        protected override PeptideDocNode CreateEmptyNode()
        {
            return new PeptideDocNode(new Model.Peptide(null, @"X", null, null, 0));
        }

        [HideWhen(AncestorsOfAnyOfTheseTypes = new []{typeof(Protein),typeof(FoldChangeBindingSource.FoldChangeRow)})]
        public Protein Protein
        {
            get { return new Protein(DataSchema, IdentityPath.Parent); }
        }

        [InvariantDisplayName("PeptideSequence")]
        public string Sequence
        {
            get {   return IsSmallMolecule()
                    ? TextUtil.EXCEL_NA
                    : ToString(); } 
        }

        [InvariantDisplayName("PeptideSequenceLength")]
        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? SequenceLength
        {
            get
            {
                if (IsSmallMolecule())
                    return null;
                return Sequence.Length;
            }
        }

        [InvariantDisplayName("PeptideModifiedSequence")]
        [ChildDisplayName("PeptideModifiedSequence{0}")]
        public ModifiedSequence ModifiedSequence
        {
            get
            {
                return ModifiedSequence.GetModifiedSequence(SrmDocument.Settings, DocNode, IsotopeLabelType.light);
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
                    return DocNode.CustomMolecule.Formula ?? string.Empty;
                }
                else
                {
                    var molecule = RefinementSettings.ConvertToSmallMolecule(RefinementSettings.ConvertToSmallMoleculesMode.formulas, SrmDocument, DocNode);
                    return molecule.Formula ?? string.Empty;
                }
            }
        }

        [DataGridViewColumnType(typeof(StandardTypeDataGridViewColumn))]
        [Importable]
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
                    throw new InvalidOperationException(Resources.Peptide_StandardType_iRT_standards_can_only_be_changed_by_modifying_the_iRT_calculator);
                }
                ModifyDocument(EditDescription.SetColumn(@"StandardType", value).ChangeElementRef(GetElementRef()),
                    doc => doc.ChangeStandardType(value, new[]{IdentityPath}));
            }
        }

        public char PreviousAa
        {
            get { return DocNode.Peptide.PrevAA; }
        }

        public char NextAa
        {
            get { return DocNode.Peptide.NextAA; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? BeginPos
        {
            get { return DocNode.Peptide.Begin; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
        public int? EndPos
        {
            get { return DocNode.Peptide.End - 1; }
        }

        [Format(NullValue = TextUtil.EXCEL_NA)]
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
                ChangeDocNode(EditDescription.SetColumn(@"ExplicitRetentionTime", value),
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
                    ChangeDocNode(EditDescription.SetColumn(@"ExplicitRetentionTimeWindow", value),
                        docNode=>docNode.ChangeExplicitRetentionTime(new ExplicitRetentionTimeInfo(docNode.ExplicitRetentionTime.RetentionTime, value)));
                }
            }
        }

        [DataGridViewColumnType(typeof(NormalizationMethodDataGridViewColumn))]
        [Importable]
        public NormalizationMethod NormalizationMethod
        {
            get { return DocNode.NormalizationMethod; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"NormalizationMethod", value),
                    docNode=>docNode.ChangeNormalizationMethod(value));
            }
        }

        [InvariantDisplayName("PeptideNote")]
        [Importable]
        public string Note
        {
            get { return DocNode.Note; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"PeptideNote", value),
                    docNode=>(PeptideDocNode) docNode.ChangeAnnotations(docNode.Annotations.ChangeNote(value)));
            }
        }

        private RetentionTimeRegression GetRetentionTimeRegression()
        {
            return SrmDocument.Settings.PeptideSettings.Prediction.RetentionTime;
        }

        public override string ToString()
        {
            var peptide = DocNode.Peptide;
            return peptide.IsCustomMolecule
                ? DocNode.CustomMolecule.ToString()
                : peptide.Target.Sequence;
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

        [InvariantDisplayName("PeptideLocator")]
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
                ChangeDocNode(EditDescription.SetColumn(@"InternalStandardConcentration", value),
                    docNode=>docNode.ChangeInternalStandardConcentration(value));
            }
        }

        [Importable]
        public double? ConcentrationMultiplier
        {
            get { return DocNode.ConcentrationMultiplier; }
            set
            {
                ChangeDocNode(EditDescription.SetColumn(@"ConcentrationMultiplier", value),
                    docNode => docNode.ChangeConcentrationMultiplier(value));
            }
        }

        public LinkValue<CalibrationCurve> CalibrationCurve
        {
            get
            {
                CalibrationCurveFitter curveFitter = GetCalibrationCurveFitter();
                CalibrationCurve calibrationCurve = curveFitter.GetCalibrationCurve();
                return new LinkValue<CalibrationCurve>(calibrationCurve, (sender, args) =>
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
                            Settings.Default.CalibrationCurveOptions.SingleReplicate)
                        {
                            Settings.Default.CalibrationCurveOptions.SingleReplicate = false;
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
            var quantifier = PeptideQuantifier.GetPeptideQuantifier(()=>DataSchema.GetReplicateSummaries().GetNormalizationData(), 
                SrmDocument.Settings, Protein.DocNode, DocNode);
            return quantifier;
        }

        public CalibrationCurveFitter GetCalibrationCurveFitter()
        {
            return _calibrationCurveFitter.Value;
        }

        public override string GetDeleteConfirmation(int nodeCount)
        {
            if (nodeCount == 1)
            {
                return string.Format(Resources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_the_peptide___0___, this);
            }
            return string.Format(Resources.Peptide_GetDeleteConfirmation_Are_you_sure_you_want_to_delete_these__0__peptides_, nodeCount);
        }

        // Small molecule IDs (in PREFERRED_ACCESSION_TYPE_ORDER) - keep these at end
        public string InChiKey
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetInChiKey() ?? string.Empty : string.Empty; }
        }

        public string CAS
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetCAS() ?? string.Empty : string.Empty; }
        }

        public string HMDB
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetHMDB() ?? string.Empty : string.Empty; }
        }

        public string InChI
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetInChI() ?? string.Empty : string.Empty; }
        }

        public string SMILES
        {
            get { return IsSmallMolecule() ? DocNode.CustomMolecule.AccessionNumbers.GetSMILES() ?? string.Empty : string.Empty; }
        }

        protected override NodeRef NodeRefPrototype
        {
            get { return MoleculeRef.PROTOTYPE; }
        }
    }
}
