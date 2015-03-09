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
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.result)]
    [DatabindingTable(RootTable = typeof(Databinding.Entities.Transition), Property = "Results!*.Value")]
    public class DbTransitionResult : DbRatioResult
    {
        public override Type EntityClass
        {
            get { return typeof(DbTransitionResult); }
        }
        public virtual DbTransition Transition { get; set; }
        [DatabindingColumn(Name = "PrecursorResult.PeptideResult.ResultFile")]
        public virtual DbResultFile ResultFile { get; set; }
        public virtual DbPrecursorResult PrecursorResult { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? RetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? Fwhm { get; set; }
        public virtual bool FwhmDegenerate { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? StartTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? EndTime { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? Area { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? Background { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? AreaRatio { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        public virtual double? AreaNormalized { get; set; }
        //        [QueryColumn(Format = Formats.PEAK_AREA)]
//        public virtual double SignalToNoise { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? Height { get; set; }
        [QueryColumn(Format = Formats.MASS_ERROR)]
        public virtual double? MassErrorPPM { get; set; }
        public virtual bool? Truncated { get; set; }
        public virtual int? PeakRank { get; set; }
        public virtual UserSet UserSetPeak { get; set; }
        public virtual int OptStep { get; set; }
        [QueryColumn(FullName = "TransitionReplicateNote")] // Not L10N
        public virtual string Note { get; set; }
    }
}
