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
    public class DbPeptideRate : DbEntity<DbPeptideRate>
    {
        public virtual DbPeptideAnalysis PeptideAnalysis { get; set; }
        public virtual PeptideQuantity PeptideQuantity { get; set; }
        public virtual String Cohort { get; set; }
        public virtual double HalfLife { get; set; }
        public virtual double InitialTurnover { get; set; }
        public virtual double LimitValue { get; set; }
        public virtual double Score { get; set; }
        public virtual bool IsComplete { get; set; }
        public virtual RateKey RateKey {
            get
            {
                return new RateKey(PeptideQuantity,Cohort);
            }
            set 
            { 
                PeptideQuantity = value.PeptideQuantity;
                Cohort = value.Cohort;
            }
        }
    }
    public class RateKey
    {
        public RateKey(PeptideQuantity peptideQuantity, String cohort)
        {
            PeptideQuantity = peptideQuantity;
            Cohort = cohort ?? "";
        }

        public PeptideQuantity PeptideQuantity { get; private set; }
        public String Cohort { get; private set; }
        public override int GetHashCode()
        {
            return PeptideQuantity.GetHashCode()*31 + Cohort.GetHashCode();
        }
        public override bool Equals(Object o)
        {
            if (o == this)
            {
                return true;
            }
            var that = o as RateKey;
            if (that == null)
            {
                return false;
            }
            return Equals(PeptideQuantity, that.PeptideQuantity)
                   && Equals(Cohort, that.Cohort);
        }
    }
}
