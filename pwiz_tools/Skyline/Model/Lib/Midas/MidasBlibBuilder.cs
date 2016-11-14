/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2016 University of Washington - Seattle, WA
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
using System.Linq;
using pwiz.Common.Collections;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model.Lib.BlibData;

namespace pwiz.Skyline.Model.Lib.Midas
{
    public class MidasBlibBuilder : ILibraryBuilder
    {
        private const string BLIB_NAME_INTERNAL = "midas"; // Not L10N
        public const string BLIB_NAME_SKYLINE = "MIDAS (blib)"; // Not L10N

        private readonly SrmDocument _doc;
        private readonly MidasLibrary _library;
        private readonly LibrarySpec _libSpec;

        public MidasBlibBuilder(SrmDocument doc, MidasLibrary library, string blibPath)
        {
            _doc = doc;
            _library = library;
            _libSpec = new BiblioSpecLiteSpec(BLIB_NAME_SKYLINE, blibPath);
        }

        public bool BuildLibrary(IProgressMonitor progress)
        {
            var bestSpectra = new List<SpectrumMzInfo>();
            foreach (var nodePep in _doc.Peptides)
            {
                foreach (var nodeTranGroup in nodePep.TransitionGroups)
                {
                    // For each precursor, export the spectrum with the highest TIC within peak boundaries
                    DbSpectrum bestSpectrum = null;
                    double bestIntensity = double.MinValue;
                    foreach (var result in nodeTranGroup.Results)
                    {
                        if (result == null)
                            continue;

                        foreach (var resultsFile in result.Where(resultsFile => resultsFile.StartRetentionTime.HasValue && resultsFile.EndRetentionTime.HasValue))
                        {
                            foreach (var spectrum in _library.GetSpectraByRetentionTime(null, nodeTranGroup.PrecursorMz, resultsFile.StartRetentionTime.Value, resultsFile.EndRetentionTime.Value))
                            {
                                double currentIntensity = spectrum.Intensities.Sum();
                                if (currentIntensity > bestIntensity)
                                {
                                    bestSpectrum = spectrum;
                                    bestIntensity = currentIntensity;
                                }
                            }
                        }
                    }

                    if (bestSpectrum != null)
                    {
                        bestSpectra.Add(new SpectrumMzInfo {
                            PrecursorMz = bestSpectrum.PrecursorMz,
                            SpectrumPeaks = _library.LoadSpectrum(bestSpectrum),
                            Key = new LibKey(nodePep.ModifiedSequence, nodeTranGroup.PrecursorCharge),
                            RetentionTimes = new[]{Tuple.Create(bestSpectrum.ResultsFile.FilePath, bestSpectrum.RetentionTime, true)}.ToList()
                        });
                    }
                }
            }

            using (var blibDb = BlibDb.CreateBlibDb(_libSpec.FilePath))
            {
                return blibDb.CreateLibraryFromSpectra(new BiblioSpecLiteSpec(BLIB_NAME_INTERNAL, _libSpec.FilePath), bestSpectra, BLIB_NAME_INTERNAL, progress) != null;
            }
        }

        public LibrarySpec LibrarySpec { get { return _libSpec; } }
    }
}
