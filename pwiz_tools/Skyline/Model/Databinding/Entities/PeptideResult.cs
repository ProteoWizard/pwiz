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
using pwiz.Common.DataBinding.Attributes;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Databinding.Entities
{
    public class PeptideResult : Result
    {
        public PeptideResult(Peptide peptide, ResultFile file) : base(peptide, file)
        {
            ChromInfo = file.FindChromInfo(peptide.DocNode.Results);
        }

        protected override void OnDocumentChanged()
        {
            base.OnDocumentChanged();
            var newChromInfo = ResultFile.FindChromInfo(Peptide.DocNode.Results);
            if (null != newChromInfo && !Equals(newChromInfo, ChromInfo))
            {
                ChromInfo = newChromInfo;
                FirePropertyChanged(null);
            }
        }
        [HideWhen(AncestorOfType = typeof(SkylineDocument))]
        [Advanced]
        public Peptide Peptide { get { return SkylineDocNode as Peptide; } }

        [Browsable(false)]
        public PeptideChromInfo ChromInfo { get; private set; }
        [Format(Formats.PEAK_FOUND_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double PeptidePeakFoundRatio { get { return ChromInfo.PeakCountRatio; } }
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? PeptideRetentionTime { get { return ChromInfo.RetentionTime; } }
        
        // TODO(nicksh): Maybe get rid of this property since it's redundant with Peptide.PredictedRetentionTime
        [Format(Formats.RETENTION_TIME, NullValue = TextUtil.EXCEL_NA)]
        public double? PredictedResultRetentionTime
        {
            get { return Peptide.PredictedRetentionTime; }
        }
        [Format(Formats.STANDARD_RATIO, NullValue = TextUtil.EXCEL_NA)]
        public double? RatioToStandard
        {
            get 
            {
                var mods = SrmDocument.Settings.PeptideSettings.Modifications;
                var standardType = mods.InternalStandardTypes.FirstOrDefault();
                var labelType = mods.GetModificationTypes().FirstOrDefault();
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
    }
}
