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
using pwiz.Skyline.Model.Databinding.Entities;

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.node)]
    [DatabindingTable(RootTable = typeof(Precursor))]
    public class DbPrecursor : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof (DbPrecursor); }
        }
        public virtual DbPeptide Peptide { get; set; }
        [QueryColumn(FullName="PrecursorCharge")] // Not L10N
        public virtual int Charge { get; set; }
        public virtual IsotopeLabelType IsotopeLabelType { get; set; }
        [QueryColumn(FullName = "PrecursorNeutralMass")] // Not L10N
        public virtual double NeutralMass { get; set; }
        [QueryColumn(FullName = "PrecursorMz")] // Not L10N
        public virtual double Mz { get; set; }
        [QueryColumn(Format = Formats.OPT_PARAMETER)]
        public virtual double CollisionEnergy { get; set; }
        [QueryColumn(Format = Formats.OPT_PARAMETER)]
        public virtual double? DeclusteringPotential { get; set; }
        public virtual string ModifiedSequence { get; set; }
        [QueryColumn(FullName = "PrecursorNote")] // Not L10N
        public virtual string Note { get; set; }
        public virtual string LibraryName { get; set; }
        public virtual string LibraryType { get; set; }
        [QueryColumn(FullName = "PrecursorLibraryRank")] // Not L10N
        public virtual int? LibraryRank { get; set; }
        public virtual double? LibraryScore1 { get; set; }
        public virtual double? LibraryScore2 { get; set; }
        public virtual double? LibraryScore3 { get; set; }
        [QueryColumn(FullName = "PrecursorIsDecoy")] // Not L10N
        public virtual bool IsDecoy { get; set; }
        [QueryColumn(FullName = "PrecursorDecoyMzShift")] // Not L10N
        public virtual int? DecoyMzShift { get; set; }
    }
}
