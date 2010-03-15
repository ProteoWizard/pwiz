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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using pwiz.Topograph.Data;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Data
{
    public class DbPeptideFileAnalysis : DbAnnotatedEntity<DbPeptideFileAnalysis>
    {
        public DbPeptideFileAnalysis()
        {
            Chromatograms = new List<DbChromatogram>();
            Peaks = new List<DbPeak>();
            PeptideDistributions = new List<DbPeptideDistribution>();
        }
        public virtual DbPeptideAnalysis PeptideAnalysis { get; set; }
        public virtual DbMsDataFile MsDataFile { get; set; }
        public virtual ICollection<DbChromatogram> Chromatograms { get; set; }
        public virtual int ChromatogramCount { get; set; }
        public virtual ICollection<DbPeak> Peaks { get; set; }
        public virtual int PeakCount { get; set; }
        public virtual ICollection<DbPeptideDistribution> PeptideDistributions { get; set; }
        public virtual int PeptideDistributionCount { get; set; }
        public virtual bool AutoFindPeak { get; set; }
        public virtual bool OverrideExcludedMasses { get; set; }
        public virtual double ChromatogramStartTime { get; set; }
        public virtual double ChromatogramEndTime { get; set; }
        public virtual int? FirstDetectedScan { get; set; }
        public virtual int? LastDetectedScan { get; set; }
        public virtual int? PeakStart { get; set; }
        public virtual double? PeakStartTime { get; set; }
        public virtual int? PeakEnd { get; set; }
        public virtual double? PeakEndTime { get; set; }
        public virtual byte[] TimesBytes { get; set; }
        public virtual byte[] ScanIndexesBytes { get; set; }
        public virtual byte[] ExcludedMasses { get; set; }
        public virtual double[] Times 
        { 
            get
            {
                return ArrayConverter.FromBytes<double>(TimesBytes);
            } 
            set
            {
                TimesBytes = ArrayConverter.ToBytes(value);
            }
        }
        public virtual int[] ScanIndexes
        {
            get
            {
                return ArrayConverter.FromBytes<int>(ScanIndexesBytes);
            }
            set
            {
                ScanIndexesBytes = ArrayConverter.ToBytes(value);
            }
        }
        public virtual Dictionary<MzKey, DbChromatogram> GetChromatogramDict()
        {
            var result = new Dictionary<MzKey, DbChromatogram>();
            foreach (var chromatogram in Chromatograms)
            {
                result.Add(chromatogram.MzKey, chromatogram);
            }
            return result;
        }
        public override string ToString()
        {
            return PeptideAnalysis.Peptide.Sequence + ":" + MsDataFile.Label;
        }
    }
}
