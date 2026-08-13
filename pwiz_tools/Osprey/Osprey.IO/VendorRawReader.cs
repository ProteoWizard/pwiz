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

using System.Collections.Generic;
using System.IO;
using pwiz.Osprey.Core;
using pwiz.ProteowizardWrapper;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Reads vendor raw files directly through ProteoWizard, producing the same
    /// <see cref="MzmlResult"/> <see cref="MzmlReader"/> produces from the mzML
    /// msconvert would have written from that same file (issue #4496). net472
    /// only: pwiz_data_cli is net472-only, so the net8.0 configuration still
    /// reads mzML.
    ///
    /// This is an adapter, not an integration. Osprey needs roughly seven scalars
    /// and two arrays per spectrum, all of which <see cref="MsDataFileImpl"/>
    /// already exposes, so there is no direct pwiz_data_cli binding here. The
    /// spectra themselves are assembled by <see cref="SpectrumBuilder"/>, shared
    /// with <see cref="MzmlReader"/>, so the two paths cannot drift in how they
    /// sort peaks or validate isolation windows.
    ///
    /// The isolation window is the detail most likely to be silently wrong in a
    /// hand-rolled binding: <see cref="MsPrecursor.IsolationWindowLower"/> and
    /// <see cref="MsPrecursor.IsolationWindowUpper"/> are OFFSETS from the target
    /// m/z (MsDataFileImpl reads MS_isolation_window_lower_offset /
    /// _upper_offset), which is exactly what
    /// <see cref="IsolationWindow"/>(center, lowerOffset, upperOffset) wants.
    /// Not a width, not absolute bounds.
    /// </summary>
    public static class VendorRawReader
    {
        /// <summary>
        /// Load all MS1 and MS2 spectra from a vendor raw file.
        ///
        /// The reader options reproduce the msconvert command line Osprey's mzML
        /// was produced with (ai/scripts/Osprey/SEA-AD/convert-one.cmd):
        /// <c>--filter "peakPicking vendor msLevel=1-"</c> becomes
        /// requireVendorCentroided for both MS levels, and <c>--simAsSpectra</c>
        /// becomes simAsSpectra. Getting these wrong does not perturb the numbers
        /// slightly, it changes WHICH spectra exist, which shifts every record
        /// after the first difference.
        /// </summary>
        /// <param name="path">The instrument file, or an mzML - in a build with
        /// ProteoWizard every format arrives here, and only
        /// <c>OSPREY_MZML_VIA_MZMLREADER</c> diverts mzML away.</param>
        /// <param name="requireVendorCentroiding">Whether to ask ProteoWizard for
        /// vendor centroiding, reproducing msconvert's
        /// <c>peakPicking vendor msLevel=1-</c>. Must be FALSE for an mzML
        /// source: the peaks are already centroided by whatever wrote the file,
        /// and MsDataFileImpl centroids through a <c>VendorOnlyPeakDetector</c>
        /// that throws when no vendor API is behind the data.</param>
        public static MzmlResult LoadAllSpectra(string path, bool requireVendorCentroiding = true)
        {
            var ms2Spectra = new List<Spectrum>();
            var ms1Spectra = new List<MS1Spectrum>();
            int unsortedCount = 0;

            // combineIonMobilitySpectra defaults to TRUE in the wrapper (Skyline wants
            // IMS in 3-array frames) and FALSE in pwiz, which is what msconvert uses.
            // Left at the wrapper default, an IMS acquisition would come back as one
            // combined frame per drift-bin group instead of the per-scan spectra the
            // mzML has - a different spectrum SET, which shifts every .spectra.bin
            // record, not just their values. Osprey reads no mobility dimension at all,
            // so the combined form has nothing to offer it either.
            //
            // Two ReaderConfig values still differ from msconvert and CANNOT be set
            // from here: ignoreCalibrationScans (wrapper hardcodes true, dropping
            // Waters lockmass scans) and allowMsMsWithoutPrecursor (wrapper hardcodes
            // false, dropping precursor-less MS2). Both are deliberate Skyline choices
            // in shared code. They bound the parity claim rather than break it: it
            // holds for Thermo, where it was measured, and Waters or Bruker PASEF data
            // needs its own comparison before being trusted.
            using (var msData = new MsDataFileImpl(path,
                       simAsSpectra: true,
                       combineIonMobilitySpectra: false,
                       requireVendorCentroidedMS1: requireVendorCentroiding,
                       requireVendorCentroidedMS2: requireVendorCentroiding))
            {
                int count = msData.SpectrumCount;
                // Per-spectrum rather than per-byte progress (the vendor reader
                // exposes no byte position), on the same throttled interval the
                // mzML read uses - a large raw file is minutes of otherwise
                // silent work.
                using (var progress = new ProgressReporter(
                           string.Format("Reading {0}", Path.GetFileName(path)), count,
                           string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                {
                    for (int i = 0; i < count; i++)
                    {
                        AddSpectrum(msData, i, ms2Spectra, ms1Spectra, ref unsortedCount);
                        progress.Report(i + 1);
                    }
                }
            }

            return new MzmlResult(ms2Spectra, ms1Spectra, unsortedCount);
        }

        private static void AddSpectrum(MsDataFileImpl msData, int scanIndex,
            List<Spectrum> ms2Spectra, List<MS1Spectrum> ms1Spectra, ref int unsortedCount)
        {
            var spectrum = msData.GetSpectrum(scanIndex);
            if (spectrum == null || (spectrum.Level != 1 && spectrum.Level != 2))
                return;

            // Zero-peak spectra are KEPT, not skipped. MzmlReader has no
            // empty-array guard, so msconvert's mzML yields a record for every
            // acquired spectrum; dropping them here cost exactly 945 MS2 records
            // (88 bytes each: a 48-byte record with no peaks plus its 40-byte
            // index entry) against the TDP-43 reference and shifted nothing else.
            double[] mzs = spectrum.Mzs ?? new double[0];
            double[] rawIntensities = spectrum.Intensities ?? new double[0];
            if (rawIntensities.Length != mzs.Length)
                return;

            // SpectraCache stores intensities as f32 while ProteoWizard hands back
            // f64. Vendor intensities originate as f32, so widening then narrowing
            // round-trips exactly; this is the same width the mzML path decodes
            // from a 32-bit binary array.
            float[] intensities = new float[rawIntensities.Length];
            for (int i = 0; i < rawIntensities.Length; i++)
                intensities[i] = (float)rawIntensities[i];

            // Index, not a vendor scan number: Spectrum.ScanNumber carries the
            // 0-based position in the source file, matching the mzML "index"
            // attribute MzmlReader reads.
            uint index = (uint)spectrum.Index;
            if (SpectrumBuilder.EnsureSorted(index, ref mzs, ref intensities))
                unsortedCount++;

            // RetentionTime is minutes on both sides (MsDataSpectrum reports
            // minutes; MzmlReader divides seconds-valued cvParams by 60).
            double retentionTime = spectrum.RetentionTime ?? 0.0;

            if (spectrum.Level == 1)
            {
                ms1Spectra.Add(SpectrumBuilder.CreateMs1Spectrum(index, retentionTime,
                    mzs, intensities));
                return;
            }

            var precursors = spectrum.Precursors;
            if (precursors == null || precursors.Count == 0)
                return; // No precursor: not a usable MS2, same as the mzML path.

            var precursor = precursors[0];
            double precursorMz = precursor.PrecursorMz?.Value ?? 0.0;
            bool hasIsolationWindow = precursor.IsolationWindowTargetMz.HasValue;
            double isoTarget = precursor.IsolationWindowTargetMz?.Value ?? 0.0;
            double isoLower = precursor.IsolationWindowLower ?? 0.0;
            double isoUpper = precursor.IsolationWindowUpper ?? 0.0;

            var ms2 = SpectrumBuilder.CreateMs2Spectrum(index, retentionTime, precursorMz,
                hasIsolationWindow, isoTarget, isoLower, isoUpper, mzs, intensities);
            if (ms2 != null)
                ms2Spectra.Add(ms2);
        }
    }
}
