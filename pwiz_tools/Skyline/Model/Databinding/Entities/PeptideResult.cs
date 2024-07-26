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
using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    [ProteomicDisplayName("PeptideResult")]
    [InvariantDisplayName("MoleculeResult")]
    public class PeptideResult : Result
    {
        private readonly CachedValues _cachedValues = new CachedValues();

        public PeptideResult(Peptide peptide, ResultFile file) : base(peptide, file)
        {
        }

        [HideWhen(AncestorOfType = typeof(Peptide))]
        [Hidden]
        [InvariantDisplayName("Molecule", ExceptInUiMode = UiModes.PROTEOMIC)]
        public Peptide Peptide { get { return SkylineDocNode as Peptide; } }

        [Browsable(false)]
        public PeptideChromInfo ChromInfo { get { return _cachedValues.GetChromInfo(this); } }
        [Format(Formats.PEAK_FOUND_RATIO, NullValue = TextUtil.EXCEL_NA)]
        [InvariantDisplayName("MoleculePeakFoundRatio", ExceptInUiMode = UiModes.PROTEOMIC)]
        public double PeptidePeakFoundRatio { get { return ChromInfo.PeakCountRatio; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        [InvariantDisplayName("MoleculeRetentionTime", ExceptInUiMode = UiModes.PROTEOMIC)]
        public double? PeptideRetentionTime { get { return ChromInfo.RetentionTime; } }
        
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? PredictedResultRetentionTime
        {
            get
            {
                var peptidePrediction = SrmDocument.Settings.PeptideSettings.Prediction;
                if (peptidePrediction.RetentionTime != null)
                {
                    var textId = SrmDocument.Settings.GetSourceTarget(Peptide.DocNode);
                    double? scoreCalc = peptidePrediction.RetentionTime.Calculator.ScoreSequence(textId);
                    if (scoreCalc.HasValue)
                    {
                        return peptidePrediction.RetentionTime.GetRetentionTime(scoreCalc.Value, ResultFile.ChromFileInfoId);
                    }
                }
                return null;
            }
        }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? RatioToStandard
        {
            get 
            {
                var mods = SrmDocument.Settings.PeptideSettings.Modifications;
                var standardType = mods.RatioInternalStandardTypes.FirstOrDefault();
                var labelType = mods.GetModificationTypes().Except(new[]{standardType}).FirstOrDefault();
                foreach (var labelRatio in ChromInfo.LabelRatios)
                {
                    if (ReferenceEquals(labelType, labelRatio.LabelType) &&
                            ReferenceEquals(standardType, labelRatio.StandardType))
                        return RatioValue.GetRatio(labelRatio.Ratio);
                }
                return null;
            }
        }
        public bool BestReplicate { get { return ResultFile.Replicate.ReplicateIndex == Peptide.DocNode.BestResult; } }
        public override string ToString()
        {
            return string.Format(@"RT: {0:0.##}", ChromInfo.RetentionTime);
        }

        [Format(Formats.PEAK_AREA_NORMALIZED)]
        public double? ModifiedAreaProportion
        {
            get
            {
                var unmodifiedSequence = Peptide.DocNode.RawUnmodifiedTextId;
                var matchingPeptides = Peptide.Protein.Peptides
                    .Where(pep => unmodifiedSequence == pep.DocNode.RawUnmodifiedTextId);
                return GetAreaProportion(matchingPeptides);
            }
        }

        [Format(Formats.PEAK_AREA_NORMALIZED)]
        public double? AttributeAreaProportion
        {
            get
            {
                if (string.IsNullOrEmpty(Peptide.AttributeGroupId))
                {
                    return null;
                }
                var matchingPeptides = SrmDocument.MoleculeGroups.SelectMany(moleculeGroup =>
                    moleculeGroup.Molecules.Where(molecule => molecule.AttributeGroupId == Peptide.AttributeGroupId)
                        .Select(molecule => new Peptide(DataSchema, new IdentityPath(moleculeGroup.Id, molecule.Id))));
                return GetAreaProportion(matchingPeptides);
            }
        }

        private double? GetAreaProportion(IEnumerable<Peptide> peptides)
        {
            var normalizedArea = GetQuantificationResult()?.NormalizedArea?.Strict;
            if (normalizedArea == null)
            {
                return null;
            }
            var total = 0.0;
            foreach (var peptide in peptides)
            {
                foreach (var peptideResult in peptide.Results.Values)
                {
                    if (peptideResult.ResultFile.Replicate.Name == ResultFile.Replicate.Name)
                    {
                        total += peptideResult.GetQuantificationResult()?.NormalizedArea?.Strict ?? 0.0;
                    }
                }
            }

            return normalizedArea / total;
        }

        [HideWhen(AncestorOfType = typeof(Peptide))]
        public ResultFile ResultFile { get { return GetResultFile(); } }

        [HideWhen(AncestorOfType = typeof(Peptide))]
        public ProteinResult ProteinResult
        {
            get { return new ProteinResult(Peptide.Protein, ResultFile.Replicate); }
        }

        [Obsolete]
        [InvariantDisplayName("PeptideResultDocumentLocation")]
        public DocumentLocation DocumentLocation
        {
            get
            {
                return new DocumentLocation(Peptide.IdentityPath.ToGlobalIndexList()).SetChromFileId(ResultFile.ChromFileInfoId.GlobalIndex);
            }
        }

        public void ChangeChromInfo(EditDescription editDescription, Func<PeptideChromInfo, PeptideChromInfo> newChromInfo)
        {
            Peptide.ChangeDocNode(editDescription, docNode=>docNode.ChangeResults(GetResultFile().ChangeChromInfo(docNode.Results, newChromInfo)));
        }

        [Importable]
        public bool ExcludeFromCalibration
        {
            get { return ChromInfo.ExcludeFromCalibration; }
            set
            {
                ChangeChromInfo(EditColumnDescription(nameof(ExcludeFromCalibration), value),
                    chromInfo => chromInfo.ChangeExcludeFromCalibration(value));
            }
        }

        public QuantificationResult GetQuantificationResult()
        {
            return _cachedValues.GetQuantificationResult(this);
        }

        public LinkValue<QuantificationResult> Quantification
        {
            get
            {
                return new LinkValue<QuantificationResult>(GetQuantificationResult(), (sender, args) =>
                {
                    SkylineWindow skylineWindow = DataSchema.SkylineWindow;
                    if (skylineWindow != null)
                    {
                        skylineWindow.ShowCalibrationForm();
                        skylineWindow.SelectedResultsIndex = ResultFile.Replicate.ReplicateIndex;
                        skylineWindow.SelectedPath = Peptide.IdentityPath;
                    }
                });
            }
        }

        public CalibrationCurveFitter GetCalibrationCurveFitter()
        {
            return _cachedValues.GetCalibrationCurveFitter(this);
        }

        public override ElementRef GetElementRef()
        {
            return MoleculeResultRef.PROTOTYPE.ChangeChromInfo(ResultFile.Replicate.ChromatogramSet, ChromInfo)
                .ChangeParent(Peptide.GetElementRef());
        }

        [ChildDisplayName("Replicate{0}")]
        public LinkValue<CalibrationCurveMetrics> ReplicateCalibrationCurve
        {
            get
            {
                var curveFitter = GetCalibrationCurveFitter();
                return new LinkValue<CalibrationCurveMetrics>(curveFitter.GetCalibrationCurveMetrics(), (sender, args) =>
                {
                    if (null == DataSchema.SkylineWindow)
                    {
                        return;
                    }
                    DataSchema.SkylineWindow.SelectedResultsIndex = ResultFile.Replicate.ReplicateIndex;
                    DataSchema.SkylineWindow.SelectedPath = Peptide.IdentityPath;
                    var calibrationForm = DataSchema.SkylineWindow.ShowCalibrationForm();
                    if (calibrationForm != null && !Settings.Default.CalibrationCurveOptions.SingleBatch)
                    {
                        if (!string.IsNullOrEmpty(ResultFile.Replicate.BatchName) ||
                            Peptide.DocNode.HasPrecursorConcentrations)
                        {
                            Settings.Default.CalibrationCurveOptions =
                                Settings.Default.CalibrationCurveOptions.ChangeSingleBatch(true);
                            calibrationForm.UpdateUI(false);
                        }
                    }
                });
            }
        }

        [ChildDisplayName("Batch{0}")]
        public FiguresOfMerit BatchFiguresOfMerit
        {
            get
            {
                CalibrationCurveFitter calibrationCurveFitter = GetCalibrationCurveFitter();
                var calibrationCurve = calibrationCurveFitter.GetCalibrationCurve();
                return calibrationCurveFitter.GetFiguresOfMerit(calibrationCurve);
            }
        }

        [InvariantDisplayName("ExplicitAnalyteConcentration")]
        public double? AnalyteConcentration
        {
            get { return ChromInfo.AnalyteConcentration; }
            set
            {
                ChangeChromInfo(EditColumnDescription(@"ExplicitAnalyteConcentration", value),
                    chromInfo => chromInfo.ChangeAnalyteConcentration(value));
            }
        }

        public int PeptideSpectrumMatchCount
        {
            get
            {
                return SrmDocument.Settings.GetRetentionTimes(GetResultFile().ChromFileInfo.FilePath,
                    Peptide.DocNode.Target, Peptide.DocNode.ExplicitMods).Length;
            }
        }

        [ProteomicDisplayName("PeptideResultLocator")]
        [InvariantDisplayName("MoleculeResultLocator")]
        public string Locator
        {
            get { return GetLocator(); }
        }

        public override bool IsEmpty()
        {
            return !ChromInfo.RetentionTime.HasValue;
        }

        private class CachedValues : CachedValues<PeptideResult, PeptideChromInfo,
            CalibrationCurveFitter, QuantificationResult>
        {
            protected override SrmDocument GetDocument(PeptideResult owner)
            {
                return owner.SrmDocument;
            }
            protected override PeptideChromInfo CalculateValue(PeptideResult owner)
            {
                return owner.ResultFile.FindChromInfo(owner.Peptide.DocNode.Results);
            }

            public PeptideChromInfo GetChromInfo(PeptideResult owner)
            {
                return GetValue(owner);
            }
            protected override CalibrationCurveFitter CalculateValue1(PeptideResult owner)
            {
                if (string.IsNullOrEmpty(owner.ResultFile.Replicate.BatchName) && !owner.Peptide.DocNode.HasPrecursorConcentrations)
                {
                    return owner.Peptide.GetCalibrationCurveFitter();
                }

                var calibrationCurveFitter = CalibrationCurveFitter.GetCalibrationCurveFitter(
                    owner.DataSchema.LazyNormalizationData, owner.SrmDocument.Settings,
                    new IdPeptideDocNode(owner.Peptide.Protein.DocNode.PeptideGroup, owner.Peptide.DocNode));
                calibrationCurveFitter.SingleBatchReplicateIndex = owner.ResultFile.Replicate.ReplicateIndex;
                return calibrationCurveFitter;
            }

            public CalibrationCurveFitter GetCalibrationCurveFitter(PeptideResult owner)
            {
                return GetValue1(owner);
            }
            protected override QuantificationResult CalculateValue2(PeptideResult owner)
            {
                return GetValue1(owner).GetPeptideQuantificationResult(owner.ResultFile.Replicate.ReplicateIndex);
            }
            public QuantificationResult GetQuantificationResult(PeptideResult owner) { return GetValue2(owner); }
        }
    }
}
