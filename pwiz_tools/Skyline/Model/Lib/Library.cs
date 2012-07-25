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
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY C:\proj\pwiz\pwiz\pwiz_tools\Skyline\Model\Lib\Library.csKIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using System.Xml.Serialization;

namespace pwiz.Skyline.Model.Lib
{
    public sealed class LibraryManager : BackgroundLoader
    {
        private readonly Dictionary<string, Library> _loadedLibraries =
            new Dictionary<string, Library>();

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            return previous == null ||
                !ReferenceEquals(document.Settings.PeptideSettings.Libraries,
                                    previous.Settings.PeptideSettings.Libraries);
        }

        protected override bool IsLoaded(SrmDocument document)
        {
            PeptideLibraries libraries = document.Settings.PeptideSettings.Libraries;

            return (!libraries.HasLibraries || libraries.IsLoaded);
        }

        protected override IEnumerable<IPooledStream> GetOpenStreams(SrmDocument document)
        {
            if (document == null)
                yield break;
            var libraries = document.Settings.PeptideSettings.Libraries.Libraries;
            foreach (var readStream in libraries.Where(library => library != null)
                                                .SelectMany(library => library.ReadStreams))
            {
                yield return readStream;
            }
        }

        protected override bool IsCanceled(IDocumentContainer container, object tag)
        {
            PeptideLibraries libraries = container.Document.Settings.PeptideSettings.Libraries;
            return !libraries.LibrarySpecs.Contains((LibrarySpec)tag);
        }

        protected override bool LoadBackground(IDocumentContainer container, SrmDocument document, SrmDocument docCurrent)
        {
            var libraries = docCurrent.Settings.PeptideSettings.Libraries;
            var dictLibraries = new Dictionary<string, Library>();
            try
            {
                foreach (LibrarySpec spec in libraries.LibrarySpecsUnloaded)
                {
                    if (spec == null)
                        continue;
                    var library = LoadLibrary(container, spec);
                    if (library == null || !ReferenceEquals(document.Id, container.Document.Id))
                    {
                        // Loading was cancelled or document changed
                        EndProcessing(document);
                        return false;
                    }
                    dictLibraries.Add(spec.Name, library);
                }

                SrmDocument docNew;
                do
                {
                    // Look for unloaded libraries in the current document that match
                    // those loaded.
                    docCurrent = container.Document;
                    libraries = docCurrent.Settings.PeptideSettings.Libraries;
                    bool changed = false;
                    var list = new List<Library>();
                    foreach (LibrarySpec spec in libraries.LibrarySpecs)
                    {
                        if (spec == null)
                            continue;
                        Library libraryExisting = libraries.GetLibrary(spec.Name);
                        Library libraryLoaded;
                        if ((libraryExisting != null && libraryExisting.IsLoaded) ||
                                !dictLibraries.TryGetValue(spec.Name, out libraryLoaded))
                            list.Add(libraryExisting);
                        else
                        {
                            list.Add(libraryLoaded);
                            changed = true;
                        }
                    }
                    // If nothing changed, end without changing the document.
                    if (!changed)
                    {
                        EndProcessing(docCurrent);
                        return false;
                    }
                    libraries = libraries.ChangeLibraries(list.ToArray());
                    docNew = docCurrent.ChangeSettings(docCurrent.Settings.ChangePeptideSettings(
                        docCurrent.Settings.PeptideSettings.ChangeLibraries(libraries)));
                }
                while (!CompleteProcessing(container, docNew, docCurrent));
            }
            finally
            {
                foreach (var library in dictLibraries.Values)
                    library.ReadStream.CloseStream();
            }

            return true;
        }

        private Library LoadLibrary(IDocumentContainer container, LibrarySpec spec)
        {
            // TODO: Something better than locking for the entire load
            lock (_loadedLibraries)
            {
                Library library;
                if (_loadedLibraries.TryGetValue(spec.Name, out library))
                    return library;

                library = spec.LoadLibrary(new LoadMonitor(this, container, spec));
                if (library != null)
                    _loadedLibraries.Add(spec.Name, library);
                return library;
            }
        }

        public Library TryGetLibrary(LibrarySpec spec)
        {
            Library library;
            _loadedLibraries.TryGetValue(spec.Name, out library);
            return library;
        }

        public delegate bool BuildFunction(IDocumentContainer documentContainer, ILibraryBuilder libraryBuilder);

        public sealed class BuildState
        {
            public BuildState(LibrarySpec librarySpec, BuildFunction buildFunc)
            {
                LibrarySpec = librarySpec;
                BuildFunc = buildFunc;
            }

            public LibrarySpec LibrarySpec { get; private set; }
            public BuildFunction BuildFunc { get; private set; }
        }

        public void BuildLibrary(IDocumentContainer container, ILibraryBuilder builder, AsyncCallback callback)
        {
            BuildFunction buildFunc = BuildLibraryBackground;
            buildFunc.BeginInvoke(container, builder, callback, new BuildState(builder.LibrarySpec, buildFunc));
        }

