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
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using NHibernate;
using pwiz.Common.Chemistry;
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
            string libLsid = string.Format(@"urn:lsid:{0}:spectral_libary:bibliospec:nr:minimal:{1}:{2}:{3}.{4}",
                libAuthority, libId, Guid.NewGuid(), majorVer, minorVer);

            var listLibrary = new List<BiblioLiteSpectrumInfo>();

            using (ISession session = OpenWriteSession())
            using (ITransaction transaction = session.BeginTransaction())
            {
                int progressPercent = 0;
                int i = 0;
                var sourceFiles = new Dictionary<string, long>();
                foreach (var spectrum in listSpectra)
                {
                    ++i;
                    var dbRefSpectrum = RefSpectrumFromPeaks(session, spectrum, sourceFiles);
                    session.Save(dbRefSpectrum);
                    listLibrary.Add(new BiblioLiteSpectrumInfo(spectrum.Key, 
                                                                dbRefSpectrum.Copies,
                                                                dbRefSpectrum.NumPeaks,
                                                                (int) (dbRefSpectrum.Id ?? 0)));
                    if (progressMonitor != null)
                    {
                        if (progressMonitor.IsCanceled)
                            return null;
                        int progressNew = (i*100/listSpectra.Count);
                        if (progressPercent != progressNew)
                        {
                            progressMonitor.UpdateProgress(status = status.ChangePercentComplete(progressNew));
                            progressPercent = progressNew;
                        }
                    }
                }

                session.Flush();
                session.Clear();
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
            }

            var libraryEntries = listLibrary.ToArray();
            return new BiblioSpecLiteLibrary(librarySpec, libLsid, majorVer, minorVer, libraryEntries, FileStreamManager.Default);
        }

        private DbRefSpectra RefSpectrumFromPeaks(ISession session, SpectrumMzInfo spectrum, IDictionary<string, long> sourceFiles)
        {
            if (string.IsNullOrEmpty(spectrum.SourceFile))
                throw new InvalidDataException(@"Spectrum must have a source file");

            var peaksInfo = spectrum.SpectrumPeaks;
            var smallMoleculeAttributes = spectrum.SmallMoleculeLibraryAttributes ?? SmallMoleculeLibraryAttributes.EMPTY;
            var isProteomic = smallMoleculeAttributes.IsEmpty;
            var ionMobility = spectrum.IonMobility ?? IonMobilityAndCCS.EMPTY;
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
                FileId = GetSpectrumSourceId(session, spectrum.SourceFile, sourceFiles),
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

                    refSpectra.RetentionTimes.Add(new DbRetentionTimes
                    {
                        BestSpectrum = rt.IsBest ? 1 : 0,
                        RetentionTime = rt.RetentionTime,
                        SpectrumSourceId = GetSpectrumSourceId(session, rt.SourceFile, sourceFiles),
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
            if (!UpdateProgressMessage(string.Format(Resources.BlibDb_MinimizeLibrary_Minimizing_library__0__, library.Name)))
                return null;

            string libAuthority = @"unknown.org";
            string libId = library.Name;
            // CONSIDER: Use version numbers of the original library?
            int libraryRevision = DbLibInfo.INITIAL_LIBRARY_REVISION;
            int schemaVersion = 0;

            bool saveRetentionTimes = false;
            bool saveRedundantLib = false;
            bool convertingToSmallMolecules = smallMoleculeConversionMap != null;

            var blibLib = library as BiblioSpecLiteLibrary;
            if (blibLib != null)
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
            else
            {
                var nistLibrary = library as NistLibrary;
                if (nistLibrary != null)
                {
                    libAuthority = NistLibrary.DEFAULT_AUTHORITY;
                    libId = nistLibrary.Id ?? libId;
                }
            }
            // Use a very specific LSID, since it really only matches this document.
            string libLsid = string.Format(@"urn:lsid:{0}:spectral_libary:bibliospec:nr:minimal:{1}:{2}:{3}.{4}",
                libAuthority, libId, Guid.NewGuid(), libraryRevision, schemaVersion);

            var dictLibrary = new Dictionary<LibKey, BiblioLiteSpectrumInfo>();
            var setRedundantSpectraIds = new HashSet<int>();

            // Hash table to store the database IDs of any source files in the library
            // Source file information is available only in Bibliospec libraries, schema version >= 1
            var dictFiles = new Dictionary<string, long>();
            var dictFilesRedundant = new Dictionary<string, long>();

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

                    foreach (var nodePep in document.Molecules)
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
                                var calcPre = document.Settings.GetPrecursorCalc(labelType, nodePep.SourceExplicitMods);
                                peptideModSeq = calcPre.GetModifiedSequence(peptideSeq, SequenceModFormatType.full_precision, false);
                            }
                            else
                            {
                                peptideModSeq = nodePep.SourceModifiedTarget;
                            }
                            LibKey libKey = peptideModSeq.GetLibKey(nodeGroup.PrecursorAdduct);
                            var newLibKey = libKey;

                            if (convertingToSmallMolecules)
                            {
                                if (smallMoleculeConversionMap.TryGetValue(peptideModSeq.GetLibKey(group.PrecursorAdduct), out newLibKey))
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
                                foreach (var spectrumInfo in library.GetSpectra(libKey, labelType, LibraryRedundancy.best))
                                {
                                    DbRefSpectra refSpectra = MakeRefSpectrum(session,
                                                                              convertingToSmallMolecules,
                                                                              spectrumInfo,
                                                                              peptideSeq,
                                                                              peptideModSeq,
                                                                              nodeGroup.PrecursorMz,
                                                                              precursorAdduct,
                                                                              smallMoleculeAttributes,
                                                                              dictFiles);

                                    session.Save(refSpectra);

                                    dictLibrary.Add(newLibKey,
                                                    new BiblioLiteSpectrumInfo(newLibKey, refSpectra.Copies,
                                                                               refSpectra.NumPeaks,
                                                                               (int) (refSpectra.Id ?? 0)));
                                }

                                session.Flush();
                                session.Clear();
                            }
                                // This is a BiblioSpec(schemaVersion >=1) library.
                            else
                            {
                                // get all the spectra, including the redundant ones if this library has any
                                var spectra = library.GetSpectra(libKey, labelType, LibraryRedundancy.all_redundant).ToArray();
                                // Avoid saving to the RefSpectra table for isotope label types that have no spectra
                                if (spectra.Length == 0)
                                    continue;

                                DbRefSpectra refSpectra = new DbRefSpectra
                                                              {
                                                                  PeptideSeq = peptideSeq.IsProteomic ? peptideSeq.Sequence : string.Empty,
                                                                  PrecursorMZ = nodeGroup.PrecursorMz,
                                                                  PrecursorCharge = precursorAdduct.AdductCharge,
                                                                  PrecursorAdduct = precursorAdduct.AsFormula(), // [M+...] format, even for proteomic charges
                                                                  MoleculeName = smallMoleculeAttributes.MoleculeName ?? string.Empty,
                                                                  ChemicalFormula = smallMoleculeAttributes.ChemicalFormula ?? string.Empty,
                                                                  InChiKey = smallMoleculeAttributes.InChiKey ?? string.Empty,
                                                                  OtherKeys = smallMoleculeAttributes.OtherKeys ?? string.Empty,
                                                                  PeptideModSeq = peptideModSeq.IsProteomic ? peptideModSeq.Sequence : string.Empty
                                                              };

                                // Get all the information for this reference spectrum.
                                // For BiblioSpec (schema ver >= 1), this can include retention time information 
                                // for this spectrum as well as any redundant spectra for the peptide.
                                // Ids of spectra in the redundant library, where available, are also returned.
                                var redundantSpectraKeys = new List<SpectrumKeyTime>();
                                BuildRefSpectra(document, session, convertingToSmallMolecules, refSpectra, spectra, dictFiles, redundantSpectraKeys);

                                session.Save(refSpectra);
                                session.Flush();
                                session.Clear();

                                dictLibrary.Add(newLibKey,
                                                new BiblioLiteSpectrumInfo(newLibKey,
                                                                           refSpectra.Copies,
                                                                           refSpectra.NumPeaks,
                                                                           (int) (refSpectra.Id ?? 0)));

                                // Save entries in the redundant library.
                                if (saveRedundantLib && redundantSpectraKeys.Count > 0)
                                {
                                    if (redundantSession == null)
                                    {
                                        redundantSession = OpenWriteSession_Redundant();
                                        redundantTransaction = redundantSession.BeginTransaction();
                                    }
                                    SaveRedundantSpectra(redundantSession, redundantSpectraKeys, dictFilesRedundant, refSpectra, library, setRedundantSpectraIds);
                                    redundantSpectraCount += redundantSpectraKeys.Count;
                                }
                            }
                        }

                        savedCount++;
                        if (!UpdateProgress(peptideCount, savedCount))
                            return null;
                    }

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
                if(redundantTransaction != null)
                {
                    redundantTransaction.Dispose();
                }
                if (redundantSession != null)
                {
                    redundantSession.Dispose();
                }
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
                                     ICollection<SpectrumKeyTime> redundantSpectraKeys)
        {
            bool foundBestSpectrum = false;

            foreach(SpectrumInfo spectrum in spectra)
            {
                if(spectrum.IsBest)
                {
                    if(foundBestSpectrum)
                    {
                        throw new InvalidDataException(
                            string.Format(Resources.BlibDb_BuildRefSpectra_Multiple_reference_spectra_found_for_peptide__0__in_the_library__1__,
                                          refSpectra.PeptideModSeq, FilePath));
                    }
                    
                    foundBestSpectrum = true;

                    MakeRefSpectrum(session, convertingToSmallMolecules, spectrum, refSpectra, dictFiles);
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

        private static DbRefSpectra MakeRefSpectrum(ISession session, bool convertingToSmallMolecules, SpectrumInfo spectrum, Target peptideSeq, Target modifiedPeptideSeq, double precMz, Adduct precChg, SmallMoleculeLibraryAttributes smallMoleculeAttributes, IDictionary<string, long> dictFiles)
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

            MakeRefSpectrum(session, convertingToSmallMolecules, spectrum, refSpectra, dictFiles);
            
            return refSpectra;
        }

        private static void MakeRefSpectrum(ISession session, bool convertingToSmallMolecules, SpectrumInfo spectrum, DbRefSpectra refSpectra, IDictionary<string, long> dictFiles)
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
            refSpectra.Score = 0.0;
            refSpectra.ScoreType = 0;
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
            long spectrumSourceId;
            if (!dictFiles.TryGetValue(filePath, out spectrumSourceId))
            {
                spectrumSourceId = SaveSourceFile(session, filePath);
                if (spectrumSourceId == 0)
                {
                    throw new SQLiteException(
                        String.Format(Resources.BlibDb_BuildRefSpectra_Error_getting_database_Id_for_file__0__,
                                      filePath));
                }

                dictFiles.Add(filePath, spectrumSourceId);
            }

            return spectrumSourceId;
        }

        private static long SaveSourceFile(ISession session, string filePath)
        {
            var sourceFile = new DbSpectrumSourceFiles {FileName = filePath};
            session.Save(sourceFile);
            return sourceFile.Id.HasValue ? (long)sourceFile.Id : 0;
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
                throw new InvalidOperationException(Resources.BlibDb_MinimizeLibraries_Libraries_must_be_fully_loaded_before_they_can_be_minimzed);

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
                        File.Copy(librarySpec.FilePath, Path.Combine(pathDirectory, fileName));
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
