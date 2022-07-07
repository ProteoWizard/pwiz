/*
 * Original author: Kaipo Tamura <kaipot .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Util;

// ReSharper disable VirtualMemberCallInConstructor

namespace pwiz.Skyline.Model.Irt
{
    public class DbIrtHistory : DbEntity
    {
        public override Type EntityClass => typeof(DbIrtHistory);

        // public virtual long? Id { get; set; } // in DbEntity
        public virtual long PeptideId { get; set; }
        public virtual double Irt { get; set; }
        public virtual string SaveTime { get; set; }

        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbIrtHistory()
        {
        }

        public DbIrtHistory(long peptideId, double irt, TimeStampISO8601 saveTime)
        {
            Id = null;
            PeptideId = peptideId;
            Irt = irt;
            SaveTime = saveTime.ToString();
        }

        public DbIrtHistory(DbIrtHistory other)
        {
            Id = other.Id;
            PeptideId = other.PeptideId;
            Irt = other.Irt;
            SaveTime = other.SaveTime;
        }

        #region object overrides

        public virtual bool Equals(DbIrtHistory other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                   PeptideId.Equals(other.PeptideId) &&
                   Irt.Equals(other.Irt) &&
                   SaveTime.Equals(other.SaveTime);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as DbIrtHistory);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = base.GetHashCode();
                result = (result * 397) ^ PeptideId.GetHashCode();
                result = (result * 397) ^ Irt.GetHashCode();
                result = (result * 397) ^ SaveTime.GetHashCode();
                return result;
            }
        }

        #endregion
    }
}
