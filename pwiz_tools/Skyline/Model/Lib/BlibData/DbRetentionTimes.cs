/*
 * Original author: Vagisha Sharma <vsharma .at. u.washington.edu>,
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

namespace pwiz.Skyline.Model.Lib.BlibData
{
    public class DbRetentionTimes : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbRetentionTimes); }
        }
        public virtual DbRefSpectra RefSpectra { get; set; }
        public virtual long RedundantRefSpectraId { get; set; }
        public virtual long SpectrumSourceId { get; set; }
        public virtual double? IonMobilityValue { get; set; }
        public virtual int? IonMobilityType { get; set; }
        public virtual double? IonMobilityHighEnergyDriftTimeOffsetMsec { get; set; }
        public virtual double? RetentionTime { get; set; }
        public virtual int BestSpectrum { get; set; }
    }
}