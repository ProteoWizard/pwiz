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
using System.Collections.Generic;
using pwiz.ProteomeDatabase.DataModel;
using pwiz.ProteomeDatabase.Util;

namespace pwiz.ProteomeDatabase.API
{
    public class Protein : EntityModel<DbProtein>, IComparable<Protein>
    {
        private List<ProteinMetadata> _alternativeNames;
        private ProteinMetadata _proteinMetadata; // name, description, accession, gene etc
        
        internal Protein(ProteomeDbPath proteomeDb, DbProtein protein)
            : this(proteomeDb, protein, (DbProteinName) null)
        {
            _proteinMetadata = ProteinMetadata.EMPTY;
        }

        internal Protein(ProteomeDbPath proteomeDb, DbProtein protein, DbProteinName primaryName)
            : base(proteomeDb, protein)
        {
            Sequence = protein.Sequence;
            if (primaryName != null)
            {
                _proteinMetadata = primaryName.GetProteinMetadata();
                if (primaryName.Protein != null)
                {
                    // grab the alternative names now, rather than going back to the db later
                    _alternativeNames = new List<ProteinMetadata>();
                    foreach (var name in primaryName.Protein.Names)
                    {
                        if (!name.IsPrimary)
                            _alternativeNames.Add(name.GetProteinMetadata());
                    }
                }
            }
        }

        internal Protein(ProteomeDbPath proteomeDbPath, DbProtein protein, IEnumerable<DbProteinName> proteinNames)
            : this(proteomeDbPath, protein, (DbProteinName) null)
        {
            _proteinMetadata = ProteinMetadata.EMPTY;
            _alternativeNames = new List<ProteinMetadata>();
            foreach (DbProteinName proteinName in proteinNames)
            {
                if (proteinName.IsPrimary)
                {
                    _proteinMetadata = proteinName.GetProteinMetadata();
                }
                else
                {
                    _alternativeNames.Add(proteinName.GetProteinMetadata()); // copies the ProteinMetadata info
                }
            }           
        }

        private void InitNames()
        {
            if (_alternativeNames != null)
            {
                return;
            }
            _alternativeNames = new List<ProteinMetadata>();
            using (var proteomeDb = OpenProteomeDb())
            using (var session = proteomeDb.OpenSession())
            {
                var protein = GetEntity(session);
                foreach (var name in protein.Names)
                {
                    if (name.Name == Name)
                    {
                        continue;
                    }
                    if (Name == null && name.IsPrimary)
                    {
                        _proteinMetadata = name.GetProteinMetadata();
                    }
                    else
                    {
                        _alternativeNames.Add(name.GetProteinMetadata());
                    }
                }
            }
        }

        public String Name
        {
            get
            {
                if (_proteinMetadata.Name == null)
                    InitNames();
                return _proteinMetadata.Name;
            }
        }

        public String Description
        {
            get
            {
                if (_proteinMetadata.Description == null && _proteinMetadata.Name == null)
                    InitNames();
                return _proteinMetadata.Description;
            }
        }

        public String PreferredName { get { return _proteinMetadata.PreferredName; }  }
        public String Accession { get { return _proteinMetadata.Accession; }  }
        public String Gene { get { return _proteinMetadata.Gene; }  }
        public String Species { get { return _proteinMetadata.Species; }  }
        public String WebSearchInfo { get { return _proteinMetadata.WebSearchInfo.ToString(); }  }

        public ProteinMetadata ProteinMetadata { get { return _proteinMetadata; } }

        public String Sequence { get; private set; }
        public IList<ProteinMetadata> AlternativeNames
        {
            get
            {
                InitNames();
                return _alternativeNames;
            }
        }

        public int CompareTo(Protein other)
        {
// ReSharper disable StringCompareToIsCultureSpecific
            return Name.CompareTo(other.Name);
// ReSharper restore StringCompareToIsCultureSpecific
        }
    }
}
