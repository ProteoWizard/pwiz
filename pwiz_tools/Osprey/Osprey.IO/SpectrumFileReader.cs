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
using System.Linq;
using Pwiz.Analysis;
using Pwiz.Data.Common.Cv;
using Pwiz.Data.MsData;
using Pwiz.Data.MsData.Readers;
using Pwiz.Util.Misc;
using pwiz.Osprey.Core;
using ISpectrumList = Pwiz.Data.MsData.Spectra.ISpectrumList;
using IVendorCentroidingSpectrumList = Pwiz.Data.MsData.Spectra.IVendorCentroidingSpectrumList;
using PwizBinaryDataArray = Pwiz.Data.MsData.Spectra.BinaryDataArray;
using PwizPrecursor = Pwiz.Data.MsData.Spectra.Precursor;
using PwizSpectrum = Pwiz.Data.MsData.Spectra.Spectrum;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Reads an mzML or vendor instrument file into the <see cref="SpectrumFileResult"/>
    /// the scoring pipeline consumes. ProteoWizard is the only parser: Osprey's own
    /// hand-written mzML reader was deleted with the .NET 8 port (issue #4497), because
    /// it was the one component in the pipeline whose agreement with ProteoWizard,
    /// msconvert and Skyline was unverified.
    ///
    /// This talks to pwiz-sharp DIRECTLY rather than through
    /// <c>pwiz_tools/Shared/ProteowizardWrapper</c>, which is how the net472 build reached
    /// ProteoWizard (issue #4496). The wrapper's net8 target is <c>net8.0-windows</c>
    /// because it depends on <c>pwiz.CommonUtil</c>, and Osprey has to stay plain
    /// <c>net8.0</c> to run on Linux with no Wine container. Osprey needs seven scalars and
    /// two arrays per spectrum out of the ~80 public members the wrapper carries for
    /// Skyline, so this is an adapter, not an integration.
    ///
    /// Going direct means the semantics the wrapper encodes have to be reproduced here on
    /// purpose rather than inherited, and each one is commented with the
    /// <c>MsDataFileImpl</c> behaviour it mirrors - that is what keeps a future divergence
    /// visible instead of silent. The spectra themselves are assembled by
    /// <see cref="SpectrumBuilder"/>, unchanged from the readers this replaces.
    /// </summary>
    public static partial class SpectrumFileReader
    {
        static SpectrumFileReader()
        {
            // ReaderList.Default carries only the built-in mzML / mzMLb / mz5 / mzXML /
            // MGF / MSn / BTDX readers. Without this a .raw path fails with "No registered
            // reader recognized the file".
            RegisterVendorReaders();
        }

        /// <summary>
        /// Load all MS1 and MS2 spectra from an mzML or vendor instrument file.
        /// </summary>
        public static SpectrumFileResult LoadAllSpectra(string path)
        {
            var ms2Spectra = new List<Spectrum>();
            var ms1Spectra = new List<MS1Spectrum>();
            int unsortedCount = 0;

            using (var msData = new MSData())
            {
                try
                {
                    ReaderList.Default.Read(path, msData, 0, CreateReaderConfig());
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
                var spectra = CreateSpectrumList(path, msData, IsVendorFormat(path));
                // A chromatogram-only file has no spectrum list at all, so there is
                // nothing to read and nothing to report progress against. Osprey has no
                // use for such a file, but an empty result says so far more clearly than
                // a NullReferenceException would.
                if (spectra != null)
                {
                    int count = spectra.Count;
                    // Per-spectrum rather than per-byte progress (a vendor reader exposes
                    // no byte position), on the same throttled interval the mzML read used
                    // - a large file is minutes of otherwise silent work.
                    using (var progress = new ProgressReporter(
                               string.Format("Reading {0}", Path.GetFileName(path)), count,
                               string.Empty, ProgressReporter.IO_INTERVAL_SECONDS))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            AddSpectrum(spectra.GetSpectrum(i, true), i, ms2Spectra,
                                ms1Spectra, ref unsortedCount);
                            progress.Report(i + 1);
                        }
                    }
                }
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

        /// <summary>
        /// The reader options that reproduce the msconvert command line Osprey's mzML was
        /// produced with (ai/scripts/Osprey/SEA-AD/convert-one.cmd), and beyond that the
        /// exact values <c>MsDataFileImpl</c> passed for the net472 vendor read - which is
        /// the configuration the raw-vs-mzML byte parity of #4502 was measured against.
        /// Getting these wrong does not perturb the numbers slightly, it changes WHICH
        /// spectra exist, which shifts every record after the first difference.
        ///
        /// Every value is set explicitly, including the ones that match the pwiz-sharp
        /// default, so a default change upstream cannot quietly move Osprey's input.
        /// </summary>
        private static ReaderConfig CreateReaderConfig()
        {
            return new ReaderConfig
            {
                // msconvert --simAsSpectra.
                SimAsSpectra = true,
                SrmAsSpectra = false,
                // Zero-peak spectra are KEPT: msconvert's mzML yields a record for every
                // acquired spectrum, and dropping them shifts every later record.
                AcceptZeroLengthSpectra = true,
                IgnoreZeroIntensityPoints = false,
                PreferOnlyMsLevel = 0,
                // Skyline's choices, inherited here so the #4502 parity claim carries
                // over unchanged: precursor-less MS2 dropped, Waters lockmass scans
                // dropped. They bound the claim to Thermo, where it was measured; Waters
                // or Bruker PASEF data needs its own comparison before being trusted.
                AllowMsMsWithoutPrecursor = false,
                IgnoreCalibrationScans = true,
                // FALSE, unlike the wrapper's Skyline-facing default of true. Combined
                // frames would give one spectrum per drift-bin group instead of the
                // per-scan spectra the mzML has - a different spectrum SET. Osprey reads
                // no mobility dimension at all, so the combined form offers it nothing.
                CombineIonMobilitySpectra = false,
                ReportSonarBins = true,
                IncludeIsolationArrays = false,
                GlobalChromatogramsAreMs1Only = true
            };
        }

        /// <summary>
        /// The spectrum list to read, wrapped in vendor centroiding for a vendor path.
        /// This is msconvert's <c>--filter "peakPicking vendor msLevel=1-"</c>, and the
        /// same wrapper <c>MsDataFileImpl</c> applies for
        /// <c>requireVendorCentroidedMS1/MS2</c>.
        ///
        /// Not requested for an mzML: those peaks are already centroided by whatever
        /// wrote the file, and asking a format with no vendor API behind it to centroid
        /// is an error rather than a no-op. "Not mzML" is not the same question - an
        /// .mzXML or .mgf reaches here too - which is why the test is
        /// <see cref="IsVendorFormat"/> rather than the negation of
        /// <see cref="IsMzml"/>.
        /// </summary>
        private static ISpectrumList CreateSpectrumList(string path, MSData msData,
            bool requireVendorCentroiding)
        {
            var spectra = msData.Run.SpectrumList;
            if (spectra == null || !requireVendorCentroiding)
                return spectra;

            // NOT the string overload. SpectrumList_PeakPicker's ParseIntegerSet splits on
            // ',' and ' ' and int.TryParse's each token, so msconvert's own "1-" spelling
            // parses to NOTHING and the picker silently matches no MS level at all. The
            // IntegerSet overload says the same thing in the form this API actually reads,
            // and is what the one other managed caller doing vendor centroiding uses
            // (pwiz-sharp/Tools/BiblioSpec/.../PwizSharpSpecFileReader.cs).
            //
            // algorithm: null because the vendor feed is the ONLY acceptable source here -
            // Osprey scores centroids, and an algorithmic fallback would quietly invent
            // peaks that ProteoWizard and Skyline never produced for the same file.
            if (!(spectra is IVendorCentroidingSpectrumList))
            {
                // Refuse rather than read profile data. Every vendor Osprey lists in
                // IsVendorFormat is expected to centroid; one that cannot (pwiz-sharp's
                // Agilent reader does not implement the interface today) would otherwise
                // hand back profile peaks that score as if they were centroids - wrong
                // answers from a run that looks completely normal.
                throw new NotSupportedException(string.Format(
                    "Cannot read '{0}': ProteoWizard has no vendor peak picking for this " +
                    "format, and Osprey scores centroided peaks. Convert the file to mzML " +
                    "with msconvert --filter \"peakPicking vendor msLevel=1-\" and read that " +
                    "instead.", path));
            }
            return new SpectrumList_PeakPicker(spectra, algorithm: null,
                preferVendorPeakPicking: true,
                msLevelsToPeakPick: new IntegerSet(1, int.MaxValue));
        }

        private static void AddSpectrum(PwizSpectrum spectrum, int spectrumIndex,
            List<Spectrum> ms2Spectra, List<MS1Spectrum> ms1Spectra, ref int unsortedCount)
        {
            if (spectrum == null)
                return;
            int level = GetMsLevel(spectrum);
            if (level != 1 && level != 2)
                return;

            double[] mzs = ToArray(spectrum.GetMZArray());
            double[] rawIntensities = ToArray(spectrum.GetIntensityArray());
            if (rawIntensities.Length != mzs.Length)
                return;

            // SpectraCache stores intensities as f32 while ProteoWizard hands back f64.
            // Vendor intensities originate as f32, so widening then narrowing round-trips
            // exactly; this is the same width the mzML path decoded from a 32-bit binary
            // array.
            float[] intensities = new float[rawIntensities.Length];
            for (int i = 0; i < rawIntensities.Length; i++)
                intensities[i] = (float) rawIntensities[i];

            // The 0-based position in the source file, which is what the record's
            // ScanNumber field carries (NOT a vendor scan number). Taken from the read
            // loop rather than Spectrum.Index: pwiz-sharp documents -1 as the "unassigned"
            // sentinel for that property, and an unchecked cast would write 4294967295
            // into every downstream cache and .blib without anything failing.
            uint index = (uint) spectrumIndex;
            if (SpectrumBuilder.EnsureSorted(index, ref mzs, ref intensities))
                unsortedCount++;

            double retentionTime = GetStartTime(spectrum) ?? 0.0;

            if (level == 1)
            {
                ms1Spectra.Add(SpectrumBuilder.CreateMs1Spectrum(index, retentionTime,
                    mzs, intensities));
                return;
            }

            var precursor = GetPrecursor(spectrum);
            if (precursor == null)
                return; // No precursor: not a usable MS2.

            // The isolation window is the detail most likely to be silently wrong in a
            // hand-rolled binding: lower_offset / upper_offset are OFFSETS from the target
            // m/z, which is exactly what IsolationWindow(center, lowerOffset, upperOffset)
            // wants. Not a width, not absolute bounds.
            double? isolationTarget = GetIsolationWindowValue(precursor,
                CVID.MS_isolation_window_target_m_z);
            double isoLower = GetIsolationWindowValue(precursor,
                CVID.MS_isolation_window_lower_offset) ?? 0.0;
            double isoUpper = GetIsolationWindowValue(precursor,
                CVID.MS_isolation_window_upper_offset) ?? 0.0;

            var ms2 = SpectrumBuilder.CreateMs2Spectrum(index, retentionTime,
                GetPrecursorMz(precursor) ?? 0.0, isolationTarget.HasValue,
                isolationTarget ?? 0.0, isoLower, isoUpper, mzs, intensities);
            if (ms2 != null)
                ms2Spectra.Add(ms2);
        }

        private static int GetMsLevel(PwizSpectrum spectrum)
        {
            var param = spectrum.CvParam(CVID.MS_ms_level);
            return param.IsEmpty ? 0 : param.ValueAs<int>();
        }

        /// <summary>
        /// Retention time in MINUTES, mirroring <c>MsDataFileImpl.GetStartTime</c>.
        ///
        /// A value already recorded in minutes is returned as recorded.
        /// <c>TimeInSeconds()/60</c> is NOT an identity in floating point - it multiplies
        /// by 60 and divides again - so it silently perturbs most retention times by an
        /// ULP: 0.5903117 becomes 0.5903116999999999. Every vendor reader that sets scan
        /// start time in UO_minute (Thermo among them) was affected, which made a direct
        /// raw read disagree with the mzML converted from that same raw file (PR #4501).
        /// </summary>
        private static double? GetStartTime(PwizSpectrum spectrum)
        {
            var scans = spectrum.ScanList.Scans;
            if (scans.Count == 0)
                return null;
            var param = scans[0].CvParam(CVID.MS_scan_start_time);
            if (param.IsEmpty)
                return null;
            if (param.Units == CVID.UO_minute)
                return param.ValueAs<double>();
            return param.TimeInSeconds() / 60;
        }

        /// <summary>
        /// The precursor Osprey scores against, chosen the way
        /// <c>MsDataSpectrum.Precursors[0]</c> chose it: precursors group by their
        /// "ms level" user param, and the HIGHEST level group is the one exposed, so this
        /// is the first precursor of that group rather than simply the first precursor on
        /// the spectrum. Reproduced rather than simplified - the two differ only for a
        /// spectrum carrying precursors at more than one level, which is precisely the
        /// case not worth changing by accident.
        /// </summary>
        private static PwizPrecursor GetPrecursor(PwizSpectrum spectrum)
        {
            // Spectrum.Precursors is a get-only List initialised by pwiz-sharp, so it is
            // the COUNT that decides, never a null reference.
            var precursors = spectrum.Precursors;
            if (precursors.Count == 0)
                return null;
            int maxLevel = precursors.Max(GetPrecursorMsLevel);
            return precursors.First(p => GetPrecursorMsLevel(p) == maxLevel);
        }

        private static int GetPrecursorMsLevel(PwizPrecursor precursor)
        {
            var param = precursor.IsolationWindow.UserParam(@"ms level");
            if (param.IsEmpty)
                param = precursor.UserParam(@"ms level");
            return param.IsEmpty ? 1 : param.ValueAs<int>();
        }

        private static double? GetPrecursorMz(PwizPrecursor precursor)
        {
            // Only the first selected ion m/z is considered, as in MsDataFileImpl.
            var selectedIon = precursor.SelectedIons.FirstOrDefault();
            if (selectedIon == null)
                return null;
            var param = selectedIon.CvParam(CVID.MS_selected_ion_m_z);
            return param.IsEmpty ? null : param.ValueAs<double>();
        }

        private static double? GetIsolationWindowValue(PwizPrecursor precursor, CVID cvid)
        {
            var param = precursor.IsolationWindow.CvParam(cvid);
            return param.IsEmpty ? null : param.ValueAs<double>();
        }

        private static double[] ToArray(PwizBinaryDataArray array)
        {
            return array == null ? new double[0] : array.Data.ToArray();
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
