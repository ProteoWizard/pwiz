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
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Data
{
    public class DbPeak : DbEntity<DbPeak>
    {
        public virtual DbPeptideFileAnalysis PeptideFileAnalysis { get; set; }
        public virtual int Charge { get; set; }
        public virtual int MassIndex { get; set; }
        public virtual double MzMin { get; set; }
        public virtual double MzMax { get; set; }
        public virtual double TotalArea { get; set; }
        public virtual double TotalError { get; set; }
        public virtual double Area { get { return Math.Max(0, TotalArea - Background); } }
        public virtual double Background { get; set; }
        public virtual int PeakStart { get; set; }
        public virtual int PeakEnd { get; set; }
        public virtual MzKey MzKey {get
            {
                return new MzKey(Charge, MassIndex);
            }
            set
            {
                Charge = value.Charge;
                MassIndex = value.MassIndex;
            }
        }
        public virtual MzRange MzRange 
        { 
            get
            {
                return new MzRange(MzMin, MzMax);
            } 
            set 
            { 
                MzMin = value.Min;
                MzMax = value.Max;
            }
        }
        public virtual double GetError()
        {
            if (TotalArea == 0)
            {
                return 0;
            }
            return Math.Max(0, (TotalError - Background) / TotalArea);
        }
        public virtual bool IsAcceptable()
        {
            if (TotalError - Background > TotalArea / 2)
            {
                return false;
            }
            return true;
        }
    }
}
