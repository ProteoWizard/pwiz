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
        public static readonly BackgroundProteome NONE =
            new BackgroundProteome(BackgroundProteomeList.GetDefault());

        private HashSet<Digestion> Digestions { get; set; }
        public BackgroundProteome(BackgroundProteomeSpec backgroundProteomeSpec) : this(backgroundProteomeSpec, false)
        {
        }

        public BackgroundProteome(BackgroundProteomeSpec backgroundProteomeSpec, bool queryDigestions)
            : base(backgroundProteomeSpec.Name, backgroundProteomeSpec.DatabasePath)
        {
            Digestions = new HashSet<Digestion>();
            if (queryDigestions)
            {
                if (!IsNone)
                {
                    try
                    {
                        Digestions.UnionWith(OpenProteomeDb().ListDigestions());
                    }
                    catch (Exception)
                    {
                        DatabaseInvalid = true;
                    }
                }
                DatabaseValidated = true;
            }
        }

        private BackgroundProteome()
        {
            Digestions = new HashSet<Digestion>();
        }

        public Digestion GetDigestion(Enzyme enzyme, DigestSettings digestSettings)
        {
            foreach (var entry in Digestions)
            {
                if (entry.Name == enzyme.Name)
                {
                    return entry;
                }
            }
            return null;
        }

        public Digestion GetDigestion(PeptideSettings peptideSettings)
        {
            return GetDigestion(peptideSettings.Enzyme, peptideSettings.DigestSettings);
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
            if (Digestions.Count != that.Digestions.Count)
            {
                return false;
            }
            foreach (var entry in Digestions)
            {
                if (!that.Digestions.Contains(entry))
                {
                    return false;
                }
            }
            return true;
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
