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
using pwiz.Osprey.Core;

namespace pwiz.Osprey.IO
{
    /// <summary>
    /// Selects a spectrum reader by file extension. The single place that knows
    /// Osprey can read more than one input format, so callers
    /// (<c>ScoringTaskShared.EnsureSpectraCache</c>) stay unaware of the source
    /// format and everything downstream keeps seeing one
    /// <see cref="MzmlResult"/>.
    ///
    /// mzML is handled by the hand-written <see cref="MzmlReader"/> on every
    /// target framework. Vendor formats go through ProteoWizard, which is
    /// net472-only (pwiz_data_cli has no .NET 8 build), so on net8.0 a vendor
    /// path is a clear error rather than a silent fallback that would produce
    /// nothing.
    /// </summary>
    public static class SpectrumFileReader
    {
        /// <summary>
        /// Load all MS1 and MS2 spectra from an mzML or vendor raw file.
        /// </summary>
        public static MzmlResult LoadAllSpectra(string path)
        {
            bool isMzml = IsMzml(path);
#if NET472
            // OSPREY_MZML_VIA_PWIZ routes mzML through ProteoWizard too, so the
            // two readers can be compared against one fixed input file. Vendor
            // centroiding is NOT requested for an mzML: those peaks are already
            // centroided, and MsDataFileImpl would centroid through a
            // VendorOnlyPeakDetector that throws with no vendor API behind it.
            if (isMzml && !OspreyEnvironment.MzmlViaPwiz)
                return MzmlReader.LoadAllSpectra(path);
            return VendorRawReader.LoadAllSpectra(path, requireVendorCentroiding: !isMzml);
#else
            if (isMzml)
            {
                if (OspreyEnvironment.MzmlViaPwiz)
                {
                    throw new NotSupportedException(
                        "OSPREY_MZML_VIA_PWIZ requires the .NET Framework build of Osprey: it reads " +
                        "mzML through ProteoWizard, whose pwiz_data_cli is net472-only. Unset the " +
                        "variable to use the built-in mzML reader, or run the net472 build.");
                }
                return MzmlReader.LoadAllSpectra(path);
            }
            throw new NotSupportedException(string.Format(
                "Cannot read '{0}': reading vendor instrument files requires the .NET Framework " +
                "build of Osprey (ProteoWizard's pwiz_data_cli is net472-only). Convert the file " +
                "to mzML with msconvert, or run the net472 build.", path));
#endif
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
