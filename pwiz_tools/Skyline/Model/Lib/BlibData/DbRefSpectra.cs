/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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

namespace pwiz.Skyline.Model.Lib.BlibData
{
    public class DbRefSpectra : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbRefSpectra); }
        }
        public virtual string PeptideSeq { get; set; }
        public virtual double PrecursorMZ { get; set; }
        public virtual int PrecursorCharge { get; set; }
        public virtual string PeptideModSeq { get; set; }
        public virtual char? PrevAA { get; set; }
        public virtual char? NextAA { get; set; }
        public virtual short Copies { get; set; }
        public virtual ushort NumPeaks { get; set; }
        public virtual DbRefSpectraPeaks Peaks { get; set; }
        public virtual ICollection<DbModification> Modifications { get; set; }
        public virtual ICollection<DbRetentionTimes> RetentionTimes { get; set; }
    }
}
