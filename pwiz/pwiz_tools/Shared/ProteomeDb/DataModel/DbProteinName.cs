/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using pwiz.ProteomeDatabase.API;

namespace pwiz.ProteomeDatabase.DataModel
{
    public class DbProteinName : DbEntity<DbProteinName>
    {
        private ProteinMetadata _proteinMetadata;

        public DbProteinName()
        {
            _proteinMetadata = ProteinMetadata.EMPTY;
        }

        public DbProteinName(DbProtein protein, ProteinMetadata proteinMetadata)
        {
            // ReSharper disable DoNotCallOverridableMethodsInConstructor
            IsPrimary = (protein==null);
            Protein = protein;
            // ReSharper restore DoNotCallOverridableMethodsInConstructor
            _proteinMetadata = proteinMetadata;
        }

        public virtual bool IsPrimary { get; set; }
        public virtual DbProtein Protein { get; set; } 

        public virtual String Name { get { return _proteinMetadata.Name; } set { _proteinMetadata = _proteinMetadata.ChangeName(value); } }
        public virtual String Description { get { return _proteinMetadata.Description; } set { _proteinMetadata = _proteinMetadata.ChangeDescription(value); } }
        public virtual String PreferredName { get { return _proteinMetadata.PreferredName; } set { _proteinMetadata = _proteinMetadata.ChangePreferredName(value); } }
        public virtual String Accession { get { return _proteinMetadata.Accession; } set { _proteinMetadata = _proteinMetadata.ChangeAccession(value); } }
        public virtual String Gene { get { return _proteinMetadata.Gene; } set { _proteinMetadata = _proteinMetadata.ChangeGene(value); } }
        public virtual String Species { get { return _proteinMetadata.Species; } set { _proteinMetadata = _proteinMetadata.ChangeSpecies(value); } }
        public virtual WebSearchInfo WebSearchInfo { get { return _proteinMetadata.WebSearchInfo; } set { _proteinMetadata = _proteinMetadata.ChangeWebSearchInfo(value); } }

        // String-based version is really only for use with protDB read/write 
        public virtual String WebSearchStatus
        {
            get { return _proteinMetadata.WebSearchInfo.ToString(); }
            set { _proteinMetadata = _proteinMetadata.ChangeWebSearchInfo(WebSearchInfo.FromString(value)); }
        }

        public virtual ProteinMetadata GetProteinMetadata() { return _proteinMetadata; }

        public virtual void SetWebSearchCompleted()
        {
            // Prepend the "done" tag to the searchterm/history.
            _proteinMetadata = _proteinMetadata.SetWebSearchCompleted();
        }

        public virtual void SetWebSearchTerm(WebSearchTerm search)
        {
            _proteinMetadata = _proteinMetadata.SetWebSearchTerm(search);
        }

        public virtual ProteinMetadata MergeProteinMetadata(DbProteinName other)
        {
            _proteinMetadata = _proteinMetadata.Merge(other.GetProteinMetadata());
            return _proteinMetadata;
        }

        public virtual ProteinMetadata ChangeProteinMetadata(ProteinMetadata other)
        {
            _proteinMetadata = other;
            return _proteinMetadata;
        }

        public virtual ProteinMetadata ClearWebSearchInfo()
        {
            // Sometimes you really just want to initialize, not have a search history.
            _proteinMetadata = _proteinMetadata.ClearWebSearchInfo();
            return _proteinMetadata;
        }
    }
}
