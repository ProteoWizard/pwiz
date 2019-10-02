/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Xml;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.Database.NHibernate;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib.Midas
{
    [XmlRoot("midas_library")]
    public class MidasLibrary : Library
    {
        private const int SCHEMA_VERSION_CURRENT = 1;

        private const double PRECURSOR_TOLERANCE_CHROM = 0.7;
        private const double PRECURSOR_TOLERANCE = 0.001;
        private const double RT_TOLERANCE = 0.001;

        private int SchemaVersion { get; set; }
        private string LibraryGuid { get; set; }
        private Dictionary<DbResultsFile, List<DbSpectrum>> _spectra;

        /// <summary>
        /// A monotonically increasing revision number associated with this library.
        /// </summary>
        private float Revision { get; set; }

        /// <summary>
        /// Path to the file on disk from which this library was loaded.  This value
        /// may be null, if the library was deserialized from XML and has not yet
        /// been loaded.
        /// </summary>
        public string FilePath { get; private set; }

        public static MidasLibrary Load(MidasLibSpec spec, ILoadMonitor monitor)
        {
            var library = new MidasLibrary(spec);
            return library.Load(monitor) ? library : null;
        }

        /// <summary>
        /// Controlled access to this <see cref="Immutable"/> class, which should be
        /// created through <see cref="Load(MidasLibSpec, ILoadMonitor)"/>.
        /// </summary>
        private MidasLibrary(LibrarySpec spec)
            : base(spec)
        {
            FilePath = spec.FilePath;
        }

        protected override LibrarySpec CreateSpec()
        {
            return new MidasLibSpec(Name, FilePath);
        }

        public override string SpecFilter
        {
            get { return MidasLibSpec.FILTER_MIDAS; }
        }

        public override IList<RetentionTimeSource> ListRetentionTimeSources()
        {
            return new RetentionTimeSource[0];
            //return _spectra.Where(kvp => kvp.Value.Any()).Select(kvp => new RetentionTimeSource(kvp.Key.BaseName, Name)).ToArray();
        }

        public override LibraryDetails LibraryDetails
        {
            get
            {
                return new LibraryDetails
                {
                    Format = @"MIDAS",
                    Revision = Revision.ToString(LocalizationHelper.CurrentCulture),
                    SpectrumCount = 0,
                    DataFiles = LibraryFiles.FilePaths.Select(f => new SpectrumSourceFileDetails(f)).ToList()
                };
            }
        }

        public override LibraryFiles LibraryFiles
        {
            get
            {
                if (_spectra == null)
                {
                    return new LibraryFiles();
                }
                else
                {
                    return new LibraryFiles
                    {
                        FilePaths = _spectra.Keys.Select(key => key.FilePath).Distinct()
                    };
                }
            }
        }

        public override IPooledStream ReadStream { get { return null; } }
        public override IEnumerable<IPooledStream> ReadStreams { get { yield break; }}

        public override string IsNotLoadedExplained
        {
            get { return _spectra != null ? null : @"MIDAS: no dictionary"; }
        }

        public override bool IsSameLibrary(Library library)
        {
            var midasLib = library as MidasLibrary;
            return midasLib != null && Equals(LibraryGuid, midasLib.LibraryGuid);
        }

        public override int CompareRevisions(Library library)
        {
            // Not a valid request, if the two libraries are not the same.
            Debug.Assert(IsSameLibrary(library));
            return Revision.CompareTo(((MidasLibrary) library).Revision);
        }

        public static string[] GetMissingFiles(SrmDocument document, IEnumerable<Library> libraries)
        {
            var results = document.Settings.MeasuredResults;
            if (results == null)
                return new string[0];
            var midasFiles = results.MSDataFileInfos.Where(file => file.HasMidasSpectra).Select(file => file.FilePath.GetFilePath()).Distinct();
            var libFiles = document.Settings.PeptideSettings.Libraries.MidasLibraries.SelectMany(lib => lib.ResultsFiles).Select(Path.GetFileName);
            foreach (var lib in libraries.Where(lib => lib != null))
                libFiles = libFiles.Concat(lib.LibraryFiles.FilePaths);
            return midasFiles.Where(f => !libFiles.Contains(Path.GetFileName(f))).ToArray();
        }

        public static IEnumerable<ChromatogramSet> UnflagFiles(IEnumerable<ChromatogramSet> chromatograms, IEnumerable<string> filenames)
        {
            var arrFiles = new HashSet<string>(filenames);
            if (!arrFiles.Any())
            {
                foreach (var chromSet in chromatograms)
                    yield return chromSet;
                yield break;
            }

            foreach (var chromSet in chromatograms)
            {
                var infos = new List<ChromFileInfo>();
                foreach (var info in chromSet.MSDataFileInfos)
                {
                    var infoToAdd = info.HasMidasSpectra && arrFiles.Contains(info.FilePath.GetFileName())
                        ? info.ChangeHasMidasSpectra(false)
                        : info;
                    infos.Add(infoToAdd);
                }
                yield return !ArrayUtil.ReferencesEqual(chromSet.MSDataFileInfos, infos)
                    ? chromSet.ChangeMSDataFileInfos(infos)
                    : chromSet;
            }
        }

        private static IEnumerable<double> ReadChromPrecursorsFromMsd(MsDataFileImpl msd, IProgressMonitor monitor)
        {
            for (var i = 0; i < msd.ChromatogramCount; i++)
            {
                if (monitor.IsCanceled)
                    yield break;

                double? precursor = null;
                try
                {
                    int tmp;
                    var chromKey = ChromKey.FromId(msd.GetChromatogramId(i, out tmp), false);
                    precursor = chromKey.Precursor;
                }
                catch
                {
                    // ignored
                }
                if (precursor.HasValue)
                    yield return precursor.Value;
            }
        }

        private static IEnumerable<DbSpectrum> ReadDbSpectraFromMsd(MsDataFileImpl msd, IProgressMonitor monitor)
        {
            for (var i = 0; i < msd.SpectrumCount; i++)
            {
                if (monitor.IsCanceled)
                    yield break;

                var spectrum = msd.GetSpectrum(i);
                var ms1Precursors = spectrum.GetPrecursorsByMsLevel(1);
                if (!ms1Precursors.Any())
                    continue;
                var precursor = ms1Precursors.First();
                yield return new DbSpectrum(new DbResultsFile(msd.FilePath), precursor.PrecursorMz.GetValueOrDefault(),
                    null, null, null, spectrum.RetentionTime.GetValueOrDefault(), spectrum.Mzs, spectrum.Intensities);
            }
        }

        private static void MatchSpectraToChrom(List<DbSpectrum> dbSpectra, List<double> chromPrecursors, IProgressMonitor monitor)
        {
            chromPrecursors = chromPrecursors.Distinct().ToList();
            chromPrecursors.Sort();
            dbSpectra.Sort((x, y) => x.PrecursorMz.CompareTo(y.PrecursorMz));
            for (int i = 0, j = 0; i < dbSpectra.Count; )
            {
                if (monitor.IsCanceled)
                    return;

                var specPrecursor = dbSpectra[i].PrecursorMz;
                var chromPrecursor = chromPrecursors[j];
                var curDiff = Math.Abs(specPrecursor - chromPrecursor);
                var nextDiff = chromPrecursors.Count > j + 1 ? Math.Abs(specPrecursor - chromPrecursors[j + 1]) : double.MaxValue;
                if (curDiff < nextDiff)
                {
                    if (curDiff <= PRECURSOR_TOLERANCE_CHROM)
                        dbSpectra[i].MatchedPrecursorMz = chromPrecursor;
                    i++;
                }
                else
                {
                    j++;
                }
            }
        }

        private static void MatchSpectraToPeptides(IEnumerable<DbSpectrum> dbSpectra, SrmDocument doc, IProgressMonitor monitor)
        {
            var precursors = (from nodePepGroup in doc.PeptideGroups
                              from nodePep in nodePepGroup.Peptides
                              from nodeTranGroup in nodePep.TransitionGroups
                              select new Tuple<double, Target, int>(
                                  nodeTranGroup.PrecursorMz,
                                  doc.Settings.GetPrecursorCalc(nodeTranGroup.TransitionGroup.LabelType, nodePep.ExplicitMods).GetModifiedSequence(nodePep.Peptide.Target, false),
                                  nodeTranGroup.PrecursorCharge
                              )).ToList();
            if (!precursors.Any())
                return;

            precursors.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            foreach (var spectrum in dbSpectra)
            {
                if (spectrum == null || !spectrum.HasPrecursorMatch)
                    continue;
                var precursor = spectrum.MatchedPrecursorMz.Value;
                var j = CollectionUtil.BinarySearch(precursors, tuple => tuple.Item1.CompareTo(precursor), true);
                if (j < 0)
                {
                    j = ~j;
                    if (j == precursors.Count || (j > 0 && precursors[j].Item1 - precursor > precursor - precursors[j-1].Item1))
                    {
                        j--;
                    }
                }
                var closest = precursors[j];
                if (Math.Abs(precursor - closest.Item1) < PRECURSOR_TOLERANCE)
                {
                    spectrum.DocumentPeptide = closest.Item2.Sequence;
                    spectrum.DocumentPrecursorCharge = closest.Item3;
                }
            }
        }

        private bool Load(IProgressMonitor monitor)
        {
            _spectra = null;
            if (FilePath == null)
                return false;
            var info = new FileInfo(FilePath);
            if (!info.Exists || info.Length == 0)
                return false;

            var progress = new ProgressStatus(string.Empty).ChangeMessage(Resources.MidasLibrary_Load_Loading_MIDAS_library);
            monitor.UpdateProgress(progress);

            var spectra = new Dictionary<DbResultsFile, List<DbSpectrum>>();
            try
            {
                using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(FilePath, typeof(MidasLibrary), false))
                using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), false))
                {
                    var libInfo = session.CreateCriteria(typeof(DbLibInfo)).List<DbLibInfo>();
                    if (libInfo.Count != 1)
                        throw new Exception(Resources.MidasLibrary_Load_Error_reading_LibInfo_from_MIDAS_library);

                    SchemaVersion = libInfo[0].SchemaVersion;
                    LibraryGuid = libInfo[0].Guid;
                    var readSpectra = session.CreateCriteria(typeof(DbSpectrum)).List<DbSpectrum>();
                    progress = progress.ChangeSegments(0, readSpectra.Count);
                    foreach (var spectrum in readSpectra)
                    {
                        if (monitor.IsCanceled)
                        {
                            monitor.UpdateProgress(progress.Cancel());
                            return false;
                        }

                        progress = progress.NextSegment();
                        monitor.UpdateProgress(progress);

                        List<DbSpectrum> list;
                        if (!spectra.TryGetValue(spectrum.ResultsFile, out list))
                        {
                            list = new List<DbSpectrum>();
                            spectra[spectrum.ResultsFile] = list;
                        }
                        list.Add(spectrum);
                    }
                }
            }
            catch
            {
                monitor.UpdateProgress(progress.Cancel());
                return false;
            }

            _spectra = spectra;
            monitor.UpdateProgress(progress.Complete());
            return true;
        }

        public IEnumerable<DbSpectrum> GetSpectraByFile(MsDataFileUri file)
        {
            return IsLoaded
                ? _spectra.Where(kvp => file == null || Equals(kvp.Key.FileName, file.GetFileName())).SelectMany(kvp => kvp.Value)
                : new DbSpectrum[0];
        }

        public IEnumerable<DbSpectrum> GetSpectraByPrecursor(MsDataFileUri file, double precursor)
        {
            return GetSpectraByFile(file).Where(spectrum =>
                spectrum.HasPrecursorMatch && Math.Abs(spectrum.MatchedPrecursorMz.GetValueOrDefault() - precursor) <= PRECURSOR_TOLERANCE);
        }

        public IEnumerable<DbSpectrum> GetSpectraByRetentionTime(MsDataFileUri file, double precursor, double rtMin, double rtMax)
        {
            var min = rtMin - RT_TOLERANCE;
            var max = rtMax + RT_TOLERANCE;
            return GetSpectraByPrecursor(file, precursor).Where(spectrum =>
                min <= spectrum.RetentionTime && spectrum.RetentionTime <= max);
        }

        public IEnumerable<DbSpectrum> GetSpectraByPeptide(MsDataFileUri file, LibKey libKey)
        {
            foreach (var spectrum in GetSpectraByFile(file))
            {
                if (string.IsNullOrWhiteSpace(spectrum.DocumentPeptide))
                {
                    continue;
                }
                var key = new PeptideLibraryKey(spectrum.DocumentPeptide,
                    spectrum.DocumentPrecursorCharge.GetValueOrDefault());
                if (LibKeyIndex.KeysMatch(libKey.LibraryKey, key))
                {
                    yield return spectrum;
                }
            }
        }

        public override bool Contains(LibKey key)
        {
            if (!key.IsPrecursorKey)
                return GetSpectraByPeptide(null, key).Any();

            var spectra = GetSpectraByPrecursor(null, key.PrecursorMz.GetValueOrDefault());
            var keyRt = key.RetentionTime;
            return !keyRt.HasValue ? spectra.Any() : spectra.Any(s => Equals(keyRt.Value, s.RetentionTime));
        }

        public override bool ContainsAny(Target target)
        {
            var key = new PeptideLibraryKey(target.Sequence, 0);
            return _spectra.SelectMany(fileSpectra => fileSpectra.Value)
                .Any(spectrum => null != spectrum.DocumentPeptide && key.UnmodifiedSequence ==
                                 new PeptideLibraryKey(spectrum.DocumentPeptide, 0).UnmodifiedSequence);
        }

        public override bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            libInfo = Contains(key) ? new BiblioSpecSpectrumHeaderInfo(Name, 1, null, null) : null;
            return libInfo != null;
        }

        public override bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            spectrum = null;
            DbSpectrum[] spectra;
            if (!key.IsPrecursorKey)
            {
                spectra = GetSpectraByPeptide(null, key).ToArray();
            }
            else
            {
                spectra = GetSpectraByPrecursor(null, key.PrecursorMz.GetValueOrDefault()).ToArray();
                var keyRt = key.RetentionTime;
                if (keyRt.HasValue)
                    spectra = spectra.Where(s => Equals(keyRt.Value, s.RetentionTime)).ToArray();
            }
            if (!spectra.Any())
                return false;

            var spec = spectra.First();
            var mi = spec.Mzs.Select((t, i) => new SpectrumPeaksInfo.MI { Mz = spec.Mzs[i], Intensity = (float)spec.Intensities[i] }); // CONSIDER(bspratt): annotation?
            spectrum = new SpectrumPeaksInfo(mi.ToArray());
            return true;
        }

        public override SpectrumPeaksInfo LoadSpectrum(object spectrumKey)
        {
            var spec = spectrumKey as DbSpectrum;
            if (spec == null)
                return null;

            var mi = spec.Mzs.Select((t, i) => new SpectrumPeaksInfo.MI { Mz = spec.Mzs[i], Intensity = (float)spec.Intensities[i] }); // CONSIDER(bspratt): annotation?
            return new SpectrumPeaksInfo(mi.ToArray());
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            retentionTimes = null;
            DbSpectrum[] spectra;
            if (!key.IsPrecursorKey)
            {
                spectra = GetSpectraByPeptide(filePath, key).ToArray();
            }
            else
            {
                spectra = GetSpectraByPrecursor(filePath, key.PrecursorMz.GetValueOrDefault()).ToArray();
                var keyRt = key.RetentionTime;
                if (keyRt.HasValue)
                    spectra = spectra.Where(s => Equals(keyRt.Value, s.RetentionTime)).ToArray();
            }
            if (!spectra.Any())
                return false;

            retentionTimes = spectra.Select(s => s.RetentionTime).ToArray();
            return true;
        }

        public override bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
        {
            retentionTimes = null;
            return false;
        }

        public override bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes)
        {
            retentionTimes = null;
            return false;
        }

        public override bool TryGetIrts(out LibraryRetentionTimes retentionTimes)
        {
            retentionTimes = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(LibKey key, MsDataFileUri filePath, out IonMobilityAndCCS[] ionMobilities)
        {
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities)
        {
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(int fileIndex, out LibraryIonMobilityInfo ionMobilities)
        {
            ionMobilities = null;
            return false;
        }

        public override IEnumerable<SpectrumInfo> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            if (redundancy == LibraryRedundancy.best)
                yield break;

            if (!key.IsPrecursorKey)
            {
                foreach (var spectrum in GetSpectraByPeptide(null, key))
                    yield return new SpectrumInfo(this, labelType, spectrum.ResultsFile.FilePath, spectrum.RetentionTime, null, false, spectrum);
                yield break;
            }

            var keyRt = key.RetentionTime;
            foreach (var spectrum in GetSpectraByPrecursor(null, key.PrecursorMz.GetValueOrDefault()))
                if (!keyRt.HasValue || Equals(keyRt.Value, spectrum.RetentionTime))
                    yield return new SpectrumInfo(this, labelType, spectrum.ResultsFile.FilePath, spectrum.RetentionTime, null, false, spectrum);
        }

        public override int? FileCount { get { return IsLoaded ? _spectra.Keys.Count : 0; } }
        public override int SpectrumCount { get { return IsLoaded ? _spectra.Sum(s => s.Value.Count(s2 => s2.HasPrecursorMatch)) : 0; } }

        public override IEnumerable<LibKey> Keys
        {
            get
            {
                if (!IsLoaded)
                    yield break;

                foreach (var spectrum in _spectra.Values.SelectMany(s => s).Where(s => s.HasPrecursorMatch))
                    yield return new LibKey(spectrum.MatchedPrecursorMz.GetValueOrDefault(), spectrum.RetentionTime);
            }
        }

        public IEnumerable<string> ResultsFiles
        {
            get
            {
                if (_spectra == null)
                    yield break;
                foreach (var key in _spectra.Keys)
                    yield return key.FilePath;
            }
        }

        #region Implementation of IXmlSerializable
        
        /// <summary>
        /// For serialization
        /// </summary>
        private MidasLibrary()
        {
        }

        private enum ATTR
        {
        //    lsid,  old version has no unique identifier
            revision
        }

        public static MidasLibrary Deserialize(XmlReader reader)
        {
            return reader.Deserialize(new MidasLibrary());
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            Revision = reader.GetFloatAttribute(ATTR.revision, 0);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttribute(ATTR.revision, Revision);
        }

        #endregion

        #region object overrides

        public bool Equals(MidasLibrary obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return base.Equals(obj) && obj.Revision == Revision && Equals(obj.FilePath, FilePath);
        }

        public override bool Equals(object obj)
        {
            return !ReferenceEquals(null, obj) && (ReferenceEquals(this, obj) || Equals(obj as MidasLibrary));
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = base.GetHashCode();
                result = (result*397) ^ Revision.GetHashCode();
                result = (result*397) ^ (FilePath != null ? FilePath.GetHashCode() : 0);
                return result;
            }
        }

        #endregion

        public static MidasLibrary Create(LibrarySpec libSpec)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(libSpec.FilePath, typeof(MidasLibrary), true))
            using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
            using (var transaction = session.BeginTransaction())
            {
                session.Save(new DbLibInfo {SchemaVersion = SCHEMA_VERSION_CURRENT, Guid = Guid.NewGuid().ToString()});
                transaction.Commit();
                return new MidasLibrary(libSpec);
            }
        }

        public static void AddSpectra(MidasLibSpec libSpec, MsDataFilePath[] resultsFiles, SrmDocument doc, ILoadMonitor monitor, out List<MsDataFilePath> failedFiles)
        {
            // Get spectra from results files
            var newSpectra = new List<DbSpectrum>();
            var progress = new ProgressStatus(string.Empty).ChangeMessage(Resources.MidasLibrary_AddSpectra_Reading_MIDAS_spectra);
            const int percentResultsFiles = 80;
            failedFiles = new List<MsDataFilePath>();
            for (var i = 0; i < resultsFiles.Length; i++)
            {
                var resultsFile = resultsFiles[i];
                try
                {
                    monitor.UpdateProgress(progress.ChangePercentComplete(i*percentResultsFiles/resultsFiles.Length));

                    var filePath = resultsFile.GetFilePath();
                    if (File.Exists(filePath))
                    {
                        var sampleIndex = resultsFile.GetSampleIndex();
                        using (var msd = new MsDataFileImpl(filePath, sampleIndex == -1 ? 0 : sampleIndex, resultsFile.GetLockMassParameters(), requireVendorCentroidedMS2: true))
                        {
                            if (ChromatogramDataProvider.HasChromatogramData(msd) && SpectraChromDataProvider.HasSpectrumData(msd))
                            {
                                var chromPrecursors = ReadChromPrecursorsFromMsd(msd, monitor).ToList();
                                newSpectra.AddRange(ReadDbSpectraFromMsd(msd, monitor));
                                MatchSpectraToChrom(newSpectra, chromPrecursors, monitor);
                            }
                        }

                        MatchSpectraToPeptides(newSpectra, doc, monitor);
                    }
                    else
                    {
                        failedFiles.Add(resultsFile);
                    }
                }
                catch (Exception x)
                {
                    monitor.UpdateProgress(progress.ChangeErrorException(x));
                    failedFiles.Add(resultsFile);
                }
                if (monitor.IsCanceled)
                {
                    monitor.UpdateProgress(progress.Cancel());
                    return;
                }
            }

            if (!newSpectra.Any())
            {
                monitor.UpdateProgress(progress.Complete());
                return;
            }

            progress = progress.ChangePercentComplete(percentResultsFiles);
            monitor.UpdateProgress(progress);

            // Add spectra to library
            var midasLib = !File.Exists(libSpec.FilePath) ? Create(libSpec) : Load(libSpec, monitor);
            if (midasLib == null)
            {
                monitor.UpdateProgress(progress.ChangeErrorException(new Exception(Resources.MidasLibrary_AddSpectra_Error_loading_MIDAS_library_for_adding_spectra_)));
                return;
            }

            progress = progress.ChangeMessage(Resources.MidasLibrary_AddSpectra_Adding_spectra_to_MIDAS_library);
            monitor.UpdateProgress(progress);

            var results = new Dictionary<string, DbResultsFile>();
            if (midasLib._spectra != null)
            {
                foreach (var kvp in midasLib._spectra)
                    results[kvp.Key.FilePath] = kvp.Key;
            }

            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(libSpec.FilePath, typeof(MidasLibrary), false))
            using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
            using (var transaction = session.BeginTransaction())
            {
                for (var i = 0; i < newSpectra.Count; i++)
                {
                    if (monitor.IsCanceled)
                    {
                        transaction.Rollback();
                        monitor.UpdateProgress(progress.Cancel());
                        return;
                    }
                    var spectrum = newSpectra[i];

                    monitor.UpdateProgress(progress.ChangePercentComplete(percentResultsFiles + (int) (100.0*i/newSpectra.Count)));

                    DbResultsFile resultsFile;
                    if (!results.TryGetValue(spectrum.ResultsFile.FilePath, out resultsFile))
                    {
                        resultsFile = new DbResultsFile(spectrum.ResultsFile) { Id = null };
                        results[spectrum.ResultsFile.FilePath] = resultsFile;
                        session.SaveOrUpdate(resultsFile);
                    }
                    else if (midasLib._spectra != null)
                    {
                        List<DbSpectrum> existingSpectra;
                        if (midasLib._spectra.TryGetValue(resultsFile, out existingSpectra) &&
                            existingSpectra.Any(x => Equals(x.ResultsFile.FilePath, spectrum.ResultsFile.FilePath) &&
                                                     Equals(x.PrecursorMz, spectrum.PrecursorMz) &&
                                                     Equals(x.RetentionTime, spectrum.RetentionTime)))
                        {
                            // This spectrum already exists in the library
                            continue;
                        }
                    }
                    var spectrumNewDisconnected = new DbSpectrum(spectrum) {Id = null, ResultsFile = resultsFile};
                    session.SaveOrUpdate(spectrumNewDisconnected);
                }
                transaction.Commit();
                monitor.UpdateProgress(progress.Complete());
            }
        }

        public void RemoveResultsFiles(params string[] resultsFiles)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(FilePath, typeof(MidasLibrary), false))
            using (var session = new SessionWithLock(sessionFactory.OpenSession(), new ReaderWriterLock(), true))
            using (var transaction = session.BeginTransaction())
            {
                foreach (var kvp in _spectra)
                {
                    if (resultsFiles.Contains(kvp.Key.FilePath))
                    {
                        foreach (var spectrum in kvp.Value)
                            session.Delete(spectrum);
                        session.Delete(kvp.Key);
                    }
                }
                transaction.Commit();
            }
        }
    }
}
