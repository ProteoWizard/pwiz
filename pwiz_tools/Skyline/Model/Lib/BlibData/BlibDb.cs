/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using pwiz.BiblioSpec;
using pwiz.BiblioSpec.IO;
using pwiz.Common.Chemistry;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib.Midas;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Lib.BlibData
{
    /// <summary>
    /// Writes the BiblioSpec libraries Skyline makes of its own: a library from a list of spectra
    /// (an imported transition list, Koina, MIDAS, a library export) and a library minimized
    /// from another to the spectra a document uses, with its redundant companion when the source
    /// has one. Everything goes through <see cref="BlibWriter"/>, the one writer of the format,
    /// so a library written here is the same database BlibBuild writes; this class holds the
    /// conversion from Skyline's spectra, keys and header information to the writer's records.
    /// </summary>
    public class BlibDb : IDisposable
    {
        private static readonly Regex REGEX_LSID =
            new Regex(@"urn:lsid:([^:]*):spectral_library:bibliospec:[^:]*:([^:]*)");

        // Spectra converted to records in parallel between writes, which are sequential
        private const int SPECTRA_PER_BATCH = 1024;

        private IProgressMonitor ProgressMonitor { get; set; }
        private IProgressStatus _progressStatus;

        private BlibDb(String path)
        {
            FilePath = path;
            _progressStatus = new ProgressStatus(string.Empty);
        }

        public String FilePath { get; private set; }

        /// <summary>
        /// The library to be written at <paramref name="path"/>: nothing is on disk until
        /// <see cref="CreateLibraryFromSpectra(BiblioSpecLiteSpec,IList{SpectrumMzInfo},string,IProgressMonitor)"/>
        /// or a minimize completes it, and a library already there is replaced then.
        /// </summary>
        public static BlibDb CreateBlibDb(String path)
        {
            return new BlibDb(path);
        }

        public void Dispose()
        {
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
            const int majorVer = BlibWriter.MAJOR_VERSION;
            const int minorVer = BlibSchema.CurrentMinorVersion;
            string libId = libraryName;
            // Use a very specific LSID, since it really only matches this document.
            string libLsid = string.Format(@"urn:lsid:{0}:spectral_library:bibliospec:nr:minimal:{1}:{2}:{3}.{4}",
                libAuthority, libId, Guid.NewGuid(), majorVer, minorVer);

            var listLibrary = new List<BiblioLiteSpectrumInfo>();
            // Ion mobilities are stored by the index of the file in LibraryFiles, but the file
            // indexes are not known until all of the spectra have been inserted, so collect the
            // values by source file id here and index them below.
            var listIonMobilitiesByFileId = new List<IList<KeyValuePair<long, IonMobilityAndCCS>>>();
            var sourceFiles = new Dictionary<string, long>();

            // A library file that already exists keeps the tables another part of Skyline put
            // there (the iRT table of a document library), and gets its library tables anew.
            using (var writer = BlibWriter.Create(FilePath, libLsid, false, File.Exists(FilePath)))
            {
                writer.BeginBatch();
                int progressPercent = -1;
                for (int start = 0; start < listSpectra.Count; start += SPECTRA_PER_BATCH)
                {
                    // The records are made in parallel (the peak blobs compressed, the
                    // modifications parsed), then written in the order of the list.
                    int count = Math.Min(SPECTRA_PER_BATCH, listSpectra.Count - start);
                    var rows = new SpectrumRows[count];
                    ParallelEx.For(0, count, i => rows[i] = SpectrumRows.FromPeaks(listSpectra[start + i]), maxThreads: 4);
                    for (int i = 0; i < count; i++)
                    {
                        if (progressMonitor != null)
                        {
                            if (progressMonitor.IsCanceled)
                                return null;
                            int progressNew = (start + i) * 100 / listSpectra.Count;
                            if (progressPercent != progressNew)
                            {
                                progressMonitor.UpdateProgress(status = status.ChangePercentComplete(progressNew));
                                progressPercent = progressNew;
                            }
                        }

                        var row = rows[i];
                        long refId = row.Write(writer, sourceFiles);
                        var spectrum = listSpectra[start + i];
                        // The file's index in LibraryFiles is not known until the library is
                        // loaded back from the file which is being written here.
                        listLibrary.Add(new BiblioLiteSpectrumInfo(spectrum.Key,
                            row.Record.Copies,
                            row.Record.NumPeaks,
                            (int)refId,
                            null,
                            spectrum.Protein));
                        listIonMobilitiesByFileId.Add(row.IonMobilitiesByFileId);
                    }
                }

                writer.CommitBatch();
                writer.Complete();

                if (progressMonitor != null)
                {
                    progressMonitor.UpdateProgress(status = status.Complete());
                }
            }

            // The library will read its source files in id order, so that is the order that
            // determines each file's index in LibraryFiles.
            var fileIndexesById = new Dictionary<long, int>();
            foreach (var fileId in sourceFiles.Values.OrderBy(id => id))
            {
                fileIndexesById.Add(fileId, fileIndexesById.Count);
            }

            var libraryEntries = listLibrary.ToArray();
            for (int i = 0; i < libraryEntries.Length; i++)
            {
                var ionMobilitiesByFileId = listIonMobilitiesByFileId[i];
                if (ionMobilitiesByFileId.Count == 0)
                {
                    continue;
                }

                libraryEntries[i] = libraryEntries[i].ChangeIonMobilities(ionMobilitiesByFileId
                    .Select(kvp => new KeyValuePair<int, IonMobilityAndCCS>(fileIndexesById[kvp.Key], kvp.Value))
                    .ToIndexedMultiArray());
            }

            return new BiblioSpecLiteLibrary(librarySpec, libLsid, majorVer, minorVer, libraryEntries, FileStreamManager.Default);
        }

        /// <summary>
        /// The rows of one spectrum from a list: its record, with the peaks encoded and the
        /// modifications parsed, and its observations, each naming its source file by name until
        /// the writer assigns the file its id.
        /// </summary>
        private class SpectrumRows
        {
            private readonly string _sourceFile;
            private readonly List<Observation> _observations = new List<Observation>();

            private SpectrumRows(BlibSpectrumRecord record, string sourceFile)
            {
                Record = record;
                _sourceFile = sourceFile;
            }

            public BlibSpectrumRecord Record { get; }

            /// <summary>The observations with an ion mobility, by the id of their source file in the library.</summary>
            public IList<KeyValuePair<long, IonMobilityAndCCS>> IonMobilitiesByFileId { get; } = new List<KeyValuePair<long, IonMobilityAndCCS>>();

            public static SpectrumRows FromPeaks(SpectrumMzInfo spectrum)
            {
                var peaksInfo = spectrum.SpectrumPeaks;
                var smallMoleculeAttributes = spectrum.SmallMoleculeLibraryAttributes ?? SmallMoleculeLibraryAttributes.EMPTY;
                var isProteomic = smallMoleculeAttributes.IsEmpty;
                var ionMobility = spectrum.IonMobility ?? IonMobilityAndCCS.EMPTY;

                var record = new BlibSpectrumRecord
                {
                    PeptideSeq = isProteomic ? FastaSequence.StripModifications(spectrum.Key.Target).Sequence : string.Empty,
                    PrecursorMz = spectrum.PrecursorMz,
                    PrecursorCharge = spectrum.Key.Charge,
                    PrecursorAdduct = spectrum.Key.Adduct.AsFormula(), // [M+...] format, even for proteomic charges
                    MoleculeName = smallMoleculeAttributes.MoleculeName ?? string.Empty,
                    ChemicalFormula = smallMoleculeAttributes.ChemicalFormula ?? string.Empty,
                    InchiKey = smallMoleculeAttributes.InChiKey ?? string.Empty,
                    OtherKeys = smallMoleculeAttributes.OtherKeys ?? string.Empty,
                    PeptideModSeq = isProteomic ? spectrum.Key.Target.Sequence : string.Empty,
                    Copies = 1,
                    RetentionTime = spectrum.RetentionTime.GetValueOrDefault(),
                    IonMobility = ionMobility.IonMobility.Mobility,
                    IonMobilityType = (int)ionMobility.IonMobility.Units,
                    IonMobilityHighEnergyOffset = ionMobility.HighEnergyIonMobilityValueOffset,
                    CollisionalCrossSectionSqA = ionMobility.CollisionalCrossSectionSqA,
                    SpecIdInFile = null,
                    Score = 0.0,
                    ScoreType = BlibWriter.SCORE_TYPE_UNKNOWN,
                    Proteins = ProteinsOf(spectrum.Protein),
                };
                SetPeaks(record, peaksInfo);
                record.Modifications = ModsFromModifiedSequence(record.PeptideModSeq);

                var rows = new SpectrumRows(record, spectrum.SourceFile);
                if (spectrum.RetentionTimes != null && spectrum.RetentionTimes.Any())
                {
                    foreach (var rt in spectrum.RetentionTimes)
                    {
                        if (string.IsNullOrEmpty(rt.SourceFile))
                            throw new InvalidDataException(@"Spectrum must have a source file");
                        rows._observations.Add(new Observation
                        {
                            SourceFile = rt.SourceFile,
                            IsBest = rt.IsBest,
                            RetentionTime = rt.RetentionTime,
                            IonMobility = rt.IonMobility.IonMobility.Mobility,
                            IonMobilityType = (int)rt.IonMobility.IonMobility.Units,
                            IonMobilityHighEnergyOffset = rt.IonMobility.HighEnergyIonMobilityValueOffset,
                            CollisionalCrossSectionSqA = rt.IonMobility.CollisionalCrossSectionSqA,
                            RedundantId = -1
                        });
                    }
                }
                else if (spectrum.RetentionTime.HasValue)
                {
                    rows._observations.Add(new Observation
                    {
                        SourceFile = spectrum.SourceFile,
                        IsBest = true,
                        RetentionTime = record.RetentionTime,
                        IonMobility = record.IonMobility,
                        IonMobilityType = record.IonMobilityType,
                        IonMobilityHighEnergyOffset = record.IonMobilityHighEnergyOffset,
                        CollisionalCrossSectionSqA = record.CollisionalCrossSectionSqA,
                        RedundantId = -1
                    });
                }
                return rows;
            }

            /// <summary>Writes the spectrum and its observations, and returns the spectrum's id.</summary>
            public long Write(BlibWriter writer, IDictionary<string, long> sourceFiles)
            {
                Record.FileId = GetSpectrumSourceId(writer, _sourceFile, sourceFiles);
                long refId = writer.AddSpectrum(Record);
                foreach (var observation in _observations)
                {
                    long fileId = GetSpectrumSourceId(writer, observation.SourceFile, sourceFiles);
                    observation.Write(writer, refId, fileId);
                    if (observation.IonMobilityType != BlibWriter.ION_MOBILITY_TYPE_NONE)
                    {
                        var ionMobilityValue = IonMobilityValue.GetIonMobilityValue(observation.IonMobility, (eIonMobilityUnits)observation.IonMobilityType);
                        var ionMobilityAndCCS = IonMobilityAndCCS.GetIonMobilityAndCCS(ionMobilityValue, observation.CollisionalCrossSectionSqA, observation.IonMobilityHighEnergyOffset);
                        IonMobilitiesByFileId.Add(new KeyValuePair<long, IonMobilityAndCCS>(fileId, ionMobilityAndCCS));
                    }
                }
                return refId;
            }
        }

        /// <summary>
        /// One <c>RetentionTimes</c> row to be written: an observation of a spectrum in a source
        /// file, with the ion mobility seen there, and for a minimized library the observation's
        /// own spectrum in the source library (<see cref="Key"/>) and in the redundant library
        /// being written (<see cref="RedundantId"/>).
        /// </summary>
        private class Observation
        {
            public string SourceFile { get; set; }
            public bool IsBest { get; set; }
            public double? RetentionTime { get; set; }
            public double? IonMobility { get; set; }
            public int IonMobilityType { get; set; } = BlibWriter.ION_MOBILITY_TYPE_NONE;
            public double? IonMobilityHighEnergyOffset { get; set; }
            public double? CollisionalCrossSectionSqA { get; set; }
            public long RedundantId { get; set; }
            /// <summary>The source file's id in the library being written, once it has one.</summary>
            public long SourceFileId { get; set; }
            public SpectrumLiteKey Key { get; set; }

            public void Write(BlibWriter writer, long refId, long sourceFileId)
            {
                writer.AddRetentionTime(refId, sourceFileId, RetentionTime, null, null, 0, IsBest, IonMobility,
                    CollisionalCrossSectionSqA, IonMobilityHighEnergyOffset, IonMobilityType, RedundantId);
            }
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
            int libraryRevision = BlibWriter.MAJOR_VERSION;
            // The writer writes the current schema whatever the source library's was
            const int schemaVersion = BlibSchema.CurrentMinorVersion;

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

                // If the document has MS1 filtering enabled we will save a minimized version
                // of the redundant library, if available.
                if (document.Settings.TransitionSettings.FullScan.IsEnabledMs)
                {
                    saveRedundantLib = File.Exists(blibLib.FilePathRedundant);
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

            // Hash table to store the score types in the library
            var dictScoreTypes = new Dictionary<string, int>();

            BlibWriter redundantWriter = null;

            var dataFiles = library.LibraryDetails.DataFiles.ToArray();
            // The precursors to minimize, found once: the optional columns a library gets must be
            // in place before the rows that carry values, so the values are surveyed first.
            var precursors = GetLibraryPrecursors(document, library, smallMoleculeConversionMap).ToList();
            // A library with protein-group q-values keeps its own groups, which carry them
            bool keepLibraryProteinGroups = library.GlobalQValueLevels.Contains(GlobalQValueLevel.protein);

            try
            {
                using (var writer = BlibWriter.Create(FilePath, libLsid))
                {
                    writer.SetRevision(libraryRevision);
                    if (saveRetentionTimes)
                        AddOptionalColumns(writer, precursors, keepLibraryProteinGroups);
                    writer.BeginBatch();
                    int savedCount = 0;
                    foreach (var precursor in precursors)
                    {
                        if (dictLibrary.ContainsKey(precursor.NewLibKey))
                            continue;

                        var nodeGroup = precursor.NodeGroup;
                        var libKey = precursor.LibKey;
                        var labelType = nodeGroup.TransitionGroup.LabelType;
                        // saveRetentionTimes will be false unless this is a BiblioSpec(schemaVersion >=1) library.
                        if (!saveRetentionTimes)
                        {
                            // get the best spectra
                            foreach (var spectrumInfo in library.GetSpectra(libKey, labelType,
                                         LibraryRedundancy.best))
                            {
                                var record = CreateRecord(precursor);
                                FillRecord(writer, convertingToSmallMolecules, spectrumInfo, record, dictFiles, dictScoreTypes,
                                    GetSpectrumSourceFile(spectrumInfo.FilePath, dataFiles));
                                long refId = writer.AddSpectrum(record);

                                dictLibrary.Add(precursor.NewLibKey,
                                    new BiblioLiteSpectrumInfo(precursor.NewLibKey, record.Copies,
                                        record.NumPeaks,
                                        (int) refId,
                                        null,
                                        precursor.ProteinName));
                            }
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

                            var record = CreateRecord(precursor);

                            // Get all the information for this reference spectrum.
                            // For BiblioSpec (schema ver >= 1), this can include retention time information
                            // for this spectrum as well as any redundant spectra for the peptide.
                            // Ids of spectra in the redundant library, where available, are also returned.
                            var observations = new List<Observation>();
                            BuildRefSpectra(writer, convertingToSmallMolecules, record, spectra,
                                dictFiles, dictScoreTypes, observations, dataFiles);

                            var globalScores = precursor.GlobalScores;
                            SetGlobalScores(record, globalScores);
                            long refId = writer.AddSpectrum(record);
                            foreach (var observation in observations)
                                observation.Write(writer, refId, observation.SourceFileId);

                            // The peptide-protein tables
                            if (keepLibraryProteinGroups)
                            {
                                foreach (var proteinGroup in globalScores.ProteinGroups)
                                    writer.AddSpectrumProtein(refId, writer.AddProtein(proteinGroup.Name, proteinGroup.QValue, proteinGroup.Pep));
                            }
                            else if (!string.IsNullOrEmpty(precursor.ProteinName))
                            {
                                writer.AddSpectrumProtein(refId, writer.AddProtein(precursor.ProteinName));
                            }

                            dictLibrary.Add(precursor.NewLibKey,
                                new BiblioLiteSpectrumInfo(precursor.NewLibKey,
                                    record.Copies,
                                    record.NumPeaks,
                                    (int)refId,
                                    null,
                                    precursor.ProteinName));

                            // Save entries in the redundant library.
                            var redundantObservations = observations.Where(observation => observation.Key != null).ToList();
                            if (saveRedundantLib && redundantObservations.Count > 0)
                            {
                                if (redundantWriter == null)
                                {
                                    redundantWriter = BlibWriter.Create(BiblioSpecLiteSpec.GetRedundantName(FilePath),
                                        libLsid.Replace(@":nr:", @":redundant:"), true);
                                    redundantWriter.SetRevision(libraryRevision);
                                    redundantWriter.BeginBatch();
                                }

                                SaveRedundantSpectra(redundantWriter, redundantObservations, dictFilesRedundant,
                                    record, library, setRedundantSpectraIds);
                            }
                        }

                        savedCount++;
                        if (!UpdateProgress(precursors.Count, savedCount))
                            return null;
                    }

                    writer.CommitBatch();
                    writer.Complete();

                    if (redundantWriter != null)
                    {
                        redundantWriter.CommitBatch();
                        redundantWriter.Complete();
                    }
                }
            }
            finally
            {
                redundantWriter?.Dispose();
            }

            var libraryEntries = dictLibrary.Values.ToArray();

            return new BiblioSpecLiteLibrary(librarySpec, libLsid, libraryRevision, schemaVersion,
                libraryEntries, FileStreamManager.Default);
        }

        /// <summary>
        /// A precursor of the document whose library information comes from the library being
        /// minimized, with the keys and names the minimized spectrum is written under.
        /// </summary>
        private class LibraryPrecursor
        {
            public TransitionGroupDocNode NodeGroup { get; set; }
            public string ProteinName { get; set; }
            public Target PeptideSeq { get; set; }
            public Target PeptideModSeq { get; set; }
            public Adduct PrecursorAdduct { get; set; }
            public SmallMoleculeLibraryAttributes SmallMoleculeAttributes { get; set; }
            /// <summary>The key the spectrum has in the source library.</summary>
            public LibKey LibKey { get; set; }
            /// <summary>The key the spectrum gets in the minimized library: the same, unless converting to small molecules.</summary>
            public LibKey NewLibKey { get; set; }
            /// <summary>The source library's global scores for the key, looked up once.</summary>
            public LibraryGlobalScores GlobalScores { get; set; }
        }

        /// <summary>
        /// The precursors of <paramref name="document"/> that take their library information from
        /// <paramref name="library"/>, in document order. One being converted to a small molecule
        /// that the map does not name is a decoy, and is left out.
        /// </summary>
        private static IEnumerable<LibraryPrecursor> GetLibraryPrecursors(SrmDocument document, Library library,
            IDictionary<LibKey, LibKey> smallMoleculeConversionMap)
        {
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

                        if (smallMoleculeConversionMap != null)
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

                        yield return new LibraryPrecursor
                        {
                            NodeGroup = nodeGroup,
                            ProteinName = proteinName,
                            PeptideSeq = peptideSeq,
                            PeptideModSeq = peptideModSeq,
                            PrecursorAdduct = precursorAdduct,
                            SmallMoleculeAttributes = smallMoleculeAttributes,
                            LibKey = libKey,
                            NewLibKey = newLibKey,
                            GlobalScores = library.GetGlobalScores(libKey)
                        };
                    }
                }
            }
        }

        /// <summary>
        /// Adds the optional columns (global q-values and PEPs) that some precursor of the
        /// document has a value for in the library, before any row is written: a column that
        /// would be NULL throughout is not added.
        /// </summary>
        private static void AddOptionalColumns(BlibWriter writer, IEnumerable<LibraryPrecursor> precursors,
            bool keepLibraryProteinGroups)
        {
            bool precursorQValue = false, precursorPep = false, peptideQValue = false, peptidePep = false;
            bool proteinQValue = false, proteinPep = false;
            foreach (var precursor in precursors)
            {
                var globalScores = precursor.GlobalScores;
                precursorQValue |= globalScores.PrecursorQValue.HasValue;
                precursorPep |= globalScores.PrecursorPep.HasValue;
                peptideQValue |= globalScores.PeptideQValue.HasValue;
                peptidePep |= globalScores.PeptidePep.HasValue;
                if (keepLibraryProteinGroups)
                {
                    proteinQValue |= globalScores.ProteinGroups.Any(group => group.QValue.HasValue);
                    proteinPep |= globalScores.ProteinGroups.Any(group => group.Pep.HasValue);
                }
            }
            if (precursorQValue)
                writer.AddOptionalColumn(BlibSchema.PrecursorQValue);
            if (precursorPep)
                writer.AddOptionalColumn(BlibSchema.PrecursorPep);
            if (peptideQValue)
                writer.AddOptionalColumn(BlibSchema.PeptideQValue);
            if (peptidePep)
                writer.AddOptionalColumn(BlibSchema.PeptidePep);
            if (proteinQValue)
                writer.AddOptionalColumn(BlibSchema.ProteinQValue);
            if (proteinPep)
                writer.AddOptionalColumn(BlibSchema.ProteinPep);
        }

        private static void SetGlobalScores(BlibSpectrumRecord record, LibraryGlobalScores globalScores)
        {
            record.PrecursorQValue = globalScores.PrecursorQValue;
            record.PrecursorPep = globalScores.PrecursorPep;
            record.PeptideQValue = globalScores.PeptideQValue;
            record.PeptidePep = globalScores.PeptidePep;
        }

        private static BlibSpectrumRecord CreateRecord(LibraryPrecursor precursor)
        {
            var smallMoleculeAttributes = precursor.SmallMoleculeAttributes;
            return new BlibSpectrumRecord
            {
                PeptideSeq = precursor.PeptideSeq.IsProteomic ? precursor.PeptideSeq.Sequence : string.Empty,
                PrecursorMz = precursor.NodeGroup.PrecursorMz,
                PrecursorCharge = precursor.PrecursorAdduct.AdductCharge,
                PrecursorAdduct = precursor.PrecursorAdduct.AsFormula(), // [M+...] format, even for proteomic charges
                MoleculeName = smallMoleculeAttributes.MoleculeName ?? string.Empty,
                ChemicalFormula = smallMoleculeAttributes.ChemicalFormula ?? string.Empty,
                InchiKey = smallMoleculeAttributes.InChiKey ?? string.Empty,
                OtherKeys = smallMoleculeAttributes.OtherKeys ?? string.Empty,
                PeptideModSeq = precursor.PeptideModSeq.IsProteomic ? precursor.PeptideModSeq.Sequence : string.Empty
            };
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

        private bool UpdateProgress(int totalPrecursorCount, int doneCount)
        {
            if (ProgressMonitor != null)
            {
                if (ProgressMonitor.IsCanceled)
                    return false;

                int progressValue = totalPrecursorCount == 0 ? 100 : doneCount * 100 / totalPrecursorCount;

                ProgressMonitor.UpdateProgress(_progressStatus = _progressStatus.ChangePercentComplete(progressValue));
            }
            return true;
        }

        private static void SaveRedundantSpectra(BlibWriter writerRedundant,
                                                 IEnumerable<Observation> redundantObservations,
                                                 IDictionary<string, long> dictFiles,
                                                 BlibSpectrumRecord record,
                                                 Library library,
                                                 ISet<int> savedSpectraIds)
        {
            foreach (var observation in redundantObservations)
            {
                var specLiteKey = observation.Key;
                if (specLiteKey.RedundantId == 0)
                {
                    continue;
                }
                if (!savedSpectraIds.Add(specLiteKey.RedundantId))
                {
                    continue;
                }
                // If this source file has already been saved, get its database Id.
                // Otherwise, save it.
                long spectrumSourceId = GetSpectrumSourceId(writerRedundant, observation.SourceFile, dictFiles);

                // Get peaks for the redundant spectrum
                var peaksInfo = library.LoadSpectrum(specLiteKey);

                var redundantSpectrum = new BlibSpectrumRecord
                {
                    Id = specLiteKey.RedundantId,
                    PeptideSeq = record.PeptideSeq,
                    PrecursorMz = record.PrecursorMz,
                    PrecursorCharge = record.PrecursorCharge,
                    PrecursorAdduct = record.PrecursorAdduct,
                    MoleculeName = record.MoleculeName,
                    ChemicalFormula = record.ChemicalFormula,
                    InchiKey = record.InchiKey,
                    OtherKeys = record.OtherKeys,
                    PeptideModSeq = record.PeptideModSeq,
                    Copies = record.Copies,
                    RetentionTime = observation.RetentionTime,
                    IonMobility = observation.IonMobility,
                    IonMobilityHighEnergyOffset = observation.IonMobilityHighEnergyOffset,
                    CollisionalCrossSectionSqA = observation.CollisionalCrossSectionSqA,
                    IonMobilityType = observation.IonMobilityType,
                    FileId = spectrumSourceId
                };
                SetPeaks(redundantSpectrum, peaksInfo);
                // The redundant library keeps the ids of the source library's spectra, which the
                // minimized library's observations refer to.
                writerRedundant.AddSpectrum(redundantSpectrum, true);
            }
        }

        private void BuildRefSpectra(BlibWriter writer,
            bool convertingToSmallMolecules,
            BlibSpectrumRecord record,
            SpectrumInfo[] spectra, // Yes, this could be IEnumerable, but then Resharper throws bogus warnings about possible multiple enumeration
            IDictionary<string, long> dictFiles,
            IDictionary<string, int> dictScoreTypes,
            ICollection<Observation> observations,
            SpectrumSourceFileDetails[] dataFiles)
        {
            bool foundBestSpectrum = false;

            foreach(SpectrumInfoLibrary spectrum in spectra)
            {
                var spectrumSourceFile = GetSpectrumSourceFile(spectrum.FilePath, dataFiles);
                if(spectrum.IsBest)
                {
                    if(foundBestSpectrum)
                    {
                        throw new InvalidDataException(
                            string.Format(BlibDataResources.BlibDb_BuildRefSpectra_Multiple_reference_spectra_found_for_peptide__0__in_the_library__1__,
                                          record.PeptideModSeq, FilePath));
                    }

                    foundBestSpectrum = true;

                    FillRecord(writer, convertingToSmallMolecules, spectrum, record, dictFiles, dictScoreTypes, spectrumSourceFile);
                }

                // Determine if this spectrum is from a file that is in the document.
                // If it is not, do not save the retention time for this spectrum, and do not
                // add it to the redundant library. However, if this is the reference (best) spectrum
                // we must save its retention time.
                // NOTE: Spectra not used in the results get used for too much now for this to be useful

                // If this source file has already been saved, get its database Id.
                // Otherwise, save it.
                long spectrumSourceId = GetSpectrumSourceId(writer, spectrum.FilePath, dictFiles,
                    spectrum.SpectrumHeaderInfo?.ScoreType, spectrumSourceFile);

                // spectrumKey in the SpectrumInfo is an integer for reference(best) spectra,
                // or object of type SpectrumLiteKey for redundant spectra
                object key = spectrum.SpectrumKey;
                var specLiteKey = key as SpectrumLiteKey;

                var observation = new Observation
                {
                    SourceFile = spectrum.FilePath,
                    SourceFileId = spectrumSourceId,
                    RedundantId = specLiteKey?.RedundantId ?? 0,
                    RetentionTime = spectrum.RetentionTime,
                    IsBest = spectrum.IsBest,
                    Key = specLiteKey
                };
                if (null != spectrum.IonMobilityInfo && spectrum.IonMobilityInfo.HasIonMobilityValue)
                {
                    observation.CollisionalCrossSectionSqA = spectrum.IonMobilityInfo.CollisionalCrossSectionSqA.GetValueOrDefault();
                    observation.IonMobilityType = (int) spectrum.IonMobilityInfo.IonMobility.Units;
                    observation.IonMobility = spectrum.IonMobilityInfo.IonMobility.Mobility.GetValueOrDefault(); // Get the low energy value
                    observation.IonMobilityHighEnergyOffset = spectrum.IonMobilityInfo.HighEnergyIonMobilityValueOffset;
                }

                observations.Add(observation);
            }
        }

        private static SpectrumSourceFileDetails GetSpectrumSourceFile(string filePath, SpectrumSourceFileDetails[] dataFiles)
        {
            return string.IsNullOrEmpty(filePath) || dataFiles.IsNullOrEmpty()
                ? null
                : dataFiles.FirstOrDefault(df => string.Equals(filePath, df.FilePath));
        }

        /// <summary>
        /// Fills the record from the spectrum: its peaks, annotations, ion mobility, retention
        /// time, source file and score, and the modifications of its sequence.
        /// </summary>
        private static void FillRecord(BlibWriter writer, bool convertingToSmallMolecules, SpectrumInfoLibrary spectrum, BlibSpectrumRecord record,
            IDictionary<string, long> dictFiles, IDictionary<string, int> dictScoreTypes, SpectrumSourceFileDetails spectrumSourceFile)
        {
            int copies = (int)spectrum.SpectrumHeaderInfo.GetRankValue(LibrarySpec.PEP_RANK_COPIES);
            var peaksInfo = spectrum.SpectrumPeaksInfo;

            record.Copies = copies;
            SetPeaks(record, peaksInfo);

            if (null != spectrum.IonMobilityInfo)
            {
                record.CollisionalCrossSectionSqA = spectrum.IonMobilityInfo.CollisionalCrossSectionSqA;
                record.IonMobilityType = (int)spectrum.IonMobilityInfo.IonMobility.Units;
                record.IonMobility = spectrum.IonMobilityInfo.IonMobility.Mobility;
                record.IonMobilityHighEnergyOffset = spectrum.IonMobilityInfo.HighEnergyIonMobilityValueOffset;
            }

            record.RetentionTime = spectrum.RetentionTime.GetValueOrDefault();

            record.FileId = spectrum.FilePath != null
                ? GetSpectrumSourceId(writer, spectrum.FilePath, dictFiles,
                    spectrum.SpectrumHeaderInfo?.ScoreType, spectrumSourceFile)
                : 0;
            record.SpecIdInFile = null;
            record.Score = spectrum.SpectrumHeaderInfo?.Score ?? 0.0;
            record.ScoreType = GetScoreTypeId(writer, spectrum.SpectrumHeaderInfo?.ScoreType, dictScoreTypes, spectrumSourceFile);
            if (convertingToSmallMolecules || !string.IsNullOrEmpty(record.MoleculeName))
            {
                record.PeptideSeq = string.Empty;
                record.PeptideModSeq = string.Empty;
                Assume.IsTrue(!string.IsNullOrEmpty(record.MoleculeName));
            }

            record.Modifications = ModsFromModifiedSequence(record.PeptideModSeq);
        }

        private static long GetSpectrumSourceId(BlibWriter writer, string filePath, IDictionary<string, long> dictFiles)
        {
            return GetSpectrumSourceId(writer, filePath, dictFiles, null, null);
        }

        /// <summary>
        /// The id of the source file row for <paramref name="filePath"/>, added on first use with
        /// the threshold and workflow the source library reports for it; 0 for no file.
        /// </summary>
        private static long GetSpectrumSourceId(BlibWriter writer, string filePath, IDictionary<string, long> dictFiles,
           string scoreName, SpectrumSourceFileDetails spectrumSourceFile)
        {
            if (string.IsNullOrEmpty(filePath))
                return 0;
            if (!dictFiles.TryGetValue(filePath, out var spectrumSourceId))
            {
                spectrumSourceId = writer.AddSourceFile(filePath, spectrumSourceFile?.IdFilePath,
                    spectrumSourceFile?.GetScoreTypeCutoff(scoreName),
                    (int)(spectrumSourceFile?.WorkflowType ?? WorkflowType.DDA));
                dictFiles.Add(filePath, spectrumSourceId);
            }
            return spectrumSourceId;
        }

        private static int GetScoreTypeId(BlibWriter writer, string scoreName, IDictionary<string, int> dictScoreTypes, SpectrumSourceFileDetails spectrumSourceFile)
        {
            if (string.IsNullOrEmpty(scoreName))
                return BlibWriter.SCORE_TYPE_UNKNOWN;
            if (dictScoreTypes.TryGetValue(scoreName, out var savedScoreTypeId))
                return savedScoreTypeId;

            var scoreType = spectrumSourceFile?.GetScoreType(scoreName) ?? new ScoreType(scoreName, null);
            var scoreTypeId = writer.AddScoreType(scoreType.NameInvariant, scoreType.ProbabilityTypeDescription);
            dictScoreTypes.Add(scoreType.NameInvariant, scoreTypeId);

            return scoreTypeId;
        }

        /// <summary>
        /// Reads modifications from a sequence with embedded modifications,
        /// e.g. AM[16.0]VLC[57.0]
        /// This results in some loss of precision, since embedded modifications
        /// are only accurate to one decimal place.  But this is all that is necessary
        /// for further use as spectral libraries for SRM method building.
        /// </summary>
        private static IReadOnlyList<BlibModification> ModsFromModifiedSequence(string modSeq)
        {
            var modifications = new List<BlibModification>();
            int iAa = 0;
            for (int i = 0; i < modSeq.Length; i++)
            {
                char c = modSeq[i];
                if (c != '[')
                {
                    iAa++;
                    continue;
                }
                int iEnd = modSeq.IndexOf(']', ++i);
                if (iEnd < 0)
                    break;
                if (double.TryParse(modSeq.Substring(i, iEnd - i), NumberStyles.Float, CultureInfo.InvariantCulture, out double modMass))
                {
                    // The position of the residue the bracket follows (1-based)
                    modifications.Add(new BlibModification(iAa, modMass));
                }
                i = iEnd;
            }
            return modifications;
        }

        private static IReadOnlyList<string> ProteinsOf(string protein)
        {
            return string.IsNullOrEmpty(protein) ? Array.Empty<string>() : new[] { protein };
        }

        /// <summary>The peaks of the spectrum as the record's blobs and annotations, with their count.</summary>
        private static void SetPeaks(BlibSpectrumRecord record, SpectrumPeaksInfo peaksInfo)
        {
            var peaks = peaksInfo.Peaks;
            var mzs = new double[peaks.Length];
            var intensities = new float[peaks.Length];
            for (int i = 0; i < peaks.Length; i++)
            {
                mzs[i] = peaks[i].Mz;
                intensities[i] = peaks[i].Intensity;
            }
            record.NumPeaks = peaks.Length;
            record.MzBlob = BlibWriter.CompressMzs(mzs);
            record.IntensityBlob = BlibWriter.CompressIntensities(intensities);
            record.Annotations = AnnotationsOf(peaksInfo);
        }

        /// <summary>
        /// The annotations of the spectrum's peaks as the library's rows. Each peak may have more
        /// than one, and a peptide spectrum usually has none, Skyline generating its own.
        /// </summary>
        private static IReadOnlyList<BlibPeakAnnotation> AnnotationsOf(SpectrumPeaksInfo peaks)
        {
            List<BlibPeakAnnotation> annotations = null;
            int i = 0;
            foreach (var peak in peaks.Peaks)
            {
                if (peak.Annotations != null)
                {
                    foreach (var annotation in peak.Annotations.Where(a => !SpectrumPeakAnnotation.IsNullOrEmpty(a)))
                    {
                        annotations ??= new List<BlibPeakAnnotation>();
                        var ion = annotation.Ion;
                        annotations.Add(new BlibPeakAnnotation(i, ion.Name, ion.Adduct.AdductCharge, ion.MonoisotopicMassMz, peak.Mz,
                            ion.NeutralFormula, ion.AccessionNumbers.GetInChiKey(), ion.AccessionNumbers.GetNonInChiKeys(),
                            ion.Adduct.ToString(), annotation.Comment));
                    }
                }
                i++;
            }
            return annotations ?? (IReadOnlyList<BlibPeakAnnotation>)Array.Empty<BlibPeakAnnotation>();
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
                        // The minimized library's spectrum ids and peak counts are those of the
                        // file written here, so its spec must name that file, even for a
                        // document library
                        // ReSharper disable once PossibleNullReferenceException
                        if (!Equals(librarySpecMin.FilePath, blibDb.FilePath))
                            librarySpecMin = (BiblioSpecLiteSpec)librarySpecMin.ChangeFilePath(blibDb.FilePath);

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
