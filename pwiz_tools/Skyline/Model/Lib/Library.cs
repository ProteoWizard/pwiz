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
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;
using pwiz.Common.Collections;
using pwiz.Common.PeakFinding;
using pwiz.Common.SystemUtil;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Model.Lib
{
    public sealed class LibraryManager : BackgroundLoader
    {
        private readonly Dictionary<string, Library> _loadedLibraries =
            new Dictionary<string, Library>();
        private readonly Dictionary<string, LibraryLoadLock> _loadingLibraries =
            new Dictionary<string, LibraryLoadLock>();

        private class LibraryLoadLock
        {
            public Library Library { get; set; }
            public bool IsLoaded { get; set; }
        }

        public override void ClearCache()
        {
            lock(_loadedLibraries)
            {
                _loadedLibraries.Clear();
            }
        }

        protected override bool StateChanged(SrmDocument document, SrmDocument previous)
        {
            return previous == null ||
                !ReferenceEquals(document.Settings.PeptideSettings.Libraries, previous.Settings.PeptideSettings.Libraries) ||
                !ReferenceEquals(document.Settings.MeasuredResults, previous.Settings.MeasuredResults);
        }

        protected override string IsNotLoadedExplained(SrmDocument document)
        {
            PeptideLibraries libraries = document.Settings.PeptideSettings.Libraries;
            if (document.Settings.MeasuredResults != null)
            {
                var missingFiles = MidasLibrary.GetMissingFiles(document, new Library[0]);
                if (missingFiles.Any())
                {
                    return TextUtil.LineSeparate("MIDAS library is missing files:", // Not L10N
                        TextUtil.LineSeparate(missingFiles));
                }
            }
            return !libraries.HasLibraries ? null : libraries.IsNotLoadedExplained;
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
            if (tag == null)
                return false;
            PeptideLibraries libraries = container.Document.Settings.PeptideSettings.Libraries;
            var missingMidasFiles = tag as string[];
            if (missingMidasFiles != null)
            {
                return !missingMidasFiles.SequenceEqual(MidasLibrary.GetMissingFiles(container.Document, new Library[0]));
            }
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
                    if (spec == null || dictLibraries.ContainsKey(spec.Name))
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

                var missingMidasFiles = MidasLibrary.GetMissingFiles(document, libraries.Libraries);
                var midasLibPath = MidasLibSpec.GetLibraryFileName(container.DocumentFilePath);
                var midasLibSpec = libraries.MidasLibrarySpecs.FirstOrDefault(libSpec => Equals(libSpec.FilePath, midasLibPath));
                var newMidasLibSpec = missingMidasFiles.Any() && midasLibSpec == null;
                MidasLibrary midasLibrary = null;
                var failedMidasFiles = new List<MsDataFilePath>();
                if (missingMidasFiles.Any())
                {
                    if (midasLibSpec == null)
                    {
                        // Need to add MIDAS LibSpec to document
                        midasLibSpec = (MidasLibSpec)LibrarySpec.CreateFromPath(MidasLibSpec.GetName(container.DocumentFilePath, libraries.LibrarySpecs), midasLibPath);
                    }
                    MidasLibrary.AddSpectra(midasLibSpec, missingMidasFiles.Select(f => new MsDataFilePath(f)).ToArray(), docCurrent, new LoadMonitor(this, container, null), out failedMidasFiles);
                    if (failedMidasFiles.Count < missingMidasFiles.Length)
                    {
                        if (!newMidasLibSpec)
                            ReloadLibraries(container, midasLibSpec);
                        midasLibrary = (MidasLibrary) LoadLibrary(midasLibSpec, () => new LoadMonitor(this, container, !newMidasLibSpec ? midasLibSpec : null));

                        if (midasLibrary != null && !dictLibraries.ContainsKey(midasLibSpec.Name))
                            dictLibraries.Add(midasLibSpec.Name, midasLibrary);
                    }
                    else
                    {
                        midasLibSpec = null;
                        newMidasLibSpec = false;
                    }
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
                    if (!changed && !newMidasLibSpec && !failedMidasFiles.Any())
                    {
                        return false;
                    }

                    docNew = docCurrent;
                    if (newMidasLibSpec)
                    {
                        // We need to add this MIDAS LibrarySpec to the document
                        var libSpecs = libraries.LibrarySpecs.ToList();
                        libSpecs.Add(midasLibSpec);
                        docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideLibraries(libs => libs.ChangeLibrarySpecs(libSpecs)));
                        libraries = docNew.Settings.PeptideSettings.Libraries;
                        list.Add(midasLibrary);
                        docNew.Settings.UpdateLists(container.DocumentFilePath);

                        // Switch to pick by filter if there are no other libraries
                        if (libSpecs.Count == 1)
                        {
                            libraries = libraries.ChangePick(PeptidePick.filter);
                            docNew = docNew.ChangeSettings(docNew.Settings.ChangeTransitionSettings(
                                settings => settings.ChangeLibraries(settings.Libraries.ChangePick(TransitionLibraryPick.none))));
                        }
                    }
                    libraries = libraries.ChangeLibraries(list.ToArray());

                    if (missingMidasFiles.Any() && docNew.Settings.HasResults)
                    {
                        var newChromatograms = MidasLibrary.UnflagFiles(docNew.Settings.MeasuredResults.Chromatograms, missingMidasFiles.Select(Path.GetFileName)).ToList();
                        if (!ArrayUtil.ReferencesEqual(docNew.Settings.MeasuredResults.Chromatograms, newChromatograms))
                        {
                            docNew = docNew.ChangeMeasuredResults(docNew.Settings.MeasuredResults.ChangeChromatograms(newChromatograms));
                        }
                    }

                    using (var settingsChangeMonitor = new SrmSettingsChangeMonitor(
                            new LoadMonitor(this, container, null), Resources.LibraryManager_LoadBackground_Updating_library_settings_for__0_, container, docCurrent))
                    {
                        try
                        {
                            docNew = docNew.ChangeSettings(docNew.Settings.ChangePeptideSettings(
                                docNew.Settings.PeptideSettings.ChangeLibraries(libraries)), settingsChangeMonitor);
                        }
                        catch (InvalidDataException x)
                        {
                            UpdateProgress(new ProgressStatus(string.Empty).ChangeErrorException(x));
                            break;
                        }
                        catch (OperationCanceledException)
                        {
                            docNew = docCurrent;    // Just continue
                        }
                    }
                }
                while (!CompleteProcessing(container, docNew, docCurrent));
            }
            finally
            {
                foreach (var library in dictLibraries.Values.Where(lib => lib.ReadStream != null))
                {
                    lock (library.ReadStream)
                    {
                        library.ReadStream.CloseStream();
                    }
                }
                EndProcessing(docCurrent);
            }

            return true;
        }

        public Library LoadLibrary(LibrarySpec spec, Func<ILoadMonitor> getMonitor)
        {
            LibraryLoadLock loadLock;

            lock (_loadedLibraries)
            {
                Library library;
                if (_loadedLibraries.TryGetValue(spec.Name, out library))
                    return library;
                // If the library has not yet been loaded, then create a new lock
                // for everyone to wait on until the library has been loaded.
                if (!_loadingLibraries.TryGetValue(spec.Name, out loadLock))
                {
                    loadLock = new LibraryLoadLock();
                    _loadingLibraries.Add(spec.Name, loadLock);
                }
            }

            lock (loadLock)
            {
                if (!loadLock.IsLoaded)
                {
                    loadLock.Library = spec.LoadLibrary(getMonitor());
                    loadLock.IsLoaded = true;
                }
            }

            lock (_loadedLibraries)
            {
                _loadingLibraries.Remove(spec.Name);
                if (loadLock.Library != null)
                {
                    // Update the newly loaded library in the dictionary, regardless of whether
                    // we were the thread that actually did the loading.
                    _loadedLibraries[spec.Name] = loadLock.Library;
                }
                return loadLock.Library;
            }
        }

        private Library LoadLibrary(IDocumentContainer container, LibrarySpec spec)
        {
            return LoadLibrary(spec, () => new LoadMonitor(this, container, spec));
        }

        public void ReloadLibraries(IDocumentContainer container, params LibrarySpec[] specs)
        {
            lock (_loadedLibraries)
            {
                foreach (var spec in specs)
                {
                    _loadedLibraries.Remove(spec.Name);
                }

                ForDocumentLibraryReload(container, specs.Select(spec => spec.Name).ToArray());
            }
        }

        public void ReleaseLibraries(params LibrarySpec[] specs)
        {
            lock (_loadedLibraries)
            {
                foreach (var spec in specs)
                {
                    _loadedLibraries.Remove(spec.Name);
                }
            }
        }

        public Library TryGetLibrary(LibrarySpec spec)
        {
            lock (_loadedLibraries)
            {
                Library library;
                _loadedLibraries.TryGetValue(spec.Name, out library);
                return library;
            }
        }

        public delegate bool BuildFunction(IDocumentContainer documentContainer,
                                           ILibraryBuilder libraryBuilder,
                                           IProgressMonitor monitor,
                                           BuildState buildState);

        public sealed class BuildState
        {
            public BuildState(LibrarySpec librarySpec, BuildFunction buildFunc)
            {
                LibrarySpec = librarySpec;
                BuildFunc = buildFunc;
            }

            public LibrarySpec LibrarySpec { get; private set; }
            public BuildFunction BuildFunc { get; private set; }
            public string ExtraMessage { get; set; }
            public IrtStandard IrtStandard { get; set; }
        }

        public void BuildLibrary(IDocumentContainer container, ILibraryBuilder builder, AsyncCallback callback)
        {
            BuildFunction buildFunc = BuildLibraryBackground;
            var monitor = new LibraryBuildMonitor(this, container);
            BuildState buildState = new BuildState(builder.LibrarySpec, buildFunc);
            buildFunc.BeginInvoke(container, builder, monitor, buildState, callback, buildState);
        }

        public bool BuildLibraryBackground(IDocumentContainer container, ILibraryBuilder builder, IProgressMonitor monitor, BuildState buildState)
        {
            LocalizationHelper.InitThread();
            // Avoid building a library that is loading or allowing the library to be loaded
            // while it is building
            LibraryLoadLock loadLock;

            lock (_loadedLibraries)
            {
                if (!_loadingLibraries.TryGetValue(builder.LibrarySpec.Name, out loadLock))
                {
                    loadLock = new LibraryLoadLock();
                    _loadingLibraries.Add(builder.LibrarySpec.Name, loadLock);
                }
            }

            bool success;
            lock (loadLock)
            {
                success = builder.BuildLibrary(monitor);
                var biblioSpecLiteBuilder = builder as BiblioSpecLiteBuilder;
                if (null != biblioSpecLiteBuilder)
                {
                    if (!string.IsNullOrEmpty(biblioSpecLiteBuilder.AmbiguousMatchesMessage))
                    {
                        buildState.ExtraMessage = biblioSpecLiteBuilder.AmbiguousMatchesMessage;
                    }
                    if (biblioSpecLiteBuilder.IrtStandard != null &&
                        biblioSpecLiteBuilder.IrtStandard != IrtStandard.NULL)
                    {
                        buildState.IrtStandard = biblioSpecLiteBuilder.IrtStandard;
                    }
                }
            }

            lock (_loadedLibraries)
            {
                _loadingLibraries.Remove(builder.LibrarySpec.Name);
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
                    ForDocumentLibraryReload(container, new[] {name});
                }

                return success;
            }
        }

        private static void ForDocumentLibraryReload(IDocumentContainer container, string[] specs)
        {
            var docOriginal = container.Document;
            if (docOriginal == null)
                return;
            var librarySettings = docOriginal.Settings.PeptideSettings.Libraries;
            if (!librarySettings.HasLibraries)
                return;
            int iSpec = librarySettings.LibrarySpecs.IndexOf(spec => spec != null && specs.Contains(spec.Name));
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
                                int i = lib.LibrarySpecs.IndexOf(spec => specs.Contains(spec.Name));
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

            public UpdateProgressResponse UpdateProgress(IProgressStatus status)
            {
                return _manager.UpdateProgress(status);
            }

            public bool HasUI { get { return false; } }
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

    public enum LibraryRedundancy { best, all, all_redundant }

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
        /// Returns the filter string to be used for finding a library of this type.
        /// </summary>
        public abstract string SpecFilter { get; }

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
        public bool IsLoaded
        {
            get { return IsNotLoadedExplained == null; }
        }

        /// <summary>
        /// Same as IsLoaded property, but returns a non-null and hopefully useful message 
        /// for test purposes when not loaded.
        /// </summary>
        public abstract string IsNotLoadedExplained { get; }

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
        /// Only contains paths for files in library
        /// Unlike LibraryDetails which contains more information
        /// </summary>
        public abstract  LibraryFiles LibraryFiles { get; }

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

        public virtual LibraryChromGroup LoadChromatogramData(object spectrumKey)
        {
            return null;
        }

        /// <summary>
        /// Attempts to get retention time information for a specific
        /// (sequence, charge) pair and file.
        /// </summary>
        /// <param name="key">A sequence, charge pair</param>
        /// <param name="filePath">A file for which the retention information is requested</param>
        /// <param name="retentionTimes">A list of retention times, if successful</param>
        /// <returns>True if retention time information was retrieved successfully</returns>
        public abstract bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes);

        /// <summary>
        /// Attempts to get retention time information for all of the
        /// (sequence, charge) pairs identified from a specific file.
        /// </summary>
        /// <param name="filePath">A file for which the retention time information is requested</param>
        /// <param name="retentionTimes"></param>
        /// <returns>True if retention time information was retrieved successfully</returns>
        public abstract bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes);

        /// <summary>
        /// Attempts to get retention time information for all of the
        /// (sequence, charge) pairs identified from a specific file by index.
        /// </summary>
        /// <param name="fileIndex">Index of a file for which the retention time information is requested</param>
        /// <param name="retentionTimes"></param>
        /// <returns>True if retention time information was retrieved successfully</returns>
        public abstract bool TryGetRetentionTimes(int fileIndex, out LibraryRetentionTimes retentionTimes);

        /// <summary>
        /// If an explicit peak boundary has been set for any of the peptide sequences, then return 
        /// that peak boundary.
        /// </summary>
        public virtual PeakBounds GetExplicitPeakBounds(MsDataFileUri filePath, IEnumerable<Target> peptideSequences)
        {
            return null;
        }

        /// <summary>
        /// Attempts to get iRT information from the library.
        /// </summary>
        /// <param name="retentionTimes">A list of iRTs, if successful</param>
        /// <returns>True if iRT information was retrieved successfully</returns>
        public abstract bool TryGetIrts(out LibraryRetentionTimes retentionTimes);

        public virtual IEnumerable<double> GetRetentionTimesWithSequences(string filePath, IEnumerable<Target> peptideSequences, ref int? fileIndex)
        {
            return new double[0];
        }


        /// <summary>
        /// Attempts to get ion mobility information for a specific
        /// (sequence, charge) pair and file.
        /// </summary>
        /// <param name="key">A sequence, charge pair</param>
        /// <param name="filePath">A file for which the ion mobility information is requested</param>
        /// <param name="ionMobilities">A list of ion mobility info, if successful</param>
        /// <returns>True if ion mobility information was retrieved successfully</returns>
        public abstract bool TryGetIonMobilityInfos(LibKey key, MsDataFileUri filePath, out IonMobilityAndCCS[] ionMobilities);

        /// <summary>
        /// Attempts to get ion mobility information for all of the
        /// (sequence, charge) pairs identified from a specific file.
        /// </summary>
        /// <param name="filePath">A file for which the ion mobility information is requested</param>
        /// <param name="ionMobilities">A list of ion mobility info, if successful</param>
        /// <returns>True if ion mobility information was retrieved successfully</returns>
        public abstract bool TryGetIonMobilityInfos(MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities);

        /// <summary>
        /// Attempts to get ion mobility information for all of the
        /// (sequence, charge) pairs identified from a specific file by index.
        /// </summary>
        /// <param name="fileIndex">Index of a file for which the ion mobility information is requested</param>
        /// <param name="ionMobilities">A list of ion mobility info, if successful</param>
        /// <returns>True if ion mobility information was retrieved successfully</returns>
        public abstract bool TryGetIonMobilityInfos(int fileIndex, out LibraryIonMobilityInfo ionMobilities);

        /// <summary>
        /// Gets all of the spectrum information for a particular (sequence, charge) pair.  This
        /// may include redundant spectra.  The spectrum points themselves are only loaded as it they
        /// requested to give this function acceptable performance.
        /// </summary>
        /// <param name="key">The sequence, charge pair requested</param>
        /// <param name="labelType">An <see cref="IsotopeLabelType"/> for which to get spectra</param>
        /// <param name="redundancy">Level of redundancy requested in returned values</param>
        /// <returns>An enumeration of <see cref="SpectrumInfo"/></returns>
        public abstract IEnumerable<SpectrumInfo> GetSpectra(LibKey key,
            IsotopeLabelType labelType, LibraryRedundancy redundancy);

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

        public IEnumerable<IRetentionTimeProvider> RetentionTimeProvidersIrt
        {
            get
            {
                LibraryRetentionTimes irts;
                if (TryGetIrts(out irts))
                    yield return irts;
            }
        }

        public IEnumerable<IRetentionTimeProvider> RetentionTimeProviders
        {
            get
            {
                var fileCount = FileCount;
                if (!fileCount.HasValue)
                    yield break;

                for (var i = 0; i < fileCount.Value; i++)
                {
                    LibraryRetentionTimes retentionTimes;
                    if (TryGetRetentionTimes(i, out retentionTimes))
                        yield return retentionTimes;
                }
            }
        }
        
        #region File reading utility functions

        protected internal static int GetInt32(byte[] bytes, int index, int offset = 0)
        {
            int ibyte = offset + index * 4;
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
                throw new InvalidDataException(Resources.Library_ReadComplete_Data_truncation_in_library_header_File_may_be_corrupted);
        }

        protected static void SafeReadComplete(Stream stream, ref byte[] buffer, int size)
        {
            if (size > buffer.Length)
                buffer = new byte[size];
            if (stream.Read(buffer, 0, size) != size)
                throw new InvalidDataException(Resources.Library_ReadComplete_Data_truncation_in_library_header_File_may_be_corrupted);
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

        public override string IsNotLoadedExplained
        {
            get { return (_libraryEntries != null) ? null : "no library entries"; } // Not L10N
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
                    throw new IOException(string.Format(Resources.CachedLibrary_LoadSpectrum_Library_entry_not_found__0__, spectrumKey));

                return new SpectrumPeaksInfo(spectrumPeaks);
        }

        protected abstract SpectrumPeaksInfo.MI[] ReadSpectrum(TInfo info);

        public override LibraryChromGroup LoadChromatogramData(object spectrumKey)
        {
            return ReadChromatogram(_libraryEntries[(int) spectrumKey]);
        }

        protected virtual LibraryChromGroup ReadChromatogram(TInfo info)
        {
            return null;
        }

        public override bool TryGetRetentionTimes(LibKey key, MsDataFileUri filePath, out double[] retentionTimes)
        {
            // By default, no retention time information is available
            retentionTimes = null;
            return false;
        }

        public override bool TryGetRetentionTimes(MsDataFileUri filePath, out LibraryRetentionTimes retentionTimes)
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

        public override bool TryGetIrts(out LibraryRetentionTimes retentionTimes)
        {
            // By default, no iRT information is available
            retentionTimes = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(LibKey key, MsDataFileUri filePath, out IonMobilityAndCCS[] ionMobilities)
        {
            // By default, no ion mobility information is available
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities)
        {
            // By default, no ion mobility information is available
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(int fileIndex, out LibraryIonMobilityInfo ionMobilities)
        {
            // By default, no ion mobility information is available
            ionMobilities = null;
            return false;
        }

        public override IEnumerable<SpectrumInfo> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            // This base class only handles best match spectra
            if (redundancy == LibraryRedundancy.best)
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

        protected IEnumerable<TInfo> LibraryEntriesWithSequences(IEnumerable<Target> peptideSequences)
        {
            foreach (var sequence in peptideSequences)
            {
                var libKey = new LibKey(sequence, Adduct.EMPTY);
                int iFirstEntry = CollectionUtil.BinarySearch(_libraryEntries, item => item.Key.CompareSequence(libKey), true);
                if (iFirstEntry < 0)
                {
                    continue;
                }
                for (int index = iFirstEntry; index < _libraryEntries.Length; index++)
                {
                    var item = _libraryEntries[index];
                    if (0 != libKey.CompareSequence(item.Key))
                    {
                        break;
                    }
                    yield return _libraryEntries[index];
                }
            }
        }

        // ReSharper disable PossibleMultipleEnumeration
        protected int FindFileInList(MsDataFileUri sourceFile, IEnumerable<string> fileNames)
        {
            string sourceFileToString = sourceFile.ToString();
            int iFile = 0;
            foreach (var fileName in fileNames)
            {
                if (fileName.Equals(sourceFileToString))
                {
                    return iFile;
                }
                iFile++;
            }
            string baseName = sourceFile.GetFileNameWithoutExtension();
            iFile = 0;
            foreach (var fileName in fileNames)
            {
                try
                {
                    if (MeasuredResults.IsBaseNameMatch(baseName, Path.GetFileNameWithoutExtension(fileName)))
                    {
                        return iFile;
                    }
                }
                catch (Exception)
                {
                    // Ignore: Invalid filename
                }
                iFile++;
            }
            return -1;
        }
        // ReSharper restore PossibleMultipleEnumeration
    }

    public sealed class LibraryRetentionTimes : IRetentionTimeProvider
    {
        private readonly IDictionary<Target, Tuple<TimeSource, double[]>> _dictPeptideRetentionTimes;

        public LibraryRetentionTimes(string path, IDictionary<Target, Tuple<TimeSource, double[]>> dictPeptideRetentionTimes)
        {
            Name = path;
            _dictPeptideRetentionTimes = dictPeptideRetentionTimes;
            if (_dictPeptideRetentionTimes.Count == 0)
            {
                MinRt = MaxRt = 0;
            }
            else
            {
                MinRt = _dictPeptideRetentionTimes.SelectMany(p => p.Value.Item2).Min();
                MaxRt = _dictPeptideRetentionTimes.SelectMany(p => p.Value.Item2).Max();
            }
            var listStdev = new List<double>();
            foreach (Tuple<TimeSource, double[]> times in _dictPeptideRetentionTimes.Values)
            {
                if (times.Item2.Length < 2)
                    continue;
                var statTimes = new Statistics(times.Item2);
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
        public double[] GetRetentionTimes(Target sequence)
        {
            Tuple<TimeSource, double[]> retentionTimes;
            if (_dictPeptideRetentionTimes.TryGetValue(sequence, out retentionTimes))
                return retentionTimes.Item2;
            return new double[0];
        }

        /// <summary>
        /// Return the average retention time for spectra that were identified to a
        /// specific modified peptide sequence, with filtering applied in an attempt
        /// to avoid peptides eluting a second time near the end of the gradient.
        /// </summary>
        public double? GetRetentionTime(Target sequence)
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
            var statTimes = new Statistics(retentionTimes);
            return statTimes.Median();
        }

        public TimeSource? GetTimeSource(Target sequence)
        {
            Tuple<TimeSource, double[]> value;
            if (_dictPeptideRetentionTimes.TryGetValue(sequence, out value))
            {
                return value.Item1;
            }
            return null;
        }

        public IEnumerable<MeasuredRetentionTime> PeptideRetentionTimes
        {
            get
            {
                return from sequence in _dictPeptideRetentionTimes.Keys
                       let time = GetRetentionTime(sequence)
                       where time.HasValue
                       select new MeasuredRetentionTime(sequence, time.Value, true);
            }
        }

        public IDictionary<Target, double> GetFirstRetentionTimes()
        {
            var dict = new Dictionary<Target, double>();
            foreach (var entry in _dictPeptideRetentionTimes)
            {
                if (entry.Value.Item2.Length == 0)
                {
                    continue;
                }
                dict.Add(entry.Key, entry.Value.Item2.Min());
            }
            return dict;
        }
    }

    public sealed class LibraryIonMobilityInfo : IIonMobilityInfoProvider
    {
        private readonly IDictionary<LibKey, IonMobilityAndCCS[]> _dictChargedPeptideDriftTimeInfos;

        public LibraryIonMobilityInfo(string path, IDictionary<LibKey, IonMobilityAndCCS[]> dictChargedPeptideDriftTimeInfos)
        {
            Name = path;
            _dictChargedPeptideDriftTimeInfos = dictChargedPeptideDriftTimeInfos;
        }

        public string Name { get; private set; }

        /// <summary>
        /// Return the median measured CCS for spectra that were identified with a
        /// specific modified peptide sequence and charge state.
        /// </summary>
        public double? GetLibraryMeasuredCollisionalCrossSection(LibKey chargedPeptide)
        {
            IonMobilityAndCCS[] ionMobilities;
            if ((!_dictChargedPeptideDriftTimeInfos.TryGetValue(chargedPeptide, out ionMobilities)) || (ionMobilities == null))
                return null;
            double? ccs = null;
            var ccsValues = Array.FindAll(ionMobilities, im => im.HasCollisionalCrossSection);
            if (ccsValues.Any())
            {
                ccs = new Statistics(ccsValues.Select(im => im.CollisionalCrossSectionSqA.Value)).Median();
            }
            return ccs;
        }

        /// <summary>
        /// Return the median measured ion mobility for spectra that were identified with a
        /// specific modified peptide sequence and charge state.  Prefer to use median CCS
        /// when possible, and calculate IM from that. If only IM values are available, convert
        /// to CCS if possible.
        /// </summary>
        public IonMobilityAndCCS GetLibraryMeasuredIonMobilityAndHighEnergyOffset(LibKey chargedPeptide, double mz, IIonMobilityFunctionsProvider ionMobilityFunctionsProvider)
        {
            IonMobilityAndCCS[] ionMobilities;
            if ((!_dictChargedPeptideDriftTimeInfos.TryGetValue(chargedPeptide, out ionMobilities)) || (ionMobilities == null))
                return IonMobilityAndCCS.EMPTY;
            IonMobilityValue ionMobility = IonMobilityValue.EMPTY;
            double? ccs = null;
            var ionMobilityInfos = ionMobilityFunctionsProvider != null ? Array.FindAll(ionMobilities, im => im.HasCollisionalCrossSection) : null;
            if (ionMobilityInfos != null && ionMobilityInfos.Any() && ionMobilityFunctionsProvider.ProvidesCollisionalCrossSectionConverter)
            {
                // Use median CCS to calculate an ion mobility value
                ccs = new Statistics(ionMobilityInfos.Select(im => im.CollisionalCrossSectionSqA.Value)).Median(); // Median is more tolerant of errors than Average
                ionMobility = IonMobilityValue.GetIonMobilityValue(ionMobilityFunctionsProvider.IonMobilityFromCCS(ccs.Value, mz, chargedPeptide.Charge).Mobility, 
                    ionMobilityFunctionsProvider.IonMobilityUnits);
            }
            else
            {
                // Use median ion mobility, convert to CCS if available
                ionMobilityInfos = Array.FindAll(ionMobilities, dt => dt.HasIonMobilityValue);
                if (ionMobilityInfos.Any())
                {
                    var units = ionMobilityInfos.First().IonMobility.Units;
                    var medianValue = new Statistics(ionMobilityInfos.Select(im => im.IonMobility.Mobility.Value)).Median(); // Median is more tolerant of errors than Average
                    ionMobility = IonMobilityValue.GetIonMobilityValue(medianValue, units);
                    if (ionMobilityFunctionsProvider != null && ionMobilityFunctionsProvider.ProvidesCollisionalCrossSectionConverter)
                    {
                        ccs = ionMobilityFunctionsProvider.CCSFromIonMobility(ionMobility, mz, chargedPeptide.Charge);
                    }
                }
            }
            if (!ionMobility.HasValue)
                return IonMobilityAndCCS.EMPTY;
            var highEnergyDriftTimeOffsetMsec = new Statistics(ionMobilityInfos.Select(im => im.HighEnergyIonMobilityValueOffset)).Median(); // Median is more tolerant of errors than Average
            return IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, ccs, highEnergyDriftTimeOffsetMsec);
       }

        public IDictionary<LibKey, IonMobilityAndCCS[]> GetIonMobilityDict()
        {
            return _dictChargedPeptideDriftTimeInfos;
        }
    }

    public abstract class LibrarySpec : XmlNamedElement
    {
        public static readonly PeptideRankId PEP_RANK_COPIES =
            new PeptideRankId("Spectrum count", Resources.LibrarySpec_PEP_RANK_COPIES_Spectrum_count); // Not L10N
        public static readonly PeptideRankId PEP_RANK_TOTAL_INTENSITY =
            new PeptideRankId("Total intensity", Resources.LibrarySpec_PEP_RANK_TOTAL_INTENSITY_Total_intensity); // Not L10N
        public static readonly PeptideRankId PEP_RANK_PICKED_INTENSITY =
            new PeptideRankId("Picked intensity", Resources.LibrarySpec_PEP_RANK_PICKED_INTENSITY_Picked_intensity); // Not L10N

        public static LibrarySpec CreateFromPath(string name, string path)
        {
            string ext = Path.GetExtension(path);
            if (Equals(ext, BiblioSpecLiteSpec.EXT))
                return new BiblioSpecLiteSpec(name, path);
            else if (Equals(ext, BiblioSpecLibSpec.EXT))
                return new BiblioSpecLibSpec(name, path);
            else if (Equals(ext, ChromatogramLibrarySpec.EXT))
                return new ChromatogramLibrarySpec(name, path);
            else if (Equals(ext, XHunterLibSpec.EXT))
                return new XHunterLibSpec(name, path);
            else if (Equals(ext, NistLibSpec.EXT))
                return new NistLibSpec(name, path);
            else if (Equals(ext, SpectrastSpec.EXT))
                return new SpectrastSpec(name, path);
            else if (Equals(ext, MidasLibSpec.EXT))
                return new MidasLibSpec(name, path);
            else if (Equals(ext, EncyclopeDiaSpec.EXT))
                return new EncyclopeDiaSpec(name, path);
            return null;
        }

        protected LibrarySpec(string name, string path)
            : base(name)
        {
            FilePath = path;
        }

        public string FilePath { get; private set; }

        /// <summary>
        /// Returns the filter string to be used for finding a library of this type.
        /// </summary>
        public abstract string Filter { get; }

        /// <summary>
        /// True if this library spec was created in order to open the current document
        /// only, and should not be stored long term in the global settings.
        /// </summary>
        public bool IsDocumentLocal { get; private set; }

        /// <summary>
        /// True if this the document-specific library spec, and should not be stored 
        /// in the global settings.
        /// </summary>
        public bool IsDocumentLibrary { get; private set; }

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

        public LibrarySpec ChangeDocumentLibrary(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.IsDocumentLibrary = prop).ChangeDocumentLocal(prop);
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
                throw new InvalidOperationException(Resources.LibrarySpec_WriteXml_Document_local_library_specs_cannot_be_persisted_to_XML);

            if (IsDocumentLibrary)
                throw new InvalidOperationException(Resources.LibrarySpec_WriteXml_Document_library_specs_cannot_be_persisted_to_XML_);

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
                other.IsDocumentLocal.Equals(IsDocumentLocal) &&
                other.IsDocumentLibrary.Equals(IsDocumentLibrary);
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
                result = (result*397) ^ IsDocumentLibrary.GetHashCode();
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
        public static readonly PeptideRankId PEPTIDE_RANK_NONE = new PeptideRankId(string.Empty, string.Empty);

        public PeptideRankId(string value, string label)
        {
            Value = value;
            Label = label;
        }

        /// <summary>
        /// Display text for user interface.
        /// </summary>
        public string Label { get; private set; }

        /// <summary>
        /// Name for us in XML.
        /// </summary>
        public string Value { get; private set; }

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
            private bool _notQuantitative;
            public double Mz { get; set; }
            public float Intensity { get; set; }
            public bool Quantitative {
                get { return !_notQuantitative; }
                set { _notQuantitative = !value; }
            }
        }
    }

    public class SmallMoleculeLibraryAttributes : IEquatable<SmallMoleculeLibraryAttributes>
    {
        public static SmallMoleculeLibraryAttributes EMPTY = new SmallMoleculeLibraryAttributes(null, null, null, null);
        public static int nItems = 4;

        public bool IsEmpty
        {
            get
            {
                return ReferenceEquals(this, EMPTY);
            }
        }

        // Helper for library caches
        public static SmallMoleculeLibraryAttributes FromBytes(byte[] buf, int offset)
        {
            var itemLengths = new int[nItems];
            var itemStarts = new int[nItems];
            for (var i = 0; i < nItems; i++)
            {
                // read item length
                itemLengths[i] = Library.GetInt32(buf, i, offset);
                itemStarts[i] = i == 0 ? offset + nItems * sizeof(int) : itemStarts[i - 1] + itemLengths[i - 1];
            }
            return Create(
                Encoding.UTF8.GetString(buf, itemStarts[0], itemLengths[0]),
                Encoding.UTF8.GetString(buf, itemStarts[1], itemLengths[1]),
                Encoding.UTF8.GetString(buf, itemStarts[2], itemLengths[2]),
                Encoding.UTF8.GetString(buf, itemStarts[3], itemLengths[3]));
        }
        public static byte[] ToBytes(SmallMoleculeLibraryAttributes attributes)
        {
            attributes = attributes ?? EMPTY;
            // Encode as <length><item><length><item>etc
            var items = new List<byte[]>
            {
                Encoding.UTF8.GetBytes(attributes.MoleculeName ?? string.Empty),
                Encoding.UTF8.GetBytes(attributes.ChemicalFormula ?? string.Empty),
                Encoding.UTF8.GetBytes(attributes.InChiKey ?? string.Empty),
                Encoding.UTF8.GetBytes(attributes.OtherKeys ?? string.Empty)
            };
            Assume.IsTrue(Equals(nItems,items.Count));
            var results = new byte[items.Sum(item => item.Length + sizeof(int))];
            var index = 0;
            foreach (var item in items)
            {
                Array.Copy(BitConverter.GetBytes(item.Length), 0, results, index, sizeof(int));
                index += sizeof(int);
            }
            foreach (var item in items)
            {
                Array.Copy(item, 0, results, index, item.Length);
                index += item.Length;
            }
            return results;
        }

        public static SmallMoleculeLibraryAttributes Create(string moleculeName, string chemicalFormula,
            string inChiKey, string otherKeys)
        {
            if (string.IsNullOrEmpty(moleculeName) && string.IsNullOrEmpty(chemicalFormula) &&
                string.IsNullOrEmpty(inChiKey) && string.IsNullOrEmpty(otherKeys))
            {
                return EMPTY;
            }
            return new SmallMoleculeLibraryAttributes(moleculeName, chemicalFormula, inChiKey, otherKeys);
        }

        private SmallMoleculeLibraryAttributes(string moleculeName, string chemicalFormula, string inChiKey, string otherKeys)
        {
            MoleculeName = moleculeName;
            ChemicalFormula = chemicalFormula;
            InChiKey = inChiKey;
            OtherKeys = otherKeys;
        }

        public string MoleculeName { get; private set; }
        public string ChemicalFormula { get; private set; }
        public string InChiKey { get; private set; }
        public string OtherKeys { get; private set; }

        public string GetPreferredKey()
        {
            return CreateMoleculeID().PrimaryAccessionValue ?? MoleculeName;
        }

        public string Validate()
        {
            return string.IsNullOrEmpty(ChemicalFormula) ||
                    (string.IsNullOrEmpty(MoleculeName) && string.IsNullOrEmpty(InChiKey) && string.IsNullOrEmpty(OtherKeys))
                ? Resources.SmallMoleculeLibraryAttributes_Validate_A_small_molecule_is_defined_by_a_chemical_formula_and_at_least_one_of_Name__InChiKey__or_other_keys__HMDB_etc_
                : null;
        }

        public MoleculeAccessionNumbers CreateMoleculeID()
        {
            return new MoleculeAccessionNumbers(OtherKeys, InChiKey);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SmallMoleculeLibraryAttributes)obj);
        }
        public bool Equals(SmallMoleculeLibraryAttributes other)
        {
            if (other == null)
                return false;
            return Equals(MoleculeName, other.MoleculeName) &&
                   Equals(ChemicalFormula, other.ChemicalFormula) &&
                   Equals(InChiKey, other.InChiKey) &&
                   Equals(OtherKeys, other.OtherKeys);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (MoleculeName != null ? MoleculeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ChemicalFormula != null ? ChemicalFormula.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InChiKey != null ? InChiKey.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OtherKeys != null ? OtherKeys.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            return GetPreferredKey();
        }

    }

    /// <summary>
    /// Transfer format for library spectra
    /// </summary>
    public class SpectrumMzInfo
    {
        public string SourceFile { get; set; }
        public LibKey Key { get; set; }
        public SmallMoleculeLibraryAttributes SmallMoleculeLibraryAttributes { get { return Key.SmallMoleculeLibraryAttributes; } }
        public IonMobilityAndCCS IonMobility { get; set; }
        public double PrecursorMz { get; set; }
        public double? RetentionTime { get; set; }
        public IsotopeLabelType Label { get; set; }
        public SpectrumPeaksInfo SpectrumPeaks { get; set; }
        public List<IonMobilityAndRT> RetentionTimes { get; set; } // (File, RT, IM, IsBest)

        public const double PRECURSOR_MZ_TOL = 0.001;

        public class IonMobilityAndRT
        {
            public string SourceFile { get; private set; }
            public IonMobilityAndCCS IonMobility { get; private set; }
            public double? RetentionTime { get; private set; }
            public bool IsBest { get; private set; }

            public IonMobilityAndRT(string sourceFile, IonMobilityAndCCS ionMobility, double? retentionTime,
                bool isBest)
            {
                SourceFile = sourceFile;
                IonMobility = ionMobility;
                RetentionTime = retentionTime;
                IsBest = isBest;
            }
        }

        /// <summary>
        /// Combine two spectra, for when transition list import has alternating light-heavy transitions,
        /// that need to be re-united with their groups at the end.
        /// </summary>
        public SpectrumMzInfo CombineSpectrumInfo(SpectrumMzInfo infoOther, out List<TransitionImportErrorInfo> spectrumErrors)
        {
            spectrumErrors = new List<TransitionImportErrorInfo>();
            if (infoOther == null)
                return this;
            if ((PrecursorMz - infoOther.PrecursorMz) > PRECURSOR_MZ_TOL || !Equals(Label, infoOther.Label) ||
                !Equals(Key, infoOther.Key))
            {
                for (int i = 0; i < infoOther.SpectrumPeaks.Peaks.Length; ++i)
                {
                    spectrumErrors.Add(new TransitionImportErrorInfo(string.Format(Resources.SpectrumMzInfo_CombineSpectrumInfo_Two_incompatible_transition_groups_for_sequence__0___precursor_m_z__1__, 
                                                                                   Key.Target,
                            Key.Target,
                            PrecursorMz),
                                                                     null, null, null));
                }
                return this;
            }
            var peaks = SpectrumPeaks.Peaks;
            var peaksOther = infoOther.SpectrumPeaks.Peaks;
            var newPeaks = peaks.Concat(peaksOther).ToArray();
            return new SpectrumMzInfo
            {
                SourceFile = infoOther.SourceFile,
                Key = infoOther.Key,
                Label = infoOther.Label,
                PrecursorMz = infoOther.PrecursorMz,
                IonMobility = infoOther.IonMobility,
                RetentionTime = infoOther.RetentionTime,
                SpectrumPeaks = new SpectrumPeaksInfo(newPeaks)
            };
        }

        public static List<SpectrumMzInfo> RemoveDuplicateSpectra(List<SpectrumMzInfo> librarySpectra)
        {
            var uniqueSpectra = new List<SpectrumMzInfo>();
            var spectraGroups = librarySpectra.GroupBy(spectrum => spectrum.Key);
            foreach (var spectraGroup in spectraGroups)
            {
                var spectraGroupList = spectraGroup.ToList();
                spectraGroupList.Sort(CompareSpectrumMzLabels);
                uniqueSpectra.Add(spectraGroupList[0]);
            }
            return uniqueSpectra;
        }

        /// <summary>
        /// Order by isotope label type (e.g. light, heavy, ...)
        /// </summary>
        public static int CompareSpectrumMzLabels(SpectrumMzInfo info1, SpectrumMzInfo info2)
        {
            return info1.Label.CompareTo(info2.Label);
        }

        public static List<SpectrumMzInfo> GetInfoFromLibrary(Library library)
        {
            var spectrumMzInfos = new List<SpectrumMzInfo>();
            foreach (var key in library.Keys)
            {
                var info = library.GetSpectra(key, null, LibraryRedundancy.best).FirstOrDefault();
                if (info == null)
                {
                    throw new IOException(string.Format(Resources.SpectrumMzInfo_GetInfoFromLibrary_Library_spectrum_for_sequence__0__is_missing_, key.Target));
                }
                spectrumMzInfos.Add(new SpectrumMzInfo
                {
                    SourceFile = info.FileName,
                    Key = key,
                    SpectrumPeaks = info.SpectrumPeaksInfo
                });
            }
            return spectrumMzInfos;
        }

        public static List<SpectrumMzInfo> MergeWithOverwrite(List<SpectrumMzInfo> originalSpectra, List<SpectrumMzInfo> overwriteSpectra)
        {
            var finalSpectra = new List<SpectrumMzInfo>(overwriteSpectra);
            var dictOriginalSpectra = originalSpectra.ToDictionary(spectrum => spectrum.Key);
            var dictOverwriteSpectra = overwriteSpectra.ToDictionary(spectrum => spectrum.Key);
            finalSpectra.AddRange(from spectrum in dictOriginalSpectra where !dictOverwriteSpectra.ContainsKey(spectrum.Key) select spectrum.Value);
            return finalSpectra;
        }
    }


    /// <summary>
    /// Information required for spectrum display in a list.
    /// </summary>
    public class SpectrumInfo
    {
        public SpectrumInfo(Library library, IsotopeLabelType labelType, object spectrumKey)
            : this(library, labelType, null, null, null, true, spectrumKey)
        {
        }

        public SpectrumInfo(Library library, IsotopeLabelType labelType,
            string filePath, double? retentionTime, IonMobilityAndCCS ionMobilityInfo, bool isBest, object spectrumKey)
        {
            Library = library;
            LabelType = labelType;
            SpectrumKey = spectrumKey;
            FilePath = filePath;
            RetentionTime = retentionTime;
            IonMobilityInfo = ionMobilityInfo ?? IonMobilityAndCCS.EMPTY;
            IsBest = isBest;
        }

        protected Library Library { get; private set; }
        public IsotopeLabelType LabelType { get; private set; }
        public string LibName { get { return Library.Name; } }
        public object SpectrumKey { get; private set; }
        public string FilePath { get; private set; }

        public string FileName
        {
            get
            {
                try
                {
                    return Path.GetFileName(FilePath);
                }
                catch
                {
                    return FilePath;
                }
            }
        }
        public double? RetentionTime { get; set; }
        public IonMobilityAndCCS IonMobilityInfo { get; private set; }
        public bool IsBest { get; private set; }

        public SpectrumHeaderInfo SpectrumHeaderInfo { get; set; }

        public SpectrumPeaksInfo SpectrumPeaksInfo { get { return Library.LoadSpectrum(SpectrumKey); } }
        public LibraryChromGroup LoadChromatogramData() { return Library.LoadChromatogramData(SpectrumKey); }
    }

    public class LibraryChromGroup
    {
        private IList<ChromData> _chromDatas = ImmutableList.Empty<ChromData>();
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public double RetentionTime { get; set; }
        public float[] Times { get; set; }
        public IList<ChromData> ChromDatas { get { return _chromDatas; } set { _chromDatas = ImmutableList.ValueOf(value); } }

        public class ChromData
        {
            public double Mz { get; set; }
            public double Height { get; set; }
            public float[] Intensities { get; set; }
            public int Charge { get; set; }
            public IonType IonType { get; set; }
            public int Ordinal { get; set; }
            public int MassIndex { get; set; }
            // public DriftTimeFilter driftTime { get; set; } TODO(bspratt) IMS in chromatogram libs?

        }
    }

    /// <summary>
    /// Links to spectral library sources
    /// </summary>
    public sealed class LibraryLink
    {
        public static readonly LibraryLink PEPTIDEATLAS = new LibraryLink("PeptideAtlas", "http://www.peptideatlas.org/speclib/"); // Not L10N
        public static readonly LibraryLink NIST = new LibraryLink("NIST", "http://peptide.nist.gov/"); // Not L10N
        public static readonly LibraryLink GPM = new LibraryLink("GPM", "ftp://ftp.thegpm.org/projects/xhunter/libs/"); // Not L10N

        private LibraryLink(string name, string href)
        {
            Name = name;
            Link = href;
        }

        public string Name { get; private set; }

        public string Link { get; private set; }
    }

    public sealed class LibraryFiles
    {
        private IEnumerable<string> _filePaths;

        public IEnumerable<string> FilePaths
        {
            get { return _filePaths ?? (_filePaths = new List<string>()); }
            set { _filePaths = value; }
        }
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
        private IEnumerable<SpectrumSourceFileDetails> _dataFiles;
        
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

        public int SpectrumCount { get; set; }
        public int UniquePeptideCount { get; set; }
        public int TotalPsmCount { get; set; }
        public IEnumerable<SpectrumSourceFileDetails> DataFiles
        { 
            get { return _dataFiles ?? (_dataFiles = new List<SpectrumSourceFileDetails>()); }
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
    /// <para>
    /// Keys are implemented as byte arrays, with the first byte providing a hint as to
    /// key contents: precursor, simple charge and amino acid sequence, or adduct and 
    /// charge and small molecule identifier followed by small molecule metadata.</para>
    /// <para>
    /// That small molecule metadata isn't used in equality checks, which stretches the notion of a "key",
    /// but in practice these keys are used not only to match libraries but also to tell the user 
    /// about the nature of the molecules in the library. As we want to readily find matches to keys with
    /// partial or missing metdata It's not possible to embed the needed metadata in the actual match part 
    /// of the key the way we can with peptides, which are actually nothing but metadata (AA is just code for 
    /// chemical formula etc).
    /// </para>
    /// </summary>
    public struct LibKey
    {
        private const byte PRECURSOR_MAGIC_BYTE = (byte) '~'; // NB was '#'=34, but MAX_PRECURSOR_CHARGE=80

        private const byte ADDUCT_MAGIC_BYTE =
            (byte) '{'; // We'd use '[' if it wasn't already in use for modifications, also this allows charge up to 122

        private const byte ADDUCT_END_MAGIC_BYTE =
            (byte) '}'; // We'd use ']' if it wasn't already in use for modifications

        private byte[] _key;
        private int _sequenceStart;
        private int _sequenceLength;

        public LibKey(string sequence, int charge) : this()
        {
            EncodeSequenceAndCharge(sequence, charge);
        }

        public LibKey(SmallMoleculeLibraryAttributes attributes, Adduct adduct) : this()
        {
            EncodeSmallMoleculeAndAdduct(attributes, adduct);
        }

        public LibKey(string primaryKey, Adduct adduct) : this()
        {
            if (adduct.IsProteomic)
            {
                EncodeSequenceAndCharge(primaryKey, adduct.AdductCharge);
            }
            else
            {
                // It is sufficient to create a key without full small mol metadata, for matching purposes
                var adductBytes = Encoding.UTF8.GetBytes(adduct.AdductFormula);
                var sequenceBytes = Encoding.UTF8.GetBytes(primaryKey);
                _sequenceLength = sequenceBytes.Length;
                var auxInfoBytes = SmallMoleculeLibraryAttributes.ToBytes(SmallMoleculeLibraryAttributes.EMPTY);
                _key = SetSmallMoleculeKeyBytes(adduct.AdductCharge, adductBytes, adductBytes.Length,
                    sequenceBytes, sequenceBytes.Length, auxInfoBytes, auxInfoBytes.Length, out _sequenceStart);
            }
        }

        private static byte[] SetSmallMoleculeKeyBytes(int charge, byte[] adductUTF8, int adductLen,
            byte[] sequenceUTF8, int seqLen,
            byte[] auxInfoUTF8, int auxInfoLen, out int sequenceStart)
        {
            var key = new byte[seqLen + adductLen + auxInfoLen + 2];
            EncodeAdduct(key, 0, adductUTF8, adductLen);
            key[adductLen] = (byte) charge;
            key[adductLen + 1] = (byte) seqLen;
            Array.Copy(sequenceUTF8, 0, key, sequenceStart = adductLen + 2, seqLen);
            Array.Copy(auxInfoUTF8, 0, key, sequenceStart + seqLen, auxInfoLen);
            return key;
        }

        public LibKey(byte[] sequence, int start, int len, int charge)
        {
            _key = new byte[len + 1];
            _key[0] = (byte) charge;
            Array.Copy(sequence, start, _key, _sequenceStart = 1, _sequenceLength = len);
        }

        // For deserializing small molecules from library cache format
        public LibKey(byte[] keybuf, int length)
        {
            _key = new byte[length];
            Array.Copy(keybuf, 0, _key, 0, length);
            _sequenceStart = GetSequenceStart(_key, out _sequenceLength);
        }

        public LibKey(byte[] adductUTF8, int adductLen, byte[] sequenceUTF8, int seqLen, byte[] auxInfoUTF8,
            int auxInfoLen, int charge)
        {
            _key = SetSmallMoleculeKeyBytes(charge, adductUTF8, adductLen, sequenceUTF8, _sequenceLength = seqLen,
                auxInfoUTF8, auxInfoLen, out _sequenceStart);
        }

        public LibKey(double precursorMz,
            double? retentionTime = null) // TODO(bspratt) probably should add ion mobility 
        {
            _key = !retentionTime.HasValue ? new byte[1 + sizeof(double)] : new byte[1 + 2 * sizeof(double)];
            _key[0] = PRECURSOR_MAGIC_BYTE;
            _sequenceStart = 0;
            _sequenceLength = 0;
            Array.Copy(BitConverter.GetBytes(precursorMz), 0, _key, 1, sizeof(double));
            if (retentionTime.HasValue)
                Array.Copy(BitConverter.GetBytes(retentionTime.Value), 0, _key, 1 + sizeof(double), sizeof(double));
        }

        public LibKey(Target target, Adduct adduct)
            : this()
        {
            if (target.IsProteomic)
                EncodeSequenceAndCharge(target.Sequence, adduct.AdductCharge);
            else
                EncodeSmallMoleculeAndAdduct(target.Molecule.GetSmallMoleculeLibraryAttributes(), adduct);
        }

        public LibKey(Target target, int charge) : this(target.Sequence, charge)
        {
        }

        private void EncodeSequenceAndCharge(string seq, int charge)
        {
            // Protonated charges can be represented by an integer
            _key = new byte[seq.Length + 1];
            _key[0] = (byte) charge;
            Encoding.ASCII.GetBytes(seq, 0, _sequenceLength = seq.Length, _key, _sequenceStart = 1);
        }

        private void EncodeSmallMoleculeAndAdduct(SmallMoleculeLibraryAttributes attributes, Adduct adduct)
        {
            // For more exotic adducts, write adduct (ie [M+Na]) followed by charge byte
            // (note we use {M+Na} instead of [M+Na] because [] have a special meaning in peptides)
            // followed by byte indicating the length of the match string (usually InChiKey)
            // followed by the length match string
            // followed by the byte encoded SmallMoleculeLibraryInfo struct
            var adductBytes = Encoding.UTF8.GetBytes(adduct.AdductFormula);

            var sequenceBytes = Encoding.UTF8.GetBytes(attributes.GetPreferredKey() ?? String.Empty);
            _sequenceLength = sequenceBytes.Length;
            var auxInfoBytes = SmallMoleculeLibraryAttributes.ToBytes(attributes);
            _key = SetSmallMoleculeKeyBytes(adduct.AdductCharge, adductBytes, adductBytes.Length, sequenceBytes,
                sequenceBytes.Length,
                auxInfoBytes, auxInfoBytes.Length, out _sequenceStart);
        }

        private LibKey(byte[] key)
        {
            _key = key;
            _sequenceStart = GetSequenceStart(key, out _sequenceLength);
        }

        public static void EncodeAdduct(byte[] buf, int start, byte[] adductBytesUTF8, int adductlen)
        {
            if (adductlen > 0)
            {
                buf[start] = ADDUCT_MAGIC_BYTE;
                if (!ReferenceEquals(buf, adductBytesUTF8))
                    Array.Copy(adductBytesUTF8, 1, buf, start+1, adductlen -2);
                buf[start+adductlen-1] = ADDUCT_END_MAGIC_BYTE;
            }
        }

        public int SequenceStart { get { return _sequenceStart; } }
        public int SequenceLength { get { return _sequenceLength; } }
        public int MatchLength { get { return _sequenceStart + _sequenceLength; } }  // N.B. for equality we ignore any small molecule metadata
        public bool IsProteomicKey { get { return IsPeptideKey(_key); } }
        public bool IsSmallMoleculeKey { get { return HasAdduct(_key); } }
        public bool IsPrecursorKey { get { return _key.Length >= 1 + sizeof(double) && _key[0] == PRECURSOR_MAGIC_BYTE; } }
        public bool HasRetentionTime { get { return IsPrecursorKey && _key.Length >= 1 + 2*sizeof(double); } }

        public string Sequence
        {
            get
            {
                if (IsPrecursorKey)
                    return null;
                if (IsProteomicKey)
                    return Encoding.UTF8.GetString(_key, SequenceStart, SequenceLength);
                return null;
            }
        }
        public Target Target
        {
            get
            {
                if (IsPrecursorKey)
                    return null;
                if (IsProteomicKey)
                    return new Target(Encoding.UTF8.GetString(_key, SequenceStart, SequenceLength));
                return new Target(SmallMoleculeLibraryAttributes);
            }
        }
        public SmallMoleculeLibraryAttributes SmallMoleculeLibraryAttributes { get { return IsSmallMoleculeKey ? SmallMoleculeLibraryAttributes.FromBytes(_key, SequenceStart + SequenceLength) : SmallMoleculeLibraryAttributes.EMPTY; } }
        public int Charge { get { return IsProteomicKey ? (sbyte) _key[0] : (IsPrecursorKey ? 0 : (sbyte)_key[SequenceStart-2]); } }
        public Adduct Adduct 
        {
            get
            {
                if (IsPrecursorKey)
                {
                    return Adduct.EMPTY;
                }
                if (IsProteomicKey)
                {
                    return Adduct.FromChargeProtonated((sbyte) _key[0]);
                }
                // Pick <adduct> and <z> out of a byte array "{<adduct>}<z><seqlen><sequence><smallmolinfo>" leaving out the { and }
                return Adduct.FromString(Encoding.UTF8.GetString(_key, 1, SequenceStart-4), Adduct.ADDUCT_TYPE.non_proteomic, (sbyte)_key[SequenceStart-2]);
            }
        }

        public bool IsModified { get { return !IsPrecursorKey && !IsSmallMoleculeKey && _key.Contains((byte)'['); } } // Not L10N
        public double? PrecursorMz { get { return IsPrecursorKey ? BitConverter.ToDouble(_key, 1) : default(double?); } }
        public double? RetentionTime { get { return HasRetentionTime ? BitConverter.ToDouble(_key, 1 + sizeof(double)) : default(double?); } }

        /// <summary>
        /// Only for use by <see cref="LibSeqKey"/>
        /// </summary>
        public byte[] Key { get { return _key; }}

        public void WriteSequence(Stream outStream)
        {
            outStream.Write(BitConverter.GetBytes(SequenceLength), 0, sizeof(int)); // Write the sequence length
            outStream.Write(_key, SequenceStart, SequenceLength); // Write the sequence (ASCII for peptides, UTF8 for small mol)
        }

        // Compare keys ignoring adduct and small mol details
        public int CompareSequence(LibKey key2)
        {
            byte[] raw1 = _key, raw2 = key2._key;
            var s1 = SequenceStart;
            var s2 = key2.SequenceStart;
            var seqLen1 = SequenceLength;
            var seqLen2 = key2.SequenceLength;
            var len = Math.Min(seqLen1, seqLen2);
            for (var i = 0; i < len; i++)
            {
                byte b1 = raw1[s1++], b2 = raw2[s2++];
                if (b1 != b2)
                    return b1 - b2;
            }
            return seqLen1 - seqLen2;
        }

        // Compare keys ignoring small mol details (so matching InChiKeys is enough, even if one has say HMDB as well)
        public int Compare(LibKey key2)
        {
            int result = CompareSequence(key2);
            if (result != 0)
            {
                return result;
            }
            result = Charge.CompareTo(key2.Charge);
            if (result != 0)
            {
                return result;
            }
            return IsSmallMoleculeKey || key2.IsSmallMoleculeKey ? Adduct.CompareTo(key2.Adduct) : 0;
        }

        public IEnumerable<byte> SequenceLookupBytes
        {
            get
            {
                if (IsPrecursorKey)
                    yield break;

                if (IsSmallMoleculeKey)
                {
                    // Get human-friendly name if any
                    var name = SmallMoleculeLibraryAttributes.MoleculeName;
                    if (!string.IsNullOrEmpty(name))
                    {
                        for (var i = 0; i < name.Length; i++)
                        {
                            yield return (byte)name[i];
                        }
                        yield break;
                    }
                    // Get the preferred key (usually InChiKey)
                    var j = 0;
                    for (var i = SequenceStart; j++ < SequenceLength; i++)
                    {
                        yield return _key[i];
                    }
                    yield break;
                }

                // Filter out any non-AA characters
                for (int i = SequenceStart; i < _key.Length; i++)
                {
                    var aa = (char)_key[i]; // ASCII encoded, so this is safe
                    if (AminoAcid.IsExAA(aa))
                        yield return (byte)aa;
                }
            }
        }

        public int ModificationCount
        {
            get
            {
                if (!IsPeptideKey(_key))
                {
                    return 0;
                }
                int count = 0;
                for (int i = SequenceStart; i < _key.Length; i++)
                {
                    if (_key[i] == '[') // Not L10N
                        count++;
                }
                return count;
            }
        }   

        #region object overrides

        public bool Equals(LibKey obj)
        {
            var len = obj.MatchLength; // N.B. for equality we ignore any small molecule metadata

            if (len != MatchLength)
                return false;

            for (int i = 0; i < len; i++)
                if (obj._key[i] != _key[i])
                    return false;

            return true;
        }

        public static bool IsPeptideKey(byte[] bytes)
        {
            return bytes[0] != PRECURSOR_MAGIC_BYTE && bytes[0] != ADDUCT_MAGIC_BYTE;
        }

        public static bool HasAdduct(byte[] bytes)
        {
            return bytes[0] == ADDUCT_MAGIC_BYTE;
        }

        // Locate the position of the molecule ID or AA sequence
        public static int GetSequenceStart(byte[] bytes, out int sequenceLength)
        {
            if (bytes.Length > 1 && bytes[0] == ADDUCT_MAGIC_BYTE)
            {
                var adductEndIndex = bytes.IndexOf(b => b==ADDUCT_END_MAGIC_BYTE);
                sequenceLength = bytes[adductEndIndex + 2];
                return adductEndIndex + 3; // After adduct is byte charge then byte sequencelen
            }
            sequenceLength = bytes.Length - 1;
            return 1;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(LibKey)) return false;
            return Equals((LibKey)obj);  // N.B. for equality we ignore any small molecule metadata
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var result = 0;
                var len = MatchLength; // N.B. for equality we ignore any small molecule metadata
                for (var b=0; b < len;)
                    result = (result*31) ^ _key[b++];
                return result;
            }
        }

        public override string ToString()
        {
            if (IsProteomicKey)
                return Target + Transition.GetChargeIndicator(Adduct);
            if (IsSmallMoleculeKey)
                return SmallMoleculeLibraryAttributes.ToString() + Adduct;
            var precursor = PrecursorMz.GetValueOrDefault().ToString("0.000", CultureInfo.CurrentCulture); // Not L10N
            if (!HasRetentionTime)
                return precursor;
            var rt = RetentionTime.GetValueOrDefault().ToString("0.00", CultureInfo.CurrentCulture); // Not L10N
            return string.Format("{0} ({1})", precursor, rt); // Not L10N
        }

        #endregion

        public void Write(Stream outStream)
        {
            PrimitiveArrays.WriteOneValue(outStream, _key.Length);
            outStream.Write(_key, 0, _key.Length);
        }

        public static LibKey Read(Stream inStream)
        {
            int length = PrimitiveArrays.ReadOneValue<int>(inStream);
            var key = new byte[length];
            inStream.Read(key, 0, key.Length);
            return new LibKey(key);
        }
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
        private IEnumerable<char> AminoAcids
        {
            get
            {
                if (_seq != null)
                {
                    foreach (char aa in _seq)
                        yield return aa;
                }
                else if (!LibKey.IsPeptideKey(_libKeyBytes))
                {
                    // This is UTF8 encoded
                    int sequenceLength;
                    var start = LibKey.GetSequenceStart(_libKeyBytes, out sequenceLength);
                    var moleculeIdentifier = Encoding.UTF8.GetString(_libKeyBytes, start, sequenceLength);
                    foreach (var t in moleculeIdentifier)
                    {
                        yield return t;
                    }
                }
                else
                {
                    for (int i = 1; i < _libKeyBytes.Length; i++)
                    {
                        char aa = (char)_libKeyBytes[i]; // ASCII encoded, so this is safe
                        if ('A' <= aa && aa <= 'Z') // Not L10N: Amino Acids
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
            using (var enumObjAa = obj.AminoAcids.GetEnumerator())
            {
                foreach (char aa in AminoAcids)
                {
                    enumObjAa.MoveNext();
                    if (aa != enumObjAa.Current)
                        return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
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
            : this(Encoding.UTF8.GetString(sequence, start, len), charge)
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
            if (obj == null) return false;
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
            return _sequence + Transition.GetChargeIndicator(Adduct.FromChargeProtonated(_charge));
        }

        #endregion
    }

    public class SpectrumSourceFileDetails
    {
        public SpectrumSourceFileDetails(String filePath)
        {
            FilePath = filePath;
            CutoffScores = new Dictionary<string, double?>();
            BestSpectrum = 0;
            MatchedSpectrum = 0;
        }

        public string FilePath { get; private set; }
        public Dictionary<string, double?> CutoffScores { get; private set; }
        public int BestSpectrum { get; set; }
        public int MatchedSpectrum { get; set; }
    }
}