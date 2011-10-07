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
    public class DbPrecursorResult : DbRatioResult
    {
        public override Type EntityClass
        {
            get { return typeof (DbPrecursorResult); }
        }
        public virtual DbPrecursor Precursor { get; set; }
        public virtual DbResultFile ResultFile { get; set; }
        public virtual DbPeptideResult PeptideResult { get; set; }
        [QueryColumn(Format=Formats.PEAK_FOUND_RATIO)]
        public virtual double PrecursorPeakFoundRatio { get; set; }
        [QueryColumn(Format=Formats.RETENTION_TIME)]
        public virtual double? BestRetentionTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MaxFwhm { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MinStartTime { get; set; }
        [QueryColumn(Format = Formats.RETENTION_TIME)]
        public virtual double? MaxEndTime { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? TotalArea { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA)]
        public virtual double? TotalBackground { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? TotalAreaRatio { get; set; }
//        [QueryColumn(Format = Formats.STANDARD_RATIO)]
//        public virtual double? StdevAreaRatio { get; set; }
        [QueryColumn(Format = Formats.PEAK_AREA_NORMALIZED)]
        public virtual double? TotalAreaNormalized { get; set; }
        public virtual int? CountTruncated { get; set; }
        public virtual bool Identified { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? LibraryDotProduct { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? IsotopeDotProduct { get; set; }
        //        [QueryColumn(Format = Formats.PEAK_AREA)]
//        public virtual double TotalSignalToNoise { get; set; }
        public virtual bool UserSetTotal { get; set; }
        public virtual int OptStep { get; set; }
        [QueryColumn(Format = Formats.OPT_PARAMETER)]
        public virtual double? OptCollisionEnergy { get; set; }
        [QueryColumn(Format = Formats.OPT_PARAMETER)]
        public virtual double? OptDeclusteringPotential { get; set; }
        [QueryColumn(FullName = "PrecursorReplicateNote")]
        public virtual string Note { get; set; }
    }
}
