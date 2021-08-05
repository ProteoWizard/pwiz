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
using System.IO;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.ProteomeDatabase.API;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.Skyline.Model.Proteome
{
    /// <summary>
    /// Class representing a background proteome database.  Background proteome databases have a name,
    /// as well as a path to the file on disk.
    /// </summary>
    [XmlRoot("background_proteome")]
    public class BackgroundProteomeSpec : XmlNamedElement
    {
        public BackgroundProteomeSpec(string name, string databasePath)
            : base(name)
        {
            DatabasePath = databasePath;
        }

        protected BackgroundProteomeSpec()
        {
        }

        private enum Attr
        {
            database_path,
        }

        [Track(defaultValues: typeof(DefaultValuesNull))]
        public AuditLogPath DatabasePathAuditLog
        {
            get { return AuditLogPath.Create(DatabasePath); }
        }
        
        public string DatabasePath { get; private set; }

        public bool IsNone
        {
            get { return Name == BackgroundProteomeList.GetDefault().Name; }
        }

        public ProteomeDb OpenProteomeDb()
        {
            return ProteomeDb.OpenProteomeDb(DatabasePath);
        }

        public ProteomeDb OpenProteomeDb(CancellationToken cancellationToken)
        {
            return ProteomeDb.OpenProteomeDb(DatabasePath, cancellationToken);
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            DatabasePath = reader.GetAttribute(Attr.database_path);
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttributeString(Attr.database_path, DatabasePath);
        }

        private static readonly IXmlElementHelper<BackgroundProteomeSpec>[] BACKGROUND_PROTEOME_HELPERS =
            {
                new XmlElementHelper<BackgroundProteomeSpec>(), 
                new XmlElementHelperSuper<BackgroundProteome, BackgroundProteomeSpec>()
            };

        public static IXmlElementHelper<BackgroundProteomeSpec>[] BackgroundProteomeHelpers
        {
            get { return BACKGROUND_PROTEOME_HELPERS; }
        }

        public static BackgroundProteomeSpec Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new BackgroundProteomeSpec());
        }

        public FastaSequence GetFastaSequence(String proteinName)
        {
            ProteinMetadata metadata;
            return GetFastaSequence(proteinName, out metadata);
        }

        public FastaSequence GetFastaSequence(String proteinName, out ProteinMetadata foundMetadata)
        {
            foundMetadata = null;
            if (IsNone)
                return null;

            using (var proteomeDb = OpenProteomeDb())
            {
                Protein protein = proteomeDb.GetProteinByName(proteinName);
                if (protein == null)
                {
                    return null;
                }
                foundMetadata = protein.ProteinMetadata;
                return MakeFastaSequence(protein);
            }
        }

        public FastaSequence MakeFastaSequence(Protein protein)
        {
            List<ProteinMetadata> alternativeProteins = new List<ProteinMetadata>();
            foreach (var alternativeName in protein.AlternativeNames)
            {
                alternativeProteins.Add(alternativeName);
            }
            return new FastaSequence(protein.ProteinMetadata.Name, protein.ProteinMetadata.Description, alternativeProteins, protein.Sequence);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (!(obj is BackgroundProteomeSpec)) return false;
            return EqualsSpec((BackgroundProteomeSpec)obj);
        }

        public bool EqualsSpec(BackgroundProteomeSpec other)
        {
            return Name == other.Name && DatabasePath == other.DatabasePath;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                {
                    return (base.GetHashCode() * 397) ^ (DatabasePath != null ? DatabasePath.GetHashCode() : 0);
                }
            }
        }
    }

    public class ProteaseImpl
    {
        private readonly Enzyme _enzyme;
        public ProteaseImpl(Enzyme enzyme)
        {
            // Background proteome databases cannot yet deal with semi-cleaving enzymes
            _enzyme = !enzyme.IsSemiCleaving ? enzyme : enzyme.ChangeSemiCleaving(false);
        }

        public IEnumerable<DigestedPeptide> Digest(Protein protein, int maxMissedCleavages)
        {
            return DigestSequence(protein.Sequence, maxMissedCleavages, null);
        }

        public IEnumerable<DigestedPeptide> DigestSequence(string proteinSequence, int maxMissedCleavages, int? maxPeptideSequenceLength)
        {
            if (string.IsNullOrEmpty(proteinSequence))
            {
                yield break;
            }
            FastaSequence fastaSequence;
            try
            {
                fastaSequence = new FastaSequence(@"name", @"description", new List<ProteinMetadata>(), proteinSequence);
            }
            catch (InvalidDataException)
            {
                // It's possible that the peptide sequence in the fasta file was bogus, in which case we just don't digest it.
                yield break;
            }
            var digestSettings = new DigestSettings(maxMissedCleavages, false);
            foreach (var digest in _enzyme.Digest(fastaSequence, digestSettings, maxPeptideSequenceLength))
            {
                var digestedPeptide = new DigestedPeptide
                {
                    Index = digest.Begin ?? 0,
                    Sequence = digest.Target.Sequence
                };
                yield return digestedPeptide;
            }
        }
        public string Name { get { return _enzyme.Name; } }
    }
}

