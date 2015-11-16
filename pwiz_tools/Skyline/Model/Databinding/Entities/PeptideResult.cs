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

using System.ComponentModel;
using System.Linq;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.DocSettings.AbsoluteQuantification;
using pwiz.Skyline.Model.GroupComparison;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class PeptideResult : Result
    {
        private readonly CachedValue<PeptideChromInfo> _chromInfo;
        private readonly CachedValue<QuantificationResult> _quantificationResult;
        public PeptideResult(Peptide peptide, ResultFile file) : base(peptide, file)
        {
            _chromInfo = CachedValue.Create(DataSchema, () => ResultFile.FindChromInfo(peptide.DocNode.Results));
            _quantificationResult = CachedValue.Create(DataSchema, GetQuantification);
        }

        [HideWhen(AncestorOfType = typeof(Peptide))]
        [Advanced]
        public Peptide Peptide { get { return SkylineDocNode as Peptide; } }

        [Browsable(false)]
        public PeptideChromInfo ChromInfo { get { return _chromInfo.Value; } }
        [Format(Formats.PEAK_FOUND_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double PeptidePeakFoundRatio { get { return ChromInfo.PeakCountRatio; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? PeptideRetentionTime { get { return ChromInfo.RetentionTime; } }
        
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? PredictedResultRetentionTime
        {
            get
            {
                var peptidePrediction = SrmDocument.Settings.PeptideSettings.Prediction;
                if (peptidePrediction.RetentionTime != null)
                {
                    string textId = SrmDocument.Settings.GetSourceTextId(Peptide.DocNode);
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
            return string.Format("RT: {0:0.##}", ChromInfo.RetentionTime);  // Not L10N
        }

        public ResultFile ResultFile { get { return GetResultFile(); } }

        [InvariantDisplayName("PeptideResultDocumentLocation")]
        public DocumentLocation DocumentLocation
        {
            get
            {
                return new DocumentLocation(Peptide.IdentityPath.ToGlobalIndexList()).SetChromFileId(ResultFile.ChromFileInfoId.GlobalIndex);
            }
        }

        public LinkValue<QuantificationResult> Quantification
        {
            get
            {
                return new LinkValue<QuantificationResult>(_quantificationResult.Value, (sender, args) =>
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

        public QuantificationResult GetQuantification()
        {
            var quantifier = PeptideQuantifier.GetPeptideQuantifier(SrmDocument.Settings, Peptide.Protein.DocNode, Peptide.DocNode);
            CalibrationCurveFitter curveFitter = new CalibrationCurveFitter(quantifier, SrmDocument.Settings);
            return curveFitter.GetQuantificationResult(ResultFile.Replicate.ReplicateIndex);
        }
    }
}
