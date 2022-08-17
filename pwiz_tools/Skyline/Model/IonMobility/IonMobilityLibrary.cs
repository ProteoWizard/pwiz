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
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.IonMobility
{
    [XmlRoot("ionmobility_library_spec")]
    public class IonMobilityLibrarySpec : XmlNamedElement, IEquatable<IonMobilityLibrarySpec> 
    {
        public const string EXT = IonMobilityDb.EXT;

        public string FilePath { get; protected set; }

        public static string FILTER_IONMOBILITYLIBRARY
        {
            get { return TextUtil.FileDialogFilter(Resources.IonMobilityDb_FILTER_IONMOBILITYLIBRARY_Ion_Mobility_Library_Files, EXT); }
        }

        public IonMobilityLibrarySpec(string name, string path) : base(name)
        {
            FilePath = path;
        }

        /// <summary>
        /// For serialization
        /// </summary>
        protected IonMobilityLibrarySpec()
        {
        }
        
        public string Filter
        {
            get { return FILTER_IONMOBILITYLIBRARY; }
        }

        public bool IsNone
        {
            get { return Name == IonMobilityLibrary.NONE.Name; }
        }

        public bool Equals(IonMobilityLibrarySpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return Name == other.Name && FilePath == other.FilePath;
        }


        public override bool Equals(object obj)
        {
            return ReferenceEquals(this, obj) || obj is IonMobilityLibrarySpec other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Name != null ? Name.GetHashCode() : 0) * 397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
            }
        }

        public static bool operator ==(IonMobilityLibrarySpec left, IonMobilityLibrarySpec right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(IonMobilityLibrarySpec left, IonMobilityLibrarySpec right)
        {
            return !Equals(left, right);
        }

    }

    [XmlRoot("ion_mobility_library")]
    public class IonMobilityLibrary : IonMobilityLibrarySpec
    {
        public static readonly IonMobilityLibrary NONE = new IonMobilityLibrary(@"None", String.Empty, null);

        private IonMobilityDb _database;

        public IonMobilityLibrary(string name, string filePath, IonMobilityDb loadedDatabase) : base(name, filePath)
        {
            _database = loadedDatabase;
        }

        [Track]
        public AuditLogPath FilePathAuditLog
        {
            get { return AuditLogPath.Create(FilePath); }
        }

        // [TrackChildren] Audit log granularity is at the file level
        public LibKeyMap<List<IonMobilityAndCCS>> IonMobilityValues
        {
            get { return _database == null ? null : _database.DictLibrary; }
        }

        [TrackChildren]
        public IonMobilityDb.IonMobilityLibraryChange Status { get { return _database == null ? IonMobilityDb.IonMobilityLibraryChange.NONE : _database.LastChange; } }

        public int Count { get { return _database == null || _database.DictLibrary == null ? -1 : _database.DictLibrary.Count; } }  // How many entries in library?

        public bool IsUsable
        {
            get { return _database != null; }
        }

        public IonMobilityLibrary Initialize(IProgressMonitor loadMonitor)
        {
            if (_database != null)
                return this;
            var database = IonMobilityDb.GetIonMobilityDb(FilePath, loadMonitor);
            // Check for the case where an exception was handled by the progress monitor
            if (database == null)
                return null;
            return ChangeDatabase(database);
        }

        public IonMobilityLibrary ChangeDatabasePath(string path)
        {
            return ChangeProp(ImClone(this), im => im.FilePath = path);
        }

        /// <summary>
        /// Saves the database to a new directory with only the ions used
        /// in a given document.
        /// </summary>
        /// <param name="pathDestDir">The directory to save to</param>
        /// <param name="document">The document for which ions are to be kept</param>
        /// <param name="smallMoleculeConversionMap">Used for changing charge,modifedSeq to adduct,molecule in small molecule conversion</param>
        /// <param name="loadedDatabase">Returns in-memory representation of the revised ion mobility table</param>
        /// <returns>The full path to the file saved</returns>
        public string PersistMinimized(string pathDestDir,
            SrmDocument document, IDictionary<LibKey, LibKey> smallMoleculeConversionMap, out IonMobilityDb loadedDatabase)
        {
            RequireUsable();

            var fname = Path.GetFileName(FilePath);
            if (smallMoleculeConversionMap != null && fname != null && 
                !fname.Contains(BiblioSpecLiteSpec.DotConvertedToSmallMolecules))
            {
                fname = fname.Replace(IonMobilityDb.EXT, BiblioSpecLiteSpec.DotConvertedToSmallMolecules + IonMobilityDb.EXT);
            }

            fname = fname ?? string.Empty; // Keeps ReSharper from complaining about possible null
            string persistPath = Path.Combine(pathDestDir, fname); 
            using (var fs = new FileSaver(persistPath))
            {
                var libraryName = fname.Replace(IonMobilityDb.EXT, String.Empty);
                var ionMobilityDbMinimal = IonMobilityDb.CreateIonMobilityDb(fs.SafeName, libraryName, true);

                // Calculate the minimal set of peptides needed for this document
                var dbPrecursors = _database.DictLibrary.LibKeys;
                var persistIonMobilities = new List<PrecursorIonMobilities>();

                var dictPrecursors = dbPrecursors.ToDictionary(p => p.LibraryKey);
                foreach (var pair in document.MoleculePrecursorPairs)
                {
                    var key = pair.NodeGroup.GetLibKey(document.Settings, pair.NodePep);
                    if (dictPrecursors.TryGetValue(key, out var dbPrecursor))
                    {
                        var im = _database.GetIonMobilityInfo(key);

                        if (smallMoleculeConversionMap != null)
                        {
                            // We are in the midst of converting a document to small molecules for test purposes
                            LibKey smallMolInfo;
                            if (smallMoleculeConversionMap.TryGetValue(new LibKey(pair.NodePep.ModifiedSequence, pair.NodeGroup.PrecursorCharge), out smallMolInfo))
                            {
                                var precursorAdduct = smallMolInfo.Adduct;
                                var smallMoleculeAttributes = smallMolInfo.SmallMoleculeLibraryAttributes;
                                dbPrecursor = new LibKey(smallMoleculeAttributes, precursorAdduct);
                            }
                            else
                            {
                                // Not being converted
                                Assume.IsTrue(pair.NodeGroup.Peptide.IsDecoy);
                                continue;
                            }
                        }
                        persistIonMobilities.Add(new PrecursorIonMobilities(dbPrecursor, im));
                        // Only add once
                        dictPrecursors.Remove(key);
                    }
                }

                loadedDatabase = ionMobilityDbMinimal.UpdateIonMobilities(persistIonMobilities);
                fs.Commit();
            }

            return persistPath;
        }

        public static Dictionary<LibKey, List<IonMobilityAndCCS>> FlatListToMultiConformerDictionary(
            IEnumerable<ValidatingIonMobilityPrecursor> mobilitiesFlat)
        {
            // Put the list into a dict for performance reasons
            var ionMobilities = new Dictionary<LibKey, List<IonMobilityAndCCS>>();
            foreach (var item in mobilitiesFlat)
            {
                var libKey = item.Precursor;
                var ionMobilityAndCcs = item.GetIonMobilityAndCCS();
                if (!ionMobilities.TryGetValue(libKey, out var mobilities))
                {
                    ionMobilities.Add(libKey, new List<IonMobilityAndCCS>() { ionMobilityAndCcs });
                }
                else
                {
                    // Multiple conformer, or just a redundant line? Discard if this CCS+units pair is already in list
                    if (!mobilities.Any(m =>
                        ((m.CollisionalCrossSectionSqA.HasValue || ionMobilityAndCcs.CollisionalCrossSectionSqA.HasValue) ?
                            Equals(m.CollisionalCrossSectionSqA, ionMobilityAndCcs.CollisionalCrossSectionSqA) && Equals(m.Units, ionMobilityAndCcs.Units) :
                            Equals(m.IonMobility, ionMobilityAndCcs.IonMobility))))
                    {
                        mobilities.Add(ionMobilityAndCcs);
                    }
                }
            }

            return ionMobilities;
        }

        public static List<ValidatingIonMobilityPrecursor> MultiConformerDictionaryToFlatList(
            IDictionary<LibKey, List<IonMobilityAndCCS>> mobilitiesDict)
        {
            var ionMobilities = new List<ValidatingIonMobilityPrecursor>();
            foreach (var item in mobilitiesDict)
            {
                var libKey = item.Key;
                foreach (var im in item.Value)
                {
                    ionMobilities.Add(new ValidatingIonMobilityPrecursor(libKey, im));
                }
            }

            return ionMobilities;
        }


        public static IonMobilityLibrary CreateFromDictionary(string libraryName, string dbDir, IDictionary<LibKey, List<IonMobilityAndCCS>> dict)
        {
            var fname = Path.GetFullPath(dbDir.Contains(IonMobilityDb.EXT) ? dbDir : Path.Combine(dbDir, libraryName + IonMobilityDb.EXT));
            IonMobilityDb ionMobilityDb;
            using (var fs = new FileSaver(fname))
            {
                ionMobilityDb = IonMobilityDb.CreateIonMobilityDb(fs.SafeName, libraryName, false);
                if (dict != null)
                {
                    var list = dict.Select(kvp => new PrecursorIonMobilities(kvp.Key, kvp.Value));
                    ionMobilityDb = ionMobilityDb.UpdateIonMobilities(list);
                }
                fs.Commit();
            }
            return new IonMobilityLibrary(libraryName, fname, ionMobilityDb);
        }

        public static IonMobilityLibrary CreateFromList(string libraryName, string dbDir, IList<ValidatingIonMobilityPrecursor> list)
        {
            var dict = FlatListToMultiConformerDictionary(list);
            return CreateFromDictionary(libraryName, dbDir, dict);
        }

        public IList<IonMobilityAndCCS> GetIonMobilityInfo(LibKey key)
        {
            if (_database != null)
                return _database.GetIonMobilityInfo(key);
            return null;
        }

        public LibKeyMap<List<IonMobilityAndCCS>> GetIonMobilityLibKeyMap()
        {
            if (_database != null)
                return _database.DictLibrary;
            return null;
        }

        private void RequireUsable()
        {
            if (!IsUsable)
                throw new InvalidOperationException(@"Unexpected use of ion mobility library before successful initialization."); // - for developer use
        }

        public static Dictionary<LibKey, IonMobilityAndCCS> CreateFromResults(SrmDocument document, string documentFilePath, bool useHighEnergyOffset,
            IProgressMonitor progressMonitor = null)
        {
            // Overwrite any existing measurements with newly derived ones
            // N.B. assumes we are not attempting to find multiple conformers
            // (so, returns Dictionary<LibKey, IonMobilityAndCCS> instead of Dictionary<LibKey, IList<IonMobilityAndCCS>>)
            Dictionary<LibKey, IonMobilityAndCCS> measured;
            using (var finder = new IonMobilityFinder(document, documentFilePath, progressMonitor) { UseHighEnergyOffset = useHighEnergyOffset })
            {
                measured = finder.FindIonMobilityPeaks(); // Returns null on cancel
            }
            return measured;
        }

        public static IonMobilityLibrary CreateFromResults(SrmDocument document, string documentFilePath, bool useHighEnergyOffset,
            string libraryName, string dbPath, IProgressMonitor progressMonitor = null)
        {
            // Overwrite any existing measurements with newly derived ones
            var measured = CreateFromResults(document, documentFilePath, useHighEnergyOffset, progressMonitor);
            var ionMobilityDb = IonMobilityDb.CreateIonMobilityDb(dbPath, libraryName, false).
                UpdateIonMobilities(measured.Select(m => new PrecursorIonMobilities(
                m.Key, m.Value)).ToList());
            return new IonMobilityLibrary(libraryName, dbPath, ionMobilityDb);
        }

        #region Property change methods

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
            FilePath = reader.GetAttribute(ATTR.database_path);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.database_path, FilePath ?? String.Empty);
        }

        /// <summary>
        /// Write XML representation, possibly in backward compatible format using
        /// old style in-document serialization
        /// </summary>
        public void WriteXml(XmlWriter writer, IonMobilityWindowWidthCalculator extraInfoForPre20_12)
        {
            if (extraInfoForPre20_12 == null)
            {
                WriteXml(writer);
                return;
            }

            // Write the contents of the currently-in-use .imsdb to old style in-document serialization
            var libKeyMap = GetIonMobilityLibKeyMap();
            if (libKeyMap == null)
                return;
            var dict = libKeyMap.AsDictionary();
            if (dict != null && dict.Any())
            {
                var oldDict =
                    dict.ToDictionary(kvp => kvp.Key,
                        kvp => kvp.Value.First()); // No multiple conformers in earlier formats
                var dtp = new DriftTimePredictor(Name,
                    oldDict, extraInfoForPre20_12.WindowWidthMode, extraInfoForPre20_12.ResolvingPower,
                    extraInfoForPre20_12.PeakWidthAtIonMobilityValueZero,
                    extraInfoForPre20_12.PeakWidthAtIonMobilityValueMax,
                    extraInfoForPre20_12.FixedWindowWidth);
                writer.WriteStartElement(DriftTimePredictor.EL.predict_drift_time); // N.B. EL.predict_drift_time is a misnomer, this covers all IMS types
                dtp.WriteXml(writer);
                writer.WriteEndElement();
            }
        }

        #endregion

        #region object overrrides

        public bool Equals(IonMobilityLibrary other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            if (!base.Equals(other))
                return false;
            // N.B. not comparing Status members, which exist only for the benefit of audit logging
            if (!Equals(other.FilePath, FilePath))
                return false;
            if (!Equals(Count, other.Count))
                return false;
            if (Count <= 0)
                return true; // Both not yet in memory
            foreach (var key in _database.DictLibrary.LibKeys)
            {
                if (!other._database.DictLibrary.TryGetValue(key, out var otherValues))
                    return false;
                _database.DictLibrary.TryGetValue(key, out var values);
                if (values.Count != otherValues.Count)
                    return false;
                foreach (var ionMobilityAndCcs in values)
                {
                    if (!otherValues.Contains(ionMobilityAndCcs))
                        return false;
                }
            }
            return true;
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
                result = (result * 397) ^ FilePath.GetHashCode();
                result = (result*397) ^ (_database != null ? _database.GetHashCode() : 0);
                return result;
            }
        }

        #endregion
    }

}
