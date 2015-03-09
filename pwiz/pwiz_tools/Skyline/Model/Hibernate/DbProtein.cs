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
using Protein = pwiz.Skyline.Model.Databinding.Entities.Protein;

namespace pwiz.Skyline.Model.Hibernate
{
    [QueryTable(TableType = TableType.node)]
    [DatabindingTable(RootTable = typeof(Protein))]
    public class DbProtein : DbEntity
    {
        public override Type EntityClass
        {
            get { return typeof(DbProtein); }
        }
        [QueryColumn(FullName = "ProteinName")] // Not L10N
        public virtual string Name { get { return _proteinMetadata.Name; } set { _proteinMetadata = _proteinMetadata.ChangeName(value); } }
        [QueryColumn(FullName = "ProteinDescription")] // Not L10N
        public virtual string Description { get { return _proteinMetadata.Description; } set { _proteinMetadata = _proteinMetadata.ChangeDescription(value); } }
        [QueryColumn(FullName = "ProteinSequence")] // Not L10N
        public virtual string Sequence { get; set; }
        [QueryColumn(FullName = "ProteinNote")] // Not L10N
        public virtual string Note { get; set; }
        [QueryColumn(FullName = "ProteinAccession")] // Not L10N
        public virtual string Accession { get { return _proteinMetadata.Accession; } set { _proteinMetadata = _proteinMetadata.ChangeAccession(value); } }
        [QueryColumn(FullName = "ProteinPreferredName")] // Not L10N
        public virtual string PreferredName { get { return _proteinMetadata.PreferredName; } set { _proteinMetadata = _proteinMetadata.ChangePreferredName(value); } }
        [QueryColumn(FullName = "ProteinGene")] // Not L10N
        public virtual string Gene { get { return _proteinMetadata.Gene; } set { _proteinMetadata = _proteinMetadata.ChangeGene(value); } }
        [QueryColumn(FullName = "ProteinSpecies")] // Not L10N
        public virtual string Species { get { return _proteinMetadata.Species; } set { _proteinMetadata = _proteinMetadata.ChangeSpecies(value); } }
        // We don't want this to appear in the Document Grid
        // [QueryColumn(FullName = "ProteinWebSearchStatus")] // Not L10N
        // public virtual string WebSearchStatus { get { return _proteinMetadata.WebSearchInfo.ToString(); } set { _proteinMetadata = _proteinMetadata.ChangeWebSearchInfo(WebSearchInfo.FromString(value)); } } 

        private ProteinMetadata _proteinMetadata;

        public DbProtein()
        {
            _proteinMetadata = ProteinMetadata.EMPTY;
        }

        public DbProtein(ProteinMetadata other)
        {
            _proteinMetadata = other;
        }
    }
}
