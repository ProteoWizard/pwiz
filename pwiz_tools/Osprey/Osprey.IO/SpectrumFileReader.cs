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
using Pwiz.Data.MsData.Readers;
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
    /// drift apart silently. The spectra themselves are assembled by
    /// <see cref="SpectrumBuilder"/>, unchanged from the readers this replaces.
    /// </summary>
    public static class SpectrumFileReader
    {
        private static bool _vendorFailuresReported;

        /// <summary>
        /// Load all MS1 and MS2 spectra from an mzML or vendor instrument file.
        /// </summary>
        public static SpectrumFileResult LoadAllSpectra(string path)
        {
            var ms2Spectra = new List<Spectrum>();
            var ms1Spectra = new List<MS1Spectrum>();
            int unsortedCount = 0;

            ReportVendorRegistrationFailures();

            bool vendorFormat = IsVendorFormat(path);
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
                // acceptZeroLengthSpectra defaults true and is left there deliberately: a
                // zero-peak spectrum still occupies a record in msconvert's mzML, and
                // dropping it would shift every later record. AllowMsMsWithoutPrecursor
                // (false) and IgnoreCalibrationScans (true) are the wrapper's own and not
                // overridable, and right for a search either way - a Waters lockmass scan
                // is not a place to look for peptides.
                using (var msData = new MsDataFileImpl(path,
                           simAsSpectra: true,
                           requireVendorCentroidedMS1: vendorFormat,
                           requireVendorCentroidedMS2: vendorFormat,
                           combineIonMobilitySpectra: false))
                {
                    // A chromatogram-only file has no spectrum list, so SpectrumCount is 0
                    // and this reads nothing. Osprey has no use for such a file, but an
                    // empty result says so far more clearly than a throw would.
                    int count = msData.SpectrumCount;
                    // Per-spectrum rather than per-byte progress (a vendor reader exposes
                    // no byte position), on the same throttled interval the mzML read used
                    // - a large file is minutes of otherwise silent work.
                    using (var progress = new ProgressReporter(
                               string.Format("Reading {0}", Path.GetFileName(path)), count,
                               string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            AddSpectrum(msData.GetSpectrum(i), i, ms2Spectra, ms1Spectra,
                                ref unsortedCount);
                            progress.Report(i + 1);
                        }
                    }
                }
            }
            catch (VendorSupportNotEnabledException ex)
            {
                // ProteoWizard's own message is right for ProteoWizard and wrong for an
                // Osprey user twice over: it names no file, and it says to rebuild
                // pwiz-sharp with --i-agree-to-the-vendor-licenses, which is that
                // project's build flag rather than how Osprey is built. Restate it in
                // terms the reader can act on, and keep the original as InnerException.
                throw new NotSupportedException(string.Format(
                    "Cannot read '{0}': this build of Osprey has no vendor instrument " +
                    "support. Rebuild with /p:IAgreeToVendorLicenses=true on Osprey.sln, " +
                    "or with 'bjam pwiz_tools/Osprey//Osprey " +
                    "--i-agree-to-the-vendor-licenses'. Otherwise convert the file to " +
                    "mzML with msconvert and read that instead.", path), ex);
            }

            return new SpectrumFileResult(ms2Spectra, ms1Spectra, unsortedCount);
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
            string ext = Path.GetExtension(path);
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
            return string.Equals(ext, @".mzml", StringComparison.OrdinalIgnoreCase);
        }

        private static void AddSpectrum(MsDataSpectrum spectrum, int spectrumIndex,
            List<Spectrum> ms2Spectra, List<MS1Spectrum> ms1Spectra, ref int unsortedCount)
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
                ms2Spectra.Add(ms2);
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
            if (_vendorFailuresReported)
                return;
            _vendorFailuresReported = true;
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

        public SpectrumFileResult(List<Spectrum> ms2, List<MS1Spectrum> ms1, int unsortedSpectrumCount = 0)
        {
            Ms2Spectra = ms2;
            Ms1Spectra = ms1;
            UnsortedSpectrumCount = unsortedSpectrumCount;
        }
    }
}
