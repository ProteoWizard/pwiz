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
    [DatabindingTable(RootTable = typeof(Databinding.Entities.Transition))]
    public class DbTransition : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbTransition); }
        }
        public virtual DbPrecursor Precursor { get; set; }
        public virtual int ProductCharge { get; set; }
        public virtual double ProductNeutralMass { get; set; }
        public virtual double ProductMz { get; set; }
        public virtual string FragmentIon { get; set; }
        public virtual string FragmentIonType { get; set; }
        public virtual int FragmentIonOrdinal { get; set; }
        public virtual string CleavageAa { get; set; }
        public virtual double LossNeutralMass { get; set; }
        public virtual string Losses { get; set; }
        [QueryColumn(FullName = "TransitionNote")] // Not L10N
        public virtual string Note { get; set; }
        public virtual int? LibraryRank { get; set; }
        public virtual double? LibraryIntensity { get; set; }
        public virtual int IsotopeDistIndex { get; set; }
        public virtual int? IsotopeDistRank { get; set; }
        [QueryColumn(Format = Formats.STANDARD_RATIO)]
        public virtual double? IsotopeDistProportion { get; set; }
        public virtual double? FullScanFilterWidth { get; set; }
        [QueryColumn(FullName = "TransitionIsDecoy")] // Not L10N
        public virtual bool IsDecoy { get; set; }
        public virtual int? ProductDecoyMzShift { get; set; }
    }
}