        private bool BuildLibraryBackground(IDocumentContainer container, ILibraryBuilder builder)
        {
            // This blocks all library loading, while a library is being built
            // TODO: Something better than locking for the entire build
            lock (_loadedLibraries)
            {
                bool success = builder.BuildLibrary(new LibraryBuildMonitor(this, container));

                if (success)
                {
                    // If the library was already loaded, make sure the new copy
                    // replaces the load in the library load cache.
                    string name = builder.LibrarySpec.Name;
                    _loadedLibraries.Remove(name);

                    // If the current document contains the newly built library,
                    // make sure it is reloaded into the document, by resetting all
                    // library-specs.  Do this inside the lock to avoid library loading
                    // happening during this check.
                    ForDocumentLibraryReload(container, name);
                }

                return success;
            }            
        }

        private static void ForDocumentLibraryReload(IDocumentContainer container, string name)
        {
            var docOriginal = container.Document;
            if (docOriginal == null)
                return;
            var librarySettings = docOriginal.Settings.PeptideSettings.Libraries;
            if (!librarySettings.HasLibraries)
                return;
            int iSpec = librarySettings.LibrarySpecs.IndexOf(spec => spec != null && Equals(name, spec.Name));
            if (iSpec == -1 || librarySettings.Libraries[iSpec] == null)
                return;

            SrmDocument docNew;
            do
            {
                docOriginal = container.Document;
                var settings =
                    docOriginal.Settings.ChangePeptideLibraries(
                        lib =>
                            {
                                var listLib = new List<Library>(lib.Libraries);
                                int i = lib.LibrarySpecs.IndexOf(spec => Equals(name, spec.Name));
                                if (i != -1)
                                    listLib[i] = null;
                                return lib.ChangeLibraries(listLib);
                            });
                docNew = docOriginal.ChangeSettings(settings);
            } while (!container.SetDocument(docNew, docOriginal));
        }

        private class LibraryBuildMonitor : IProgressMonitor
        {
            private readonly LibraryManager _manager;
            // Might want this someday...
// ReSharper disable NotAccessedField.Local
            private readonly IDocumentContainer _container;
// ReSharper restore NotAccessedField.Local

            public LibraryBuildMonitor(LibraryManager manager, IDocumentContainer container)
            {
                _manager = manager;
                _container = container;
            }

            // TODO: Some way to cancel a library build
            public bool IsCanceled
            {
                get { return false; }
            }

