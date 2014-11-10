/*
 * Original author: Brian Pratt <bspratt .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Linq;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.SettingsUI.IonMobility;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.IonMobility
{
    [XmlRoot("ion_mobility_library")]
    public class IonMobilityLibrary : IonMobilityLibrarySpec
    {
        public static readonly IonMobilityLibrary NONE = new IonMobilityLibrary("None", String.Empty); // Not L10N

        private IonMobilityDb _database;

        public IonMobilityLibrary(string name, string databasePath)
            : base(name)
        {
            DatabasePath = databasePath;
        }

        public string DatabasePath { get; private set; }

        public override bool IsNone
        {
            get { return Name == NONE.Name; }
        }

        public override bool IsUsable
        {
            get { return _database != null; }
        }

        public override IonMobilityLibrarySpec Initialize(IProgressMonitor loadMonitor)
        {
            if (_database != null)
                return this;
            var database = IonMobilityDb.GetIonMobilityDb(DatabasePath, loadMonitor);
            // Check for the case where an exception was handled by the progress monitor
            if (database == null)
                return null;
            return ChangeDatabase(database);
        }

        public override string PersistencePath
        {
            get { return DatabasePath; }
        }

        /// <summary>
        /// Saves the database to a new directory with only the charged peptides used
        /// in a given document.
        /// </summary>
        /// <param name="pathDestDir">The directory to save to</param>
        /// <param name="document">The document for which charged peptides are to be kept</param>
        /// <returns>The full path to the file saved</returns>
        public override string PersistMinimized(string pathDestDir, SrmDocument document)
        {
            RequireUsable();

            string persistPath = Path.Combine(pathDestDir, Path.GetFileName(PersistencePath) ?? String.Empty); 
            using (var fs = new FileSaver(persistPath))
            {
                var ionMobilityDbMinimal = IonMobilityDb.CreateIonMobilityDb(fs.SafeName);

                // Calculate the minimal set of peptides needed for this document
                var dbPeptides = _database.GetPeptides().ToList();
                var persistPeptides = new List<ValidatingIonMobilityPeptide>();
                var dictPeptides = dbPeptides.ToDictionary(pep => pep.PeptideModSeq);
                foreach (var peptide in document.Molecules)
                {
                    string modifiedSeq = document.Settings.GetSourceTextId(peptide);
                    DbIonMobilityPeptide dbPeptide;
                    if (dictPeptides.TryGetValue(modifiedSeq, out dbPeptide))
                    {
                        persistPeptides.Add(new ValidatingIonMobilityPeptide(dbPeptide.Sequence,dbPeptide.CollisionalCrossSection,dbPeptide.HighEnergyDriftTimeOffsetMsec));
                        // Only add once
                        dictPeptides.Remove(modifiedSeq);
                    }
                }

                ionMobilityDbMinimal.UpdatePeptides(persistPeptides, new ValidatingIonMobilityPeptide[0]);
                fs.Commit();
            }

            return persistPath;
        }

        public override DriftTimeInfo GetDriftTimeInfo(String seq, ChargeRegressionLine regressionLine)
        {
            if (_database != null)
                return _database.GetDriftTimeInfo(seq, regressionLine);
            return null;
        }

        private void RequireUsable()
        {
            if (!IsUsable)
                throw new InvalidOperationException("Unexpected use of ion mobility library before successful initialization."); // Not L10N - for developer use
        }

        #region Property change methods

        public IonMobilityLibrary ChangeDatabasePath(string path)
        {
            return ChangeProp(ImClone(this), im => im.DatabasePath = path);
        }

        public IonMobilityLibrary ChangeDatabase(IonMobilityDb database)
        {
            return ChangeProp(ImClone(this), im => im._database = database);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private IonMobilityLibrary()
        {
        }

        enum ATTR
        {
            database_path
        }

        public static IonMobilityLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new IonMobilityLibrary());
        }

        public override void ReadXml(XmlReader reader)
        {
            base.ReadXml(reader);
            DatabasePath = reader.GetAttribute(ATTR.database_path);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.database_path, DatabasePath ?? String.Empty);
        }

        #endregion

        #region object overrrides

        public bool Equals(IonMobilityLibrary other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other._database, _database) && Equals(other.DatabasePath, DatabasePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as IonMobilityLibrary);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ DatabasePath.GetHashCode();
                result = (result*397) ^ (_database != null ? _database.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

}