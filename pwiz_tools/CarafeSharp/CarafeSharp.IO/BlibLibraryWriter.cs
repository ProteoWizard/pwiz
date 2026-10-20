/*
 * Original author: Michael MacCoss <maccoss .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Based on Carafe (https://github.com/maccoss/carafe) main.java.ai.SkylineIO (DecoyPairs, LibInfo)
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
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using pwiz.BiblioSpec.IO;
using pwiz.CarafeSharp.Contracts;
using pwiz.CarafeSharp.Core;
using pwiz.Common.Pipeline;

namespace pwiz.CarafeSharp.IO
{
    /// <summary>
    /// Writes a predicted library as a BiblioSpec .blib that Skyline and Osprey read, through
    /// <see cref="BlibWriter"/>, the one writer every tool of ours writes the format through.
    /// Each precursor is one RefSpectra row with its peaks in library order (most intense
    /// first), one RefSpectraPeakAnnotations row per peak naming the ion (<c>b3</c>, <c>y7</c>,
    /// a <c>-loss</c> suffix when there is a neutral loss) with its charge, the modifications,
    /// its proteins, and one RetentionTimes row. Nothing marks decoys in the standard tables;
    /// they are ordinary spectra whose accessions carry the decoy prefix, and the optional
    /// DecoyPairs table (Carafe's) pairs them with their targets.
    /// <para>
    /// The values a prediction has nothing for are as Carafe writes them: one copy, no score
    /// (<see cref="BlibWriter.SCORE_TYPE_UNKNOWN"/>), no peak bounds, no total ion current, and
    /// the 1/K0 without its CCS when <c>-ccs</c> predicts one (given a CCS, Skyline would convert
    /// it with the data file's own calibration instead of using this value).
    /// </para>
    /// </summary>
    public sealed class BlibLibraryWriter : IDisposable
    {
        public const string FILE_NAME = @"carafe_spectral_library.blib";

        /// <summary>The <c>LibMetadata</c> key of the full command line that made the library.</summary>
        public const string KEY_COMMAND = @"carafe.command";

        /// <summary>The most annotation rows one INSERT takes; a spectrum with more peaks takes several.</summary>
        public const int MAX_PEAKS_PER_INSERT = BlibWriter.MAX_ANNOTATIONS_PER_INSERT;

        /// <summary>The IonMobilityTypes id of a spectrum with no ion mobility.</summary>
        public const int ION_MOBILITY_TYPE_NONE = BlibWriter.ION_MOBILITY_TYPE_NONE;

        /// <summary>The IonMobilityTypes id of timsTOF 1/K0, which <c>-ccs</c> writes.</summary>
        public const int ION_MOBILITY_TYPE_INVERSE_K0 = BlibWriter.ION_MOBILITY_TYPE_INVERSE_K0;

        private readonly BlibWriter _writer;
        private readonly long _sourceFileId;

        /// <summary>
        /// Starts the library that <see cref="Complete"/> writes to <paramref name="path"/>, with
        /// one source file named <paramref name="sourceFileName"/> (Carafe uses the library file
        /// name without its extension). It is written to a temp beside <paramref name="path"/>,
        /// so until then any library already there is left as it is. With
        /// <paramref name="stamp"/>, the library records it in its <c>LibMetadata</c> table
        /// (<see cref="BlibMetadata"/>, the slot Osprey's blib writer uses, which Skyline ignores)
        /// in the same commit as its content, so a resume check can tell whether to reuse it. With
        /// <paramref name="libraryContract"/>, the library claims the Osprey library contract in
        /// the same table (<see cref="LibraryContract.METADATA_KEY"/>); the caller has checked, or
        /// will check and discard the library, that it meets it. <paramref name="commandLine"/>,
        /// when given, is recorded under <see cref="KEY_COMMAND"/>: how the library was made.
        /// </summary>
        public BlibLibraryWriter(string path, string sourceFileName, ArtifactStamp stamp = null, bool libraryContract = false,
            string commandLine = null)
        {
            _writer = new BlibWriter(path);
            try
            {
                // The metadata rows go first, so a library that reaches any spectrum carries them.
                if (stamp != null)
                    _writer.AddMetadata(ArtifactStamp.PARQUET_KEY, stamp.ToString());
                if (libraryContract)
                    _writer.AddMetadata(LibraryContract.METADATA_KEY, LibraryContract.METADATA_VALUE);
                if (commandLine != null)
                    _writer.AddMetadata(KEY_COMMAND, commandLine);
                _sourceFileId = _writer.AddSourceFile(sourceFileName, null, null, BlibWriter.WORKFLOW_TYPE_DIA);
            }
            catch
            {
                _writer.Dispose();
                throw;
            }
        }

        /// <summary>RefSpectra rows written so far; the next spectrum gets this plus one as its id.</summary>
        public int SpectrumCount
        {
            get { return _writer.SpectrumCount; }
        }

        /// <summary>
        /// Writes spectra in order, in one transaction, compressing their peaks in parallel
        /// first. Returns the RefSpectra id of the first.
        /// </summary>
        /// <param name="spectra">The spectra, in id order.</param>
        /// <param name="maxCompressionThreads">
        /// The most threads the compression may use, or -1 for no limit. Each spectrum's blobs are
        /// the same whichever thread compresses them.
        /// </param>
        public int WriteBatch(IReadOnlyList<LibrarySpectrum> spectra, int maxCompressionThreads = -1)
        {
            var mzBlobs = new byte[spectra.Count][];
            var intensityBlobs = new byte[spectra.Count][];
            Parallel.For(0, spectra.Count, new ParallelOptions { MaxDegreeOfParallelism = maxCompressionThreads }, i =>
            {
                EncodePeaks(spectra[i], out mzBlobs[i], out intensityBlobs[i]);
            });
            int firstId = _writer.SpectrumCount + 1;
            _writer.BeginBatch();
            for (int i = 0; i < spectra.Count; i++)
                InsertSpectrum(spectra[i], mzBlobs[i], intensityBlobs[i]);
            _writer.CommitBatch();
            return firstId;
        }

        /// <summary>Adds the DecoyPairs table (Carafe's, <see cref="BlibSchema.CreateDecoyPairs"/>) with these rows.</summary>
        public void WriteDecoyPairs(IReadOnlyList<DecoyPairRow> rows)
        {
            _writer.BeginBatch();
            foreach (var row in rows)
                _writer.AddDecoyPair(row.RefSpectraId, row.IsDecoy, row.IsEntrapment, row.PairingId, row.TargetRefSpectraId, row.Method);
            _writer.CommitBatch();
        }

        /// <summary>
        /// Records the spectrum count, adds BiblioSpec's indexes, closes the file and moves it
        /// over the final name, replacing any library there.
        /// </summary>
        public void Complete()
        {
            _writer.Complete();
        }

        /// <summary>
        /// Moves the completed library to <paramref name="path"/>, which nothing may hold, when
        /// <see cref="Complete"/> could not move it over the final name.
        /// </summary>
        public void CommitAs(string path)
        {
            _writer.CommitAs(path);
        }

        /// <summary>
        /// The BiblioSpec peak blobs of a spectrum: m/z as float64 of the float32 library m/z,
        /// intensity as float32, each compressed when that is smaller.
        /// </summary>
        public static void EncodePeaks(LibrarySpectrum spectrum, out byte[] mzBlob, out byte[] intensityBlob)
        {
            int count = spectrum.Fragments.Count;
            var mz = new double[count];
            var intensity = new float[count];
            for (int i = 0; i < count; i++)
            {
                mz[i] = spectrum.Fragments[i].Mz;
                intensity[i] = spectrum.Fragments[i].RelativeIntensity;
            }
            mzBlob = BlibWriter.CompressMzs(mz);
            intensityBlob = BlibWriter.CompressIntensities(intensity);
        }

        /// <summary>The name of a peak's annotation: ion type, ordinal, and <c>-loss</c> when there is one.</summary>
        public static string AnnotationName(LibraryFragment fragment)
        {
            string name = fragment.IonType + fragment.Ordinal.ToString(CultureInfo.InvariantCulture);
            return fragment.HasLoss ? name + @"-" + fragment.LossType : name;
        }

        /// <summary>Reverses the BiblioSpec blob encoding: raw when the size matches, else zlib.</summary>
        public static byte[] DecodeBlob(byte[] blob, int expectedLength)
        {
            return BlibWriter.DecodeBlob(blob, expectedLength);
        }

        /// <summary>Closes the file; without <see cref="Complete"/>, deletes it and leaves the final name as it was.</summary>
        public void Dispose()
        {
            _writer.Dispose();
        }

        private void InsertSpectrum(LibrarySpectrum spectrum, byte[] mzBlob, byte[] intensityBlob)
        {
            int ionMobilityType = spectrum.IonMobility.HasValue ? ION_MOBILITY_TYPE_INVERSE_K0 : ION_MOBILITY_TYPE_NONE;
            var record = new BlibSpectrumRecord
            {
                PeptideSeq = spectrum.Sequence,
                PeptideModSeq = spectrum.SkylineModifiedSequence ?? spectrum.Sequence,
                PrecursorMz = spectrum.PrecursorMz,
                PrecursorCharge = spectrum.Charge,
                NumPeaks = spectrum.Fragments.Count,
                IonMobility = spectrum.IonMobility,
                IonMobilityType = ionMobilityType,
                RetentionTime = spectrum.RetentionTime,
                FileId = _sourceFileId,
                MzBlob = mzBlob,
                IntensityBlob = intensityBlob,
                Annotations = Annotations(spectrum.Fragments),
                Modifications = Modifications(spectrum.SkylineModifications),
                Proteins = Proteins(spectrum.ProteinId),
            };
            long refId = _writer.AddSpectrum(record);
            // No start or end time: Skyline would read them as explicit peak boundaries.
            _writer.AddRetentionTime(refId, _sourceFileId, spectrum.RetentionTime, null, null, 0.0, true,
                spectrum.IonMobility, null, null, ionMobilityType);
        }

        /// <summary>One annotation per peak, in peak order, its mzObserved exactly the stored peak m/z.</summary>
        private static IReadOnlyList<BlibPeakAnnotation> Annotations(IReadOnlyList<LibraryFragment> fragments)
        {
            var annotations = new BlibPeakAnnotation[fragments.Count];
            for (int i = 0; i < annotations.Length; i++)
            {
                var fragment = fragments[i];
                annotations[i] = new BlibPeakAnnotation(i, AnnotationName(fragment), fragment.Charge, fragment.TheoreticalMz, fragment.Mz);
            }
            return annotations;
        }

        private static IReadOnlyList<BlibModification> Modifications(IReadOnlyList<SkylineModification> modifications)
        {
            var result = new BlibModification[modifications.Count];
            for (int i = 0; i < result.Length; i++)
                result[i] = new BlibModification(modifications[i].Position, modifications[i].Mass);
            return result;
        }

        /// <summary>The accessions of a <c>;</c>-separated protein list; none for an unmapped spectrum.</summary>
        private static IReadOnlyList<string> Proteins(string proteinId)
        {
            if (proteinId == LibrarySpectrum.NO_PROTEIN)
                return Array.Empty<string>();
            return proteinId.Split(';');
        }
    }
}
