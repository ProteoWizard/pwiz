/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.BlibData;

namespace pwiz.Skyline.Model.IonMobility
{
    public class DbIonMobilityPeptide : DbEntity, IPeptideData
    {
        public override Type EntityClass
        {
            get { return typeof(DbIonMobilityPeptide); }
        }

        // public virtual long? ID { get; set; } // in DbEntity
        public virtual string PeptideModSeq { get; set; }
        public virtual double CollisionalCrossSection { get; set; }
        public virtual double HighEnergyDriftTimeOffsetMsec { get; set; }

        public virtual string Sequence { get { return PeptideModSeq; } }
        
        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbIonMobilityPeptide()
        {            
        }

        public DbIonMobilityPeptide(DbIonMobilityPeptide other)
            : this(other.PeptideModSeq, other.CollisionalCrossSection, other.HighEnergyDriftTimeOffsetMsec)
        {
            Id = other.Id;
        }

        public DbIonMobilityPeptide(string sequence, double collisionalCrossSection, double highEnergyDriftTimeOffsetMsec)
        {
            PeptideModSeq = sequence;
            CollisionalCrossSection = collisionalCrossSection;
            HighEnergyDriftTimeOffsetMsec = highEnergyDriftTimeOffsetMsec;
        }

        #region object overrides

        public virtual bool Equals(DbIonMobilityPeptide other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(other.PeptideModSeq, PeptideModSeq) &&
                   other.HighEnergyDriftTimeOffsetMsec.Equals(other.HighEnergyDriftTimeOffsetMsec) &&
                   other.CollisionalCrossSection.Equals(CollisionalCrossSection);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DbIonMobilityPeptide);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (PeptideModSeq != null ? PeptideModSeq.GetHashCode() : 0);
                result = (result*397) ^ CollisionalCrossSection.GetHashCode();
                result = (result*397) ^ HighEnergyDriftTimeOffsetMsec.GetHashCode();
                return result;
            }
        }

        #endregion
    }

}
