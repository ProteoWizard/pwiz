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
using pwiz.Common.Chemistry;
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

        public static bool IsNullOrEmpty(IonMobilityLibrarySpec lib)
        {
            return lib == null || lib.IsNone;
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
                var persistIonMobilities = new List<PrecursorIonMobilities>();
                var processed = new HashSet<LibKey>();

                foreach (var pair in document.MoleculePrecursorPairs)
                {
                    var test = 
                        new PrecursorIonMobilities(pair.NodePep.ModifiedTarget, pair.NodeGroup.PrecursorAdduct, 0, 0, 0, eIonMobilityUnits.none);
                    var dbPrecursor = new LibKey(test.Target, test.PrecursorAdduct, PrecursorFilter.EMPTY);
                    if (processed.Contains(dbPrecursor))
                    {
                        continue;
                    }
                    processed.Add(dbPrecursor);

                    var dbHits = _database.DictLibrary.ItemsMatching(dbPrecursor, LibKeyIndex.LibraryMatchType.ion).ToList();
                    if (dbHits.Any())
                    {
                        if (smallMoleculeConversionMap != null)
                        {
                            // We are in the midst of converting a document to small molecules for test purposes
                            LibKey smallMolInfo;
                            if (smallMoleculeConversionMap.TryGetValue(new LibKey(pair.NodePep.ModifiedSequence, pair.NodeGroup.PrecursorCharge, PrecursorFilter.EMPTY), out smallMolInfo))
                            {
                                var precursorAdduct = smallMolInfo.Adduct;
                                var smallMoleculeAttributes = smallMolInfo.SmallMoleculeLibraryAttributes;
                                dbPrecursor = new LibKey(smallMoleculeAttributes, precursorAdduct, PrecursorFilter.EMPTY);
                            }
                            else
                            {
                                // Not being converted
                                Assume.IsTrue(pair.NodeGroup.Peptide.IsDecoy);
                                continue;
                            }
                        }
                        persistIonMobilities.Add(new PrecursorIonMobilities(dbPrecursor.Target, dbPrecursor.Adduct, test.IonMobilities));
                    }
                }

                loadedDatabase = ionMobilityDbMinimal.UpdateIonMobilities(persistIonMobilities);
                fs.Commit();
            }

            return persistPath;
        }

        // For use in creating a hashset of non-redundant precursors+CCS values
        // Considers two precursors equal if molecule and adduct agree, and
        // CCS values agree (unless ion mobility units disagree, i.e. we have values for drift and for 1/K0)
        private class IonAndCCSComparer : IEqualityComparer<LibraryKey>
        {
            public bool Equals(LibraryKey x, LibraryKey y)
            {
                if (x == null || y == null)
                {
                    return x == null && y == null;
                }

                if (Equals(x.Adduct, y.Adduct) && Equals(x.Target, y.Target))
                {
                    // Same ion
                    if (x.PrecursorFilter.CollisionalCrossSectionSqA.HasValue || y.PrecursorFilter.CollisionalCrossSectionSqA.HasValue)
                    {
                        // Consider equal if CCS matches, and IM units match (so we don't admit IM conflicts)
                        return Equals(x.PrecursorFilter.CollisionalCrossSectionSqA, y.PrecursorFilter.CollisionalCrossSectionSqA) && 
                               Equals(x.PrecursorFilter.IonMobilityUnits, y.PrecursorFilter.IonMobilityUnits);
                    }
                    if (x.PrecursorFilter.IonMobility.HasValue || y.PrecursorFilter.IonMobility.HasValue)
                    {
                        return Equals(x.PrecursorFilter.IonMobility, y.PrecursorFilter.IonMobility);
                    }
                }

                return false;
            }

            public int GetHashCode(LibraryKey obj)
            {
                var result =  obj.Target.GetHashCode();
                result = (result * 397) ^ obj.Adduct.GetHashCode();
                if (obj.PrecursorFilter.CollisionalCrossSectionSqA.HasValue)
                {
                    result = (result * 397) ^ obj.PrecursorFilter.CollisionalCrossSectionSqA.GetHashCode();
                }
                else
                {
                    result = (result * 397) ^ obj.PrecursorFilter.IonMobility.GetHashCode();
                }
                return result;
            }
        }

        public static LibKeyIndex FlatListToLibKeyIndex(IEnumerable<ValidatingIonMobilityPrecursor> mobilitiesFlat)
        {
            // Put the list into a hash for performance reasons
            var ionMobilities = new HashSet<LibraryKey>(new IonAndCCSComparer());
            foreach (var item in mobilitiesFlat)
            {
                ionMobilities.Add(item.Precursor);
            }
            return new LibKeyIndex(ionMobilities.ToList());
        }

        public static List<ValidatingIonMobilityPrecursor> MultiConformerDictionaryToFlatList(LibKeyIndex mobilitiesDict)
        {
            var ionMobilities = new List<ValidatingIonMobilityPrecursor>();
            foreach (var item in mobilitiesDict)
            {
                var libKey = item.LibraryKey;
                ionMobilities.Add(new ValidatingIonMobilityPrecursor(libKey.Target, libKey.Adduct, libKey.PrecursorFilter.IonMobilityAndCCS));
            }

            return ionMobilities;
        }


        public static IonMobilityLibrary CreateFromLibKeyIndex(string libraryName, string dbDir, LibKeyIndex dict)
        {
            var fname = Path.GetFullPath(dbDir.Contains(IonMobilityDb.EXT) ? dbDir : Path.Combine(dbDir, libraryName + IonMobilityDb.EXT));
            IonMobilityDb ionMobilityDb;
            using (var fs = new FileSaver(fname))
            {
                ionMobilityDb = IonMobilityDb.CreateIonMobilityDb(fs.SafeName, libraryName, false);
                if (dict != null)
                {
                    var list = dict.Select(k => new PrecursorIonMobilities(k.LibraryKey.Target, k.LibraryKey.Adduct, k.LibraryKey.PrecursorFilter.IonMobilityAndCCS));
                    ionMobilityDb = ionMobilityDb.UpdateIonMobilities(list);
                }
                fs.Commit();
            }
            return new IonMobilityLibrary(libraryName, fname, ionMobilityDb);
        }

        public static IonMobilityLibrary CreateFromList(string libraryName, string dbDir, IList<ValidatingIonMobilityPrecursor> list)
        {
            var dict = FlatListToLibKeyIndex(list);
            return CreateFromLibKeyIndex(libraryName, dbDir, dict);
        }

        public IList<IonMobilityAndCCS> GetIonMobilityInfo(LibKey key)
        {
            if (_database != null)
                return _database.GetIonMobilityInfo(key);
            return null;
        }

        public LibKeyIndex GetIonMobilityLibKeyIndex()
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
                UpdateIonMobilities(measured.Select(m => 
                    new PrecursorIonMobilities(m.Key.Target, m.Key.Adduct, m.Value)).ToList());
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
            var libKeyMap = GetIonMobilityLibKeyIndex();
            if (libKeyMap == null)
                return;
            if (libKeyMap.Any())
            {
                var dtp = new DriftTimePredictor(Name,
                    libKeyMap, extraInfoForPre20_12.WindowWidthMode, extraInfoForPre20_12.ResolvingPower,
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
            if (!Equals(Count, other.Count) && Count >= 0 && other.Count >= 0)
                return false; // Both in memory but different sizes
            if (Count < 0 || other.Count < 0)
                return true; // One or both not yet in memory
            if (!LibKeyIndex.AreEquivalent(_database.DictLibrary, other._database.DictLibrary))
                return false;
            foreach (var entry in _database.DictLibrary)
            {
                var libKey = entry.LibraryKey;
                var mobilities = GetIonMobilityInfo(libKey);
                var otherMobilities = other.GetIonMobilityInfo(libKey);
                if (mobilities.Count != otherMobilities.Count || mobilities.Any(m => !otherMobilities.Contains(m)))
                {
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