            public void UpdateProgress(ProgressStatus status)
            {
                _manager.UpdateProgress(status);
            }
        }
    }

    /// <summary>
    /// Implement on a class for building a specific type of library.
    /// </summary>
    public interface ILibraryBuilder
    {
        /// <summary>
        /// Build the library with progress monitoring, and the ability
        /// to cancel.
        /// </summary>
        /// <param name="progress">Sink for progress updates, and source of user cancel status</param>
        bool BuildLibrary(IProgressMonitor progress);

        /// <summary>
        /// A <see cref="LibrarySpec"/> referencing the library to be built.
        /// </summary>
        LibrarySpec LibrarySpec { get; }
    }

    public abstract class Library : XmlNamedElement
    {
        protected Library(LibrarySpec spec) : base(spec.Name)
        {
            FileNameHint = Path.GetFileName(spec.FilePath);
        }

        /// <summary>
        /// Original file name used to create this library, for use in finding
        /// the library, if its identifying name is not present in the
        /// <see cref="SpectralLibraryList"/>
        /// </summary>
        public string FileNameHint { get; private set; }

        /// <summary>
        /// Creates the appropriate library spec for this library, given a path
        /// to the library.
        /// </summary>
        /// <param name="path">Path to the library file on disk</param>
        /// <returns>A new <see cref="LibrarySpec"/></returns>
        public abstract LibrarySpec CreateSpec(string path);

        /// <summary>
        /// Returns the <see cref="IPooledStream"/> for the stream on which this library
        /// relies for its data reading.
        /// </summary>
        public abstract IPooledStream ReadStream { get; }

        /// <summary>
        /// Returns all open <see cref="IPooledStream"/> associated with the library.
        /// Default implementation returns the single stream from <see cref="ReadStream"/>.
        /// </summary>
        public virtual IEnumerable<IPooledStream> ReadStreams
        {
            get
            {
                if (ReadStream != null)
                    yield return ReadStream;
            }
        }

        /// <summary>
        /// True if this library is loaded and may be used to query spectral
        /// data.  False if it is merely a placeholder loaded from a document
        /// which has not yet been connected to the actual library data.
        /// </summary>
        public abstract bool IsLoaded { get; }

        /// <summary>
        /// Determines if this library identifies itself as being the same
        /// as another library.
        /// </summary>
        /// <param name="library">Library to check for identity</param>
        /// <returns>True if the libraries have the same identity</returns>
        public abstract bool IsSameLibrary(Library library);

        /// <summary>
        /// Used to determine relative ordering of this library with another
        /// in an odered progression of revisions.  This check is only valid
        /// if <see cref="IsSameLibrary"/> is true for the library parameter.
        /// </summary>
        /// <param name="library">Library to compare revisions with</param>
        /// <returns>0 if revisions are equal,
        ///          1 if the given library is new than this,
        ///         -1 if the given library is older than this</returns>
        public abstract int CompareRevisions(Library library);

        /// <summary>
        /// Determines if the library contains a specific (modified sequence, charge) pair.
        /// </summary>
        /// <param name="key">A sequence, charge pair</param>
        /// <returns>True if the library contains the key</returns>
        public abstract bool Contains(LibKey key);

        /// <summary>
        /// Determines if the library contains any spectra for a peptide, based on its
        /// unmodified amino acid sequence.
        /// </summary>
        /// <param name="key">An unmodified sequence optimized to consume minimal memory</param>
        /// <returns>True if the library contains any spectra for this peptide regardless of modification or charge</returns>
        public abstract bool ContainsAny(LibSeqKey key);

        /// <summary>
        /// Some details for the library. 
        /// This can be the library revision, program version, 
		/// build date or a hyperlink to the library source 
        /// (e.g. http://peptide.nist.gov/ for NIST libraries)
        /// </summary>
        public abstract LibraryDetails LibraryDetails { get; }

        /// <summary>
        /// Attempts to get spectrum header information for a specific
        /// (sequence, charge) pair.
        /// </summary>
        /// <param name="key">A sequence, charge pair</param>
        /// <param name="libInfo">The spectrum header information, if successful</param>
        /// <returns>True if the library contains the key</returns>
        public abstract bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo);

        /// <summary>
        /// Attempts to get spectrum peak information for a specific
        /// (sequence, charge) pair.
        /// </summary>
        /// <param name="key">A sequence, charge pair</param>
        /// <param name="spectrum">The spectrum peak information, if successful</param>
        /// <returns>True if the spectrum was retrieved successfully</returns>
        public abstract bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum);

        /// <summary>
        /// Loads a spectrum given a key provided by the library.
        /// </summary>
        /// <param name="spectrumKey">A key that uniquely identifies the spectrum</param>
        /// <returns>The requested spectrum peak information</returns>
        public abstract SpectrumPeaksInfo LoadSpectrum(object spectrumKey);

        /// <summary>
        /// Attempts to get retention time information for a specific
        /// (sequence, charge) pair and file.
        /// </summary>
        /// <param name="key">A sequence, charge pair</param>
        /// <param name="filePath">A file for which the retention information is requested</param>
        /// <param name="retentionTimes">A list of retention times, if successful</param>
        /// <returns>True if retention time information was retrieved successfully</returns>
        public abstract bool TryGetRetentionTimes(LibKey key, string filePath, out double[] retentionTimes);

        /// <summary>
        /// Attempts to get retention time information for all of the
        /// (sequence, charge) pairs identified from a specific file.
        /// </summary>
        /// <param name="filePath">A file for which the retention time information is requested</param>
        /// <param name="retentionTimes"></param>
        /// <returns>True if retention time information was retrieved successfully</returns>
        public abstract bool TryGetRetentionTimes(string filePath, out LibraryRetentionTimes retentionTimes);

        /// <summary>
        /// Attempts to get retention time information for all of the
        /// (sequence, charge) pairs identified from a specific file by index.
        /// </summary>
        /// <param name="fileIndex">Index of a file for which the retention time information is requested</param>
        /// <param name="retentionTimes"></param>
        /// <returns>True if retention time information was retrieved successfully</returns>
        public abstract bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes);

        /// <summary>
        /// Gets all of the spectrum information for a particular (sequence, charge) pair.  This
        /// may include redundant spectra.  The spectrum points themselves are only loaded as it they
        /// requested to give this function acceptable performance.
        /// </summary>
        /// <param name="key">The sequence, charge pair requested</param>
        /// <param name="labelType">An <see cref="IsotopeLabelType"/> for which to get spectra</param>
        /// <param name="bestMatch">True if only the non-redundant best-match spectrum is requested</param>
        /// <returns>An enumeration of <see cref="SpectrumInfo"/></returns>
        public abstract IEnumerable<SpectrumInfo> GetSpectra(LibKey key,
            IsotopeLabelType labelType, bool bestMatch);

        /// <summary>
        /// Returns the number of files or mass spec runs for which this library
        /// contains spectra, or null if this is unknown.
        /// </summary>
        public abstract int? FileCount { get; }

        /// <summary>
        /// Returns the total number of spectra loaded from the library.
        /// </summary>
        public abstract int SpectrumCount { get; }

        /// <summary>
        /// Returns an enumerator for the keys of the spectra loaded from the library.
        /// </summary>
        public abstract IEnumerable<LibKey> Keys { get; }

        /// <summary>
        /// Returns a list of <see cref="RetentionTimeSource"/> objects representing
        /// the data files that this Library can provide peptide retention time
        /// values for.
        /// </summary>
        public virtual IList<RetentionTimeSource> ListRetentionTimeSources()
        {
            return new RetentionTimeSource[0];
        }
        
        #region File reading utility functions

        protected static int GetInt32(byte[] bytes, int index)
        {
            int ibyte = index * 4;
            return bytes[ibyte] | bytes[ibyte + 1] << 8 | bytes[ibyte + 2] << 16 | bytes[ibyte + 3] << 24;
        }

        protected static float GetSingle(byte[] bytes, int index)
        {
            return BitConverter.ToSingle(bytes, index * 4);
        }

        protected static int ReadSize(Stream stream)
        {
            byte[] libSize = new byte[4];
            ReadComplete(stream, libSize, libSize.Length);
            return GetInt32(libSize, 0);
        }

        protected static string ReadString(Stream stream, int countBytes)
        {
            byte[] stringBytes = new byte[countBytes];
            ReadComplete(stream, stringBytes, countBytes);
            return Encoding.UTF8.GetString(stringBytes);
        }

        protected static void ReadComplete(Stream stream, byte[] buffer, int size)
        {
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException("Data truncation in library header. File may be corrupted.");
        }

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected Library()
        {
        }

        private enum ATTR
        {
            file_name_hint
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            FileNameHint = reader.GetAttribute(ATTR.file_name_hint);
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.file_name_hint, FileNameHint);
        }

        #endregion
    }

    public interface ICachedSpectrumInfo
    {
        LibKey Key { get; }        
    }

    public abstract class CachedLibrary<TInfo> : Library
        where TInfo : ICachedSpectrumInfo
    {
        protected CachedLibrary()
        {
        }

        protected CachedLibrary(LibrarySpec spec) : base(spec)
        {
        }

        protected TInfo[] _libraryEntries;

        protected Dictionary<LibSeqKey, bool> _setSequences;

        protected string CachePath { get; set; }

        public override bool IsLoaded
        {
            get { return _libraryEntries != null; }
        }

        public override bool ContainsAny(LibSeqKey key)
        {
            return (_setSequences != null && _setSequences.ContainsKey(key));
        }

        public override bool Contains(LibKey key)
        {
            return FindEntry(key) != -1;
        }

        protected static int CompareSpectrumInfo(TInfo info1, TInfo info2)
        {
            return info1.Key.Compare(info2.Key);
        }

        protected int FindEntry(LibKey key)
        {
            if (_libraryEntries == null)
                return -1;
            return FindEntry(key, 0, _libraryEntries.Length - 1);
        }

        private int FindEntry(LibKey key, int left, int right)
        {
            // Binary search for the right key
            if (left > right)
                return -1;
            int mid = (left + right) / 2;
            int compare = key.Compare(_libraryEntries[mid].Key);
            if (compare < 0)
                return FindEntry(key, left, mid - 1);
            if (compare > 0)
                return FindEntry(key, mid + 1, right);
            return mid;
        }

        public override bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                libInfo = CreateSpectrumHeaderInfo(_libraryEntries[i]);
                return true;

            }
            libInfo = null;
            return false;
        }

        protected abstract SpectrumHeaderInfo CreateSpectrumHeaderInfo(TInfo info);

        public override bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum)
        {
            int i = FindEntry(key);
            if (i != -1)
            {
                var spectrumPeaks = ReadSpectrum(_libraryEntries[i]);
                if (spectrumPeaks != null)
                {
                    spectrum = new SpectrumPeaksInfo(spectrumPeaks);
                    return true;
                }
            }
            spectrum = null;
            return false;
        }

        public override SpectrumPeaksInfo LoadSpectrum(object spectrumKey)
        {
                var spectrumPeaks = ReadSpectrum(_libraryEntries[(int)spectrumKey]);
                if (spectrumPeaks == null)
                    throw new IOException(string.Format("Library entry not found {0}.", spectrumKey));

                return new SpectrumPeaksInfo(spectrumPeaks);
        }

        protected abstract SpectrumPeaksInfo.MI[] ReadSpectrum(TInfo info);

        public override bool TryGetRetentionTimes(LibKey key, string filePath, out double[] retentionTimes)
        {
            // By default, no retention time information is available
            retentionTimes = null;
            return false;
        }

        public override bool TryGetRetentionTimes(string filePath, out LibraryRetentionTimes retentionTimes)
        {
            // By default, no retention time information is available
            retentionTimes = null;
            return false;
        }

        public override bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes)
        {
            // By default, no retention time information is available
            retentionTimes = null;
            return false;
        }

        public override IEnumerable<SpectrumInfo> GetSpectra(LibKey key, IsotopeLabelType labelType, bool bestMatch)
        {
            // This base class only handles best match spectra
            if (bestMatch)
            {
                int i = FindEntry(key);
                if (i != -1)
                {
                    yield return new SpectrumInfo(this, labelType, i)
                    {
                        SpectrumHeaderInfo = CreateSpectrumHeaderInfo(_libraryEntries[i])
                    };
                }
            }
        }

        public override int? FileCount
        {
            get { return null; }
        }

        public override int SpectrumCount
        {
            get { return _libraryEntries == null ? 0 : _libraryEntries.Length; }
        }

        public override IEnumerable<LibKey> Keys
        {
            get
            {
                if (IsLoaded)
                    foreach (var entry in _libraryEntries)
                        yield return entry.Key;
            }
        }
    }

    public sealed class LibraryRetentionTimes : IRetentionTimeProvider
    {
        private readonly IDictionary<string, double[]> _dictPeptideRetentionTimes;

        public LibraryRetentionTimes(string path, IDictionary<string, double[]> dictPeptideRetentionTimes)
        {
            Name = path;
            _dictPeptideRetentionTimes = dictPeptideRetentionTimes;
            MinRt = _dictPeptideRetentionTimes.SelectMany(p => p.Value).Min();
            MaxRt = _dictPeptideRetentionTimes.SelectMany(p => p.Value).Max();
            var listStdev = new List<double>();
            foreach (double[] times in _dictPeptideRetentionTimes.Values)
            {
                if (times.Length < 2)
                    continue;
                var statTimes = new Statistics(times);
                listStdev.Add(statTimes.StdDev());
            }
            var statStdev = new Statistics(listStdev);
            MeanStdev = statStdev.Mean();
        }

        public string Name { get; private set; }
        public double MinRt { get; private set; }
        public double MaxRt { get; private set; }
        public double MeanStdev { get; private set; }

        /// <summary>
        /// Returns all retention times for spectra that were identified to a
        /// specific modified peptide sequence.
        /// </summary>
        public double[] GetRetentionTimes(string sequence)
        {
            double[] retentionTimes;
            if (_dictPeptideRetentionTimes.TryGetValue(sequence, out retentionTimes))
                return retentionTimes;
            return new double[0];
        }

        /// <summary>
        /// Return the average retention time for spectra that were identified to a
        /// specific modified peptide sequence, with filtering applied in an attempt
        /// to avoid peptides eluting a second time near the end of the gradient.
        /// </summary>
        public double? GetRetentionTime(string sequence)
        {
            double[] retentionTimes = GetRetentionTimes(sequence);
            if (retentionTimes.Length == 0)
                return null;
            if (retentionTimes.Length == 1)
                return retentionTimes[0];

            double meanTimes = retentionTimes[0];
            // Anything 3 times the mean standard deviation away from the mean is suspicious
            double maxDelta = MeanStdev*3;
            for (int i = 1; i < retentionTimes.Length; i++)
            {
                double time = retentionTimes[i];
                double delta = time - meanTimes;
                // If the time is more than the max delta from the other times, and closer
                // to the end than to the other times, then do not include it or anything
                // after it.
                if (delta > maxDelta && delta > MaxRt - time)
                {
                    double[] subsetTimes = new double[i];
                    Array.Copy(retentionTimes, subsetTimes, i);
                    retentionTimes = subsetTimes;
                    break;
                }
                // Adjust the running mean.
                meanTimes += (time - meanTimes)/(i+1);
            }
            return retentionTimes.Average();
        }

        public TimeSource? GetTimeSource(string sequence)
        {
            return TimeSource.scan;
        }

        public IEnumerable<MeasuredRetentionTime> PeptideRetentionTimes
        {
            get
            {
                return from sequence in _dictPeptideRetentionTimes.Keys
                       let time = GetRetentionTime(sequence)
                       where time.HasValue
                       select new MeasuredRetentionTime(sequence, time.Value);
            }
        }

        public IDictionary<string, double> GetFirstRetentionTimes()
        {
            var dict = new Dictionary<string, double>();
            foreach (var entry in _dictPeptideRetentionTimes)
            {
                if (entry.Value.Length == 0)
                {
                    continue;
                }
                dict.Add(entry.Key, entry.Value.Min());
            }
            return dict;
        }
    }

    public abstract class LibrarySpec : XmlNamedElement
    {
        public static readonly PeptideRankId PEP_RANK_COPIES =
            new PeptideRankId("Spectrum count");
        public static readonly PeptideRankId PEP_RANK_TOTAL_INTENSITY =
            new PeptideRankId("Total intensity");
        public static readonly PeptideRankId PEP_RANK_PICKED_INTENSITY =
            new PeptideRankId("Picked intensity");

        protected LibrarySpec(string name, string path)
            : base(name)
        {
            FilePath = path;
        }

        public string FilePath { get; private set; }

        /// <summary>
        /// True if this library spec was created in order to open the current document
        /// only, and should not be stored long term in the global settings.
        /// </summary>
        public bool IsDocumentLocal { get; private set; }

        public abstract Library LoadLibrary(ILoadMonitor loader);

        public abstract IEnumerable<PeptideRankId> PeptideRankIds { get; }

        #region Property change methods

        public LibrarySpec ChangeFilePath(string prop)
        {
            return ChangeProp(ImClone(this), im => im.FilePath = prop);
        }

        public LibrarySpec ChangeDocumentLocal(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsDocumentLocal = prop);
        }        

        #endregion

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected LibrarySpec()
        {
        }

        private enum ATTR
        {
            file_path
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            FilePath = reader.GetAttribute(ATTR.file_path);
            // Consume tag
            reader.Read();
        }

        public override void WriteXml(XmlWriter writer)
        {
            if (IsDocumentLocal)
                throw new InvalidOperationException("Document local library specs cannot be persisted to XML.");

            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeString(ATTR.file_path, FilePath);
        }

        #endregion

        #region object overrides

        public bool Equals(LibrarySpec other)
        {
            if (ReferenceEquals(null, other)) return false;
            if (ReferenceEquals(this, other)) return true;
            return base.Equals(other) &&
                Equals(other.FilePath, FilePath) &&
                other.IsDocumentLocal.Equals(IsDocumentLocal);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj as LibrarySpec);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int result = base.GetHashCode();
                result = (result*397) ^ FilePath.GetHashCode();
                result = (result*397) ^ IsDocumentLocal.GetHashCode();
                return result;
            }
        }

        #endregion
    }

    /// <summary>
    /// Identity class for a type peptide ranking, with values for
    /// displaying in the user interface, and persisting to XML.
    /// </summary>
    public sealed class PeptideRankId
    {
        public static readonly PeptideRankId PEPTIDE_RANK_NONE = new PeptideRankId("");

        public PeptideRankId(string label)
        {
            Label = label;
        }

        /// <summary>
        /// Display text for user interface.
        /// </summary>
        public string Label { get; private set; }

        /// <summary>
        /// Name for us in XML.
        /// </summary>
        public string Value { get { return Label; } }

        public override string ToString() { return Label; }
    }

    public abstract class SpectrumHeaderInfo : Immutable, IXmlSerializable
    {
        protected SpectrumHeaderInfo(string libraryName)
        {
            LibraryName = libraryName;
        }

        public string LibraryName { get; private set; }

        public SpectrumHeaderInfo ChangeLibraryName(string prop)
        {
            return ChangeProp(ImClone(this), im => im.LibraryName = prop);
        }

        /// <summary>
        /// Value used in ranking peptides.
        /// </summary>
        /// <param name="rankId">Indentifier of the value to return</param>
        /// <returns>The value to use in ranking</returns>
        public virtual float GetRankValue(PeptideRankId rankId)
        {
            // If super class has not provided a number of copies, return 1.
            if (rankId == LibrarySpec.PEP_RANK_COPIES)
                return 1;
            return float.MinValue;
        }

        public abstract IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues { get; }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected SpectrumHeaderInfo()
        {
        }

        private enum ATTR
        {
            library_name
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public virtual void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            LibraryName = reader.GetAttribute(ATTR.library_name);
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttributeString(ATTR.library_name, LibraryName);
        }

        #endregion

        #region object overrides

        public bool Equals(SpectrumHeaderInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.LibraryName, LibraryName);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (SpectrumHeaderInfo)) return false;
            return Equals((SpectrumHeaderInfo) obj);
        }

        public override int GetHashCode()
        {
            return LibraryName.GetHashCode();
        }

        #endregion
    }

    public sealed class TransitionLibInfo
    {
        public TransitionLibInfo(int rank, float intensity)
        {
            Rank = rank;
            Intensity = intensity;
        }

        public int Rank { get; private set; }
        public float Intensity { get; private set; }

        #region object overrides

        public bool Equals(TransitionLibInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj.Intensity == Intensity && obj.Rank == Rank;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != typeof (TransitionLibInfo)) return false;
            return Equals((TransitionLibInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Intensity.GetHashCode()*397) ^ Rank;
            }
        }

        #endregion
    }

    public sealed class SpectrumPeaksInfo
    {
        public SpectrumPeaksInfo(MI[] spectrum)
        {
            Peaks = spectrum;
        }

        /// <summary>
        /// This array must be highly performant.  Making this class
        /// <see cref="Immutable"/>, and using a <see cref="ReadOnlyCollection{T}"/>
        /// caused iteration of this list to show up as a hotspot in
        /// a profiler.
        /// </summary>
        public MI[] Peaks { get; private set; }

        public IEnumerable<double> MZs
        {
            get
            {
                foreach (var mi in Peaks)
                    yield return mi.Mz;
            }
        }

        public IEnumerable<double> Intensities
        {
            get
            {
                foreach (var mi in Peaks)
                    yield return mi.Intensity;
            }
        }

        public struct MI
        {
            public double Mz { get; set; }
            public float Intensity { get; set; }
        }
    }

    /// <summary>
    /// Information required for spectrum display in a list.
    /// </summary>
    public sealed class SpectrumInfo
    {
        private readonly Library _library;

        public SpectrumInfo(Library library, IsotopeLabelType labelType, object spectrumKey)
            : this(library, labelType, null, null, true, spectrumKey)
        {
        }

        public SpectrumInfo(Library library, IsotopeLabelType labelType,
            string filePath, double? retentionTime, bool isBest, object spectrumKey)
        {
            _library = library;
            SpectrumKey = spectrumKey;

            LabelType = labelType;
            FilePath = filePath;
            RetentionTime = retentionTime;
            IsBest = isBest;
        }

        public string LibName { get { return _library.Name; } }
        public IsotopeLabelType LabelType { get; private set; }
        public string FilePath { get; private set; }
        public string FileName { get { return Path.GetFileName(FilePath); } }
        public double? RetentionTime { get; private set; }
        public bool IsBest { get; private set; }
        public object SpectrumKey { get; private set; }

        public SpectrumHeaderInfo SpectrumHeaderInfo { get; set; }

        public SpectrumPeaksInfo SpectrumPeaksInfo { get { return _library.LoadSpectrum(SpectrumKey); } }
    }

    /// <summary>
    /// Links to spectral library sources
    /// </summary>
    public sealed class LibraryLink
    {
        public static readonly LibraryLink PEPTIDEATLAS = new LibraryLink("PeptideAtlas", "http://www.peptideatlas.org/speclib/");
        public static readonly LibraryLink NIST = new LibraryLink("NIST", "http://peptide.nist.gov/");
        public static readonly LibraryLink GPM = new LibraryLink("GPM", "ftp://ftp.thegpm.org/projects/xhunter/libs/");

        private LibraryLink(string name, string href)
        {
            Name = name;
            Link = href;
        }

        public string Name { get; private set; }

        public string Link { get; private set; }
    }

    /// <summary>
    /// Some spectrum library details that can be displayed in a dialog box.
    /// This can be the format of the library (e.g. BiblioSpec, SpectraST etc.),
    /// a library revision (when available), number of peptides etc.
    /// Optionally, appropriate links to spectral library sources can also be included.
    /// </summary>
    public sealed class LibraryDetails
    {
        private readonly IList<LibraryLink> _libLinks;
        private IEnumerable<string> _dataFiles;
        
        public LibraryDetails()
        {
            _libLinks = new List<LibraryLink>();
        }
        public void AddLink(LibraryLink link)
        {
            _libLinks.Add(link);
        }

        public string Id { get; set; }

        // e.g. BiblioSpec, SpectraST etc.
        public string Format { get; set; }

        // library revision
        public string Revision { get; set; }

        // version of the program that generated the library
        public string Version { get; set; }

        public int PeptideCount { get; set; }

        public int TotalPsmCount { get; set; }

		public IEnumerable<String> DataFiles
        { 
            get { return _dataFiles ?? (_dataFiles = new List<string>(0)); }
		    set { _dataFiles = value; }
        }

        public IEnumerable<LibraryLink> LibLinks
        {
            get { return _libLinks; }
        }
    }

    /// <summary>
    /// Key for use in dictionaries that store library header information in
    /// memory.  The key is tuned to consume as little memory as possible, and
    /// reduces the memory consumed by the <see cref="BiblioSpecLibrary"/> by
    /// about 60% over <see cref="LibKeyOld"/>.
    /// <para>
    /// The sequence could be tuned further to store amino acids as nibbles
    /// instead of bytes, because it will need to support modification
    /// information, and because already the byte arrays are less than
    /// 50% of the total dictionary size for <see cref="BiblioSpecLibrary"/>
    /// further compression is left as an exercise for the future.</para>
    /// </summary>
    public struct LibKey
    {
        private readonly byte[] _key;

        public LibKey(string sequence, int charge)
            : this(Encoding.Default.GetBytes(sequence), 0, sequence.Length, charge)
        {
        }

        public LibKey(byte[] sequence, int start, int len, int charge)
        {
            _key = new byte[len + 1];
            _key[0] = (byte)charge;
            Array.Copy(sequence, start, _key, 1, len);
        }

        public string Sequence { get { return Encoding.Default.GetString(_key, 1, _key.Length - 1); } }
        public int Charge { get { return _key[0]; } }
        public bool IsModified { get { return _key.Contains((byte)'['); } }

        /// <summary>
        /// Only for use by <see cref="LibSeqKey"/>
        /// </summary>
        public byte[] Key { get { return _key; }}

        public void WriteSequence(Stream outStream)
        {
            outStream.Write(BitConverter.GetBytes(_key.Length - 1), 0, sizeof(int));
            outStream.Write(_key, 1, _key.Length - 1);
        }

        public int Compare(LibKey key2)
        {
            byte[] raw1 = _key, raw2 = key2._key;
            int len = Math.Min(raw1.Length, raw2.Length);
            for (int i = 0; i < len; i++)
            {
                byte b1 = raw1[i], b2 = raw2[i];
                if (b1 != b2)
                    return b1 - b2;
            }
            return raw1.Length - raw2.Length;
        }

        public IEnumerable<char> AminoAcids
        {
            get
            {
                for (int i = 1; i < _key.Length; i++)
                {
                    char aa = (char)_key[i];
                    if (AminoAcid.IsExAA(aa))
                        yield return aa;
                }
            }
        }

        public int ModificationCount
        {
            get
            {
                int count = 0;
                for (int i = 1; i < _key.Length; i++)
                {
                    if (_key[i] == '[')
                        count++;
                }
                return count;
            }
        }   

        #region object overrides

        public bool Equals(LibKey obj)
        {
            int len = obj._key.Length;

            if (len != _key.Length)
                return false;

            for (int i = 0; i < len; i++)
                if (obj._key[i] != _key[i])
                    return false;

            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(LibKey)) return false;
            return Equals((LibKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = 0;
                foreach (byte b in _key)
                    result = (result*31) ^ b;
                return result;
            }
        }

        public override string ToString()
        {
            return Sequence + Transition.GetChargeIndicator(Charge);
        }

        #endregion
    }

    /// <summary>
    /// This key represents a plain, unmodified peptide amino acid sequence, but
    /// allows either a plain string or the bytes from a potentially modified
    /// <see cref="LibKey"/> to be used as the internal representation of the
    /// amino acides.  Using the <see cref="LibKey"/> bytes greatly reduces the
    /// memory cost of keeping a set of these in every library.
    /// </summary>
    public struct LibSeqKey
    {
        private readonly int _length;
        private readonly int _cachedHash;
        private readonly string _seq;
        private readonly byte[] _libKeyBytes;

        /// <summary>
        /// Creates a key from a plain string of amino acid characters.
        /// </summary>
        /// <param name="seq">String of amino acid characters</param>
        public LibSeqKey(string seq) : this()
        {
            _seq = seq;
            _length = seq.Length;
            _cachedHash = GetHashCodeInternal();
        }

        /// <summary>
        /// Creates a key from an existing <see cref="LibKey"/> using the same
        /// key bytes to minimize memory impact.
        /// </summary>
        /// <param name="key">A <see cref="LibKey"/> with modified sequence bytes from a library</param>
        public LibSeqKey(LibKey key) : this()
        {
            _libKeyBytes = key.Key;
            _length = AminoAcids.Count();
            _cachedHash = GetHashCodeInternal();
        }

        public LibSeqKey(LibKey key, int hash, int aminoAcid)
            : this()
        {
            _libKeyBytes = key.Key;
            _length = aminoAcid;
            _cachedHash = hash;
        }

        public int Length { get { return _length; } }

        /// <summary>
        /// Enumerates the amino acid characters of the sequence, with special handling for
        /// the case where the internal representation is the byte sequence from at
        /// <see cref="LibKey"/>.
        /// </summary>
        IEnumerable<char> AminoAcids
        {
            get
            {
                if (_seq != null)
                {
                    foreach (char aa in _seq)
                        yield return aa;
                }
                else
                {
                    for (int i = 1; i < _libKeyBytes.Length; i++)
                    {
                        char aa = (char)_libKeyBytes[i];
                        if ('A' <= aa && aa <= 'Z')
                            yield return aa;
                    }
                }
            }
        }

        #region object overrides

        public bool Equals(LibSeqKey obj)
        {
            // Length check short-cut
            if (obj._length != _length)
                return false;

            // Compare each amino acid
            var enumObjAa = obj.AminoAcids.GetEnumerator();
            foreach (char aa in AminoAcids)
            {
                enumObjAa.MoveNext();
                if (aa != enumObjAa.Current)
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(LibSeqKey)) return false;
            return Equals((LibSeqKey)obj);
        }

        public override int GetHashCode()
        {
            return _cachedHash;
        }
        private int GetHashCodeInternal()
        {
            unchecked
            {
                int result = _length.GetHashCode();
                foreach (char aa in AminoAcids)
                {
                    result = (result*31) ^ aa;
                }
                return result;
            }
        }
        #endregion
    }

    /// <summary>
    /// This version supports the same interface, but uses considerably more
    /// memory, both in the unicode strings and doubling the size of the
    /// struct stored in the dictionary.
    /// </summary>
    public struct LibKeyOld
    {
        private readonly string _sequence;
        private readonly int _charge;

        public LibKeyOld(string sequence, int charge)
        {
            _sequence = sequence;
            _charge = charge;
        }

        public LibKeyOld(byte[] sequence, int start, int len, int charge)
            : this(Encoding.Default.GetString(sequence, start, len), charge)
        {
        }

        public string Sequence { get { return _sequence; } }
        public int Charge { get { return _charge; } }

        #region object overrides

        public bool Equals(LibKeyOld obj)
        {
            return obj._charge == _charge && Equals(obj._sequence, _sequence);
        }

        public override bool Equals(object obj)
        {
            if (obj.GetType() != typeof(LibKeyOld)) return false;
            return Equals((LibKeyOld)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (_charge * 397) ^ _sequence.GetHashCode();
            }
        }

        public override string ToString()
        {
            return _sequence + Transition.GetChargeIndicator(_charge);
        }

        #endregion
    }
}