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
using System.IO;
#if OSPREY_VENDOR_READER
// Only the ProteoWizard build reads OspreyEnvironment here; without the
// conditional this is a redundant-using warning in the default build.
using pwiz.Osprey.Core;
#endif

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Selects a spectrum reader by file extension. The single place that knows
    /// Osprey can read more than one input format, so callers
    /// (<c>ScoringTaskShared.EnsureSpectraCache</c>) stay unaware of the source
    /// format and everything downstream keeps seeing one
    /// <see cref="MzmlResult"/>.
    ///
    /// Which reader handles mzML depends on whether the build HAS ProteoWizard:
    ///
    /// * With it (net472, <c>/p:OspreyVendorReader=true</c>): ProteoWizard reads
    ///   everything, mzML included. One parser for all mass spec data is the
    ///   intended end state, and byte-level parity with <see cref="MzmlReader"/>
    ///   is proven, so there is nothing left for a second parser to add.
    /// * Without it (net8.0, or net472 by default): <see cref="MzmlReader"/> reads
    ///   mzML because it is the only reader present, and a vendor path is a clear
    ///   error rather than a silent fallback that would produce nothing.
    ///
    /// ProteoWizard is net472-only today (<c>pwiz_data_cli</c> has no .NET 8
    /// build). Once #4178 supplies one, <see cref="MzmlReader"/> should be removed
    /// and this class stops having a decision to make.
    /// </summary>
    public static class SpectrumFileReader
    {
#if !OSPREY_VENDOR_READER
        // Named once so both "cannot read this file" messages say the same thing
        // about how to get a build that can.
        private const string VENDOR_READER_ABSENT =
            "Vendor reading is an opt-in build capability: build the net472 configuration with " +
            "/p:OspreyVendorReader=true, which requires a bjam build to have staged " +
            "pwiz_tools/Shared/ProteowizardWrapper/obj/x64.";
#endif

        /// <summary>
        /// Load all MS1 and MS2 spectra from an mzML or vendor raw file.
        /// </summary>
        public static MzmlResult LoadAllSpectra(string path)
        {
            bool isMzml = IsMzml(path);
#if OSPREY_VENDOR_READER
            // EVERY format goes through ProteoWizard in a build that has it, mzML
            // included. Reader-vs-reader parity is proven byte-for-byte on three
            // datasets, so a second mzML parser earns nothing but a second place for
            // a defect to live - and it is the one parser here that ProteoWizard,
            // Skyline and msconvert do not already agree on. MzmlReader survives only
            // because pwiz_data_cli has no .NET 8 build; when #4178 lands it should be
            // deleted outright and this method collapses to a single call.
            //
            // OSPREY_MZML_VIA_MZMLREADER forces the hand-written reader back for one
            // run. That is what keeps the parity check expressible: the same mzML read
            // both ways must produce byte-identical .spectra.bin.
            //
            // Vendor centroiding is NOT requested for an mzML: those peaks are already
            // centroided, and MsDataFileImpl would centroid through a
            // VendorOnlyPeakDetector that throws with no vendor API behind it.
            if (isMzml && OspreyEnvironment.MzmlViaMzmlReader)
                return MzmlReader.LoadAllSpectra(path);
            // Ask for vendor centroiding only where a vendor API can actually do it.
            // "Not mzML" is not the same question: an .mzXML or .mgf reaches here too,
            // and requesting it for one of those lands in MsDataFileImpl's
            // VendorOnlyPeakDetector, whose constructor leaves a null algorithm and
            // whose first spectrum throws NoVendorPeakPickingException - an error about
            // peak picking that says nothing about the real problem, the format choice.
            return VendorRawReader.LoadAllSpectra(path, requireVendorCentroiding: IsVendorFormat(path));
#else
            // No ProteoWizard in this build, so mzML is MzmlReader's by necessity and
            // OSPREY_MZML_VIA_MZMLREADER is a no-op rather than an error: it asks for
            // what already happens.
            if (isMzml)
                return MzmlReader.LoadAllSpectra(path);
            throw new NotSupportedException(string.Format(
                "Cannot read '{0}': it is not an mzML, and this build of Osprey cannot read vendor " +
                "instrument files. {1} Otherwise convert the file to mzML with msconvert.",
                path, VENDOR_READER_ABSENT));
#endif
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
        /// Whether this path is handled by the hand-written mzML parser. Covers
        /// the gzipped form because <see cref="MzmlReader"/> reads it.
        /// </summary>
        public static bool IsMzml(string path)
        {
            string ext = Path.GetExtension(path);
            if (string.Equals(ext, @".gz", StringComparison.OrdinalIgnoreCase))
                ext = Path.GetExtension(Path.GetFileNameWithoutExtension(path));
            return string.Equals(ext, @".mzml", StringComparison.OrdinalIgnoreCase);
        }
    }
}
