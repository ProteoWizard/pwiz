/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace pwiz.Topograph.Data
{
    [DataContract]
    public class DbPeptideFileAnalysis : DbAnnotatedEntity<DbPeptideFileAnalysis>
    {
        public DbPeptideFileAnalysis()
        {
            Peaks = new List<DbPeak>();
        }
        public virtual DbPeptideAnalysis PeptideAnalysis { get; set; }
        public virtual DbChromatogramSet ChromatogramSet { get; set; }
        [DataMember]
        public virtual DbMsDataFile MsDataFile { get; set; }
        public virtual ICollection<DbPeak> Peaks { get; set; }
        public virtual int PeakCount { get; set; }
        public virtual bool AutoFindPeak { get; set; }
        public virtual bool OverrideExcludedMasses { get; set; }
        public virtual double ChromatogramStartTime { get; set; }
        public virtual double ChromatogramEndTime { get; set; }
        public virtual byte[] ExcludedMasses { get; set; }
        public virtual string BasePeakName { get; set; }
        public virtual double? TracerPercent { get; set; }
        public virtual double? DeconvolutionScore { get; set; }
        public virtual double? PrecursorEnrichment { get; set; }
        public virtual string PrecursorEnrichmentFormula { get; set; }
        public virtual double? Turnover { get; set; }
        public virtual double? TurnoverScore { get; set; }
        public virtual int PsmCount { get; set; }
        public virtual string IntegrationNote { get; set; }
        public virtual bool IsCalculated { get { return PeakCount != 0 && TracerPercent.HasValue; } }
        public override string ToString()
        {
            return PeptideAnalysis.Peptide.Sequence + ":" + MsDataFile.Label;
        }
    }
}
