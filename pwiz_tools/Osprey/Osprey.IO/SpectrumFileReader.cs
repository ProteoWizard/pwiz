/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
 *
 * Based on osprey (https://github.com/MacCossLab/osprey)
 *   by Michael J. MacCoss, MacCoss Lab, Department of Genome Sciences, UW
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
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Pwiz.Data.MsData.Readers;
using pwiz.Osprey.Centroid;
using pwiz.Osprey.Core;
using pwiz.ProteowizardWrapper;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Reads an mzML or vendor instrument file into the <see cref="SpectrumFileResult"/>
    /// the scoring pipeline consumes. ProteoWizard is the only parser: Osprey's own
    /// hand-written mzML reader was deleted with the .NET 8 port (issue #4497), because
    /// it was the one component in the pipeline whose agreement with ProteoWizard,
    /// msconvert and Skyline was unverified.
    ///
    /// It reaches ProteoWizard through <see cref="MsDataFileImpl"/> - the same wrapper
    /// Skyline uses, backed by pwiz-sharp on net8. Osprey read pwiz-sharp directly at
    /// first because the wrapper's net8 target was <c>net8.0-windows</c> and Osprey must
    /// stay plain <c>net8.0</c> to run on Linux without the Wine container. Splitting the
    /// WinForms half of <c>CommonUtil</c> into <c>CommonBaseUI</c> removed that
    /// constraint, and with it the reason to keep a second binding.
    ///
    /// What that buys is one definition of what "reading a spectrum for a search" means.
    /// Six semantics used to be reproduced here by hand and kept in step by comment -
    /// retention time in minutes without an ULP shift, isolation windows as offsets,
    /// vendor centroiding, <c>CombineIonMobilitySpectra=false</c>,
    /// <c>AllowMsMsWithoutPrecursor=false</c> and <c>IgnoreCalibrationScans=true</c>.
    /// They are now inherited from the same place Skyline gets them, so the two cannot
    /// drift apart silently.
    ///
    /// What Osprey does NOT take from the wrapper is lockmass correction: it passes no
    /// <c>LockMassParameters</c>, where Skyline does, so Waters data is read with
    /// uncorrected m/z. That is the pre-existing behaviour rather than a regression - no
    /// reader Osprey has ever had applied lockmass - but it is a real difference from
    /// Skyline on the same file, and worth closing before Osprey claims Waters support.
    ///
    /// The spectra themselves are assembled by <see cref="SpectrumBuilder"/>, unchanged
    /// from the readers this replaces.
    /// </summary>
    public static class SpectrumFileReader
    {
        public const string EXT_MZML = @".mzML";

        // Commands and file names the vendor-file errors tell the user to run or open. They go
        // into the messages as arguments, never inside translated text.
        private const string CMD_MSCONVERT = @"msconvert";
        private const string CMD_MSCONVERT_PEAK_PICKING = CMD_MSCONVERT + @" --filter ""peakPicking vendor msLevel=1-""";
        private const string MSBUILD_VENDOR_LICENSES = @"/p:IAgreeToVendorLicenses=true";
        private const string OSPREY_SOLUTION = @"Osprey.sln";
        private const string BJAM_VENDOR_LICENSES = @"bjam pwiz_tools/Osprey//Osprey --i-agree-to-the-vendor-licenses";

        // Centrix: spectra read, then centroided in parallel, this many at a time. Reading stays sequential (the
        // reader's forward walk); centroiding costs several times the read.
        private const int CENTROID_BATCH = 512;

        private static int _vendorFailuresReported;

        /// <summary>
        /// Load all MS1 and MS2 spectra from an mzML or vendor instrument file.
        /// </summary>
        public static SpectrumFileResult LoadAllSpectra(string path)
        {
            return LoadAllSpectra(path, null);
        }

        /// <summary>
        /// Load all MS1 and MS2 spectra from an mzML or vendor instrument file. With
        /// <paramref name="centroiding"/> (<c>--centroid centrix</c>), Osprey centroids profile spectra itself:
        /// <list type="bullet">
        /// <item>A vendor file is opened without vendor peak picking, so its profile is what is read.</item>
        /// <item>Any peak sigma not given is first measured from profile spectra sampled across the file
        /// (<see cref="CreateCentroiders"/>).</item>
        /// <item>Spectra are read in file order and centroided by Centrix in parallel, <see cref="CENTROID_BATCH"/> at a
        /// time, each on its own, so the result does not depend on the thread count.</item>
        /// <item>Spectra the file holds centroided pass through unchanged.</item>
        /// </list>
        /// The sigmas used are returned on the result, for the log. The caller (<c>ScoringTaskShared</c>) caches the
        /// spectra under a name tagged with the settings (<see cref="ArtifactPaths.SpectraCacheTag"/>), never in place of
        /// the vendor-centroided cache.
        /// </summary>
        public static SpectrumFileResult LoadAllSpectra(string path, CentroidingOptions centroiding)
        {
            var ms2Spectra = new List<Spectrum>();
            var ms1Spectra = new List<MS1Spectrum>();
            var acquisition = new AcquisitionInfo();
            int unsortedCount = 0;
            double sigmaMs1 = double.NaN, sigmaMs2 = double.NaN;

            ReportVendorRegistrationFailures();

            // Centroiding the profile ourselves means asking the vendor library for none.
            bool vendorFormat = IsVendorFormat(path) && centroiding == null;
            try
            {
                // Every argument that differs from the wrapper's default is Osprey's read
                // of what a search needs, and each matches the msconvert command line the
                // reference mzML was produced with
                // (ai/scripts/Osprey/SEA-AD/convert-one.cmd):
                //
                //   simAsSpectra              msconvert --simAsSpectra.
                //   requireVendorCentroided*  msconvert --filter "peakPicking vendor
                //                             msLevel=1-", for a vendor path only. See
                //                             IsVendorFormat.
                //   combineIonMobilitySpectra FALSE, against the wrapper's Skyline-facing
                //                             default of true. Combined frames give one
                //                             spectrum per drift-bin group rather than the
                //                             per-scan spectra the mzML has - a different
                //                             spectrum SET, and Osprey reads no mobility
                //                             dimension at all.
                //
                // acceptZeroLengthSpectra is left at the wrapper's default of true, which
                // is NOT msconvert's default of false. It is read only by the Agilent and
                // Sciex readers, so it cannot affect mzML or Thermo, and Osprey takes the
                // scan number from this read loop rather than from the spectrum - so a
                // dropped record shifts nothing either way. Where it does apply, matching
                // msconvert would be the more defensible choice; left alone here because
                // changing it is a Sciex/Agilent question this change cannot measure.
                //
                // AllowMsMsWithoutPrecursor (false) and IgnoreCalibrationScans (true) are
                // the wrapper's own and not overridable, and right for a search either way -
                // a Waters lockmass scan is not a place to look for peptides.
                // mzmlDecodeThreads: the library defaults this to 1 (decode inline) because a
                // host reading several FILES at once must not go wide underneath its own
                // parallelism. Osprey is the other shape - one file at a time behind a
                // one-permit gate - so it is the caller the option was written for. Only the
                // mzML reader honours it, and only on a forward index walk, which is what the
                // loop below does. See OspreyEnvironment.MzmlDecodeThreads for the measurements.
                using (var msData = new MsDataFileImpl(path,
                           simAsSpectra: true,
                           requireVendorCentroidedMS1: vendorFormat,
                           requireVendorCentroidedMS2: vendorFormat,
                           combineIonMobilitySpectra: false,
                           mzmlDecodeThreads: OspreyEnvironment.MzmlDecodeThreads))
                {
                    // A chromatogram-only file has no spectrum list, so SpectrumCount is 0
                    // and this reads nothing. Osprey has no use for such a file, but an
                    // empty result says so far more clearly than a throw would.
                    int count = msData.SpectrumCount;
                    // Per-spectrum rather than per-byte progress (a vendor reader exposes
                    // no byte position), on the same throttled interval the mzML read used
                    // - a large file is minutes of otherwise silent work.
                    using (var progress = new ProgressReporter(
                               string.Format(OspreyIOResources.SpectrumFileReader_LoadAllSpectra_Reading__0_, Path.GetFileName(path)), count,
                               string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                    {
                        if (centroiding == null)
                        {
                            for (int i = 0; i < count; i++)
                            {
                                AddSpectrum(msData.GetSpectrum(i), i, ms2Spectra, ms1Spectra, acquisition,
                                    ref unsortedCount);
                                progress.Report(i + 1);
                            }
                        }
                        else
                        {
                            var centroiders = CreateCentroiders(msData, centroiding, out sigmaMs1, out sigmaMs2);
                            // Read in order, centroided in parallel a batch at a time: Centrix costs several
                            // times the read, and each spectrum is centroided on its own.
                            var batch = new MsDataSpectrum[CENTROID_BATCH];
                            var parallel = new ParallelOptions { MaxDegreeOfParallelism = centroiding.Threads };
                            for (int start = 0; start < count; start += CENTROID_BATCH)
                            {
                                int size = Math.Min(CENTROID_BATCH, count - start);
                                for (int k = 0; k < size; k++)
                                    batch[k] = msData.GetSpectrum(start + k);
                                Parallel.For(0, size, parallel, k => CentroidProfile(batch[k], centroiders));
                                for (int k = 0; k < size; k++)
                                {
                                    AddSpectrum(batch[k], start + k, ms2Spectra, ms1Spectra, acquisition,
                                        ref unsortedCount);
                                    batch[k] = null;
                                }
                                progress.Report(start + size);
                            }
                        }
                    }
                }
            }
            catch (InvalidOperationException ex) when (
                ex.Message.Contains(@"NoVendorPeakPickingException"))
            {
                // ProteoWizard refuses to centroid, one spectrum at a time, by throwing from
                // VendorOnlyPeakDetector with that marker in the message - the same string
                // Skyline matches in ChromCacheBuilder, ScanProvider and
                // SpectraChromDataProvider. Osprey has to translate it too, or the user gets
                // an internal peak-picker message naming neither the file nor an action.
                //
                // Refusing is CORRECT and must stay: Osprey scores centroids, and reading
                // profile peaks as if they were centroids is a wrong answer from a run that
                // looks entirely normal. The reachable case today is Agilent, whose
                // SpectrumList_Agilent does not implement IVendorCentroidingSpectrumList
                // while every other vendor Osprey lists does. Note MsDataFileImpl's
                // SupportsVendorPeakPicking cannot be used to pre-empt this: it answers
                // "is this a vendor reader" (true for Agilent), not "does it centroid".
                throw new NotSupportedException(string.Format(
                    OspreyIOResources.SpectrumFileReader_LoadAllSpectra_Cannot_read___0____ProteoWizard_has_no_vendor_peak_picking_for_this_format__and_Osprey_,
                    path, CMD_MSCONVERT_PEAK_PICKING), ex);
            }
            catch (VendorSupportNotEnabledException ex)
            {
                // ProteoWizard's own message is right for ProteoWizard and wrong for an
                // Osprey user twice over: it names no file, and it says to rebuild
                // pwiz-sharp with --i-agree-to-the-vendor-licenses, which is that
                // project's build flag rather than how Osprey is built. Restate it in
                // terms the reader can act on, and keep the original as InnerException.
                throw new NotSupportedException(string.Format(
                    OspreyIOResources.SpectrumFileReader_LoadAllSpectra_Cannot_read___0____this_build_of_Osprey_has_no_vendor_instrument_support__Rebuild_with__p_,
                    path, MSBUILD_VENDOR_LICENSES, OSPREY_SOLUTION, BJAM_VENDOR_LICENSES, CMD_MSCONVERT), ex);
            }

            return new SpectrumFileResult(ms2Spectra, ms1Spectra, unsortedCount)
            {
                CentroidSigmaMs1 = sigmaMs1,
                CentroidSigmaMs2 = sigmaMs2,
                Acquisition = acquisition,
            };
        }

        /// <summary>
        /// What the source file says about its acquisition (<see cref="SourceRunMetadata"/>),
        /// from its declared instrument and its first <see cref="SourceRunMetadata.MAX_MS2_SPECTRA"/>
        /// MS2 spectra. Read only for a training export, never by the search, so it adds no work
        /// to a parse. Null when the source is not present - an HPC node handed only the spectra
        /// cache - or cannot be opened; the export's footer then leaves the keys empty.
        /// </summary>
        public static SourceRunMetadata TryReadSourceMetadata(string path)
        {
            if (string.IsNullOrEmpty(path) || !(File.Exists(path) || Directory.Exists(path)))
                return null;
            try
            {
                var metadata = new SourceRunMetadata();
                using (var msData = new MsDataFileImpl(path, simAsSpectra: true, combineIonMobilitySpectra: false))
                {
                    metadata.ObserveFile(msData);
                    int count = msData.SpectrumCount;
                    for (int i = 0; i < count && metadata.NMs2Sampled < SourceRunMetadata.MAX_MS2_SPECTRA; i++)
                    {
                        var spectrum = msData.GetSpectrum(i);
                        if (spectrum == null || spectrum.Level != 2 || spectrum.Precursors.Count == 0)
                            continue;
                        metadata.ObserveMs2(spectrum, spectrum.Precursors[0]);
                    }
                }
                return metadata;
            }
            catch (Exception ex) when (!(ex is OutOfMemoryException))
            {
                return null;
            }
        }

        /// <summary>
        /// Each MS2 spectrum's fill time and analyzer, read from the source's metadata without its peaks: for a
        /// spectra cache written before they were saved beside it (<see cref="AcquisitionSidecar"/>). The spectrum
        /// list is opened as <see cref="LoadAllSpectra(string, CentroidingOptions)"/> opens it (SIM scans as
        /// spectra, ion mobility uncombined), so the scan numbers are the cache's.
        /// </summary>
        public static AcquisitionInfo ReadAcquisition(string path)
        {
            var acquisition = new AcquisitionInfo();
            using (var msData = new MsDataFileImpl(path, simAsSpectra: true, combineIonMobilitySpectra: false))
            {
                int count = msData.SpectrumCount;
                for (int i = 0; i < count; i++)
                {
                    var metadata = msData.GetSpectrumMetadata(i);
                    if (metadata.MsLevel == 2)
                        acquisition.Add((uint)i, metadata.InjectionTime ?? double.NaN, metadata.Analyzer);
                }
            }
            return acquisition;
        }

        /// <summary>
        /// Whether this path is a vendor instrument format, i.e. one with a vendor API
        /// behind it that can centroid. Deliberately a positive list rather than
        /// "anything that is not mzML": ProteoWizard also reads mzXML, MGF and MS2,
        /// and none of those has a vendor peak picker to ask.
        ///
        /// Several of these are DIRECTORIES rather than files (Agilent and Bruker .d,
        /// Waters .raw), which is why the extension is taken from the path rather than
        /// from any file inside it.
        /// </summary>
        public static bool IsVendorFormat(string path)
        {
            // TrimEnd first: Path.GetExtension returns EMPTY for a path ending in a
            // separator, and these vendor formats are the ones most likely to arrive that
            // way - tab completion and shell globs both append a separator to a DIRECTORY,
            // which Agilent .d, Bruker .d and Waters .raw are. Without this, such a path
            // reads with vendor centroiding off and Osprey scores profile peaks as
            // centroids: no error, no warning, and a cache that looks structurally correct.
            string ext = Path.GetExtension(
                path?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
            if (string.IsNullOrEmpty(ext))
                return false;
            return string.Equals(ext, @".raw", StringComparison.OrdinalIgnoreCase)      // Thermo, Waters
                   || string.Equals(ext, @".d", StringComparison.OrdinalIgnoreCase)     // Agilent, Bruker
                   || string.Equals(ext, @".wiff", StringComparison.OrdinalIgnoreCase)  // Sciex
                   || string.Equals(ext, @".wiff2", StringComparison.OrdinalIgnoreCase) // Sciex
                   || string.Equals(ext, @".lcd", StringComparison.OrdinalIgnoreCase);  // Shimadzu
        }

        /// <summary>
        /// Whether this path is an mzML, gzipped or not. No longer selects a reader -
        /// ProteoWizard reads every format - but callers still ask, e.g. to decide whether
        /// an input needs a vendor runtime present.
        /// </summary>
        public static bool IsMzml(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.Equals(ext, @".gz", StringComparison.OrdinalIgnoreCase))
                ext = Path.GetExtension(Path.GetFileNameWithoutExtension(path));
            return string.Equals(ext, EXT_MZML, StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// The Centrix centroider of each MS level, index 1 for MS1 and 2 for every MS2 and above: a linear ion trap
        /// scans the two at their own rates, so each has its own peak sigma. A sigma not given (0) is measured from
        /// profile spectra sampled evenly across the run, up to <see cref="CentrixSigmaCalibrator.SAMPLE_SPECTRA"/> of
        /// them read before the main pass (<see cref="CentrixSigmaCalibrator"/>, <see cref="CentrixCalibration"/>).
        /// </summary>
        private static CentrixCentroider[] CreateCentroiders(MsDataFileImpl msData, CentroidingOptions centroiding,
            out double sigmaMs1, out double sigmaMs2)
        {
            var ms1 = centroiding.Ms1.Copy();
            var ms2 = centroiding.Ms2.Copy();
            if (!(ms1.SigmaTh > 0) || !(ms2.SigmaTh > 0))
            {
                var calibrator = new CentrixSigmaCalibrator();
                calibrator.Sample(msData.SpectrumCount, !(ms1.SigmaTh > 0), !(ms2.SigmaTh > 0), i =>
                {
                    var spectrum = msData.GetSpectrum(i);
                    if (spectrum != null && !spectrum.Centroided && spectrum.Level >= 1 && spectrum.Mzs != null &&
                        spectrum.Intensities != null)
                    {
                        calibrator.Add(spectrum.Level, spectrum.Mzs, spectrum.Intensities);
                    }
                });
                if (!(ms1.SigmaTh > 0))
                    ms1.SigmaTh = calibrator.Sigma(1);
                if (!(ms2.SigmaTh > 0))
                    ms2.SigmaTh = calibrator.Sigma(2);
            }
            sigmaMs1 = ms1.SigmaTh;
            sigmaMs2 = ms2.SigmaTh;
            return new[] { null, new CentrixCentroider(ms1), new CentrixCentroider(ms2) };
        }

        /// <summary>
        /// A profile spectrum's peaks replaced by its Centrix centroids, and the spectrum marked centroided; any other
        /// spectrum unchanged. The centroids' intensities are their peaks' summed intensities, about 3.4 times the
        /// height a Stellar vendor centroid reports, so absolute intensities differ between the two modes.
        /// </summary>
        private static void CentroidProfile(MsDataSpectrum spectrum, CentrixCentroider[] centroiders)
        {
            // A spectrum with a non-finite m/z is left as read, for AddSpectrum to refuse (EnsureSorted).
            if (spectrum == null || spectrum.Centroided || spectrum.Level < 1 || spectrum.Mzs == null ||
                spectrum.Intensities == null || spectrum.Intensities.Length != spectrum.Mzs.Length ||
                !Array.TrueForAll(spectrum.Mzs, double.IsFinite))
            {
                return;
            }
            var centroids = centroiders[Math.Min(spectrum.Level, 2)].Centroid(spectrum.Mzs, spectrum.Intensities);
            var mzs = new double[centroids.Count];
            var intensities = new double[centroids.Count];
            for (int i = 0; i < centroids.Count; i++)
            {
                mzs[i] = centroids[i].Mz;
                intensities[i] = centroids[i].Intensity;
            }
            spectrum.SetArrays(mzs, intensities);
            spectrum.Centroided = true;
        }

        private static void AddSpectrum(MsDataSpectrum spectrum, int spectrumIndex,
            List<Spectrum> ms2Spectra, List<MS1Spectrum> ms1Spectra, AcquisitionInfo acquisition,
            ref int unsortedCount)
        {
            if (spectrum == null)
                return;
            int level = spectrum.Level;
            if (level != 1 && level != 2)
                return;

            double[] mzs = spectrum.Mzs;
            if (mzs == null || spectrum.Intensities == null ||
                spectrum.Intensities.Length != mzs.Length)
                return;

            // SpectraCache stores intensities as f32 while ProteoWizard hands back f64.
            // Vendor intensities originate as f32, so widening then narrowing round-trips
            // exactly; this is the same width the mzML path decoded from a 32-bit binary
            // array.
            float[] intensities = MsDataFileImpl.ToFloatArray(spectrum.Intensities);

            // The 0-based position in the source file, which is what the record's
            // ScanNumber field carries (NOT a vendor scan number). Taken from the read
            // loop rather than MsDataSpectrum.Index, which the wrapper assigns from
            // pwiz-sharp's Spectrum.Index - documented as -1 when unassigned, and an
            // unchecked cast would write 4294967295 into every downstream cache and .blib
            // without anything failing.
            uint index = (uint) spectrumIndex;
            if (SpectrumBuilder.EnsureSorted(index, ref mzs, ref intensities))
                unsortedCount++;

            double retentionTime = spectrum.RetentionTime ?? 0.0;

            if (level == 1)
            {
                ms1Spectra.Add(SpectrumBuilder.CreateMs1Spectrum(index, retentionTime,
                    mzs, intensities));
                return;
            }

            // Precursors is the HIGHEST ms-level precursor group, not simply the first
            // precursor on the spectrum; the two differ only for a spectrum carrying
            // precursors at more than one level. MsPrecursor is a STRUCT, so this has to
            // test the count - FirstOrDefault() would hand back a default struct whose
            // m/z is null and score an MS2 with a precursor of 0.
            var precursors = spectrum.Precursors;
            if (precursors.Count == 0)
                return; // No precursor: not a usable MS2.
            var precursor = precursors[0];

            // IsolationWindowLower / Upper are OFFSETS from the target m/z, which is what
            // IsolationWindow(center, lowerOffset, upperOffset) wants. Not a width, not
            // absolute bounds.
            var isolationTarget = precursor.IsolationWindowTargetMz;
            var ms2 = SpectrumBuilder.CreateMs2Spectrum(index, retentionTime,
                precursor.PrecursorMz?.Value ?? 0.0, isolationTarget.HasValue,
                isolationTarget?.Value ?? 0.0, precursor.IsolationWindowLower ?? 0.0,
                precursor.IsolationWindowUpper ?? 0.0, mzs, intensities);
            if (ms2 != null)
            {
                ms2Spectra.Add(ms2);
                acquisition.Add(index, spectrum.Metadata?.InjectionTime ?? double.NaN, spectrum.Metadata?.Analyzer);
            }
        }

        /// <summary>
        /// Report vendor readers that failed to register, once per process. The wrapper
        /// records them rather than logging, because it has no opinion about where a host
        /// writes diagnostics. Left unreported they surface much later as ProteoWizard's
        /// "No registered reader recognized the file" - a message about the FORMAT when
        /// the cause was the BUILD, with no way to tell the two apart.
        /// </summary>
        private static void ReportVendorRegistrationFailures()
        {
            // Interlocked, not a plain check-then-set: ScoringTaskShared reads files from
            // concurrent workers, and two threads arriving together would each see false and
            // interleave the whole list on one line.
            if (Interlocked.Exchange(ref _vendorFailuresReported, 1) != 0)
                return;
            foreach (string failure in VendorReaderRegistration.Failures)
                OspreyOutput.Out.WriteLine($@"[warn] vendor reader {failure}");
        }
    }

    /// <summary>
    /// Everything <see cref="SpectrumFileReader"/> pulls out of one spectrum file.
    /// </summary>
    public class SpectrumFileResult
    {
        public List<Spectrum> Ms2Spectra { get; private set; }
        public List<MS1Spectrum> Ms1Spectra { get; private set; }
        public int UnsortedSpectrumCount { get; private set; }

        /// <summary>The peak sigma, Th, Centrix centroided MS1 with; NaN when it did not centroid.</summary>
        public double CentroidSigmaMs1 { get; set; } = double.NaN;

        /// <summary>The peak sigma, Th, Centrix centroided MS2 with; NaN when it did not centroid.</summary>
        public double CentroidSigmaMs2 { get; set; } = double.NaN;

        /// <summary>Each MS2 spectrum's fill time and analyzer, as the file reports them.</summary>
        public AcquisitionInfo Acquisition { get; set; }

        public SpectrumFileResult(List<Spectrum> ms2, List<MS1Spectrum> ms1, int unsortedSpectrumCount = 0)
        {
            Ms2Spectra = ms2;
            Ms1Spectra = ms1;
            UnsortedSpectrumCount = unsortedSpectrumCount;
        }
    }
}
