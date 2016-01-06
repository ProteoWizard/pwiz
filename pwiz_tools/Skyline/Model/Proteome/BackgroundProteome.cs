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
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Proteome
{
    /// <summary>
    /// Class representing the state of a background proteome database.  The proteome is only digested when it is first needed,
    /// so the BackgroundProteome class keeps track of which enzymes have been used to digest the proteome, and what the
    /// value of "MaxMissedCleavages" was when the digestion was performed.
    /// </summary>
    [XmlRoot("background_proteome")]
    public class BackgroundProteome : BackgroundProteomeSpec
    {
        private bool? _needsProteinMetadataSearch; // Nullable for lazy evaluation, for quicker ctor

        public static readonly BackgroundProteome NONE =
            new BackgroundProteome(BackgroundProteomeList.GetDefault());

        private HashSet<string> DigestionNames { get; set; }
        public BackgroundProteome(BackgroundProteomeSpec backgroundProteomeSpec) : this(backgroundProteomeSpec, false)
        {
            // We intentionally do not copy the _needsProteinMetadataSearch member
        }

        public BackgroundProteome(BackgroundProteomeSpec backgroundProteomeSpec, bool queryDigestions)
            : base(backgroundProteomeSpec.Name, backgroundProteomeSpec.DatabasePath)
        {
            DigestionNames = new HashSet<string>();
            if (!IsNone)
            {
                try
                {
                    using (var proteomeDb = OpenProteomeDb())
                    {
                        if (queryDigestions)
                            DigestionNames.UnionWith(proteomeDb.ListDigestions().Select(digestion => digestion.Name));
                    }
                }
                catch (Exception)
                {
                    DatabaseInvalid = true;
                }
            }
            if (queryDigestions)
                DatabaseValidated = true;
        }

        private BackgroundProteome()
        {
            DigestionNames = new HashSet<string>();
        }

        public bool NeedsProteinMetadataSearch
        {
            get
            {
                if (!_needsProteinMetadataSearch.HasValue)  // Lazy evaluation for quicker ctor
                {
                    _needsProteinMetadataSearch = true; // Until proven otherwise
                    if (!IsNone)
                    {
                        try
                        {
                            using (var proteomeDb = OpenProteomeDb())
                            {
                                _needsProteinMetadataSearch = proteomeDb.HasProteinNamesWithUnresolvedMetadata();
                            }
                        }
                        catch (Exception)
                        {
                            DatabaseInvalid = true;
                        }
                    }
                }
                return _needsProteinMetadataSearch.Value;
            }
        }


        public bool HasDigestion(PeptideSettings peptideSettings)
        {
            return DigestionNames.Contains(peptideSettings.Enzyme.Name);
        }
        
        public Digestion GetDigestion(ProteomeDb proteomeDb, PeptideSettings peptideSettings)
        {
            return proteomeDb.GetDigestion(peptideSettings.Enzyme.Name);
        }

        /// <summary>
        /// True if the database file does not exist
        /// </summary>
        public bool DatabaseInvalid { get; private set; }
        /// <summary>
        /// True if we have checked whether the database file exists
        /// </summary>
        public bool DatabaseValidated { get; private set; }

        public BackgroundProteomeSpec BackgroundProteomeSpec
        {
            get { return this;}
        }

        public new static BackgroundProteome Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new BackgroundProteome());
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override bool Equals(Object other)
        {
            if (other == this)
            {
                return true;
            }
            if (!base.Equals(other))
            {
                return false;
            }
            BackgroundProteome that = other as BackgroundProteome;
            if (that == null)
            {
                return false;
            }
            if (DatabaseInvalid != that.DatabaseInvalid 
                || DatabaseValidated != that.DatabaseValidated)
            {
                return false;
            }
            return DigestionNames.SetEquals(that.DigestionNames);
        }
        // ReSharper disable InconsistentNaming
        public enum DuplicateProteinsFilter
        {
            NoDuplicates,
            FirstOccurence,
            AddToAll
        }
        // ReSharper restore InconsistentNaming
    }
}
