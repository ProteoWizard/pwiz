/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.IO;
using pwiz.Skyline.Model.Hibernate;
// ReSharper disable VirtualMemberCallInConstructor

namespace pwiz.Skyline.Model.Lib.Midas
{
    public class DbResultsFile : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof (DbSpectrum); }
        }

        // public virtual long? ID { get; set; } // in DbEntity
        public virtual string FilePath { get; set; }

        public virtual string FileName { get { return Path.GetFileName(FilePath); } }
        public virtual string BaseName { get { return Path.GetFileNameWithoutExtension(FilePath); } }

        /// <summary>
        /// For NHibernate only
        /// </summary>
        protected DbResultsFile()
        {            
        }

        public DbResultsFile(DbResultsFile other)
            : this(other.FilePath)
        {
            Id = other.Id;
        }

        public DbResultsFile(string filePath)
        {
            FilePath = filePath;
        }

        #region object overrides

        public override bool Equals(object obj)
        {
            var other = obj as DbResultsFile;
            return other != null && (ReferenceEquals(this, other) || (base.Equals(other) && Equals(FilePath, other.FilePath)));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = base.GetHashCode();
                result = (result*397) ^ FilePath.GetHashCode();
                return result;
            }
        }

        #endregion
    }
}
