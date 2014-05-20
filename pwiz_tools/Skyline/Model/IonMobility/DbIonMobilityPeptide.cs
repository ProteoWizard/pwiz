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
using System.Collections.Generic;
using System.Linq;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;

namespace pwiz.Skyline.Model.IonMobility
{
    public class DbIonMobilityPeptide : DbEntity, IPeptideData
    {
        public override Type EntityClass
        {
            get { return typeof(DbIonMobilityPeptide); }
        }

        /*
        CREATE TABLE RetentionTimes (
            Id id INTEGER PRIMARY KEY autoincrement not null,
            PeptideModSeq VARCHAR(200),
            CollisionalCrossSection DOUBLE
        )
        */
        // public virtual long? ID { get; set; } // in DbEntity
        public virtual string PeptideModSeq { get; set; }
        public virtual double CollisionalCrossSection { get; set; }

        public virtual string Sequence { get { return PeptideModSeq; } }
        
        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbIonMobilityPeptide()
        {            
        }

        public DbIonMobilityPeptide(DbIonMobilityPeptide other)
            : this(other.PeptideModSeq, other.CollisionalCrossSection)
        {
            Id = other.Id;
        }

        public DbIonMobilityPeptide(LibKey peptide, double collisionalCrossSection)
            : this(peptide.Sequence, collisionalCrossSection)
        {
        }

        public DbIonMobilityPeptide(string sequence, double collisionalCrossSection)
        {
            PeptideModSeq = sequence;
            CollisionalCrossSection = collisionalCrossSection;
        }

        public static List<DbIonMobilityPeptide> FindNonConflicts(IList<DbIonMobilityPeptide> oldPeptides, IList<DbIonMobilityPeptide> newPeptides, out IList<Tuple<DbIonMobilityPeptide, DbIonMobilityPeptide>> conflicts)
        {
            var peptidesNoConflict = new List<DbIonMobilityPeptide>();
            conflicts = new List<Tuple<DbIonMobilityPeptide, DbIonMobilityPeptide>>();
            var dictOld = oldPeptides.ToDictionary(pep => pep.PeptideModSeq);
            var dictNew = newPeptides.ToDictionary(pep => pep.PeptideModSeq);
            foreach (var newPeptide in newPeptides)
            {
                DbIonMobilityPeptide oldPeptide;
                // A conflict occurs only when there is another peptide of the same sequence, and different CCS
                if (!dictOld.TryGetValue(newPeptide.PeptideModSeq, out oldPeptide) || 
                    Math.Abs(newPeptide.CollisionalCrossSection - oldPeptide.CollisionalCrossSection) < COLLISIONAL_CROSS_SECTION_MIN_DIFF )
                {
                    peptidesNoConflict.Add(newPeptide);
                }
                else
                {
                    conflicts.Add(new Tuple<DbIonMobilityPeptide, DbIonMobilityPeptide>(newPeptide, oldPeptide));
                }
            }
            foreach (var oldPeptide in oldPeptides)
            {
                DbIonMobilityPeptide newPeptide;
                if (!dictNew.TryGetValue(oldPeptide.PeptideModSeq, out newPeptide))
                    peptidesNoConflict.Add(oldPeptide);
            }
            return peptidesNoConflict;
        }

        public const double COLLISIONAL_CROSS_SECTION_MIN_DIFF = 0.001;  // TODO - what's a good value here?

        #region object overrides

        public virtual bool Equals(DbIonMobilityPeptide other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(other.PeptideModSeq, PeptideModSeq) &&
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
                return result;
            }
        }

        #endregion
    }

}
