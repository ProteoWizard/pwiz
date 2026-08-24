/*
 * Original author: Brian Pratt <bspratt .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2026 University of Washington - Seattle, WA
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using pwiz.Common.DataBinding;
using pwiz.Common.Spectra;
using pwiz.CommonMsData;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.Model.Results.Spectra
{
    /// <summary>
    /// Reports which of the columns the spectrum filter editor offers are actually answerable for a given
    /// document's data, so the editor can mark those and leave the rest plain. Filtering on collision
    /// energy in data that records none is as empty an act as filtering on a CV term no file carries, so
    /// both kinds of column are judged the same way here: a column is worth offering if some spectrum
    /// gives it a value.
    ///
    /// The two kinds are answered from different places, though.
    ///
    /// The interpreted columns (<see cref="SpectrumClassColumn.ALL"/>) read typed fields that import always
    /// records, so the per-file metadata in the .skyd answers them outright, with no file access at all.
    ///
    /// The CV/user parameters are only persisted when the document already filters on them (see
    /// <see cref="SpectraChromDataProvider"/>), so a document that has never carried a CV filter - exactly
    /// the document whose author is about to write their first one - has nothing stored, and the files
    /// themselves have to be read.
    ///
    /// Two approximations keep that read affordable. Only the head of a file is examined, on the assumption
    /// that a run does not start emitting a new term partway through. And only one file per distinct
    /// <see cref="FileSignature"/> is opened, because which terms appear is a property of what wrote the
    /// file - the vendor format, the instrument, the conversion that produced it - not of the sample in it.
    /// A hundred replicates off one instrument therefore cost one file open, not a hundred.
    /// </summary>
    public static class SpectrumColumnScanner
    {
        /// <summary>
        /// How many spectra of a file to consider, whether read from it or taken from its stored metadata.
        /// A value that appears at all generally appears from the start, so the head of a run answers the
        /// question without walking a set of spectra that can run to the millions.
        /// </summary>
        public const int MAX_SPECTRA_PER_FILE = 100;

        /// <summary>
        /// How many distinct file signatures to examine before giving up on the rest. Grouping costs
        /// nothing, so this only bites on a genuinely heterogeneous document, where the alternative is an
        /// open per instrument with no bound at all.
        /// </summary>
        public const int MAX_FILES_SCANNED = 8;

        /// <summary>
        /// CV accessions already learned by reading, keyed by <see cref="FileSignature"/>. Held across
        /// documents on the premise behind the signature itself: the same writer yields the same terms, so
        /// a file read for one document answers for another. Never invalidated - re-converting a file with
        /// different settings, under an unchanged instrument configuration, would go unnoticed until
        /// restart. The cost of being wrong is a mark on a term the file no longer carries.
        /// </summary>
        private static readonly Dictionary<string, HashSet<string>> ACCESSIONS_BY_SIGNATURE =
            new Dictionary<string, HashSet<string>>();

        /// <summary>
        /// What a scan established about a document's columns. The editor marks a column as unanswerable,
        /// so it matters which columns were actually determined: saying nothing about a column we never
        /// learned anything about is right, while marking it would assert an absence we did not observe.
        /// The two kinds are tracked apart because they are learned from different places and one can
        /// succeed while the other fails - a document shared without its raw files still answers every
        /// interpreted property from the .skyd, and answers nothing about CV terms it never captured.
        /// </summary>
        /// <summary>
        /// What a scan was able to say about one column.
        /// </summary>
        public enum Standing
        {
            /// <summary>Nothing was established - no results, or the data could not be examined.</summary>
            Undetermined,
            /// <summary>This data has values for the column.</summary>
            Answerable,
            /// <summary>This data was examined for the column and has nothing to match on.</summary>
            Unanswerable
        }

        public class Availability
        {
            public static readonly Availability UNKNOWN = new Availability(new HashSet<PropertyPath>(), false, false);

            public Availability(HashSet<PropertyPath> answerable, bool interpretedDetermined, bool cvDetermined)
            {
                Answerable = answerable;
                InterpretedDetermined = interpretedDetermined;
                CvDetermined = cvDetermined;
            }

            private HashSet<PropertyPath> Answerable { get; }
            private bool InterpretedDetermined { get; }
            private bool CvDetermined { get; }

            /// <summary>
            /// Where a column stands for this data. Three states rather than two, because "we looked and
            /// this data does not have it" and "we never established anything" are different claims and a
            /// caller presenting them identically would assert the first while only entitled to the second.
            ///
            /// Takes <paramref name="isCvColumn"/> from the caller rather than resolving the path: the
            /// filter editor holds a <see cref="SpectrumClassColumn"/> only for the CV columns, so a
            /// column-shaped argument would read every interpreted property as null and never mark one.
            /// </summary>
            public Standing GetStanding(PropertyPath propertyPath, bool isCvColumn)
            {
                if (propertyPath == null)
                {
                    return Standing.Undetermined;
                }
                if (Answerable.Contains(propertyPath))
                {
                    // Only ever populated for a column that was examined, so this needs no flag check.
                    return Standing.Answerable;
                }
                return (isCvColumn ? CvDetermined : InterpretedDetermined)
                    ? Standing.Unanswerable
                    : Standing.Undetermined;
            }
        }

        /// <summary>
        /// Empties the learned accessions. They are deliberately never invalidated in normal use, which
        /// also means the first scan in a process is the only one that opens a file - so a test covering
        /// the reading path has to start from empty or it silently exercises the cache instead.
        /// </summary>
        public static void ResetCacheForTest()
        {
            lock (ACCESSIONS_BY_SIGNATURE)
            {
                ACCESSIONS_BY_SIGNATURE.Clear();
            }
        }

        /// <summary>
        /// What this document's data can answer among <paramref name="candidates"/>.
        /// <see cref="Availability.UNKNOWN"/> when the document has no results, so a filter authored
        /// before importing anything is not marked up on the strength of no evidence at all.
        /// </summary>
        public static Availability GetAvailability(SrmDocument document,
            IEnumerable<SpectrumClassColumn> candidates, string documentFilePath, ILongWaitBroker broker)
        {
            var measuredResults = document.Settings.MeasuredResults;
            if (measuredResults == null)
            {
                return Availability.UNKNOWN;
            }

            var representatives = GetFilesToScan(measuredResults);
            var sampledSpectra = GetSampledSpectra(measuredResults);
            var accessions = GetObservedAccessions(representatives, sampledSpectra, documentFilePath, broker,
                out bool cvDetermined);

            // The interpreted properties read typed fields that import always records, so having sampled
            // any spectrum at all settles them; having sampled none settles nothing.
            bool interpretedDetermined = sampledSpectra.Count > 0;

            var answerable = new HashSet<PropertyPath>();
            foreach (var column in candidates)
            {
                var accession = SpectrumClassColumn.GetCvAccession(column);
                bool isAnswerable = accession != null
                    ? accessions.Contains(accession)
                    : sampledSpectra.Any(spectrum => HasValue(column.GetValue(spectrum)));
                if (isAnswerable)
                {
                    answerable.Add(column.PropertyPath);
                }
            }
            return new Availability(answerable, interpretedDetermined, cvDetermined);
        }

        /// <summary>
        /// Whether a column produced something a filter could match on. An empty precursor list or an empty
        /// scan description says the same thing a null does - that this data has nothing to filter on here.
        /// </summary>
        private static bool HasValue(object value)
        {
            switch (value)
            {
                case null:
                    return false;
                case string text:
                    return !string.IsNullOrEmpty(text);
                case ICollection collection:
                    return collection.Count > 0;
                default:
                    return true;
            }
        }

        /// <summary>
        /// The head of each representative file's stored metadata. These carry the interpreted fields for
        /// every document with results, and the CV terms too for a document that was already filtering on
        /// them when it was imported.
        /// </summary>
        private static IList<SpectrumMetadata> GetSampledSpectra(MeasuredResults measuredResults)
        {
            var sampled = new List<SpectrumMetadata>();
            foreach (var metadata in measuredResults.GetResultFileMetadatas().Values)
            {
                sampled.AddRange(metadata.SpectrumMetadatas.Take(MAX_SPECTRA_PER_FILE));
            }
            return sampled;
        }

        /// <summary>
        /// The CV accessions this document's data carries: those already stored from a previous import,
        /// plus those found by reading files whose terms were never stored.
        ///
        /// The "determined" result says whether the CV terms were actually established. Terms already
        /// captured in the cache settle it; otherwise at least one file has to be read, and if none could
        /// be - a document shared without its raw data, or a cancelled scan - then nothing is known about
        /// which terms the data carries, which is different from knowing it carries none.
        /// </summary>
        private static HashSet<string> GetObservedAccessions(IList<ChromFileInfo> representatives,
            IEnumerable<SpectrumMetadata> sampledSpectra, string documentFilePath, ILongWaitBroker broker,
            out bool determined)
        {
            var accessions = new HashSet<string>();
            bool anyCaptured = false;
            foreach (var spectrum in sampledSpectra)
            {
                foreach (var term in spectrum.OtherParams)
                {
                    accessions.Add(term.Accession);
                    anyCaptured = true;
                }
            }

            bool anyFileRead = false;
            for (int i = 0; i < representatives.Count; i++)
            {
                if (broker != null)
                {
                    if (broker.IsCanceled)
                    {
                        break;
                    }
                    broker.ProgressValue = i * 100 / representatives.Count;
                }
                if (ReadAccessionsCached(representatives[i], documentFilePath, broker, out var fileAccessions))
                {
                    anyFileRead = true;
                    accessions.UnionWith(fileAccessions);
                }
            }
            determined = anyCaptured || anyFileRead;
            return accessions;
        }

        /// <summary>
        /// One file per distinct signature, in replicate order, capped at <see cref="MAX_FILES_SCANNED"/>.
        /// </summary>
        private static IList<ChromFileInfo> GetFilesToScan(MeasuredResults measuredResults)
        {
            var bySignature = new Dictionary<string, ChromFileInfo>();
            foreach (var fileInfo in measuredResults.MSDataFileInfos)
            {
                if (fileInfo.FilePath == null || bySignature.Count >= MAX_FILES_SCANNED)
                {
                    continue;
                }
                var signature = FileSignature(fileInfo);
                if (!bySignature.ContainsKey(signature))
                {
                    bySignature.Add(signature, fileInfo);
                }
            }
            return bySignature.Values.ToList();
        }

        /// <summary>
        /// What makes two files likely to carry the same terms: the format they are in, and the instrument
        /// configuration recorded for them. Both are already in memory from the .skyd, so grouping by this
        /// costs no I/O.
        /// </summary>
        private static string FileSignature(ChromFileInfo fileInfo)
        {
            var signature = new StringBuilder(fileInfo.FilePath.GetExtension() ?? string.Empty);
            foreach (var instrument in fileInfo.InstrumentInfoList)
            {
                signature.Append(@"|").Append(instrument.Model).Append(@"/").Append(instrument.Ionization)
                    .Append(@"/").Append(instrument.Analyzer).Append(@"/").Append(instrument.Detector);
            }
            return signature.ToString();
        }

        /// <summary>
        /// Reads one file's terms, or reports that it could not. False means nothing was learned from this
        /// file, which the caller needs to tell apart from learning that it carries no terms.
        /// </summary>
        private static bool ReadAccessionsCached(ChromFileInfo fileInfo, string documentFilePath,
            ILongWaitBroker broker, out HashSet<string> accessions)
        {
            var signature = FileSignature(fileInfo);
            lock (ACCESSIONS_BY_SIGNATURE)
            {
                if (ACCESSIONS_BY_SIGNATURE.TryGetValue(signature, out var cached))
                {
                    accessions = cached;
                    return true;
                }
            }

            accessions = ReadAccessions(fileInfo.FilePath, documentFilePath,
                broker?.CancellationToken ?? CancellationToken.None);
            if (accessions == null)
            {
                // Unreadable or canceled. Not cached, so a file that is merely offline right now gets
                // another chance the next time the editor is opened.
                accessions = new HashSet<string>();
                return false;
            }
            lock (ACCESSIONS_BY_SIGNATURE)
            {
                ACCESSIONS_BY_SIGNATURE[signature] = accessions;
            }
            return true;
        }

        /// <summary>
        /// The accessions in the head of one file, or null if it could not be read. Missing or unreadable
        /// files are ordinary here - documents get shared without their raw data - so this reports the
        /// failure as "nothing learned" rather than raising it at the user, who did not ask for a file to
        /// be opened and cannot act on the news that one is gone.
        /// </summary>
        private static HashSet<string> ReadAccessions(MsDataFileUri filePath, string documentFilePath,
            CancellationToken cancellationToken)
        {
            try
            {
                var openParams = new OpenMsDataFileParams(cancellationToken)
                {
                    DownloadPath = Path.GetDirectoryName(documentFilePath) ?? Directory.GetCurrentDirectory()
                };
                using var dataFile = filePath.OpenMsDataFile(openParams);
                return dataFile.GetDistinctOtherParams(MAX_SPECTRA_PER_FILE, cancellationToken)
                    .Select(term => term.Accession)
                    .ToHashSet();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
