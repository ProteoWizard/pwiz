/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.result)]
    [DatabindingTable(RootTable = typeof(Databinding.Entities.Peptide), Property = "Results!*.Value")]
    public class DbPeptideResult : DbRatioResult
    {
        public override Type EntityClass
        {
            get { return typeof(DbPeptideResult); }
        }
        public virtual DbPeptide Peptide { get; set; }
        public virtual DbResultFile ResultFile { get; set; }
        [DatabindingColumn(Name="ResultFile")]
        public virtual DbProteinResult ProteinResult { get; set; }
        [QueryColumn(Format=Formats.PEAK_FOUND_RATIO)]
        public virtual double PeptidePeakFoundRatio { get; set; }
        [QueryColumn(Format=Formats.RETENTION_TIME)]
        public virtual double? PeptideRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? PredictedResultRetentionTime { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? RatioToStandard { get; set; }
        public virtual bool BestReplicate { get; set; }
        [QueryColumn(FullName = "PeptideResultNote")] // Not L10N
        public virtual String Note { get; set; }
    }
}
