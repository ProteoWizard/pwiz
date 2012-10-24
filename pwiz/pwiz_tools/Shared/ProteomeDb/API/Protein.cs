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

namespace pwiz.ProteomeDatabase.API
{
    public class Protein : EntityModel<DbProtein>, IComparable<Protein>
    {
        private List<AlternativeName> _alternativeNames;
        private String _name;
        private String _description;
        
        public Protein(ProteomeDb proteomeDb, DbProtein protein)
            : this(proteomeDb, protein,(DbProteinName) null)
        {
        }

        public Protein(ProteomeDb proteomeDb, DbProtein protein, DbProteinName primaryName)
            : base(proteomeDb, protein)
        {
            Sequence = protein.Sequence;
            if (primaryName != null)
            {
                _name = primaryName.Name;
                _description = primaryName.Description;
            }
        }

        public Protein(ProteomeDb proteomeDb, DbProtein protein, IEnumerable<DbProteinName> proteinNames)
            : this(proteomeDb, protein, (DbProteinName) null)
        {
            _alternativeNames = new List<AlternativeName>();
            foreach (DbProteinName proteinName in proteinNames)
            {
                if (proteinName.IsPrimary)
                {
                    _name = proteinName.Name;
                    _description = proteinName.Description;
                }
                else
                {
                    _alternativeNames.Add(new AlternativeName{ Description = proteinName.Description, Name = proteinName.Name});
                }
            }           
        }

        private void InitNames()
        {
            if (_alternativeNames != null)
            {
                return;
            }
            _alternativeNames = new List<AlternativeName>();
            using (var session = ProteomeDb.OpenSession())
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
                        _name = name.Name;
                        _description = name.Description;
                    }
                    else
                    {
                        _alternativeNames.Add(new AlternativeName { Name = name.Name, Description = name.Description });
                    }
                }
            }
        }

        public String Name
        {
            get
            {
                if (_name == null)
                    InitNames();
                return _name;
            }
        }

        public String Description
        {
            get
            {
                if (_description == null)
                    InitNames();
                return _description;
            }
        }

        public String Sequence { get; private set; }
        public IList<AlternativeName> AlternativeNames
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

    public class AlternativeName
    {
        public String Name { get; set; }
        public String Description { get; set; }
    }
}
