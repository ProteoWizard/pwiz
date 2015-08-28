/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Optimization
{
    [XmlRoot("optimized_library")]
    public class OptimizationLibrary : XmlNamedElement
    {
        public static readonly OptimizationLibrary NONE = new OptimizationLibrary("None", string.Empty); // Not L10N

        private OptimizationDb _database;

        public OptimizationLibrary(string name, string databasePath)
            : base(name)
        {
            DatabasePath = databasePath;
        }

        public string DatabasePath { get; private set; }

        public bool IsNone
        {
            get { return Name == NONE.Name; }
        }

        public bool IsUsable
        {
            get { return _database != null; }
        }

        public OptimizationLibrary Initialize(SrmDocument document, IProgressMonitor loadMonitor)
        {
            if (_database != null || IsNone)
                return this;

            var database = OptimizationDb.GetOptimizationDb(DatabasePath, loadMonitor, document);
            // Check for the case where an exception was handled by the progress monitor
            if (database == null)
                return null;
            return ChangeDatabase(database);
        }

        public string PersistencePath
        {
            get { return DatabasePath; }
        }

        /// <summary>
        /// Saves the database to a new directory with only the optimizations used
        /// in a given document.
        /// </summary>
        /// <param name="pathDestDir">The directory to save to</param>
        /// <param name="document">The document for which peptides are to be kept</param>
        /// <returns>The full path to the file saved</returns>
        public string PersistMinimized(string pathDestDir, SrmDocument document)
        {
            RequireUsable();

            string persistPath = Path.Combine(pathDestDir, Path.GetFileName(PersistencePath) ?? string.Empty);  // ReSharper
            using (var fs = new FileSaver(persistPath))
            {
                var optDbMinimal = OptimizationDb.CreateOptimizationDb(fs.SafeName);

                // Calculate the minimal set of optimizations needed for this document
                var persistOptimizations = new List<DbOptimization>();
                var dictOptimizations = _database.GetOptimizations().ToDictionary(opt => opt.Key);
                foreach (PeptideGroupDocNode seq in document.MoleculeGroups)
                {
                    // Skip peptide groups with no transitions
                    if (seq.TransitionCount == 0)
                        continue;
                    foreach (PeptideDocNode peptide in seq.Children)
                    {
                        foreach (TransitionGroupDocNode group in peptide.Children)
                        {
                            string modSeq = document.Settings.GetSourceTextId(peptide); 
                            int charge = group.PrecursorCharge;
                            foreach (TransitionDocNode transition in group.Children)
                            {
                                foreach (var optType in Enum.GetValues(typeof(OptimizationType)).Cast<OptimizationType>())
                                {
                                    var optimizationKey = new OptimizationKey(optType, modSeq, charge, transition.FragmentIonName, transition.Transition.Charge);
                                    DbOptimization dbOptimization;
                                    if (dictOptimizations.TryGetValue(optimizationKey, out dbOptimization))
                                    {
                                        persistOptimizations.Add(new DbOptimization(dbOptimization.Key, dbOptimization.Value));
                                        // Only add once
                                        dictOptimizations.Remove(optimizationKey);
                                    }
                                }
                            }
                        }
                    }
                }

                optDbMinimal.UpdateOptimizations(persistOptimizations, new DbOptimization[0]);
                fs.Commit();
            }

            return persistPath;
        }

        public IEnumerable<DbOptimization> GetOptimizations()
        {
            RequireUsable();
            return _database.GetOptimizations();
        }

        public DbOptimization GetOptimization(OptimizationType type, string seq, int charge, string fragment, int productCharge)
        {
            RequireUsable();
            var key = new OptimizationKey(type, seq, charge, fragment, productCharge);
            double value;
            return (_database.DictLibrary.TryGetValue(new OptimizationKey(type, seq, charge, fragment, productCharge), out value))
                ? new DbOptimization(key, value)
                : null;
        }

        public DbOptimization GetOptimization(OptimizationType type, string seq, int charge)
        {
            return GetOptimization(type, seq, charge, null, 0);
        }

        public bool HasType(OptimizationType type)
        {
            return GetOptimizations().Any(opt => Equals(opt.Type, (int) type));
        }

        private void RequireUsable()
        {
            if (!IsUsable)
                throw new InvalidOperationException(Resources.OptimizationLibrary_RequireUsable_Unexpected_use_of_optimization_library_before_successful_initialization_);
        }

        #region Property change methods

        public OptimizationLibrary ChangeDatabasePath(string path)
        {
            return ChangeProp(ImClone(this), im => im.DatabasePath = path);
        }

        public OptimizationLibrary ChangeDatabase(OptimizationDb database)
        {
            return ChangeProp(ImClone(this), im => im._database = database);
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For serialization
        /// </summary>
        private OptimizationLibrary()
        {
        }

        enum ATTR
        {
            database_path
        }

        public static OptimizationLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new OptimizationLibrary());
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
            writer.WriteAttribute(ATTR.database_path, DatabasePath ?? string.Empty);
        }

        #endregion

        #region object overrrides

        public bool Equals(OptimizationLibrary other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) && Equals(other._database, _database) && Equals(other.DatabasePath, DatabasePath);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as OptimizationLibrary);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ DatabasePath.GetHashCode();
                // TODO: Get the code for getting a reference equality hashcode from Nick
                result = (result*397) ^ (_database != null ? _database.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }
}
