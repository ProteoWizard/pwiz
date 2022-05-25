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
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.AuditLog;
using pwiz.Skyline.Model.Crosslinking;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Model.Irt;
using pwiz.Skyline.Model.Lib.ChromLib;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Prosit;
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
            return !ReferenceEquals(document.Settings.PeptideSettings.Libraries, previous.Settings.PeptideSettings.Libraries) ||
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
                    return TextUtil.LineSeparate(@"MIDAS library is missing files:",
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
                            libraries = libraries
                                .ChangeRankId(null)
                                .ChangePick(PeptidePick.filter);
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
                        catch (OperationCanceledException)
                        {
                            docNew = docCurrent;    // Just continue
                        }
                        catch (Exception x)
                        {
                            if (ExceptionUtil.IsProgrammingDefect(x))
                            {
                                throw;
                            }
                            settingsChangeMonitor.ChangeProgress(s => s.ChangeErrorException(x));
                            break;
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
                {
                    if (Equals(spec, library.CreateSpec(library.FileNameHint)))
                    {
                        return library;
                    }
                    else
                    {
                        _loadedLibraries.Remove(spec.Name);
                    }
                }
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

        public void UnloadChangedLibraries(IEnumerable<LibrarySpec> specs)
        {
            lock (_loadedLibraries)
            {
                foreach (var spec in specs)
                {
                    Library library;
                    if (_loadedLibraries.TryGetValue(spec.Name, out library))
                    {
                        var specCompare = library.CreateSpec(library.FileNameHint);
                        if (!Equals(spec, specCompare))
                        {
                            _loadedLibraries.Remove(spec.Name);
                        }
                    }
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
            public string BuildCommandArgs { get; set; }
            public string BuildOutput { get; set; }
            public string ExtraMessage { get; set; }
            public IrtStandard IrtStandard { get; set; }
        }

        public void BuildLibrary(IDocumentContainer container, ILibraryBuilder builder, Action<BuildState, bool> callback)
        {
            var monitor = new LibraryBuildMonitor(this, container);
            var buildState = new BuildState(builder.LibrarySpec, BuildLibraryBackground);
            ActionUtil.RunAsync(() => callback(buildState, BuildLibraryBackground(container, builder, monitor, buildState)), @"Library Build");
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
                var iRTCapableBuilder = builder as IiRTCapableLibraryBuilder;
                if (null != iRTCapableBuilder)
                {
                    buildState.BuildCommandArgs = iRTCapableBuilder.BuildCommandArgs;
                    buildState.BuildOutput = iRTCapableBuilder.BuildOutput;
                    if (!string.IsNullOrEmpty(iRTCapableBuilder.AmbiguousMatchesMessage))
                    {
                        buildState.ExtraMessage = iRTCapableBuilder.AmbiguousMatchesMessage;
                    }
                    if (iRTCapableBuilder.IrtStandard != null &&
                        !iRTCapableBuilder.IrtStandard.IsEmpty)
                    {
                        buildState.IrtStandard = iRTCapableBuilder.IrtStandard;
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
            UseExplicitPeakBounds = spec.UseExplicitPeakBounds;
        }

        /// <summary>
        /// Original file name used to create this library, for use in finding
        /// the library, if its identifying name is not present in the
        /// <see cref="SpectralLibraryList"/>
        /// </summary>
        public string FileNameHint { get; private set; }

        public bool UseExplicitPeakBounds { get; private set; }

        /// <summary>
        /// Creates the appropriate library spec for this library, given a path
        /// to the library.
        /// </summary>
        /// <param name="path">Path to the library file on disk</param>
        /// <returns>A new <see cref="LibrarySpec"/></returns>
        public virtual LibrarySpec CreateSpec(string path)
        {
            return CreateSpec().ChangeFilePath(path)
                .ChangeUseExplicitPeakBounds(UseExplicitPeakBounds);
        }
        
        protected abstract LibrarySpec CreateSpec();

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
        /// <param name="target">An unmodified sequence</param>
        /// <returns>True if the library contains any spectra for this peptide regardless of modification or charge</returns>
        public abstract bool ContainsAny(Target target);

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
        public virtual ExplicitPeakBounds GetExplicitPeakBounds(MsDataFileUri filePath, IEnumerable<Target> peptideSequences)
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
        /// Attempts to get ion mobility information for selected
        /// (sequence, charge) pairs identified from a specific file.
        /// </summary>
        /// <param name="targetIons">A list of sequence, charge pairs</param>
        /// <param name="filePath">A file for which the ion mobility information is requested</param>
        /// <param name="ionMobilities">A list of ion mobility info, if successful</param>
        /// <returns>True if ion mobility information was retrieved successfully</returns>
        public abstract bool TryGetIonMobilityInfos(LibKey[] targetIons, MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities);

        /// <summary>
        /// Attempts to get ion mobility information for all of the
        /// (sequence, charge) pairs identified from a specific file by index.
        /// </summary>
        /// <param name="targetIons">A list of sequence, charge pairs</param>
        /// <param name="fileIndex">Index of a file for which the ion mobility information is requested</param>
        /// <param name="ionMobilities">A list of ion mobility info, if successful</param>
        /// <returns>True if ion mobility information was retrieved successfully</returns>
        public abstract bool TryGetIonMobilityInfos(LibKey[] targetIons, int fileIndex, out LibraryIonMobilityInfo ionMobilities);

        /// <summary>
        /// Attempts to get ion mobility information for all of the
        /// (sequence, charge) pairs identified from all files.
        /// </summary>
        /// <param name="targetIons">A list of sequence, charge pairs</param>
        /// <param name="ionMobilities">A list of ion mobility info, if successful</param>
        /// <returns>True if ion mobility information was retrieved successfully</returns>
        public abstract bool TryGetIonMobilityInfos(LibKey[] targetIons, out LibraryIonMobilityInfo ionMobilities);

        /// <summary>
        /// Gets all of the spectrum information for a particular (sequence, charge) pair.  This
        /// may include redundant spectra.  The spectrum points themselves are only loaded as it they
        /// requested to give this function acceptable performance.
        /// </summary>
        /// <param name="key">The sequence, charge pair requested</param>
        /// <param name="labelType">An <see cref="IsotopeLabelType"/> for which to get spectra</param>
        /// <param name="redundancy">Level of redundancy requested in returned values</param>
        /// <returns>An enumeration of <see cref="SpectrumInfo"/></returns>
        public abstract IEnumerable<SpectrumInfoLibrary> GetSpectra(LibKey key,
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

        protected bool Equals(Library other)
        {
            return base.Equals(other) && string.Equals(FileNameHint, other.FileNameHint) && UseExplicitPeakBounds == other.UseExplicitPeakBounds;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((Library) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (FileNameHint != null ? FileNameHint.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ UseExplicitPeakBounds.GetHashCode();
                return hashCode;
            }
        }

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected Library()
        {
        }

        private enum ATTR
        {
            file_name_hint,
            use_explicit_peak_bounds
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            FileNameHint = reader.GetAttribute(ATTR.file_name_hint);
            UseExplicitPeakBounds = reader.GetBoolAttribute(ATTR.use_explicit_peak_bounds, true);
        }

        public override void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            base.WriteXml(writer);
            writer.WriteAttributeIfString(ATTR.file_name_hint, FileNameHint);
            writer.WriteAttribute(ATTR.use_explicit_peak_bounds, UseExplicitPeakBounds, true);
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

        protected LibKeyMap<TInfo> _libraryEntries;
        protected string CachePath { get; set; }

        public override string IsNotLoadedExplained
        {
            get { return (_libraryEntries != null) ? null : @"no library entries"; }
        }

        public override bool ContainsAny(Target target)
        {
            return _libraryEntries.ItemsWithUnmodifiedSequence(target).Any();
        }

        public override bool Contains(LibKey key)
        {
            return FindEntry(key) != -1;
        }

        protected int FindExactEntry(LibKey key)
        {
            if (_libraryEntries == null)
                return -1;
            return _libraryEntries.IndexOf(key.LibraryKey);
        }

        protected int FindEntry(LibKey key)
        {
            if (_libraryEntries == null)
            {
                return -1;
            }
            foreach (var entry in _libraryEntries.Index.ItemsMatching(key, true))
            {
                return entry.OriginalIndex;
            }
            return -1;
        }

        protected virtual void SetLibraryEntries(IEnumerable<TInfo> entries)
        {
            var entryList = ImmutableList.ValueOf(entries);

            _libraryEntries = new LibKeyMap<TInfo>(entryList, entryList.Select(entry=>entry.Key.LibraryKey));
        }

        protected List<TInfo> FilterInvalidLibraryEntries(ref IProgressStatus status, IEnumerable<TInfo> entries)
        {
            var validEntries = new List<TInfo>();
            var invalidKeys = new List<LibKey>();
            foreach (var entry in entries)
            {
                if (!IsValidLibKey(entry.Key))
                {
                    invalidKeys.Add(entry.Key);
                }
                else
                {
                    validEntries.Add(entry);
                }
            }

            status = WarnInvalidEntries(status, validEntries.Count, invalidKeys);
            return validEntries;
        }

        protected bool IsValidLibKey(LibKey libKey)
        {
            try
            {
                var unused = libKey.LibraryKey.CreatePeptideIdentityObj();
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        protected IProgressStatus WarnInvalidEntries(IProgressStatus progressStatus, int validEntryCount,
            ICollection<LibKey> invalidEntries)
        {
            if (invalidEntries.Count == 0)
            {
                return progressStatus;
            }
            var invalidText = TextUtil.LineSeparate(invalidEntries.Take(10).Select(key => key.ToString()));
            string warningMessage = string.Format(Resources.CachedLibrary_WarnInvalidEntries_,
                Name, invalidEntries.Count, invalidEntries.Count + validEntryCount, invalidText);
            progressStatus = progressStatus.ChangeWarningMessage(warningMessage);
            return progressStatus;
        }

        public override bool TryGetLibInfo(LibKey key, out SpectrumHeaderInfo libInfo)
        {
            var index = FindEntry(key);
            
            if (index != -1)
            {
                libInfo = CreateSpectrumHeaderInfo(_libraryEntries[index]);
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

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, MsDataFileUri filePath, out LibraryIonMobilityInfo ionMobilities)
        {
            // By default, no ion mobility information is available
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, int fileIndex, out LibraryIonMobilityInfo ionMobilities)
        {
            // By default, no ion mobility information is available
            ionMobilities = null;
            return false;
        }

        public override bool TryGetIonMobilityInfos(LibKey[] targetIons, out LibraryIonMobilityInfo ionMobilities)
        {
            // By default, no ion mobility information is available
            ionMobilities = null;
            return false;
        }

        public override IEnumerable<SpectrumInfoLibrary> GetSpectra(LibKey key, IsotopeLabelType labelType, LibraryRedundancy redundancy)
        {
            // This base class only handles best match spectra
            if (redundancy == LibraryRedundancy.best)
            {
                int i = FindEntry(key);
                if (i != -1)
                {
                    yield return new SpectrumInfoLibrary(this, labelType, i)
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
            get { return _libraryEntries == null ? 0 : _libraryEntries.Count; }
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
            return peptideSequences.SelectMany(LibraryEntriesWithSequence);
        }

        protected IEnumerable<TInfo> LibraryEntriesWithSequence(Target target)
        {
            return _libraryEntries.ItemsMatching(new LibKey(target, Adduct.EMPTY).LibraryKey, false);
        }

        // ReSharper disable PossibleMultipleEnumeration
        protected int FindFileInList(MsDataFileUri sourceFile, IEnumerable<string> fileNames)
        {
            if (fileNames == null)
            {
                return -1;
            }
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
        private readonly TargetMap<Tuple<TimeSource, double[]>> _dictPeptideRetentionTimes;

        public LibraryRetentionTimes(string path, IDictionary<Target, Tuple<TimeSource, double[]>> dictPeptideRetentionTimes)
        {
            Name = path;
            _dictPeptideRetentionTimes = new TargetMap<Tuple<TimeSource, double[]>>(dictPeptideRetentionTimes);
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
        private readonly LibKeyMap<IonMobilityAndCCS[]> _dictLibKeyIonMobility;

        public static LibraryIonMobilityInfo EMPTY = new LibraryIonMobilityInfo(String.Empty, false, new Dictionary<LibKey, IonMobilityAndCCS[]>());

        public LibraryIonMobilityInfo(string path, bool supportMultipleConformers, IDictionary<LibKey, IonMobilityAndCCS[]> dict) 
            : this(path, supportMultipleConformers, new LibKeyMap<IonMobilityAndCCS[]>(
                ImmutableList.ValueOf(dict.Values), dict.Keys.Select(key=>key.LibraryKey)))
        {
        }

        public LibraryIonMobilityInfo(string path, bool supportMultipleConformers, LibKeyMap<IonMobilityAndCCS[]> dictLibKeyIonMobility)
        {
            Name = path ?? string.Empty;
            SupportsMultipleConformers = supportMultipleConformers;
            _dictLibKeyIonMobility = dictLibKeyIonMobility;
        }

        public string Name { get; private set; }

        public bool SupportsMultipleConformers { get; private set; } // If false, average any redundancies (as with spectral libraries)

        public bool IsEmpty { get { return _dictLibKeyIonMobility == null || _dictLibKeyIonMobility.Count == 0;} }

        /// <summary>
        /// Return the median measured CCS for spectra that were identified with a
        /// specific modified peptide sequence and charge state.
        /// </summary>
        public double? GetLibraryMeasuredCollisionalCrossSection(LibKey chargedPeptide)
        {
            IonMobilityAndCCS[] ionMobilities;
            if ((!_dictLibKeyIonMobility.TryGetValue(chargedPeptide, out ionMobilities)) || (ionMobilities == null))
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
        /// CONSIDER: when we support multiple conformers, is there maybe some difference magnitude at which we should not be averaging (based on resolving power maybe)?
        /// </summary>
        public IonMobilityAndCCS GetLibraryMeasuredIonMobilityAndCCS(LibKey chargedPeptide, double mz, IIonMobilityFunctionsProvider ionMobilityFunctionsProvider)
        {
            IonMobilityAndCCS[] ionMobilities;
            if ((!_dictLibKeyIonMobility.TryGetValue(chargedPeptide, out ionMobilities)) || (ionMobilities == null))
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
                    else // No mobility -> conversion provided, just return median CCS
                    {
                        var ccsValues = ionMobilityInfos.Where(im => im.HasCollisionalCrossSection)
                            .Select(im => im.CollisionalCrossSectionSqA.Value).ToArray();
                        if (ccsValues.Any())
                        {
                            ccs = new Statistics(ccsValues).Median(); // Median is more tolerant of errors than Average
                        }
                    }
                }
            }
            if (!ionMobility.HasValue)
                return IonMobilityAndCCS.EMPTY;
            var highEnergyDriftTimeOffsetMsec = new Statistics(ionMobilityInfos.Where(im => im.HighEnergyIonMobilityValueOffset.HasValue).Select(im => im.HighEnergyIonMobilityValueOffset.Value)).Median(); // Median is more tolerant of errors than Average
            return IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobility, ccs, highEnergyDriftTimeOffsetMsec);
        }

        public IDictionary<LibKey, IonMobilityAndCCS[]> GetIonMobilityDict()
        {
            return _dictLibKeyIonMobility.AsDictionary();
        }
    }

    public abstract class LibrarySpec : XmlNamedElement
    {
        public static readonly PeptideRankId PEP_RANK_COPIES =
            new PeptideRankId(@"Spectrum count", () => Resources.LibrarySpec_PEP_RANK_COPIES_Spectrum_count);
        public static readonly PeptideRankId PEP_RANK_TOTAL_INTENSITY =
            new PeptideRankId(@"Total intensity", () => Resources.LibrarySpec_PEP_RANK_TOTAL_INTENSITY_Total_intensity);
        public static readonly PeptideRankId PEP_RANK_PICKED_INTENSITY =
            new PeptideRankId(@"Picked intensity", () => Resources.LibrarySpec_PEP_RANK_PICKED_INTENSITY_Picked_intensity);

        public static LibrarySpec CreateFromPath(string name, string path)
        {
            if (PathEx.HasExtension(path, BiblioSpecLiteSpec.EXT))
                return new BiblioSpecLiteSpec(name, path);
            else if (PathEx.HasExtension(path, BiblioSpecLibSpec.EXT))
                return new BiblioSpecLibSpec(name, path);
            else if (PathEx.HasExtension(path, ChromatogramLibrarySpec.EXT))
                return new ChromatogramLibrarySpec(name, path);
            else if (PathEx.HasExtension(path, XHunterLibSpec.EXT))
                return new XHunterLibSpec(name, path);
            else if (PathEx.HasExtension(path, NistLibSpec.EXT))
                return new NistLibSpec(name, path);
            else if (PathEx.HasExtension(path, SpectrastSpec.EXT))
                return new SpectrastSpec(name, path);
            else if (PathEx.HasExtension(path, MidasLibSpec.EXT))
                return new MidasLibSpec(name, path);
            else if (PathEx.HasExtension(path, EncyclopeDiaSpec.EXT))
                return new EncyclopeDiaSpec(name, path);
            return null;
        }

        protected LibrarySpec(string name, string path)
            : base(name)
        {
            FilePath = path;
            UseExplicitPeakBounds = true;
        }

        [Track]
        public AuditLogPath FilePathAuditLog
        {
            get { return AuditLogPath.Create(FilePath); }
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

        [Track(defaultValues:typeof(DefaultValuesTrue))]
        public bool UseExplicitPeakBounds { get; private set; }

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

        public LibrarySpec ChangeUseExplicitPeakBounds(bool prop)
        {
            return ChangeProp(ImClone(this), im => im.UseExplicitPeakBounds = prop);
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
            file_path,
            use_explicit_peak_bounds
        }

        public override void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            base.ReadXml(reader);
            FilePath = reader.GetAttribute(ATTR.file_path);
            UseExplicitPeakBounds = reader.GetBoolAttribute(ATTR.use_explicit_peak_bounds, true);
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
            writer.WriteAttribute(ATTR.use_explicit_peak_bounds, UseExplicitPeakBounds, true);
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
                other.IsDocumentLibrary.Equals(IsDocumentLibrary) &&
                other.UseExplicitPeakBounds.Equals(UseExplicitPeakBounds);
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
    public sealed class PeptideRankId : IAuditLogObject
    {
        public static readonly PeptideRankId PEPTIDE_RANK_NONE = new PeptideRankId(string.Empty, () => string.Empty);

        private Func<string> _labelFunc;

        public PeptideRankId(string value, Func<string> labelFunc)
        {
            Value = value;
            _labelFunc = labelFunc;
        }

        /// <summary>
        /// Display text for user interface.
        /// </summary>
        public string Label { get { return _labelFunc(); } }

        /// <summary>
        /// Name for us in XML.
        /// </summary>
        public string Value { get; private set; }

        public override string ToString() { return Label; }

        public string AuditLogText { get { return Label; } }
        public bool IsName { get { return true; } }

        private bool Equals(PeptideRankId other)
        {
            return string.Equals(Label, other.Label) && string.Equals(Value, other.Value);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is PeptideRankId && Equals((PeptideRankId) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return ((Label != null ? Label.GetHashCode() : 0) * 397) ^ (Value != null ? Value.GetHashCode() : 0);
            }
        }
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
            if (ReferenceEquals(rankId, LibrarySpec.PEP_RANK_COPIES))
                return 1;
            return float.MinValue;
        }

        public abstract IEnumerable<KeyValuePair<PeptideRankId, string>> RankValues { get; }
        public string Protein { get; protected set; } // Some .blib and .clib files provide a protein accession (or Molecule List Name for small molecules)

        #region Implementation of IXmlSerializable

        /// <summary>
        /// For XML serialization
        /// </summary>
        protected SpectrumHeaderInfo()
        {
        }

        private enum ATTR
        {
            library_name,
            protein
        }

        public XmlSchema GetSchema()
        {
            return null;
        }

        public virtual void ReadXml(XmlReader reader)
        {
            // Read tag attributes
            LibraryName = reader.GetAttribute(ATTR.library_name);
            Protein = reader.GetAttribute(ATTR.protein);
        }

        public virtual void WriteXml(XmlWriter writer)
        {
            // Write tag attributes
            writer.WriteAttributeString(ATTR.library_name, LibraryName);
            writer.WriteAttributeIfString(ATTR.protein, Protein);
        }

        #endregion

        #region object overrides

        public bool Equals(SpectrumHeaderInfo obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return Equals(obj.LibraryName, LibraryName) &&
                   Equals(obj.Protein, Protein);
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

        public IEnumerable<IEnumerable<SpectrumPeakAnnotation>> Annotations
        {
            get
            {
                foreach (var mi in Peaks)
                    yield return mi.Annotations;
            }
        }

        private bool Equals(SpectrumPeaksInfo other)
        {
            return ArrayUtil.EqualsDeep(Peaks, other.Peaks);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            return obj is SpectrumPeaksInfo other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (Peaks != null ? Peaks.GetHashCode() : 0);
        }

        public struct MI
        {
            private bool _notQuantitative;
            private List<SpectrumPeakAnnotation> _annotations; // A peak may have multiple annotations
            public double Mz { get; set; }
            public float Intensity { get; set; }
            public bool Quantitative {
                get { return !_notQuantitative; }
                set { _notQuantitative = !value; }
            }
            public List<SpectrumPeakAnnotation> Annotations
            {
                get { return _annotations; } 
                set { _annotations = value; }
            }

            public MI ChangeAnnotations(List<SpectrumPeakAnnotation> newAnnotations)
            {
                if (!CollectionUtil.EqualsDeep(newAnnotations, Annotations))
                {
                    // Because this is a struct, it does not need to be cloned
                    // This operation will not affect the memory of the original object
                    var result = this;
                    result._annotations = newAnnotations;
                    return result;
                }
                return this;
            }

            public MI ChangeIntensity(float intensity)
            {
                var result = this;
                result.Intensity = intensity;
                return result;
            }

            public SpectrumPeakAnnotation AnnotationsFirstOrDefault
            {
                get { return Annotations == null || Annotations.Count == 0 ? 
                    SpectrumPeakAnnotation.EMPTY : 
                    Annotations[0] ?? SpectrumPeakAnnotation.EMPTY; }
            }

            public IEnumerable<SpectrumPeakAnnotation> GetAnnotationsEnumerator()
            {
                if (Annotations == null || Annotations.Count == 0)
                {
                    yield return SpectrumPeakAnnotation.EMPTY;
                }
                else
                {
                    foreach (var spectrumPeakAnnotation in Annotations)
                    {
                        yield return spectrumPeakAnnotation ?? SpectrumPeakAnnotation.EMPTY;
                    }
                }
            }

            public CustomIon AnnotationsAggregateDescriptionIon
            {
                get
                {
                    if (Annotations != null)
                    {
                        var aggregateName = AnnotationsFirstOrDefault.Ion.Name ?? string.Empty;
                        var nAnnotations = Annotations.Count;
                        for (var i = 1; i < nAnnotations; i++)
                        {
                            var name = Annotations[i].Ion.Name;
                            if (!string.IsNullOrEmpty(name))
                            {
                                aggregateName += @"/" + name;
                            }
                        }
                        if (!string.IsNullOrEmpty(aggregateName))
                        {
                            return AnnotationsFirstOrDefault.Ion.ChangeName(aggregateName);
                        }
                    }
                    return AnnotationsFirstOrDefault.Ion;
                }
            }

            public bool Equals(MI other)
            {
                return _notQuantitative == other._notQuantitative &&
                       ArrayUtil.EqualsDeep(_annotations, other._annotations) && Mz.Equals(other.Mz) &&
                       Intensity.Equals(other.Intensity);
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                return obj is MI other && Equals(other);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _notQuantitative.GetHashCode();
                    hashCode = (hashCode * 397) ^ (_annotations != null ? _annotations.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ Mz.GetHashCode();
                    hashCode = (hashCode * 397) ^ Intensity.GetHashCode();
                    return hashCode;
                }
            }
        }
    }

    public class SmallMoleculeLibraryAttributes : IEquatable<SmallMoleculeLibraryAttributes>
    {
        public static SmallMoleculeLibraryAttributes EMPTY = new SmallMoleculeLibraryAttributes(null, null, null, null, null, null);
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

        public static void ParseMolecularFormulaOrMassesString(string molecularFormulaOrMassesString,
            out string molecularFormula, out TypedMass? massMono, out TypedMass? massAverage)
        {
            if (molecularFormulaOrMassesString != null && molecularFormulaOrMassesString.Contains(CustomMolecule.MASS_SPLITTER))
            {
                var parts = molecularFormulaOrMassesString.Split(CustomMolecule.MASS_SPLITTER); // We didn't have a formula so we saved masses
                massMono = new TypedMass(double.Parse(parts[0], CultureInfo.InvariantCulture), MassType.Monoisotopic);
                massAverage = new TypedMass(double.Parse(parts[1], CultureInfo.InvariantCulture), MassType.Average);
                molecularFormula = null;
            }
            else
            {
                massMono = null;
                massAverage = null;
                molecularFormula = molecularFormulaOrMassesString;
            }
        }

        public static string FormatChemicalFormulaOrMassesString(string chemicalFormula, TypedMass? massMono, TypedMass? massAverage) // For serialization - represents formula or masses, depending on what's available
        {
            if (!string.IsNullOrEmpty(chemicalFormula))
            {
                return chemicalFormula;

            }
            if (massMono != null && massAverage != null)
            {
                Assume.IsTrue(massMono.Value.IsMonoIsotopic());
                Assume.IsTrue(massAverage.Value.IsAverage());
                return CustomMolecule.FormattedMasses(massMono.Value.Value, massAverage.Value.Value); // Format as dd.ddd/dd.ddd
            }

            return string.Empty;
        }

        public static byte[] ToBytes(SmallMoleculeLibraryAttributes attributes)
        {
            attributes = attributes ?? EMPTY;
            // Encode as <length><item><length><item>etc
            var items = new List<byte[]>
            {
                Encoding.UTF8.GetBytes(attributes.MoleculeName ?? string.Empty),
                Encoding.UTF8.GetBytes(attributes.ChemicalFormulaOrMassesString ?? string.Empty), // If no formula provided, encode monoMass and averageMass instead
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

        public static SmallMoleculeLibraryAttributes Create(string moleculeName, string chemicalFormula, TypedMass? massMono, TypedMass? massAverage,
            string inChiKey, string otherKeys)
        {
            if (string.IsNullOrEmpty(moleculeName) && string.IsNullOrEmpty(chemicalFormula) &&
                massMono == null && massAverage == null &&
                string.IsNullOrEmpty(inChiKey) && string.IsNullOrEmpty(otherKeys))
            {
                return EMPTY;
            }
            return new SmallMoleculeLibraryAttributes(moleculeName, chemicalFormula, massMono, massAverage, inChiKey, otherKeys);
        }

        public static SmallMoleculeLibraryAttributes Create(string moleculeName, string chemicalFormulaOrMassesString,
            string inChiKey, IDictionary<string, string> otherKeys)
        {
            return Create(moleculeName, chemicalFormulaOrMassesString, inChiKey, otherKeys == null ? string.Empty : string.Join(@"\t", otherKeys.Select(kvp => kvp.Key + @":" + kvp.Value)));
        }

        public static SmallMoleculeLibraryAttributes Create(string moleculeName, string chemicalFormulaOrMassesString,
            string inChiKey, string otherKeys)
        {
            ParseMolecularFormulaOrMassesString(chemicalFormulaOrMassesString,
                out var chemicalFormula, out var massMono, out var massAverage);
            if (string.IsNullOrEmpty(moleculeName) && string.IsNullOrEmpty(chemicalFormula) &&
                massMono == null && massAverage == null &&
                string.IsNullOrEmpty(inChiKey) && string.IsNullOrEmpty(otherKeys))
            {
                return EMPTY;
            }
            return new SmallMoleculeLibraryAttributes(moleculeName, chemicalFormula, massMono, massAverage, inChiKey, otherKeys);
        }

        private SmallMoleculeLibraryAttributes(string moleculeName, string chemicalFormula, TypedMass? massMono, TypedMass? massAverage, string inChiKey, string otherKeys)
        {
            MoleculeName = moleculeName;
            ChemicalFormulaOrMassesString = FormatChemicalFormulaOrMassesString(chemicalFormula, massMono, massAverage); // If no formula provided, encode monoMass and averageMass instead
            InChiKey = inChiKey;
            OtherKeys = otherKeys;
        }

        public string MoleculeName { get; private set; }
        public string ChemicalFormulaOrMassesString { get; private set; } // If no formula provided, encodes monoMass and averageMass instead as <mono>-slash-<average>
        public string ChemicalFormula => ChemicalFormulaOrMassesString != null && !ChemicalFormulaOrMassesString.Contains(CustomMolecule.MASS_SPLITTER) // Returns null if ChemicalFormulaOrMassesString encodes masses instead of formula
            ? ChemicalFormulaOrMassesString
            : null;

        public string InChiKey { get; private set; }
        public string OtherKeys { get; private set; }

        public string GetPreferredKey()
        {
            return CreateMoleculeID().PrimaryAccessionValue ?? MoleculeName;
        }

        public string Validate()
        {
            return string.IsNullOrEmpty(ChemicalFormulaOrMassesString) ||
                    (string.IsNullOrEmpty(MoleculeName) && string.IsNullOrEmpty(InChiKey) && string.IsNullOrEmpty(OtherKeys))
                ? Resources.SmallMoleculeLibraryAttributes_Validate_A_small_molecule_is_defined_by_a_chemical_formula_and_at_least_one_of_Name__InChiKey__or_other_keys__HMDB_etc_
                : null;
        }

        public MoleculeAccessionNumbers CreateMoleculeID()
        {
            return new MoleculeAccessionNumbers(OtherKeys, InChiKey);
        }

        public List<KeyValuePair<string,string>> LocalizedKeyValuePairs
        {
            get
            {
                var smallMolLines = new List<KeyValuePair<string, string>>();
                if (!string.IsNullOrEmpty(MoleculeName))
                {
                    smallMolLines.Add(new KeyValuePair<string, string> (Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Name, MoleculeName));
                }
                ParseMolecularFormulaOrMassesString(ChemicalFormulaOrMassesString, out var chemicalFormula, out var massMono, out var massAverage);
                if (!string.IsNullOrEmpty(chemicalFormula))
                {
                    smallMolLines.Add(new KeyValuePair<string, string> (Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Formula, chemicalFormula));
                }
                if (massMono != null)
                {
                    smallMolLines.Add(new KeyValuePair<string, string>(Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Monoisotopic_mass, massMono.ToString()));
                }
                if (massAverage != null)
                {
                    smallMolLines.Add(new KeyValuePair<string, string>(Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_Average_mass, chemicalFormula));
                }
                if (!string.IsNullOrEmpty(InChiKey))
                {
                    smallMolLines.Add(new KeyValuePair<string, string> (Resources.SmallMoleculeLibraryAttributes_KeyValuePairs_InChIKey, InChiKey));
                }
                if (!string.IsNullOrEmpty(OtherKeys))
                {
                    // Add a separate line for each molecule accession number
                    var accessionNumDict = MoleculeAccessionNumbers.FormatAccessionNumbers(OtherKeys);
                    smallMolLines.AddRange(accessionNumDict);
                }
                return smallMolLines;
            }
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
                   Equals(ChemicalFormulaOrMassesString, other.ChemicalFormulaOrMassesString) &&
                   Equals(InChiKey, other.InChiKey) &&
                   Equals(OtherKeys, other.OtherKeys);
        }
        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (MoleculeName != null ? MoleculeName.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ChemicalFormulaOrMassesString != null ? ChemicalFormulaOrMassesString.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (InChiKey != null ? InChiKey.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (OtherKeys != null ? OtherKeys.GetHashCode() : 0);
                return hashCode;
            }
        }

        /// <summary>
        /// Return a SmallMoleculeLibraryAttributes object that represents the union of this and other, or null if
        /// conflicts prevent that
        /// </summary>
        public SmallMoleculeLibraryAttributes Merge(SmallMoleculeLibraryAttributes other)
        {
            if (other == null || Equals(other))
                return this;

            // If only one is named, could still be a match
            var consensusName = MoleculeName;
            if (!Equals(MoleculeName, other.MoleculeName))
            {
                if (string.IsNullOrEmpty(MoleculeName))
                {
                    consensusName = other.MoleculeName;
                }
                else if (!string.IsNullOrEmpty(other.MoleculeName))
                {
                    return null; // Conflict
                }
            }

            if (!Equals(ChemicalFormulaOrMassesString, other.ChemicalFormulaOrMassesString))
            {
                return null; // Conflict
            }

            var consensusInChiKey = InChiKey;
            var consensusOtherKeys = OtherKeys;
            if (!Equals(InChiKey, other.InChiKey) || !Equals(OtherKeys, other.OtherKeys))
            {
                var consensusAccession = this.CreateMoleculeID().Union(other.CreateMoleculeID());
                if (consensusAccession == null)
                {
                    return null; // Conflict
                }
                consensusInChiKey = consensusAccession.GetInChiKey();
                consensusOtherKeys = consensusAccession.GetNonInChiKeys();
            }
            
            return Create(consensusName, ChemicalFormulaOrMassesString, consensusInChiKey,
                consensusOtherKeys);
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
        public string Protein { get; set; } // Also used as Molecule List Name for small molecules
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
                    SpectrumPeaks = info.SpectrumPeaksInfo,
                    Protein = info.Protein
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

    public abstract class SpectrumInfo
    {
        public SpectrumInfo(IsotopeLabelType labelType, bool isBest)
        {
            LabelType = labelType;
            IsBest = isBest;
        }

        protected bool Equals(SpectrumInfo other)
        {
            return Equals(LabelType, other.LabelType) && string.Equals(Name, other.Name) && IsBest == other.IsBest &&
                   Equals(SpectrumPeaksInfo, other.SpectrumPeaksInfo) &&
                   Equals(ChromatogramData, other.ChromatogramData);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((SpectrumInfo) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (LabelType != null ? LabelType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Name != null ? Name.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ IsBest.GetHashCode();
                hashCode = (hashCode * 397) ^ (SpectrumPeaksInfo != null ? SpectrumPeaksInfo.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (ChromatogramData != null ? ChromatogramData.GetHashCode() : 0);
                return hashCode;
            }
        }

        public IsotopeLabelType LabelType { get; protected set; }
        public abstract string Name { get; }
        public bool IsBest { get; protected set; }
        public abstract SpectrumPeaksInfo SpectrumPeaksInfo { get; }

        public abstract LibraryChromGroup ChromatogramData { get; }
    }

    public class SpectrumInfoLibrary : SpectrumInfo
    {
        private Library _library;
        // Cache peaks and chromatograms to avoid loading every time
        // CONSIDER: Synchronization required?
        private SpectrumPeaksInfo _peaksInfo;
        private LibraryChromGroup _chromGroup;

        public SpectrumInfoLibrary(Library library, IsotopeLabelType labelType, object spectrumKey):
            this(library, labelType, null, null, null, null, true, spectrumKey)
        {
        }

        public SpectrumInfoLibrary(Library library, IsotopeLabelType labelType, string filePath,
            double? retentionTime, IonMobilityAndCCS ionMobilityInfo, string protein, bool isBest, object spectrumKey) :
                base(labelType, true)
        {
            _library = library;
            LabelType = labelType;
            SpectrumKey = spectrumKey;
            FilePath = filePath;
            RetentionTime = retentionTime;
            IonMobilityInfo = ionMobilityInfo ?? IonMobilityAndCCS.EMPTY;
            Protein = protein;
            IsBest = isBest;
        }

        public object SpectrumKey { get; private set; }

        public override string Name
        {
            get { return _library.Name; }
        }

        public override SpectrumPeaksInfo SpectrumPeaksInfo
        {
            get { return _peaksInfo = _peaksInfo ?? _library.LoadSpectrum(SpectrumKey); }
        }

        public override LibraryChromGroup ChromatogramData
        {
            get { return _chromGroup = _chromGroup ?? _library.LoadChromatogramData(SpectrumKey); }
        }

        public SpectrumHeaderInfo SpectrumHeaderInfo { get; set; }

        public string FilePath { get; private set; }

        public string FileName
        {
            get
            {
                try {
                    return Path.GetFileName(FilePath);
                }
                catch {
                    return FilePath;
                }
            }
        }
        public double? RetentionTime { get; set; }
        public IonMobilityAndCCS IonMobilityInfo { get; private set; }
        public string Protein { get; private set; } // Also used as Molecule List Name for small molecules
    }

    public class SpectrumInfoProsit : SpectrumInfo
    {
        public static readonly string NAME = @"Prosit";

        private SpectrumPeaksInfo _peaksInfo;

        public SpectrumInfoProsit(PrositMS2Spectra ms2Spectrum, TransitionGroupDocNode precursor, IsotopeLabelType labelType, int nce)
            : base(labelType, true)
        {
            _peaksInfo = ms2Spectrum?.GetSpectrum(precursor).SpectrumPeaks;
            Precursor = precursor;
            NCE = nce;
        }

        public override string Name
        {
            get { return NAME; }
        }

        public override SpectrumPeaksInfo SpectrumPeaksInfo
        {
            get { return _peaksInfo; }
        }

        public override LibraryChromGroup ChromatogramData
        {
            get { return null; }
        }

        public TransitionGroupDocNode Precursor { get; }

        public int NCE { get; }
    }

    public class LibraryChromGroup
    {
        private IList<ChromData> _chromDatas = ImmutableList.Empty<ChromData>();
        public double StartTime { get; set; }
        public double EndTime { get; set; }
        public double RetentionTime { get; set; }
        public double? CCS { get; set; }
        public float[] Times { get; set; }
        public IList<ChromData> ChromDatas { get { return _chromDatas; } set { _chromDatas = ImmutableList.ValueOf(value); } }

        protected bool Equals(LibraryChromGroup other)
        {
            return ArrayUtil.EqualsDeep(_chromDatas, other._chromDatas) && StartTime.Equals(other.StartTime) &&
                   EndTime.Equals(other.EndTime) && RetentionTime.Equals(other.RetentionTime) &&
                   Equals(CCS, other.CCS) &&
                   ArrayUtil.EqualsDeep(Times, other.Times);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != GetType()) return false;
            return Equals((LibraryChromGroup) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (_chromDatas != null ? _chromDatas.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ StartTime.GetHashCode();
                hashCode = (hashCode * 397) ^ EndTime.GetHashCode();
                hashCode = (hashCode * 397) ^ RetentionTime.GetHashCode();
                hashCode = (hashCode * 397) ^ (CCS??0).GetHashCode();
                hashCode = (hashCode * 397) ^ (Times != null ? Times.GetHashCode() : 0);
                return hashCode;
            }
        }

        public class ChromData
        {
            public double Mz { get; set; }
            public double Height { get; set; }
            public float[] Intensities { get; set; }
            public Adduct Charge { get; set; }
            public IonType IonType { get; set; }
            public int Ordinal { get; set; }
            public int MassIndex { get; set; }
            public string FragmentName { get; set; } // Small molecule use
            public IonMobilityValue IonMobility { get; set; } 

            protected bool Equals(ChromData other)
            {
                return Mz.Equals(other.Mz) && Height.Equals(other.Height) && Equals(Intensities, other.Intensities) &&
                       Charge == other.Charge && IonType == other.IonType && Ordinal == other.Ordinal &&
                       Equals(IonMobility, other.IonMobility) &&
                       Equals(FragmentName, other.FragmentName) &&
                       MassIndex == other.MassIndex;
            }

            public override bool Equals(object obj)
            {
                if (ReferenceEquals(null, obj)) return false;
                if (ReferenceEquals(this, obj)) return true;
                if (obj.GetType() != GetType()) return false;
                return Equals((ChromData) obj);
            }

            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = Mz.GetHashCode();
                    hashCode = (hashCode * 397) ^ Height.GetHashCode();
                    hashCode = (hashCode * 397) ^ (Intensities != null ? Intensities.GetHashCode() : 0);
                    hashCode = (hashCode * 397) ^ Charge.GetHashCode();
                    hashCode = (hashCode * 397) ^ (string.IsNullOrEmpty(FragmentName) ? 0 : FragmentName.GetHashCode());
                    hashCode = (hashCode * 397) ^ (int) IonType;
                    hashCode = (hashCode * 397) ^ Ordinal;
                    hashCode = (hashCode * 397) ^ MassIndex;
                    hashCode = (hashCode * 397) ^ (IonMobility != null ? IonMobility.GetHashCode() : 0);
                    return hashCode;
                }
            }
        }
    }

    /// <summary>
    /// Links to spectral library sources
    /// </summary>
    public sealed class LibraryLink
    {
        public static readonly LibraryLink PEPTIDEATLAS = new LibraryLink(@"PeptideAtlas", @"http://www.peptideatlas.org/speclib/");
        public static readonly LibraryLink NIST = new LibraryLink(@"NIST", @"http://peptide.nist.gov/");
        public static readonly LibraryLink GPM = new LibraryLink(@"GPM", @"ftp://ftp.thegpm.org/projects/xhunter/libs/");

        private LibraryLink(string name, string href)
        {
            Name = name;
            Link = href;
        }

        public string Name { get; private set; }

        public string Link { get; private set; }

        // This appears in stack traces when we report unhandled parsing issues
        public override string ToString()
        {
            var result = new List<string>();
            if (!string.IsNullOrEmpty(Name))
                result.Add($@"LinkName: {Name} ");
            if (!string.IsNullOrEmpty(Link))
                result.Add($@"LinkURL: {Link} ");
            return TextUtil.LineSeparate(result);
        }
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

        // This appears in stack traces when we report unhandled parsing issues
        public override string ToString()
        {
            var lines = new List<string>();
            if (!string.IsNullOrEmpty(Format))
                lines.Add($@"Format: {Format}");
            if (!string.IsNullOrEmpty(Id))
                lines.Add($@"LSID: {Id}");
            if (!string.IsNullOrEmpty(Revision))
                lines.Add($@"FileRevision: {Revision}");
            if (!string.IsNullOrEmpty(Version))
                lines.Add($@"SchemaVersion: {Version}");
            if (_dataFiles != null && _dataFiles.Any())
                lines.AddRange(_dataFiles.Select(df => df.ToString()));
            if (_libLinks != null && _libLinks.Any())
                lines.AddRange(_libLinks.Select(link => link.ToString()));
            return TextUtil.LineSeparate(lines);
        }
    }

    /// <summary>
    /// Key for use in dictionaries that store library header information in
    /// memory.
    /// </summary>
    public struct LibKey
    {
        public static LibKey EMPTY = new LibKey(SmallMoleculeLibraryAttributes.EMPTY, Adduct.EMPTY);

        public LibKey(LibraryKey libraryKey) : this()
        {
            LibraryKey = libraryKey;
        }

        public LibKey(string sequence, int charge) : this()
        {
            LibraryKey = (LibraryKey) CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(sequence, charge)
                         ?? new PeptideLibraryKey(sequence, charge);
        }

        public LibKey(SmallMoleculeLibraryAttributes attributes, Adduct adduct) : this()
        {
            LibraryKey = new MoleculeLibraryKey(attributes, adduct);
        }

        public LibKey(string primaryKey, Adduct adduct) : this()
        {
            if (adduct.IsProteomic)
            {
                LibraryKey = (LibraryKey)
                             CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(primaryKey, adduct.AdductCharge)
                             ?? new PeptideLibraryKey(primaryKey, adduct.AdductCharge);
            }
            else
            {
                LibraryKey = new MoleculeLibraryKey(SmallMoleculeLibraryAttributes.Create(primaryKey, null, null, string.Empty), adduct);
            }
        }

        [Track]
        public LibraryKey LibraryKey { get; private set; }

        public LibKey(double precursorMz,
            double? retentionTime = null)
            : this() // TODO(bspratt) probably should add ion mobility 
        {
            LibraryKey = new PrecursorLibraryKey(precursorMz, retentionTime);
        }

        public LibKey(Target target, Adduct adduct)
            : this()
        {
            if (target.IsProteomic)
            {
                LibraryKey = (LibraryKey)
                             CrosslinkSequenceParser.TryParseCrosslinkLibraryKey(target.Sequence, adduct.AdductCharge)
                             ?? new PeptideLibraryKey(target.Sequence, adduct.AdductCharge);
            }
            else
                LibraryKey = new MoleculeLibraryKey(target.Molecule.GetSmallMoleculeLibraryAttributes(), adduct);
        }

        public LibKey(Target target, int charge) : this(target.Sequence, charge)
        {
        }

        public bool IsProteomicKey { get { return LibraryKey is PeptideLibraryKey; } }
        public bool IsSmallMoleculeKey { get { return LibraryKey is MoleculeLibraryKey; } }
        public bool IsPrecursorKey { get { return LibraryKey is PrecursorLibraryKey; } }
        public bool HasRetentionTime { get { return IsPrecursorKey && ((PrecursorLibraryKey)LibraryKey).RetentionTime.HasValue; } }

        public string Sequence
        {
            get
            {
                var peptideKey = LibraryKey as PeptideLibraryKey;
                return peptideKey == null ? null : peptideKey.ModifiedSequence;
            }
        }
        public Target Target
        {
            get
            {
                return LibraryKey.Target;
            }
        }

        public SmallMoleculeLibraryAttributes SmallMoleculeLibraryAttributes
        {
            get
            {
                var moleculeLibraryKey = LibraryKey as MoleculeLibraryKey;
                return moleculeLibraryKey == null
                    ? SmallMoleculeLibraryAttributes.EMPTY
                    : moleculeLibraryKey.SmallMoleculeLibraryAttributes;
            }
        }
        public int Charge { get
        {
            return IsProteomicKey
                ? ((PeptideLibraryKey) LibraryKey).Charge
                : (IsPrecursorKey ? 0 : ((MoleculeLibraryKey) LibraryKey).Adduct.AdductCharge);
        } }
        public Adduct Adduct 
        {
            get
            {
                return LibraryKey.Adduct;
            }
        }

        public bool IsModified { get
        {
            var key = LibraryKey as PeptideLibraryKey;
            return key != null && key.HasModifications;
        } }

        public double? PrecursorMz
        {
            get
            {
                var key = LibraryKey as PrecursorLibraryKey;
                return key != null ? key.Mz : default(double?);
            }
        }

        public double? RetentionTime
        {
            get
            {
                var key = LibraryKey as PrecursorLibraryKey;
                return key != null ? key.RetentionTime : default(double?);
            }
        }

        public bool HasModifications
        {
            get
            {
                var peptideKey = LibraryKey as PeptideLibraryKey;
                return peptideKey != null && peptideKey.HasModifications;
            }
        }

        public int ModificationCount
        {
            get
            {
                var peptideKey = LibraryKey as PeptideLibraryKey;
                return peptideKey != null ? peptideKey.ModificationCount : 0;
            }
        }

        public static implicit operator LibraryKey(LibKey libKey)
        {
            return libKey.LibraryKey;
        }

        #region object overrides

        public bool Equals(LibKey obj)
        {
            return LibraryKey.IsEquivalentTo(obj.LibraryKey);
        }

        public override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj.GetType() != typeof(LibKey)) return false;
            return Equals((LibKey)obj);  // N.B. for equality we ignore any small molecule metadata
        }

        public override int GetHashCode()
        {
            return LibraryKey.GetEquivalencyHashCode();
        }

        public override string ToString()
        {
            return LibraryKey.ToString();
        }

        #endregion

        public void Write(Stream outStream)
        {
            LibraryKey.Write(outStream);
        }

        public static LibKey Read(ValueCache valueCache, Stream inStream)
        {
            return new LibKey(LibraryKey.Read(valueCache, inStream));
        }
    }

    public class SpectrumSourceFileDetails
    {
        public SpectrumSourceFileDetails(string filePath, string idFilePath = null)
        {
            FilePath = filePath;
            IdFilePath = idFilePath;
            CutoffScores = new Dictionary<string, double?>();
            BestSpectrum = 0;
            MatchedSpectrum = 0;
        }

        public string FilePath { get; private set; }
        public string IdFilePath { get; set; }
        public Dictionary<string, double?> CutoffScores { get; private set; }
        public int BestSpectrum { get; set; }
        public int MatchedSpectrum { get; set; }

        public override string ToString()
        {
            var result = new List<string>();
            if (!string.IsNullOrEmpty(IdFilePath))
                result.Add($@"IdFilePath: {IdFilePath}");
            if (!string.IsNullOrEmpty(FilePath))
                result.Add($@"FilePath: {FilePath}");
            return TextUtil.LineSeparate(result);
        }
    }
}
