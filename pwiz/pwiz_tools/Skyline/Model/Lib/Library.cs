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
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.DocSettings.Extensions;
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
            foreach (var library in document.Settings.PeptideSettings.Libraries.Libraries)
            {
                if (library != null && library.ReadStream != null)
                    yield return library.ReadStream;
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

        public void BuildLibrary(IDocumentContainer container, ILibraryBuilder builder)
        {
            Action<IDocumentContainer, ILibraryBuilder> buildAction = BuildLibraryBackground;
            buildAction.BeginInvoke(container, builder, null, null);
        }

        private void BuildLibraryBackground(IDocumentContainer container, ILibraryBuilder builder)
        {
            // This blocks all library loading, while a library is being built
            // TODO: Something better than locking for the entire build
            lock (_loadedLibraries)
            {
                builder.BuildLibrary(new LibraryBuildMonitor(this, container));
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
        }

        private static void ForDocumentLibraryReload(IDocumentContainer container, string name)
        {
            var docOriginal = container.Document;
            if (docOriginal == null)
                return;
            var librarySettings = docOriginal.Settings.PeptideSettings.Libraries;
            if (!librarySettings.HasLibraries)
                return;
            int iSpec = librarySettings.LibrarySpecs.IndexOf(spec => Equals(name, spec.Name));
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
// ReSharper disable UnaccessedField.Local
            // Might want this someday...
            private readonly IDocumentContainer _container;
// ReSharper restore UnaccessedField.Local

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
        void BuildLibrary(IProgressMonitor progress);

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
        /// Determines if the library contains a specific (sequence, charge) pair.
        /// </summary>
        /// <param name="key">A sequence, charge pair</param>
        /// <returns>True if the library contains the key</returns>
        public abstract bool Contains(LibKey key);

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
        /// <returns></returns>
        public abstract bool TryLoadSpectrum(LibKey key, out SpectrumPeaksInfo spectrum);

        /// <summary>
        /// Returns the total number of spectra loaded from the library.
        /// </summary>
        public abstract int Count { get; }

        /// <summary>
        /// Returns an enumerator for the keys of the spectra loaded from the library.
        /// </summary>
        public abstract IEnumerable<LibKey> Keys { get; }

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

        public LibrarySpec ChangeDocumentLocal(bool prop)
        {
            return ChangeProp(ImClone(this), (im, v) => im.IsDocumentLocal = v, prop);
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


    public sealed class LibraryRankedSpectrumInfo : Immutable
    {
        private readonly ReadOnlyCollection<RankedMI> _spectrum;

        public LibraryRankedSpectrumInfo(SpectrumPeaksInfo info,
                IsotopeLabelType typeInfo, TransitionGroup group,
                SrmSettings settings, ExplicitMods mods,
                IEnumerable<int> charges, IEnumerable<IonType> types,
                IEnumerable<int> rankCharges, IEnumerable<IonType> rankTypes)
            : this(info, typeInfo, group, settings, mods, charges, types, rankCharges, rankTypes, false, true, -1)
        {
        }

        public LibraryRankedSpectrumInfo(SpectrumPeaksInfo info, IsotopeLabelType typeInfo,
                TransitionGroup group, SrmSettings settings, ExplicitMods mods,
                bool useFilter, int minPeaks)
            : this(info, typeInfo, group, settings, mods,
                null, // charges
                null, // types
                // ReadOnlyCollection enumerators are too slow, and show under a profiler
                settings.TransitionSettings.Filter.ProductCharges.ToArray(),
                settings.TransitionSettings.Filter.IonTypes.ToArray(),
                useFilter, false, minPeaks)
        {
        }

        private LibraryRankedSpectrumInfo(SpectrumPeaksInfo info, IsotopeLabelType typeInfo,
            TransitionGroup group, SrmSettings settings, ExplicitMods mods,
            IEnumerable<int> charges, IEnumerable<IonType> types,
            IEnumerable<int> rankCharges, IEnumerable<IonType> rankTypes,
            bool useFilter, bool matchAll, int minPeaks)
        {
            LabelType = typeInfo;

            if (!useFilter)
            {
                if (charges == null)
                    charges = Transition.ALL_CHARGES;
                if (types == null)
                    types = Transition.ALL_TYPES;
                matchAll = true;
            }

            RankParams rp = new RankParams
                                {
                                    sequence = group.Peptide.Sequence,
                                    precursorCharge = group.PrecursorCharge,
                                    charges = charges ?? rankCharges,
                                    types = types ?? rankTypes,
                                    matchAll = matchAll,
                                    rankCharges = rankCharges,
                                    rankTypes = rankTypes
                                };

            // Get necessary mass calculators and masses
            var calcMatchPre = settings.GetPrecursorCalc(typeInfo, mods);
            var calcMatch = settings.GetFragmentCalc(typeInfo, mods);
            var calcPredict = settings.GetFragmentCalc(group.LabelType, mods);
            rp.precursorMz = SequenceMassCalc.GetMZ(calcMatchPre.GetPrecursorMass(rp.sequence),
                rp.precursorCharge);
            rp.massPreMatch = calcMatch.GetPrecursorFragmentMass(rp.sequence);
            rp.massPrePredict = rp.massPreMatch;
            rp.massesMatch = calcMatch.GetFragmentIonMasses(rp.sequence);
            rp.massesPredict = rp.massesMatch;
            if (!ReferenceEquals(calcPredict, calcMatch))
            {
                rp.massPrePredict = calcPredict.GetPrecursorFragmentMass(rp.sequence);
                rp.massesPredict = calcPredict.GetFragmentIonMasses(rp.sequence);
            }

            // Get values of interest from the settings.
            var tranSettings = settings.TransitionSettings;
            var filter = tranSettings.Filter;
            var libraries = tranSettings.Libraries;
            var instrument = tranSettings.Instrument;

            // Create arrays because ReadOnlyCollection enumerators are too slow
            // In some cases these collections must be enumerated for every ion
            // allowed in the library specturm.
            rp.startFinder = filter.FragmentRangeFirst;
            rp.endFinder = filter.FragmentRangeLast;
            rp.pro = filter.IncludeNProline;
            rp.gluasp = filter.IncludeCGluAsp;

            // Get library settings
            rp.tolerance = libraries.IonMatchTolerance;
            rp.pick = tranSettings.Libraries.Pick;
            int ionMatchCount = libraries.IonCount;
            // If no library filtering will happen, return all rankings for view in the UI
            if (!useFilter || rp.pick == TransitionLibraryPick.none)
            {
                if (rp.pick == TransitionLibraryPick.none)
                    rp.pick = TransitionLibraryPick.all;
                ionMatchCount = -1;
            }

            // Get instrument settings
            rp.minMz = instrument.MinMz;
            rp.maxMz = instrument.MaxMz;

            // Get the library spectrum mass-intensity pairs
            IList<SpectrumPeaksInfo.MI> listMI = info.Peaks;

            // Because sorting and matching observed ions with predicted
            // ions appear as bottlenecks in a profiler, a minimum number
            // of peaks may be supplied to allow the use of a 2-phase linear
            // filter that can significantly reduce the number of peaks
            // needing the O(n*log(n)) sorting and the O(n*m) matching.

            int len = listMI.Count;
            float intensityCutoff = 0;

            if (minPeaks != -1)
            {
                // Start searching for good cut-off at mean intensity.
                double totalIntensity = 0;
                foreach (float intensity in info.Intensities)
                    totalIntensity += intensity;

                FindIntensityCutoff(listMI, 0, (float) (totalIntensity/len) * 2, minPeaks, 1, ref intensityCutoff, ref len);
            }

            // Create filtered peak array storing original index for m/z ordering
            // to avoid needing to sort to return to this order.
            RankedMI[] arrayRMI = new RankedMI[len];
            for (int i = 0, j = 0, lenOrig = listMI.Count; i < lenOrig ; i++)
            {
                SpectrumPeaksInfo.MI mi = listMI[i];
                if (mi.Intensity >= intensityCutoff || intensityCutoff == 0)
                {
                    arrayRMI[j] = new RankedMI(mi, j);
                    j++;
                }
            }

            // The one expensive sort is used to determine rank order
            // by intensity.
            Array.Sort(arrayRMI, OrderIntensityTryDesc);

            RankedMI[] arrayResult = new RankedMI[ionMatchCount != -1 ? ionMatchCount : arrayRMI.Length];

            for (int i = 0; i < arrayRMI.Length; i++)
            {
                RankedMI rmi = arrayRMI[i];
                rmi.CalculateRank(rp);

                // If not filtering for only the highest ionMatchCount ranks
                if (ionMatchCount == -1)
                {
                    // Put the ranked record back where it started in the
                    // m/z ordering to avoid a second sort.
                    arrayResult[rmi.IndexMz] = rmi;
                }
                // Otherwise, if this ion was ranked, add it to the result array
                else if (rmi.Rank > 0)
                {
                    int countRanks = rmi.Rank;
                    arrayResult[countRanks - 1] = rmi;
                    // And stop when the array is full
                    if (countRanks == ionMatchCount)
                        break;
                }
            }

            // If not enough ranked ions were found, fill the rest of the results array
            if (ionMatchCount != -1)
            {
                for (int i = rp.Ranked; i < ionMatchCount; i++)
                    arrayResult[i] = RankedMI.EMPTY;
            }

            _spectrum = MakeReadOnly(arrayResult);
        }

        public IsotopeLabelType LabelType { get; private set; }

        private static void FindIntensityCutoff(IEnumerable<SpectrumPeaksInfo.MI> listMI, float left, float right, int minPeaks, int calls, ref float cutoff, ref int len)
        {
            if (calls < 3)
            {
                float mid = (left + right)/2;
                int count = FilterPeaks(listMI, mid);
                if (count < minPeaks)
                    FindIntensityCutoff(listMI, left, mid, minPeaks, calls + 1, ref cutoff, ref len);
                else
                {
                    cutoff = mid;
                    len = count;
                    if (count > minPeaks*1.5)
                        FindIntensityCutoff(listMI, mid, right, minPeaks, calls + 1, ref cutoff, ref len);
                }
            }
        }

        private static int FilterPeaks(IEnumerable<SpectrumPeaksInfo.MI> listMI, float intensityCutoff)
        {
            int nonNoise = 0;
            foreach (SpectrumPeaksInfo.MI mi in listMI)
            {
                if (mi.Intensity >= intensityCutoff)
                    nonNoise++;
            }
            return nonNoise;
        }

        public IList<RankedMI> Peaks { get { return _spectrum; } }

        public IEnumerable<RankedMI> PeaksRanked
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                {
                    if (rmi.Rank > 0)
                        yield return rmi;
                }
            }
        }

        public IEnumerable<RankedMI> PeaksMatched
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                {
                    if (rmi.Ordinal > 0)
                        yield return rmi;
                }
            }
        }

        public IEnumerable<double> MZs
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                    yield return rmi.ObservedMz;
            }
        }

        public IEnumerable<double> Intensities
        {
            get
            {
                foreach (RankedMI rmi in _spectrum)
                    yield return rmi.Intensity;
            }
        }

        public class RankParams
        {
            public string sequence { get; set; }
            public int precursorCharge { get; set; }
            public double precursorMz { get; set; }
            public double massPreMatch { get; set; }
            public double massPrePredict { get; set; }
            public double[,] massesMatch { get; set; }
            public double[,] massesPredict { get; set; }
            public IEnumerable<int> charges { get; set; }
            public IEnumerable<IonType> types { get; set; }
            public IEnumerable<int> rankCharges { get; set; }
            public IEnumerable<IonType> rankTypes { get; set; }
            public IStartFragmentFinder startFinder { get; set; }
            public IEndFragmentFinder endFinder { get; set; }
            public bool pro { get; set; }
            public bool gluasp { get; set; }
            public TransitionLibraryPick pick { get; set; }
            public double tolerance { get; set; }
            public double minMz { get; set; }
            public double maxMz { get; set; }
            public bool matchAll { get; set; }
            public bool matched { get; set; }
            private readonly HashSet<double> _seenMz = new HashSet<double>();
            private double _seenFirst;
            public bool IsSeen(double mz)
            {
                return _seenMz.Contains(mz);
            }
            public bool HasSeenOnce { get { return _seenFirst != 0; } }

            public void Seen(double mz)
            {
                if (matchAll && _seenFirst == 0)
                    _seenFirst = mz;
                else
                    _seenMz.Add(mz);
            }

            public void Clean()
            {
                if (_seenFirst != 0)
                    _seenMz.Add(_seenFirst);
                matched = false;
            }

            private int _rank = 1;
            public int Ranked { get { return _rank - 1;  }}
            public int RankNext() { return _rank++; }
        }

        public sealed class RankedMI
        {
            private SpectrumPeaksInfo.MI _mi;

            public static readonly RankedMI EMPTY = new RankedMI(new SpectrumPeaksInfo.MI(), 0);

            public RankedMI(SpectrumPeaksInfo.MI mi, int indexMz)
            {
                _mi = mi;

                IndexMz = indexMz;
            }

            public int Rank { get; private set; }

            public IonType IonType { get; private set; }
            public IonType IonType2 { get; private set; }

            public int Ordinal { get; private set; }
            public int Ordinal2 { get; private set; }

            public int Charge { get; private set; }
            public int Charge2 { get; private set; }

            public int IndexMz { get; private set; }

            public float Intensity { get { return _mi.Intensity; } }

            public double ObservedMz { get { return _mi.Mz; } }

            public double PredictedMz { get; private set; }

            public void CalculateRank(RankParams rp)
            {
                // Rank based on filtered range, if the settings use it in picking
                bool filter = (rp.pick == TransitionLibraryPick.filter);

                // Look for a predicted match within the acceptable tolerance
                int len = rp.massesMatch.GetLength(1);
                foreach (IonType type in rp.types)
                {
                    if (Transition.IsPrecursor(type))
                    {
                        if (!MatchNext(rp, type, len, rp.precursorCharge, len+1, filter, len, len))
                        {
                            // If matched return.  Otherwise look for other ion types.
                            if (rp.matched)
                            {
                                rp.Clean();
                                return;
                            }
                        }
                        continue;
                    }

                    foreach (int charge in rp.charges)
                    {
                        // Product ion charge can never be lower than precursor.
                        if (rp.precursorCharge < charge)
                            continue;

                        int start = 0, end = 0;
                        if (filter)
                        {
                            start = rp.startFinder.FindStartFragment(rp.massesMatch, type, charge, rp.precursorMz);
                            end = rp.endFinder.FindEndFragment(type, start, len);
                            if (Transition.IsCTerminal(type))
                                Helpers.Swap(ref start, ref end);
                        }

                        // These inner loops are performance bottlenecks, and the following
                        // code duplication proved the fastest implementation under a
                        // profiler.  Apparently .NET failed to inline an attempt to put
                        // the loop contents in a function.
                        if (Transition.IsCTerminal(type))
                        {
                            for (int i = len - 1; i >= 0; i--)
                            {
                                if (!MatchNext(rp, type, i, charge, len, filter, end, start))
                                {
                                    if (rp.matched)
                                    {
                                        rp.Clean();
                                        return;                                        
                                    }
                                    break;
                                }
                            }
                        }
                        else
                        {
                            for (int i = 0; i < len; i++)
                            {
                                if (!MatchNext(rp, type, i, charge, len, filter, end, start))
                                {
                                    if (rp.matched)
                                    {
                                        rp.Clean();
                                        return;
                                    }
                                    break;
                                }
                            }
                        }
                    }
                }
            }

            private bool MatchNext(RankParams rp, IonType type, int offset, int charge, int len, bool filter, int end, int start)
            {
                bool precursorMatch = Transition.IsPrecursor(type);
                double ionMass = !precursorMatch ? rp.massesMatch[(int)type, offset] : rp.massPreMatch;
                double ionMz = SequenceMassCalc.GetMZ(ionMass, charge);
                // Unless trying to match everything, stop looking outside the instrument range
                if (!rp.matchAll && ionMz > rp.maxMz)
                    return false;
                // Check filter properties, if apropriate
                if ((rp.matchAll || ionMz >= rp.minMz) && Math.Abs(ionMz - ObservedMz) < rp.tolerance)
                {
                    // Make sure each m/z value is only used for the most intense peak
                    // that is within the tolerance range.
                    if (rp.IsSeen(ionMz))
                        return true; // Keep looking
                    rp.Seen(ionMz);

                    int ordinal = Transition.OffsetToOrdinal(type, offset, len + 1);
                    // If this m/z aready matched a different ion, just remember the second ion.
                    if (Ordinal > 0)
                    {
                        IonType2 = type;
                        Charge2 = charge;
                        Ordinal2 = ordinal;
                        rp.matched = true;
                        return false;
                    }
                    else
                    {
                        // Avoid using the same predicted m/z on two different peaks
                        double predictedMass = !precursorMatch ?
                            rp.massesPredict[(int)type, offset] : rp.massPrePredict;
                        double predictedMz = SequenceMassCalc.GetMZ(predictedMass, charge);
                        if (predictedMz == ionMz || !rp.IsSeen(predictedMz))
                        {
                            rp.Seen(predictedMz);

                            if (!filter || (start <= offset && offset <= end) ||
                                (rp.pro && TransitionGroup.IsPro(rp.sequence, offset)) ||
                                (rp.gluasp && TransitionGroup.IsGluAsp(rp.sequence, offset)))
                            {
                                if (!rp.matchAll || (rp.minMz <= ionMz && ionMz <= rp.maxMz &&
                                                     rp.rankTypes.Contains(type) && rp.rankCharges.Contains(charge)))
                                    Rank = rp.RankNext();                                
                            }
                            IonType = type;
                            Charge = charge;
                            Ordinal = ordinal;
                            PredictedMz = predictedMz;
                            rp.matched = (!rp.matchAll);
                            return rp.matchAll;
                        }
                    }
                }
                // Stop looking once the mass has been passed
                return (ionMz <= ObservedMz);
            }
        }
/*
        private static int OrderMz(RankedMI mi1, RankedMI mi2)
        {
            return (mi1.ObservedMz.CompareTo(mi2.ObservedMz));
        }

        private static int OrderIntensityDesc(RankedMI mi1, RankedMI mi2)
        {
            return -(mi1.Intensity.CompareTo(mi2.Intensity));
        }
*/
        private static int OrderIntensityTryDesc(RankedMI mi1, RankedMI mi2)
        {
            float i1 = mi1.Intensity, i2 = mi2.Intensity;
            return (i1 > i2 ? -1 : (i1 < i2 ? 1 : 0));
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