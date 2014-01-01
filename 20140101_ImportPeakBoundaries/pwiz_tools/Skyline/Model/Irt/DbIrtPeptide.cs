/*
 * Original author: John Chilton <jchilton .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.BlibData;

namespace pwiz.Skyline.Model.Irt
{
    public interface IPeptideData
    {
        string Sequence { get; }
    }

    public class DbIrtPeptide : DbEntity, IPeptideData
    {
        public override Type EntityClass
        {
            get { return typeof(DbIrtPeptide); }
        }

        /*
        CREATE TABLE RetentionTimes (
            Id id INTEGER PRIMARY KEY autoincrement not null,
            PeptideModSeq VARCHAR(200),
            iRT REAL,
            Standard BIT,
            PeakTime BIT
        )
        */
        // public virtual long? ID { get; set; } // in DbEntity
        public virtual string PeptideModSeq { get; set; }
        public virtual double Irt { get; set; }
        public virtual bool Standard { get; set; }
        public virtual int? TimeSource { get; set; } // null = unknown, 0 = scan, 1 = peak

        public virtual string Sequence { get { return PeptideModSeq; } }

        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbIrtPeptide()
        {            
        }

        public DbIrtPeptide(DbIrtPeptide other)
            : this(other.PeptideModSeq, other.Irt, other.Standard, other.TimeSource)
        {
            Id = other.Id;
        }

        public DbIrtPeptide(string seq, double irt, bool standard, TimeSource timeSource)
            : this(seq, irt, standard, (int) timeSource)
        {            
        }

        public DbIrtPeptide(string seq, double irt, bool standard, int? timeSource)
        {
            PeptideModSeq = seq;
            Irt = irt;
            Standard = standard;
            TimeSource = timeSource;
        }

        #region object overrides

        public virtual bool Equals(DbIrtPeptide other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   Equals(other.PeptideModSeq, PeptideModSeq) &&
                   other.Irt.Equals(Irt) &&
                   other.Standard.Equals(Standard) &&
                   other.TimeSource.Equals(TimeSource);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DbIrtPeptide);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ (PeptideModSeq != null ? PeptideModSeq.GetHashCode() : 0);
                result = (result*397) ^ Irt.GetHashCode();
                result = (result*397) ^ Standard.GetHashCode();
                result = (result*397) ^ (TimeSource.HasValue ? TimeSource.Value : 0);
                return result;
            }
        }

        #endregion
    }

    class PepIrtComparer : Comparer<DbIrtPeptide>
    {
        public override int Compare(DbIrtPeptide one, DbIrtPeptide two)
        {
            if (one == null)
                return 1;
            if (two == null)
                return -1;
            return one.Irt.CompareTo(two.Irt);
        }
    }
}
