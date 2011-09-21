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
using pwiz.Skyline.Model.Lib.BlibData;

namespace pwiz.Skyline.Model.Irt
{
    public class DbIrtPeptide : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbIrtPeptide); }
        }

        /*
        CREATE TABLE RetentionTimes (
            ID id INTEGER PRIMARY KEY autoincrement not null,
            PeptideModSeq VARCHAR(200),
            iRT REAL,
            Standard BIT
        )
        */
        public virtual int? ID { get; set; }
        public virtual string PeptideModSeq { get; set; }
        public virtual double Irt { get; set; }
        public virtual bool Standard { get; set; }

        public DbIrtPeptide()
        {
        }

        public DbIrtPeptide(string seq, double irt)
            : this(seq, irt, false)
        {
        }

        public DbIrtPeptide(string seq, double irt, bool standard)
        {
            PeptideModSeq = seq;
            Irt = irt;
            Standard = standard;
        }
    }

    class PepIrtComparer : Comparer<DbIrtPeptide>
    {
        public override int Compare(DbIrtPeptide one, DbIrtPeptide two)
        {
            return one.Irt.CompareTo(two.Irt);
        }
    }
}
