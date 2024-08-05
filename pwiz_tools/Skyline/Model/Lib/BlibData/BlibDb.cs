/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using NHibernate;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.Database;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib.BlibData
{
    public class BlibDb : IDisposable
    {
        private static readonly Regex REGEX_LSID =
            new Regex(@"urn:lsid:([^:]*):spectral_library:bibliospec:[^:]*:([^:]*)");

        private IProgressMonitor ProgressMonitor { get; set; }
        private IProgressStatus _progressStatus;

        private BlibDb(String path)
        {
            FilePath = path;
            SessionFactory = BlibSessionFactoryFactory.CreateSessionFactory(path, false);
            DatabaseLock = new ReaderWriterLock();
            _progressStatus = new ProgressStatus(string.Empty);
        }

        public ISession OpenSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, false);
        }

        public ISession OpenWriteSession()
        {
            return new SessionWithLock(SessionFactory.OpenSession(), DatabaseLock, true);
        }

        private void CreateSessionFactory_Redundant(string path)
        {
            SessionFactory_Redundant = BlibSessionFactoryFactory.CreateSessionFactory_Redundant(path, true);
            DatabaseLock_Redundant = new ReaderWriterLock();
        }

        private ISession OpenWriteSession_Redundant()
        {
            return new SessionWithLock(SessionFactory_Redundant.OpenSession(), DatabaseLock_Redundant, true);
        }


        public ReaderWriterLock DatabaseLock { get; private set; }
        private ReaderWriterLock DatabaseLock_Redundant { get; set;  }

        public String FilePath { get; private set; }

        private ISessionFactory SessionFactory { get; set; }
        private ISessionFactory SessionFactory_Redundant { get; set; }

        public static BlibDb OpenBlibDb(String path)
        {
            return new BlibDb(path);
        }

        public static BlibDb CreateBlibDb(String path)
        {
            using (BlibSessionFactoryFactory.CreateSessionFactory(path, true))
            {
            }
            return OpenBlibDb(path);
        }

        public void Dispose()
        {
            if (SessionFactory != null)
            {
                SessionFactory.Dispose();
                SessionFactory = null;
            }
            if (SessionFactory_Redundant != null)
            {
                SessionFactory_Redundant.Dispose();
                SessionFactory_Redundant = null;
            }
        }

        public string[] GetSourceFilePaths()
        {
            using (var session = OpenSession())
            {
                var query = session.CreateQuery(@"SELECT FileName From " + typeof(DbSpectrumSourceFiles));
                return query.List<string>().ToArray();
            }
        }

        public string[] GetIdFilePaths()
        {
            using (var session = OpenSession())
            {
                var query = session.CreateQuery(@"SELECT IdFileName From " + typeof(DbSpectrumSourceFiles));
                return query.List<string>().ToArray();
            }
        }

        public int GetSpectraCount()
        {
            using (var session = OpenSession())
            {
                return
                    Convert.ToInt32(
                        session.CreateQuery(@"SELECT Count(P.Id) From " + typeof(DbRefSpectra) + @" P").UniqueResult());
            }
        }

        private class ProteinTablesBuilder
        {
            private ISession _session;
            private Dictionary<string, int> _namedProteinIds;
            private Dictionary<int, int> _peptideIdProteinId;

            public ProteinTablesBuilder(ISession session)
            {
                _session = session;
                _namedProteinIds = new Dictionary<string, int>();
                _peptideIdProteinId = new Dictionary<int, int>();
            }

            public void Add(DbRefSpectra refSpectra, string proteinName)
            {
                if (!string.IsNullOrEmpty(proteinName))
                {
                    if (!_namedProteinIds.TryGetValue(proteinName, out var proteinTableId))
                    {
                        proteinTableId = _namedProteinIds.Count + 1;
                        _namedProteinIds.Add(proteinName, proteinTableId);
                    }
                    _peptideIdProteinId.Add((int)(refSpectra.Id ?? 0), proteinTableId);
                }
            }

            public void Write()
            {
                // Output the protein - peptide relationships
                if (_namedProteinIds.Any())
                {
                    if (!SqliteOperations.TableExists(_session.Connection, @"RefSpectraProteins"))
                    {
                        using (var cmd = _session.Connection.CreateCommand())
                        {
                            cmd.CommandText = @"CREATE TABLE RefSpectraProteins (RefSpectraId INTEGER not null, ProteinId INTEGER not null)";
                            cmd.ExecuteNonQuery();
                        }
                    }

                    var existingProteinsCount = 0;
                    if (SqliteOperations.TableExists(_session.Connection, @"Proteins"))
                    {
                        using (var cmd = _session.Connection.CreateCommand())
                        {
                            cmd.CommandText = @"SELECT COALESCE(MAX(id), 0) FROM Proteins";
                            existingProteinsCount = Convert.ToInt32(cmd.ExecuteScalar());
                        }
                    }
                    else
                    {
                        using (var cmd = _session.Connection.CreateCommand())
                        {
                            cmd.CommandText = @"CREATE TABLE Proteins (id INTEGER primary key autoincrement, accession TEXT)";
                            cmd.ExecuteNonQuery();
                        }
                    }

                    using (var insertCommand = _session.Connection.CreateCommand())
                    {
                        insertCommand.CommandText = @"INSERT INTO Proteins (id, accession) VALUES(?,?)";
                        insertCommand.Parameters.Add(new SQLiteParameter());
                        insertCommand.Parameters.Add(new SQLiteParameter());
                        foreach (var kvp in _namedProteinIds)
                        {
                            ((SQLiteParameter)insertCommand.Parameters[0]).Value = kvp.Value + existingProteinsCount; // Id
                            ((SQLiteParameter)insertCommand.Parameters[1]).Value = kvp.Key; // Accession
                            insertCommand.ExecuteNonQuery();
                        }
                    }

                    using (var insertCommand = _session.Connection.CreateCommand())
                    {
                        insertCommand.CommandText = @"INSERT INTO RefSpectraProteins (RefSpectraId, ProteinId) VALUES(?,?)";
                        insertCommand.Parameters.Add(new SQLiteParameter());
                        insertCommand.Parameters.Add(new SQLiteParameter());
                        foreach (var kvp in _peptideIdProteinId)
                        {
                            ((SQLiteParameter)insertCommand.Parameters[0]).Value = kvp.Key; // RefSpectraId
                            ((SQLiteParameter)insertCommand.Parameters[1]).Value = kvp.Value + existingProteinsCount; // ProteinId
                            insertCommand.ExecuteNonQuery();
                        }
                    }
                }

                _session.Flush();
                _session.Clear();
            }
        }

        /// <summary>
        /// Make a BiblioSpec SQLite library from a list of spectra and their intensities.
        /// </summary>
        /// <param name="librarySpec">Library spec for which the new library is created</param>
        /// <param name="listSpectra">List of existing spectra, by LibKey</param>
        /// <param name="libraryName">Name of the library to be created</param>
        /// <param name="progressMonitor">Progress monitor to display progress in creating library</param>
        /// <returns>A library of type <see cref="BiblioSpecLiteLibrary"/></returns>
        public BiblioSpecLiteLibrary CreateLibraryFromSpectra(BiblioSpecLiteSpec librarySpec,
                                                              IList<SpectrumMzInfo> listSpectra,
                                                              string libraryName,
                                                              IProgressMonitor progressMonitor)
        {
            IProgressStatus status = new ProgressStatus(Resources.BlibDb_CreateLibraryFromSpectra_Creating_spectral_library_for_imported_transition_list);
            return CreateLibraryFromSpectra(librarySpec, listSpectra, libraryName, progressMonitor, ref status);
        }

        private static object ExecuteScalar(string commandText, SQLiteConnection connection)
        {
            using var cmd = new SQLiteCommand(commandText, connection);
            return cmd.ExecuteScalar();
        }

        private static void ExecuteNonQuery(string commandText, SQLiteConnection connection)
        {
            using var cmd = new SQLiteCommand(commandText, connection);
            cmd.ExecuteNonQuery();
        }

        private class SpectrumInserter : IDisposable
        {
            private const int _spectraBufferSize = 12;
            private class InsertCommands
            {
                public SQLiteCommand[] _insertSpectraCmd;
                public SQLiteCommand[] _insertAnnotationsCmd;
                public SQLiteCommand[] _insertPeaksCmd;
                public SQLiteCommand[] _insertRetentionTimesCmd;
                public SQLiteCommand[] _insertModificationsCmd;
                public List<DbRefSpectra> _buffer;
            }
            private ConcurrentDictionary<int, InsertCommands> _insertCommandsByThread;
            private long _lastSpectraId, _lastAnnotationId, _lastRetentionTimesId, _lastModificationId;

            private List<PropertyInfo> _dbRefSpectraProperties;
            private List<PropertyInfo> _dbRefSpectraPeaksProperties;
            private List<PropertyInfo> _dbRefSpectraPeakAnnotationsProperties;
            private List<PropertyInfo> _dbModificationProperties;
            private List<PropertyInfo> _dbRetentionTimeProperties;

            private static SQLiteCommand[] GenerateInsertCommand(SQLiteConnection connection, string table, IList<PropertyInfo> properties)
            {
                string PropertySqlName(PropertyInfo p)
                {
                    return p.PropertyType.BaseType == typeof(DbEntity) ? p.Name + @"Id" : p.Name;
                }

                var result = new SQLiteCommand[2]; // individual and batch
                // ReSharper disable LocalizableElement
                string valuesBlock = "(" + string.Join(",", Enumerable.Repeat("?", properties.Count)) + ")";
                string sql = "INSERT INTO " + table + " (" +
                             string.Join(",", properties.Select(PropertySqlName)) + ") VALUES " + valuesBlock;

                var cmd = result[0] = new SQLiteCommand(sql, connection);
                for (int i = 0; i < properties.Count; ++i)
                    cmd.Parameters.Add(new SQLiteParameter());

                string batchSql = sql + "," + string.Join(",", Enumerable.Repeat(valuesBlock, _spectraBufferSize - 1));
                cmd = result[1] = new SQLiteCommand(batchSql, connection);
                for(int j = 0; j < _spectraBufferSize; ++j)
                {
                    for (int i = 0; i < properties.Count; ++i)
                        cmd.Parameters.Add(new SQLiteParameter());
                }
                // ReSharper restore LocalizableElement
                return result;
            }

            private static SQLiteCommand[] CloneCommands(SQLiteCommand[] commands)
            {
                var result = new SQLiteCommand[commands.Length];
                for (var i = 0; i < commands.Length; i++)
                {
                    var cmd = commands[i];
                    result[i] = cmd.Clone() as SQLiteCommand;
                }
                return result;
            }

            private static IEnumerable<PropertyInfo> GetProperties(Type dbType)
            {
                foreach (var property in dbType.GetProperties())
                {
                    if (property.Name == "EntityClass")
                        continue;
                    yield return property;
                }
            }

            private static object GetPropertyValue(object obj, PropertyInfo property)
            {
                if (property.PropertyType.BaseType == typeof(DbEntity))
                    return ((DbEntity)property.GetValue(obj)).Id;
                return property.GetValue(obj);
            }

            public SpectrumInserter(ISession session)
            {
                var connection = session.Connection as SQLiteConnection;

                _lastSpectraId = (long)ExecuteScalar(@"SELECT IFNULL(MAX(id), 0) FROM RefSpectra", connection);
                _lastAnnotationId = (long)ExecuteScalar(@"SELECT IFNULL(MAX(id), 0) FROM RefSpectraPeakAnnotations", connection);
                _lastRetentionTimesId = (long)ExecuteScalar(@"SELECT IFNULL(MAX(id), 0) FROM RetentionTimes", connection);
                _lastModificationId = (long)ExecuteScalar(@"SELECT IFNULL(MAX(id), 0) FROM Modifications", connection);

                _dbRefSpectraProperties = new List<PropertyInfo>();
                foreach (var property in typeof(DbRefSpectra).GetProperties())
                {
                    if (property.Name == "EntityClass")
                        continue;
                    if (property.PropertyType.IsValueType || property.PropertyType == typeof(string))
                        _dbRefSpectraProperties.Add(property);
                }

                _dbRefSpectraPeaksProperties = new List<PropertyInfo>();
                foreach (var property in typeof(DbRefSpectraPeaks).GetProperties())
                {
                    if (property.Name == "EntityClass" || property.Name == "Id")
                        continue;
                    _dbRefSpectraPeaksProperties.Add(property);
                }

                _dbRefSpectraPeakAnnotationsProperties = GetProperties(typeof(DbRefSpectraPeakAnnotations)).ToList();
                _dbModificationProperties = GetProperties(typeof(DbModification)).ToList();
                _dbRetentionTimeProperties = GetProperties(typeof(DbRetentionTimes)).ToList();

                _insertCommandsByThread = new ConcurrentDictionary<int, InsertCommands>();
                var masterCmds = _insertCommandsByThread[0] = new InsertCommands();

                masterCmds._insertSpectraCmd = GenerateInsertCommand(connection, @"RefSpectra", _dbRefSpectraProperties);
                masterCmds._insertPeaksCmd = GenerateInsertCommand(connection, @"RefSpectraPeaks", _dbRefSpectraPeaksProperties);
                masterCmds._insertAnnotationsCmd = GenerateInsertCommand(connection, @"RefSpectraPeakAnnotations", _dbRefSpectraPeakAnnotationsProperties);
                masterCmds._insertModificationsCmd = GenerateInsertCommand(connection, @"Modifications", _dbModificationProperties);
                masterCmds._insertRetentionTimesCmd = GenerateInsertCommand(connection, @"RetentionTimes", _dbRetentionTimeProperties);
            }

            public void InsertSpectrum(DbRefSpectra dbRefSpectrum)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;

                if (!_insertCommandsByThread.TryGetValue(threadId, out InsertCommands insertCommands))
                {
                    var masterCmds = _insertCommandsByThread[0];
                    insertCommands = _insertCommandsByThread[threadId] = new InsertCommands();
                    insertCommands._insertSpectraCmd = CloneCommands(masterCmds._insertSpectraCmd);
                    insertCommands._insertPeaksCmd = CloneCommands(masterCmds._insertPeaksCmd);
                    insertCommands._insertAnnotationsCmd = CloneCommands(masterCmds._insertAnnotationsCmd);
                    insertCommands._insertModificationsCmd = CloneCommands(masterCmds._insertModificationsCmd);
                    insertCommands._insertRetentionTimesCmd = CloneCommands(masterCmds._insertRetentionTimesCmd);
                    insertCommands._buffer = new List<DbRefSpectra>(_spectraBufferSize);
                }

                var buffer = insertCommands._buffer;
                buffer.Add(dbRefSpectrum);
                dbRefSpectrum.Id = Interlocked.Increment(ref _lastSpectraId);
                if (_spectraBufferSize > 1 && buffer.Count < _spectraBufferSize)
                    return;

                FlushBuffer(insertCommands);
                buffer.Clear();
            }

            private void FlushBuffer(InsertCommands insertCommands = null)
            {
                int threadId = Thread.CurrentThread.ManagedThreadId;

                if (insertCommands == null)
                    if (!_insertCommandsByThread.TryGetValue(threadId, out insertCommands))
                        return;

                var buffer = insertCommands._buffer;
                if (!buffer.Any())
                    return;

                int whichCmd = _spectraBufferSize > 1 && buffer.Count == _spectraBufferSize ? 1 : 0;
                var _insertSpectraCmd = insertCommands._insertSpectraCmd[whichCmd];
                var _insertPeaksCmd = insertCommands._insertPeaksCmd[whichCmd];
                var _insertAnnotationsCmd = insertCommands._insertAnnotationsCmd[0];
                var _insertRetentionTimesCmd = insertCommands._insertRetentionTimesCmd[0];
                var _insertModificationsCmd = insertCommands._insertModificationsCmd[0];

                int spectraCmdParameter = 0, peaksCmdParameter = 0;
                foreach (var dbRefSpectrum in buffer)
                {
                    {
                        foreach (var property in _dbRefSpectraProperties)
                            _insertSpectraCmd.Parameters[spectraCmdParameter++].Value = GetPropertyValue(dbRefSpectrum, property);

                        dbRefSpectrum.Peaks.RefSpectra ??= dbRefSpectrum;
                        foreach (var property in _dbRefSpectraPeaksProperties)
                            _insertPeaksCmd.Parameters[peaksCmdParameter++].Value = GetPropertyValue(dbRefSpectrum.Peaks, property);
                    }

                    if (whichCmd == 0)
                    {
                        spectraCmdParameter = 0;
                        peaksCmdParameter = 0;
                        _insertSpectraCmd.ExecuteNonQuery();
                        _insertPeaksCmd.ExecuteNonQuery();
                    }
                }

                if (whichCmd == 1)
                {
                    _insertSpectraCmd.ExecuteNonQuery();
                    _insertPeaksCmd.ExecuteNonQuery();
                }

                foreach (var dbRefSpectrum in buffer)
                {
                    if (dbRefSpectrum.PeakAnnotations != null)
                    {
                        foreach (var annotation in dbRefSpectrum.PeakAnnotations)
                        {
                            int i = 0;
                            annotation.Id = Interlocked.Increment(ref _lastAnnotationId);
                            annotation.RefSpectra = dbRefSpectrum;
                            foreach (var property in _dbRefSpectraPeakAnnotationsProperties)
                                _insertAnnotationsCmd.Parameters[i++].Value = GetPropertyValue(annotation, property);
                            _insertAnnotationsCmd.ExecuteNonQuery();
                        }
                    }

                    {
                        foreach (var retentionTime in dbRefSpectrum.RetentionTimes)
                        {
                            int i = 0;
                            retentionTime.Id = Interlocked.Increment(ref _lastRetentionTimesId);
                            retentionTime.RefSpectra = dbRefSpectrum;
                            foreach (var property in _dbRetentionTimeProperties)
                                _insertRetentionTimesCmd.Parameters[i++].Value = GetPropertyValue(retentionTime, property);
                            _insertRetentionTimesCmd.ExecuteNonQuery();
                        }
                    }

                    if (dbRefSpectrum.Modifications != null)
                    {
                        foreach (var modification in dbRefSpectrum.Modifications)
                        {
                            int i = 0;
                            modification.Id = Interlocked.Increment(ref _lastModificationId);
                            modification.RefSpectra = dbRefSpectrum;
                            foreach (var property in _dbModificationProperties)
                                _insertModificationsCmd.Parameters[i++].Value = GetPropertyValue(modification, property);
                            _insertModificationsCmd.ExecuteNonQuery();
                        }
                    }
                }

            }

            public void Dispose()
            {
                foreach (var cmd in _insertCommandsByThread)
                {
                    if (cmd.Key > 0)
                    {
                        FlushBuffer(cmd.Value);
                        cmd.Value._buffer.Clear();
                    }
                    cmd.Value._insertSpectraCmd.ForEach(c => c.Dispose());
                    cmd.Value._insertAnnotationsCmd.ForEach(c => c.Dispose());
                    cmd.Value._insertPeaksCmd.ForEach(c => c.Dispose());
                    cmd.Value._insertRetentionTimesCmd.ForEach(c => c.Dispose());
                    cmd.Value._insertModificationsCmd.ForEach(c => c.Dispose());
                }
                _insertCommandsByThread.Clear();
            }
        }

        public BiblioSpecLiteLibrary CreateLibraryFromSpectra(BiblioSpecLiteSpec librarySpec,
            IList<SpectrumMzInfo> listSpectra,
            string libraryName,
            IProgressMonitor progressMonitor,
            ref IProgressStatus status)
        {
            const string libAuthority = BiblioSpecLiteLibrary.DEFAULT_AUTHORITY;
            const int majorVer = 1;
            const int minorVer = DbLibInfo.SCHEMA_VERSION_CURRENT;
            string libId = libraryName;
            // Use a very specific LSID, since it really only matches this document.
            string libLsid = string.Format(@"urn:lsid:{0}:spectral_library:bibliospec:nr:minimal:{1}:{2}:{3}.{4}",
                libAuthority, libId, Guid.NewGuid(), majorVer, minorVer);

            var listLibrary = new List<BiblioLiteSpectrumInfo>();

            var localStatus = status;
            using (ISession session = OpenWriteSession())
            {
                // speed up writing by turning off filesystem synchronization and journaling
                var connection = session.Connection as SQLiteConnection;
                ExecuteNonQuery(@"PRAGMA journal_mode=OFF;
                PRAGMA synchronous=OFF;
                PRAGMA defer_foreign_keys = ON;", connection);

                using ITransaction transaction = session.BeginTransaction();
                int progressPercent = -1;
                int i = 0;
                var sourceFiles = new Dictionary<string, long>();
                var proteinTablesBuilder = new ProteinTablesBuilder(session);
                using (var spectrumInserter = new SpectrumInserter(session))
                {
                    ParallelEx.ForEach(listSpectra, spectrum =>
                    {
                        var dbRefSpectrum = RefSpectrumFromPeaks(session, spectrum, sourceFiles);
                        spectrumInserter.InsertSpectrum(dbRefSpectrum);
                        var ionMobilitiesByFileId = new IndexedIonMobilities(
                            dbRefSpectrum.RetentionTimes.Where(rt => !Equals(rt.IonMobilityType, 0)).
                                Select(rt =>
                                {
                                    var ionMobilityValue = IonMobilityValue.GetIonMobilityValue(rt.IonMobility, (eIonMobilityUnits)rt.IonMobilityType);
                                    var ionMobilityAndCCS = IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobilityValue, rt.CollisionalCrossSectionSqA, rt.IonMobilityHighEnergyOffset);
                                    return new KeyValuePair<int, IonMobilityAndCCS>((int)rt.SpectrumSourceId, ionMobilityAndCCS);
                                }));
                        lock(listLibrary)
                        {
                            listLibrary.Add(new BiblioLiteSpectrumInfo(spectrum.Key,
                                dbRefSpectrum.Copies,
                                dbRefSpectrum.NumPeaks,
                                (int)(dbRefSpectrum.Id ?? 0),
                                spectrum.Protein,
                                default(IndexedRetentionTimes),
                                ionMobilitiesByFileId));
                            proteinTablesBuilder.Add(dbRefSpectrum, spectrum.Protein);
                            if (progressMonitor != null)
                            {
                                if (progressMonitor.IsCanceled)
                                    return;
                                int progressNew = (i*100/listSpectra.Count);
                                if (progressPercent != progressNew)
                                {
                                    progressMonitor.UpdateProgress(localStatus = localStatus.ChangePercentComplete(progressNew));
                                    progressPercent = progressNew;
                                }
                            }

                            ++i;
                        }
                    }, maxThreads:4);
                }

                if (progressMonitor?.IsCanceled ?? false)
                    return null;

                status = localStatus;

                session.Flush();
                session.Clear();

                proteinTablesBuilder.Write();
                // Simulate ctime(d), which is what BlibBuild uses.
                string createTime = string.Format(@"{0:ddd MMM dd HH:mm:ss yyyy}", DateTime.Now); // CONSIDER: localize? different date/time format in different countries
                DbLibInfo libInfo = new DbLibInfo
                {
                    LibLSID = libLsid,
                    CreateTime = createTime,
                    NumSpecs = listLibrary.Count,
                    MajorVersion = majorVer,
                    MinorVersion = minorVer
                };

                session.Save(libInfo);
                session.Flush();
                session.Clear();
                transaction.Commit();

                if (progressMonitor != null)
                {
                    progressMonitor.UpdateProgress(status = status.Complete());
                }
            }

            var libraryEntries = listLibrary.ToArray();
            return new BiblioSpecLiteLibrary(librarySpec, libLsid, majorVer, minorVer, libraryEntries, FileStreamManager.Default);
        }

        private DbRefSpectra RefSpectrumFromPeaks(ISession session, SpectrumMzInfo spectrum, IDictionary<string, long> sourceFiles)
        {
            //if (string.IsNullOrEmpty(spectrum.SourceFile))
            //    throw new InvalidDataException(@"Spectrum must have a source file");

            var peaksInfo = spectrum.SpectrumPeaks;
            var smallMoleculeAttributes = spectrum.SmallMoleculeLibraryAttributes ?? SmallMoleculeLibraryAttributes.EMPTY;
            var isProteomic = smallMoleculeAttributes.IsEmpty;
            var ionMobility = spectrum.IonMobility ?? IonMobilityAndCCS.EMPTY;

            long fileId;
            lock (session)
                fileId = GetSpectrumSourceId(session, spectrum.SourceFile, sourceFiles);

            var refSpectra = new DbRefSpectra
            {
                PeptideSeq = isProteomic ? FastaSequence.StripModifications(spectrum.Key.Target).Sequence : string.Empty,
                PrecursorMZ = spectrum.PrecursorMz,
                PrecursorCharge = spectrum.Key.Charge,
                PrecursorAdduct = spectrum.Key.Adduct.AsFormula(), // [M+...] format, even for proteomic charges
                MoleculeName = smallMoleculeAttributes.MoleculeName ?? string.Empty,
                ChemicalFormula = smallMoleculeAttributes.ChemicalFormula ?? string.Empty,
                InChiKey = smallMoleculeAttributes.InChiKey ?? string.Empty,
                OtherKeys = smallMoleculeAttributes.OtherKeys ?? string.Empty,
                PeptideModSeq = isProteomic ? spectrum.Key.Target.Sequence : string.Empty,
                Copies = 1,
                NumPeaks = (ushort)peaksInfo.Peaks.Length,
                RetentionTime = spectrum.RetentionTime.GetValueOrDefault(),
                IonMobility = ionMobility.IonMobility.Mobility,
                IonMobilityType = (int)ionMobility.IonMobility.Units,
                IonMobilityHighEnergyOffset = ionMobility.HighEnergyIonMobilityValueOffset,
                CollisionalCrossSectionSqA = ionMobility.CollisionalCrossSectionSqA,
                FileId = fileId,
                SpecIdInFile = null,
                Score = 0.0,
                ScoreType = 0
            };

            refSpectra.Peaks = new DbRefSpectraPeaks
            {
                RefSpectra = refSpectra,
                PeakIntensity = IntensitiesToBytes(peaksInfo.Peaks),
                PeakMZ = MZsToBytes(peaksInfo.Peaks)
            };

            refSpectra.PeakAnnotations = DbRefSpectraPeakAnnotations.Create(refSpectra, peaksInfo);

            refSpectra.RetentionTimes = new List<DbRetentionTimes>();
            if (spectrum.RetentionTimes != null && spectrum.RetentionTimes.Any())
            {
                foreach (var rt in spectrum.RetentionTimes) 
                {
                    if (string.IsNullOrEmpty(rt.SourceFile))
                        throw new InvalidDataException(@"Spectrum must have a source file");
                    long rtFileId;
                    lock (session)
                        rtFileId = GetSpectrumSourceId(session, rt.SourceFile, sourceFiles);
                    refSpectra.RetentionTimes.Add(new DbRetentionTimes
                    {
                        BestSpectrum = rt.IsBest ? 1 : 0,
                        RetentionTime = rt.RetentionTime,
                        SpectrumSourceId = rtFileId,
                        IonMobility = rt.IonMobility.IonMobility.Mobility,
                        IonMobilityType = (int)rt.IonMobility.IonMobility.Units,
                        IonMobilityHighEnergyOffset = rt.IonMobility.HighEnergyIonMobilityValueOffset,
                        CollisionalCrossSectionSqA = rt.IonMobility.CollisionalCrossSectionSqA,
                        RedundantRefSpectraId = -1
                    });
                }
            }
            else if (spectrum.RetentionTime.HasValue)
            {
                refSpectra.RetentionTimes.Add(new DbRetentionTimes
                {
                    BestSpectrum = 1,
                    RetentionTime = refSpectra.RetentionTime,
                    SpectrumSourceId = refSpectra.FileId.GetValueOrDefault(),
                    IonMobility = refSpectra.IonMobility,
                    IonMobilityHighEnergyOffset = refSpectra.IonMobilityHighEnergyOffset,
                    IonMobilityType = refSpectra.IonMobilityType,
                    CollisionalCrossSectionSqA = refSpectra.CollisionalCrossSectionSqA,
                    RedundantRefSpectraId = -1
                });
            }

            ModsFromModifiedSequence(refSpectra);
            return refSpectra;
        }

        /// <summary>
        /// Minimize any library type to a fully functional BiblioSpec SQLite library.
        /// </summary>
        /// <param name="librarySpec">Library spec for which the new library is created</param>
        /// <param name="library">Existing library to minimize</param>
        /// <param name="document">Document for which only used spectra are included in the new library</param>
        /// <param name="smallMoleculeConversionMap">For converting to small molecules - a map from charge,modifiedSeq to adduct,molecule</param>
        /// <returns>A new minimized <see cref="BiblioSpecLiteLibrary"/></returns>
        private BiblioSpecLiteLibrary MinimizeLibrary(BiblioSpecLiteSpec librarySpec,
            Library library, SrmDocument document,
            IDictionary<LibKey, LibKey> smallMoleculeConversionMap)
        {
            if (!UpdateProgressMessage(string.Format(BlibDataResources.BlibDb_MinimizeLibrary_Minimizing_library__0__, library.Name)))
                return null;

            string libAuthority = @"unknown.org";
            string libId = library.Name;
            // CONSIDER: Use version numbers of the original library?
            int libraryRevision = DbLibInfo.INITIAL_LIBRARY_REVISION;
            int schemaVersion = 0;

            bool saveRetentionTimes = false;
            bool saveRedundantLib = false;
            bool convertingToSmallMolecules = smallMoleculeConversionMap != null;

            if (library is BiblioSpecLiteLibrary blibLib)
            {
                string libraryLsid = blibLib.Lsid;
                Match matchLsid = REGEX_LSID.Match(libraryLsid);
                if (matchLsid.Success)
                {
                    libAuthority = matchLsid.Groups[1].Value;
                    libId = matchLsid.Groups[2].Value;
                }
                else
                {
                    libAuthority = BiblioSpecLiteLibrary.DEFAULT_AUTHORITY;
                }

                // We may have a RetentionTimes table if schemaVersion if 1 or greater.
                saveRetentionTimes = blibLib.SchemaVersion >= 1;
                libraryRevision = blibLib.Revision;
                schemaVersion = saveRetentionTimes ? DbLibInfo.SCHEMA_VERSION_CURRENT : blibLib.SchemaVersion;

                // If the document has MS1 filtering enabled we will save a minimized version
                // of the redundant library, if available.
                if(document.Settings.TransitionSettings.FullScan.IsEnabledMs)
                {
                    String redundantLibPath = blibLib.FilePathRedundant;
                    if(File.Exists(redundantLibPath))
                    {
                        string path = BiblioSpecLiteSpec.GetRedundantName(FilePath); 
                        CreateSessionFactory_Redundant(path);
                        saveRedundantLib = true;
                    }
                }
            }
            else if (library is BiblioSpecLibrary)
                libAuthority = BiblioSpecLiteLibrary.DEFAULT_AUTHORITY;
            else if (library is XHunterLibrary)
                libAuthority = XHunterLibrary.DEFAULT_AUTHORITY;
            else if (library is NistLibrary nistLibrary)
            {
                libAuthority = NistLibrary.DEFAULT_AUTHORITY;
                libId = nistLibrary.Id ?? libId;
            }
            // Use a very specific LSID, since it really only matches this document.
            string libLsid = string.Format(@"urn:lsid:{0}:spectral_library:bibliospec:nr:minimal:{1}:{2}:{3}.{4}",
                libAuthority, libId, Guid.NewGuid(), libraryRevision, schemaVersion);

            var dictLibrary = new Dictionary<LibKey, BiblioLiteSpectrumInfo>();
            var setRedundantSpectraIds = new HashSet<int>();

            // Hash table to store the database IDs of any source files in the library
            // Source file information is available only in Bibliospec libraries, schema version >= 1
            var dictFiles = new Dictionary<string, long>();
            var dictFilesRedundant = new Dictionary<string, long>();

            // Hash table to score the score types in the library
            var dictScoreTypes = new Dictionary<string, ushort>();

            ISession redundantSession = null;
            ITransaction redundantTransaction = null;
            int redundantSpectraCount = 0;

            try
            {
                using (ISession session = OpenWriteSession())
                using (ITransaction transaction = session.BeginTransaction())
                {
                    int peptideCount = document.MoleculeCount;
                    int savedCount = 0;
                    var proteinTablesBuilder = new ProteinTablesBuilder(session);

                    foreach (var moleculeGroup in document.MoleculeGroups)
                    {
                        var proteinName = moleculeGroup.Name;
                        foreach (var nodePep in moleculeGroup.Molecules)
                        {
                            foreach (TransitionGroupDocNode nodeGroup in nodePep.Children)
                            {
                                // Only get library info from precursors that use the desired library
                                if (!nodeGroup.HasLibInfo || !Equals(nodeGroup.LibInfo.LibraryName, library.Name))
                                    continue;

                                TransitionGroup group = nodeGroup.TransitionGroup;
                                var peptideSeq = nodePep.SourceUnmodifiedTarget;
                                var precursorAdduct = group.PrecursorAdduct;
                                IsotopeLabelType labelType = nodeGroup.TransitionGroup.LabelType;

                                var smallMoleculeAttributes = nodePep.Peptide.GetSmallMoleculeLibraryAttributes();
                                Target peptideModSeq;
                                if (nodePep.IsProteomic)
                                {
                                    var calcPre =
                                        document.Settings.GetPrecursorCalc(labelType, nodePep.SourceExplicitMods);
                                    peptideModSeq = calcPre.GetModifiedSequence(peptideSeq,
                                        SequenceModFormatType.lib_precision, false);
                                }
                                else
                                {
                                    peptideModSeq = nodePep.SourceModifiedTarget;
                                }

                                LibKey libKey = peptideModSeq.GetLibKey(nodeGroup.PrecursorAdduct);
                                var newLibKey = libKey;

                                if (convertingToSmallMolecules)
                                {
                                    if (smallMoleculeConversionMap.TryGetValue(
                                        peptideModSeq.GetLibKey(group.PrecursorAdduct), out newLibKey))
                                    {
                                        precursorAdduct = newLibKey.Adduct;
                                        smallMoleculeAttributes = newLibKey.SmallMoleculeLibraryAttributes;
                                    }
                                    else
                                    {
                                        // Not being converted
                                        Assume.IsTrue(group.Peptide.IsDecoy);
                                        continue; // Not wanted in library
                                    }
                                }

                                if (dictLibrary.ContainsKey(newLibKey))
                                    continue;

                                // saveRetentionTimes will be false unless this is a BiblioSpec(schemaVersion >=1) library.
                                if (!saveRetentionTimes)
                                {
                                    // get the best spectra
                                    foreach (var spectrumInfo in library.GetSpectra(libKey, labelType,
                                        LibraryRedundancy.best))
                                    {
                                        DbRefSpectra refSpectra = MakeRefSpectrum(session,
                                            convertingToSmallMolecules,
                                            spectrumInfo,
                                            peptideSeq,
                                            peptideModSeq,
                                            nodeGroup.PrecursorMz,
                                            precursorAdduct,
                                            smallMoleculeAttributes,
                                            dictFiles,
                                            dictScoreTypes);

                                        session.Save(refSpectra);

                                        dictLibrary.Add(newLibKey,
                                            new BiblioLiteSpectrumInfo(newLibKey, refSpectra.Copies,
                                                refSpectra.NumPeaks,
                                                (int) (refSpectra.Id ?? 0),
                                                proteinName));
                                    }

                                    session.Flush();
                                    session.Clear();
                                }
                                // This is a BiblioSpec(schemaVersion >=1) library.
                                else
                                {
                                    // get all the spectra, including the redundant ones if this library has any
                                    var spectra = library.GetSpectra(libKey, labelType, LibraryRedundancy.all_redundant)
                                        .ToArray();
                                    // Avoid saving to the RefSpectra table for isotope label types that have no spectra
                                    if (spectra.Length == 0)
                                        continue;

                                    DbRefSpectra refSpectra = new DbRefSpectra
                                    {
                                        PeptideSeq = peptideSeq.IsProteomic ? peptideSeq.Sequence : string.Empty,
                                        PrecursorMZ = nodeGroup.PrecursorMz,
                                        PrecursorCharge = precursorAdduct.AdductCharge,
                                        PrecursorAdduct =
                                            precursorAdduct.AsFormula(), // [M+...] format, even for proteomic charges
                                        MoleculeName = smallMoleculeAttributes.MoleculeName ?? string.Empty,
                                        ChemicalFormula = smallMoleculeAttributes.ChemicalFormula ?? string.Empty,
                                        InChiKey = smallMoleculeAttributes.InChiKey ?? string.Empty,
                                        OtherKeys = smallMoleculeAttributes.OtherKeys ?? string.Empty,
                                        PeptideModSeq = peptideModSeq.IsProteomic
                                            ? peptideModSeq.Sequence
                                            : string.Empty
                                    };

                                    // Get all the information for this reference spectrum.
                                    // For BiblioSpec (schema ver >= 1), this can include retention time information 
                                    // for this spectrum as well as any redundant spectra for the peptide.
                                    // Ids of spectra in the redundant library, where available, are also returned.
                                    var redundantSpectraKeys = new List<SpectrumKeyTime>();
                                    BuildRefSpectra(document, session, convertingToSmallMolecules, refSpectra, spectra,
                                        dictFiles, dictScoreTypes, redundantSpectraKeys);

                                    session.Save(refSpectra);
                                    session.Flush();
                                    session.Clear();

                                    dictLibrary.Add(newLibKey,
                                        new BiblioLiteSpectrumInfo(newLibKey,
                                            refSpectra.Copies,
                                            refSpectra.NumPeaks,
                                            (int) (refSpectra.Id ?? 0),
                                            proteinName));

                                    // Save entries in the redundant library.
                                    if (saveRedundantLib && redundantSpectraKeys.Count > 0)
                                    {
                                        if (redundantSession == null)
                                        {
                                            redundantSession = OpenWriteSession_Redundant();
                                            redundantTransaction = redundantSession.BeginTransaction();
                                        }

                                        SaveRedundantSpectra(redundantSession, redundantSpectraKeys, dictFilesRedundant,
                                            refSpectra, library, setRedundantSpectraIds);
                                        redundantSpectraCount += redundantSpectraKeys.Count;
                                    }

                                    // Prepare to build peptide-protein tables
                                    proteinTablesBuilder.Add(refSpectra, proteinName);
                                }
                            }

                            savedCount++;
                            if (!UpdateProgress(peptideCount, savedCount))
                                return null;
                        }
                    }

                    // Output the protein - peptide relationships
                    proteinTablesBuilder.Write();

                    // Simulate ctime(d), which is what BlibBuild uses.
                    string createTime = string.Format(@"{0:ddd MMM dd HH:mm:ss yyyy}", DateTime.Now); // CONSIDER: localize? different date/time format in different countries
                    DbLibInfo libInfo = new DbLibInfo
                                            {
                                                LibLSID = libLsid,
                                                CreateTime = createTime,
                                                NumSpecs = dictLibrary.Count,
                                                MajorVersion = libraryRevision,
                                                MinorVersion = schemaVersion
                                            };

                    session.Save(libInfo);
                    session.Flush();
                    session.Clear();

                    transaction.Commit();

                    if (redundantTransaction != null)
                    {
                        var scoreType = new DbScoreTypes {Id = 0, ScoreType = @"UNKNOWN"};
                        redundantSession.Save(scoreType);

                        libInfo = new DbLibInfo
                                      {
                                          LibLSID = libLsid.Replace(@":nr:", @":redundant:"),
                                          CreateTime = createTime,
                                          NumSpecs = redundantSpectraCount,
                                          MajorVersion = libraryRevision,
                                          MinorVersion = schemaVersion
                                      };
                        redundantSession.Save(libInfo);
                        redundantSession.Flush();
                        redundantSession.Clear();

                        redundantTransaction.Commit();
                    }
                }

            }
            finally
            {
                redundantTransaction?.Dispose();
                redundantSession?.Dispose();
            }

            var libraryEntries = dictLibrary.Values.ToArray();

            return new BiblioSpecLiteLibrary(librarySpec, libLsid, libraryRevision, schemaVersion,
                libraryEntries, FileStreamManager.Default);
        }

        private bool UpdateProgressMessage(string message)
        {
            if (ProgressMonitor != null)
            {
                if (ProgressMonitor.IsCanceled)
                    return false;

                ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangeMessage(message));
            }
            return true;
        }

        private bool UpdateProgress(int totalPeptideCount, int doneCount)
        {
            if (ProgressMonitor != null)
            {
                if (ProgressMonitor.IsCanceled)
                    return false;

                int progressValue = (doneCount) * 100 / totalPeptideCount;

                ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(progressValue));
            }
            return true;
        }

        private static void SaveRedundantSpectra(ISession sessionRedundant,
                                                 IEnumerable<SpectrumKeyTime> redundantSpectraIds,
                                                 IDictionary<string, long> dictFiles,
                                                 DbRefSpectra refSpectra,
                                                 Library library,
                                                 ISet<int> savedSpectraIds)
        {
            foreach (var specLiteKey in redundantSpectraIds)
            {
                if (specLiteKey.Key.RedundantId == 0)
                {
                    continue;
                }
                if (!savedSpectraIds.Add(specLiteKey.Key.RedundantId))
                {
                    continue;
                }
                // If this source file has already been saved, get its database Id.
                // Otherwise, save it.
                long spectrumSourceId = GetSpectrumSourceId(sessionRedundant, specLiteKey.FilePath, dictFiles);

                // Get peaks for the redundant spectrum
                var peaksInfo = library.LoadSpectrum(specLiteKey.Key);

                var ionMobility = specLiteKey.Time.IonMobility;
                var ionMobilityHighEnergyOffset = specLiteKey.Time.IonMobilityHighEnergyOffset;
                var collisionalCrossSectionSqA = specLiteKey.Time.CollisionalCrossSectionSqA;
                int? ionMobilityType = specLiteKey.Time.IonMobilityType;

                var redundantSpectra = new DbRefSpectraRedundant
                                           {
                                               Id = specLiteKey.Key.RedundantId,
                                               PeptideSeq = refSpectra.PeptideSeq,
                                               PrecursorMZ = refSpectra.PrecursorMZ,
                                               PrecursorCharge = refSpectra.PrecursorCharge,
                                               PrecursorAdduct = refSpectra.PrecursorAdduct,
                                               MoleculeName = refSpectra.MoleculeName,
                                               ChemicalFormula = refSpectra.ChemicalFormula,
                                               InChiKey = refSpectra.InChiKey,
                                               OtherKeys = refSpectra.OtherKeys,
                                               PeptideModSeq = refSpectra.PeptideModSeq,
                                               NumPeaks = (ushort) peaksInfo.Peaks.Length,
                                               Copies = refSpectra.Copies,
                                               RetentionTime = specLiteKey.Time.RetentionTime,
                                               IonMobility = ionMobility,
                                               IonMobilityHighEnergyOffset =  ionMobilityHighEnergyOffset,
                                               CollisionalCrossSectionSqA = collisionalCrossSectionSqA,
                                               IonMobilityType = ionMobilityType,
                                               FileId = spectrumSourceId
                                           };

                var peaks = new DbRefSpectraRedundantPeaks
                                {
                                    RefSpectra = redundantSpectra,
                                    PeakIntensity = IntensitiesToBytes(peaksInfo.Peaks),
                                    PeakMZ = MZsToBytes(peaksInfo.Peaks)
                                };
                redundantSpectra.Peaks = peaks;
                redundantSpectra.PeakAnnotations = DbRefSpectraPeakAnnotations.Create(refSpectra, peaksInfo);
                sessionRedundant.Save(redundantSpectra);
            }

            sessionRedundant.Flush();
            sessionRedundant.Clear();
        }

        private void BuildRefSpectra(SrmDocument document,
            ISession session,
            bool convertingToSmallMolecules,
            DbRefSpectra refSpectra,
            SpectrumInfo[] spectra, // Yes, this could be IEnumerable, but then Resharper throws bogus warnings about possible multiple enumeration
            IDictionary<string, long> dictFiles,
            IDictionary<string, ushort> dictScoreTypes,
            ICollection<SpectrumKeyTime> redundantSpectraKeys)
        {
            bool foundBestSpectrum = false;

            foreach(SpectrumInfoLibrary spectrum in spectra)
            {
                if(spectrum.IsBest)
                {
                    if(foundBestSpectrum)
                    {
                        throw new InvalidDataException(
                            string.Format(BlibDataResources.BlibDb_BuildRefSpectra_Multiple_reference_spectra_found_for_peptide__0__in_the_library__1__,
                                          refSpectra.PeptideModSeq, FilePath));
                    }
                    
                    foundBestSpectrum = true;

                    MakeRefSpectrum(session, convertingToSmallMolecules, spectrum, refSpectra, dictFiles, dictScoreTypes);
                }

                // Determine if this spectrum is from a file that is in the document.
                // If it is not, do not save the retention time for this spectrum, and do not
                // add it to the redundant library. However, if this is the reference (best) spectrum
                // we must save its retention time. 
                // NOTE: Spectra not used in the results get used for too much now for this to be useful
//                var matchingFile = document.Settings.HasResults
//                    ? document.Settings.MeasuredResults.FindMatchingMSDataFile(MsDataFileUri.Parse(spectrum.FilePath))
//                    : null;
//                if (!spectrum.IsBest && matchingFile == null)
//                    continue;

                // If this source file has already been saved, get its database Id.
                // Otherwise, save it.
                long spectrumSourceId = GetSpectrumSourceId(session, spectrum.FilePath, dictFiles);

                // spectrumKey in the SpectrumInfo is an integer for reference(best) spectra,
                // or object of type SpectrumLiteKey for redundant spectra
                object key = spectrum.SpectrumKey;
                var specLiteKey = key as SpectrumLiteKey;

                var dbRetentionTimes = new DbRetentionTimes
                {
                    RedundantRefSpectraId = specLiteKey != null ? specLiteKey.RedundantId : 0,
                    RetentionTime = spectrum.RetentionTime,
                    SpectrumSourceId = spectrumSourceId,
                    BestSpectrum = spectrum.IsBest ? 1 : 0,
                    IonMobility = null,
                    IonMobilityHighEnergyOffset = null,
                    CollisionalCrossSectionSqA = null,
                    IonMobilityType = (int)eIonMobilityUnits.none
                };
                if (null != spectrum.IonMobilityInfo && spectrum.IonMobilityInfo.HasIonMobilityValue)
                {
                    dbRetentionTimes.CollisionalCrossSectionSqA = spectrum.IonMobilityInfo.CollisionalCrossSectionSqA.GetValueOrDefault();
                    dbRetentionTimes.IonMobilityType = (int) spectrum.IonMobilityInfo.IonMobility.Units;
                    dbRetentionTimes.IonMobility = spectrum.IonMobilityInfo.IonMobility.Mobility.GetValueOrDefault(); // Get the low energy value
                    dbRetentionTimes.IonMobilityHighEnergyOffset = spectrum.IonMobilityInfo.HighEnergyIonMobilityValueOffset;
                }

                if (refSpectra.RetentionTimes == null)
                    refSpectra.RetentionTimes = new List<DbRetentionTimes>();

                refSpectra.RetentionTimes.Add(dbRetentionTimes);
               
                if (specLiteKey != null)
                {
                    redundantSpectraKeys.Add(new SpectrumKeyTime(specLiteKey, dbRetentionTimes, spectrum.FilePath));
                }
            }
        }

        private class SpectrumKeyTime
        {
            public SpectrumKeyTime(SpectrumLiteKey key, DbRetentionTimes time, string filePath)
            {
                Key = key;
                Time = time;
                FilePath = filePath;
            }

            public SpectrumLiteKey Key { get; private set; }
            public DbRetentionTimes Time { get; private set; }
            public string FilePath { get; private set; }
        }

        private static DbRefSpectra MakeRefSpectrum(ISession session, bool convertingToSmallMolecules, SpectrumInfoLibrary spectrum,
            Target peptideSeq, Target modifiedPeptideSeq, double precMz, Adduct precChg, SmallMoleculeLibraryAttributes smallMoleculeAttributes,
            IDictionary<string, long> dictFiles, IDictionary<string, ushort> dictScoreTypes)
        {
            var refSpectra = new DbRefSpectra
            {
                PeptideSeq = peptideSeq.IsProteomic ? peptideSeq.Sequence : string.Empty,
                PrecursorMZ = precMz,
                PrecursorCharge = precChg.AdductCharge,
                PrecursorAdduct = precChg.AsFormula(), // [M+...] format, even for proteomic charges
                PeptideModSeq = modifiedPeptideSeq.IsProteomic ? modifiedPeptideSeq.Sequence : string.Empty,
                ChemicalFormula = smallMoleculeAttributes.ChemicalFormula ?? string.Empty,
                MoleculeName = smallMoleculeAttributes.MoleculeName ?? string.Empty,
                InChiKey = smallMoleculeAttributes.InChiKey ?? string.Empty,
                OtherKeys = smallMoleculeAttributes.OtherKeys ?? string.Empty
            };
            MakeRefSpectrum(session, convertingToSmallMolecules, spectrum, refSpectra, dictFiles, dictScoreTypes);
            return refSpectra;
        }

        private static void MakeRefSpectrum(ISession session, bool convertingToSmallMolecules, SpectrumInfoLibrary spectrum, DbRefSpectra refSpectra,
            IDictionary<string, long> dictFiles, IDictionary<string, ushort> dictScoreTypes)
        {
            short copies = (short)spectrum.SpectrumHeaderInfo.GetRankValue(LibrarySpec.PEP_RANK_COPIES);
            var peaksInfo = spectrum.SpectrumPeaksInfo;

            refSpectra.Copies = copies;
            refSpectra.NumPeaks = (ushort) peaksInfo.Peaks.Length;

            refSpectra.Peaks = new DbRefSpectraPeaks
                                   {
                                       RefSpectra = refSpectra,
                                       PeakIntensity = IntensitiesToBytes(peaksInfo.Peaks),
                                       PeakMZ = MZsToBytes(peaksInfo.Peaks)
                                   };

            refSpectra.PeakAnnotations = DbRefSpectraPeakAnnotations.Create(refSpectra, peaksInfo);

            if (null != spectrum.IonMobilityInfo)
            {
                refSpectra.CollisionalCrossSectionSqA = spectrum.IonMobilityInfo.CollisionalCrossSectionSqA;
                refSpectra.IonMobilityType = (int)spectrum.IonMobilityInfo.IonMobility.Units;
                refSpectra.IonMobility = spectrum.IonMobilityInfo.IonMobility.Mobility;
                refSpectra.IonMobilityHighEnergyOffset = spectrum.IonMobilityInfo.HighEnergyIonMobilityValueOffset;
            }

            refSpectra.RetentionTime = spectrum.RetentionTime.GetValueOrDefault();
            refSpectra.FileId = spectrum.FilePath != null ? (long?)GetSpectrumSourceId(session, spectrum.FilePath, dictFiles) : null;
            refSpectra.SpecIdInFile = null;
            refSpectra.Score = spectrum.SpectrumHeaderInfo?.Score ?? 0.0;
            refSpectra.ScoreType = !string.IsNullOrEmpty(spectrum.SpectrumHeaderInfo?.ScoreType)
                ? GetScoreTypeId(session, spectrum.SpectrumHeaderInfo.ScoreType, dictScoreTypes)
                : (ushort) 0;
            if (convertingToSmallMolecules || !string.IsNullOrEmpty(refSpectra.MoleculeName))
            {
                refSpectra.PeptideSeq = string.Empty;
                refSpectra.PeptideModSeq = string.Empty;
                Assume.IsTrue(!string.IsNullOrEmpty(refSpectra.MoleculeName));
            }

            ModsFromModifiedSequence(refSpectra);
        }

        private static long GetSpectrumSourceId(ISession session, string filePath, IDictionary<string, long> dictFiles)
        {
            if (!dictFiles.TryGetValue(filePath, out var spectrumSourceId))
            {
                spectrumSourceId = SaveSourceFile(session, filePath);
                if (spectrumSourceId == 0)
                {
                    throw new SQLiteException(string.Format(BlibDataResources.BlibDb_BuildRefSpectra_Error_getting_database_Id_for_file__0__, filePath));
                }
                dictFiles.Add(filePath, spectrumSourceId);
            }
            return spectrumSourceId;
        }

        private static ushort GetScoreTypeId(ISession session, string scoreName, IDictionary<string, ushort> dictScoreTypes)
        {
            if (!dictScoreTypes.TryGetValue(scoreName, out var scoreTypeId))
            {
                scoreTypeId = SaveScoreType(session, scoreName);
                if (scoreTypeId == 0)
                {
                    throw new SQLiteException(string.Format(BlibDataResources.BlibDb_GetScoreTypeId_Error_getting_database_Id_for_score__0_, scoreName));
                }
                dictScoreTypes.Add(scoreName, scoreTypeId);
            }
            return scoreTypeId;
        }

        private static long SaveSourceFile(ISession session, string filePath)
        {
            var sourceFile = new DbSpectrumSourceFiles {FileName = filePath, IdFileName = null, CutoffScore = null};
            session.Save(sourceFile);
            return sourceFile.Id.GetValueOrDefault();
        }

        private static ushort SaveScoreType(ISession session, string scoreName)
        {
            var scoreType = new DbScoreTypes {ScoreType = scoreName};
            session.Save(scoreType);
            return (ushort) scoreType.Id.GetValueOrDefault();
        }

        /// <summary>
        /// Reads modifications from a sequence with embedded modifications,
        /// e.g. AM[16.0]VLC[57.0]
        /// This results in some loss of precision, since embedded modifications
        /// are only accurate to one decimal place.  But this is all that is necessary
        /// for further use as spectral libraries for SRM method building.
        /// </summary>
        /// <param name="refSpectra"></param>
        private static void ModsFromModifiedSequence(DbRefSpectra refSpectra)
        {
            string modSeq = refSpectra.PeptideModSeq;
            for (int i = 0, iAa = 0; i < modSeq.Length; i++)
            {
                char c = modSeq[i];
                if (c != '[')
                    iAa++;
                else
                {
                    int iEnd = modSeq.IndexOf(']', ++i);
                    double modMass;
                    if (double.TryParse(modSeq.Substring(i, iEnd - i), out modMass))
                    {
                        if (refSpectra.Modifications == null)
                            refSpectra.Modifications = new List<DbModification>();

                        refSpectra.Modifications.Add(new DbModification
                                                         {
                                                             RefSpectra = refSpectra,
                                                             Mass = modMass,
                                                             Position = iAa
                                                         });
                        i = iEnd;
                        iAa++;
                    }
                }
            }
        }

        private static byte[] IntensitiesToBytes(SpectrumPeaksInfo.MI[] peaks)
        {
            const int sizeInten = sizeof(float);
            byte[] peakIntens = new byte[peaks.Length * sizeInten];
            for (int i = 0; i < peaks.Length; i++)
            {
                int offset = i*sizeInten;
                Array.Copy(BitConverter.GetBytes(peaks[i].Intensity), 0, peakIntens, offset, sizeInten);
            }
            return peakIntens.Compress();
        }

        private static byte[] MZsToBytes(SpectrumPeaksInfo.MI[] peaks)
        {
            const int sizeMz = sizeof(double);
            byte[] peakMZs = new byte[peaks.Length * sizeMz];
            for (int i = 0; i < peaks.Length; i++)
            {
                int offset = i * sizeMz;
                Array.Copy(BitConverter.GetBytes(peaks[i].Mz), 0, peakMZs, offset, sizeMz);
            }
            return peakMZs.Compress();
        }

        /// <summary>
        /// Minimizes all libraries in a document to produce a new document with
        /// just the library information necessary for the spectra referenced by
        /// the nodes in the document.
        /// </summary>
        /// <param name="document">Document for which to minimize library information</param>
        /// <param name="pathDirectory">Directory into which new minimized libraries are built</param>
        /// <param name="nameModifier">A name modifier to append to existing names for
        ///     full libraries to create new library names</param>
        /// <param name="dictOldNameToNew">Optional dictionary for mapping old library names to new (should be null, or empty, on entry)</param>
        /// <param name="progressMonitor">Broker to communicate status and progress</param>
        /// <returns>A new document instance with minimized libraries</returns>
        public static SrmDocument MinimizeLibraries(SrmDocument document,
            string pathDirectory, string nameModifier,
            Dictionary<string, string> dictOldNameToNew,
            IProgressMonitor progressMonitor)
        {
            return MinimizeLibrariesHelper(document, pathDirectory, nameModifier,
                null, dictOldNameToNew, progressMonitor);
        }

        /// <summary>
        /// Minimizes all libraries in a document to produce a new document with
        /// just the library information necessary for the spectra referenced by
        /// the nodes in the document, converting to small molecules along the way.
        /// </summary>
        /// <param name="document">Document for which to minimize library information</param>
        /// <param name="pathDirectory">Directory into which new minimized libraries are built</param>
        /// <param name="nameModifier">A name modifier to append to existing names for
        ///     full libraries to create new library names</param>
        /// <param name="smallMoleculeConversionInfo">Used for changing charge,modifedSeq to adduct,molecule in small molecule conversion</param>
        /// <param name="dictOldNameToNew">Optional dictionary for mapping old library names to new (should be null, or empty, on entry)</param>
        /// <param name="progressMonitor">Broker to communicate status and progress</param>
        /// <returns>A new document instance with minimized libraries</returns>
        public static SrmDocument MinimizeLibrariesAndConvertToSmallMolecules(SrmDocument document,
            string pathDirectory, string nameModifier,
            IDictionary<LibKey, LibKey> smallMoleculeConversionInfo,
            Dictionary<string, string> dictOldNameToNew,
            IProgressMonitor progressMonitor)
        {
            return MinimizeLibrariesHelper(document, pathDirectory, nameModifier,
                smallMoleculeConversionInfo, dictOldNameToNew, progressMonitor);
        }

        private static SrmDocument MinimizeLibrariesHelper(SrmDocument document,
            string pathDirectory, string nameModifier, 
            IDictionary<LibKey, LibKey> smallMoleculeConversionInfo,
            Dictionary<string, string> dictOldNameToNew,
            IProgressMonitor progressMonitor)
        {
            var settings = document.Settings;
            var pepLibraries = settings.PeptideSettings.Libraries;
            if (!pepLibraries.HasLibraries)
                return document;
            if (!pepLibraries.IsLoaded)
                throw new InvalidOperationException(BlibDataResources.BlibDb_MinimizeLibraries_Libraries_must_be_fully_loaded_before_they_can_be_minimzed);

            // Separate group nodes by the libraries to which they refer
            var setUsedLibrarySpecs = new HashSet<LibrarySpec>();
            foreach (var librarySpec in pepLibraries.LibrarySpecs)
            {
                string libraryName = librarySpec.Name;
                if (document.MoleculeTransitionGroups.Contains(nodeGroup =>
                        nodeGroup.HasLibInfo && Equals(nodeGroup.LibInfo.LibraryName, libraryName)))
                {
                    setUsedLibrarySpecs.Add(librarySpec);   
                }
            }

            foreach (var midasLibSpec in pepLibraries.MidasLibrarySpecs)
                setUsedLibrarySpecs.Add(midasLibSpec);

            var listLibraries = new List<Library>();
            var listLibrarySpecs = new List<LibrarySpec>();
            dictOldNameToNew = dictOldNameToNew ?? new Dictionary<string, string>();
            Assume.IsFalse(dictOldNameToNew.Any());
            if (setUsedLibrarySpecs.Count > 0)
            {
                Directory.CreateDirectory(pathDirectory);

                var usedNames = new HashSet<string>();
                for (int i = 0; i < pepLibraries.LibrarySpecs.Count; i++)
                {
                    var librarySpec = pepLibraries.LibrarySpecs[i];
                    if (!setUsedLibrarySpecs.Contains(librarySpec))
                        continue;

                    string baseName = Path.GetFileNameWithoutExtension(librarySpec.FilePath);
                    string fileName = GetUniqueName(baseName, usedNames);

                    if (smallMoleculeConversionInfo != null &&
                        !fileName.Contains(BiblioSpecLiteSpec.DotConvertedToSmallMolecules))
                    {
                        fileName += BiblioSpecLiteSpec.DotConvertedToSmallMolecules; 
                    }

                    if (librarySpec is MidasLibSpec)
                    {
                        listLibrarySpecs.Add(librarySpec);
                        listLibraries.Add(pepLibraries.Libraries[i]);
                        fileName += MidasLibSpec.EXT;
                        File.Copy(librarySpec.FilePath ?? string.Empty, // Prevent ReSharper from complaining about possible null arg
                            Path.Combine(pathDirectory, fileName));
                        dictOldNameToNew.Add(librarySpec.Name, librarySpec.Name);
                        continue;
                    }

                    fileName += BiblioSpecLiteSpec.EXT;

                    using (var blibDb = CreateBlibDb(Path.Combine(pathDirectory, fileName)))
                    {
                        blibDb.ProgressMonitor = progressMonitor;
                        var librarySpecMin = librarySpec as BiblioSpecLiteSpec;
                        if (librarySpecMin == null || !librarySpecMin.IsDocumentLibrary)
                        {
                            string nameMin = librarySpec.Name;
                            // Avoid adding the modifier a second time, if it has
                            // already been done once.
                            if (!nameMin.EndsWith(nameModifier + @")"))
                                nameMin = string.Format(@"{0} ({1})", librarySpec.Name, nameModifier);
                            librarySpecMin = new BiblioSpecLiteSpec(nameMin, blibDb.FilePath);
                        }
                        else if (smallMoleculeConversionInfo != null &&
                                 !librarySpecMin.Name.Contains(BiblioSpecLiteSpec.DotConvertedToSmallMolecules))
                        {
                            librarySpecMin =
                                librarySpecMin.ChangeName(librarySpecMin.Name + BiblioSpecLiteSpec.DotConvertedToSmallMolecules) as BiblioSpecLiteSpec;
                        }

                        listLibraries.Add(blibDb.MinimizeLibrary(librarySpecMin,
                            pepLibraries.Libraries[i], document, smallMoleculeConversionInfo));
                        
                        // Terminate if user canceled
                        if (progressMonitor != null && progressMonitor.IsCanceled)
                            return document;

                        listLibrarySpecs.Add(librarySpecMin);
                        // ReSharper disable once PossibleNullReferenceException
                        dictOldNameToNew.Add(librarySpec.Name, librarySpecMin.Name);
                    }
                }

                document = (SrmDocument) document.ChangeAll(node =>
                    {
                        var nodeGroup = node as TransitionGroupDocNode;
                        if (nodeGroup == null || !nodeGroup.HasLibInfo)
                            return node;

                        string libName = nodeGroup.LibInfo.LibraryName;
                        string libNameNew = dictOldNameToNew[libName];
                        if (Equals(libName, libNameNew))
                            return node;
                        var libInfo = nodeGroup.LibInfo.ChangeLibraryName(libNameNew);
                        return nodeGroup.ChangeLibInfo(libInfo);
                    },
                    (int) SrmDocument.Level.TransitionGroups);
            }

            var peptideLibraries = settings.PeptideSettings.Libraries;
            if (peptideLibraries.RankId != null &&
                !listLibrarySpecs.Any(spec => spec.PeptideRankIds.Contains(peptideLibraries.RankId)))
            {
                peptideLibraries = pepLibraries.ChangeRankId(null);
            }
            peptideLibraries = peptideLibraries.ChangeLibraries(listLibrarySpecs, listLibraries)
                .ChangeDocumentLibrary(listLibrarySpecs.Any(spec => spec.IsDocumentLibrary));
            return document.ChangeSettingsNoDiff(
                settings.ChangePeptideSettings(settings.PeptideSettings.ChangeLibraries(peptideLibraries)));
        }

        private static string GetUniqueName(string name, HashSet<string> usedNames)
        {
            if (usedNames.Contains(name))
            {
                // Append increasing number until a unique name is found
                string nameNew;
                int counter = 2;
                do
                {
                    nameNew = name + counter++;
                }
                while (usedNames.Contains(nameNew));
                name = nameNew;
            }
            usedNames.Add(name);
            return name;
        }
    }
}
