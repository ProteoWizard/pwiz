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

namespace pwiz.Topograph.Data
{
    public class DbPeptideDistribution : DbEntity<DbPeptideDistribution>
    {
        public DbPeptideDistribution()
        {
            PeptideAmounts = new List<DbPeptideAmount>();
        }
        public virtual DbPeptideFileAnalysis PeptideFileAnalysis { get; set; }
        public virtual PeptideQuantity PeptideQuantity { get; set; }
        public virtual ICollection<DbPeptideAmount> PeptideAmounts { get; set; }
        public virtual int PeptideAmountCount { get; set; }
        public virtual double TracerPercent { get; set; }
        public virtual double Score { get; set; }
        public virtual double? PrecursorEnrichment { get; set; }
        public virtual string PrecursorEnrichmentFormula { get; set; }
        public virtual double? Turnover { get; set; }
    }

    public enum PeptideQuantity
    {
        tracer_count,
        precursor_enrichment,
    }
}
