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
    [QueryTable(TableType = TableType.node)]
    [DatabindingTable(RootTable = typeof(Databinding.Entities.Peptide))]
    public class DbPeptide : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbPeptide); }
        }
        public virtual DbProtein Protein { get; set; }
        [QueryColumn(FullName="PeptideSequence")] // Not L10N
        public virtual string Sequence { get; set; }
        [QueryColumn(FullName = "PeptideModifiedSequence")] // Not L10N
        public virtual string ModifiedSequence { get; set; }
        public virtual string StandardType { get; set; }    // iRT, Global
        public virtual int? BeginPos { get; set; }
        public virtual int? EndPos { get; set; }
        public virtual int MissedCleavages { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? RetentionTimeCalculatorScore { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? PredictedRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? AverageMeasuredRetentionTime { get; set; }
        [QueryColumn(FullName = "PeptideNote")] // Not L10N
        public virtual string Note { get; set; }
    }
}
