/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
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
using System.Data.SQLite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.BiblioSpec.IO;
using pwiz.Skyline.Model.DocSettings;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Reads the spectra of a <c>.blib</c> a test wrote, for the test to check them one by one:
    /// each record with its peak annotations, as <see cref="BlibReader"/> reads them.
    /// </summary>
    public class SpectralLibraryTestUtil
    {
        public static IList<BlibSpectrumRecord> GetRefSpectraFromPath(string filename)
        {
            using (var reader = new BlibReader(filename))
            {
                return GetRefSpectra(reader);
            }
        }

        public static IList<BlibSpectrumRecord> GetRefSpectra(SQLiteConnection connection)
        {
            using (var reader = new BlibReader(connection))
            {
                return GetRefSpectra(reader);
            }
        }

        private static IList<BlibSpectrumRecord> GetRefSpectra(BlibReader reader)
        {
            var list = new List<BlibSpectrumRecord>();
            foreach (var spectrum in reader.ReadSpectra(false))
            {
                spectrum.Annotations = reader.ReadAnnotations(spectrum.Id);
                list.Add(spectrum);
            }
            return list;
        }

        public static void CheckRefSpectra(IList<BlibSpectrumRecord> spectra, string peptideSeq, string peptideModSeq,
            int precursorCharge, double precursorMz, int numPeaks, double rT, IonMobilityAndCCS im = null)
        {
            for (var i = 0; i < spectra.Count; i++)
            {
                var spectrum = spectra[i];
                if (spectrum.PeptideSeq.Equals(peptideSeq) &&
                    spectrum.PeptideModSeq.Equals(peptideModSeq) &&
                    spectrum.PrecursorCharge.Equals(precursorCharge) &&
                    Math.Abs((spectrum.RetentionTime ?? 0) - rT) < 0.001 &&
                    Math.Abs(spectrum.PrecursorMz - precursorMz) < 0.001 &&
                    spectrum.NumPeaks.Equals(numPeaks))
                {
                    spectra.RemoveAt(i);
                    return;
                }
            }

            Assert.Fail("{0} [{1}], precursor charge {2}, precursor m/z {3}, RT {4} with {5} peaks not found",
                peptideSeq, peptideModSeq, precursorCharge, precursorMz, rT, numPeaks);
        }
    }
}
