/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com >
 *
 * Copyright 2024 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Lib.BlibData;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace TestPerf
{
    [TestClass]
    public class PerfCreateLibraryFromSpectraTest : AbstractUnitTestEx
    {
        [TestMethod]
        public void CreateLibraryFromSpectraTest()
        {
            var calc = new SequenceMassCalc(MassType.Monoisotopic);
            var spectra = new List<SpectrumMzInfo>();

            IEnumerable<string> EnumeratePeptides()
            {
                char[] AAs = AminoAcid.All.ToArray();
                const int length = 5;
                int maxPeptides = 100000;
                int[] peptide = Enumerable.Repeat(0, length).ToArray();
                for (int position = length - 1; position >= 0 && maxPeptides >= 0;)
                {
                    if (peptide[position] + 1 >= AAs.Length)
                    {
                        for(int position2 = position; position2 < peptide.Length; ++position2)
                            peptide[position2] = 0;
                        --position;
                        continue;
                    }
                    ++peptide[position];
                    position = length - 1;
                    --maxPeptides;
                    yield return string.Join("", peptide.Take(length).Select(p => AAs[p]).Concat("K"));
                }
            }

            ParallelEx.ForEach(EnumeratePeptides(), peptide =>
            {
                var target = new Target(peptide);
                var key = target.GetLibKey(Adduct.M_PLUS_3);
                var mi = new SpectrumPeaksInfo.MI[100 + spectra.Count % 5];
                for(int j = 0; j < mi.Length; ++j)
                    mi[j] = new SpectrumPeaksInfo.MI { Mz = 100 + j, Intensity = 1 + j % 3 };

                lock(spectra)
                    spectra.Add(new SpectrumMzInfo()
                    {
                        Key = key,
                        PrecursorMz = calc.GetPrecursorMass(key.Target),
                        SourceFile = "testSource",
                        SpectrumPeaks = new SpectrumPeaksInfo(mi),
                        IonMobility = IonMobilityAndCCS.EMPTY,
                        RetentionTime = 123.4
                    });
            });

            //var sw = System.Diagnostics.Stopwatch.StartNew();
            string blibPath = PathEx.GetTempFileNameWithExtension(".blib");
            using (var blib = BlibDb.CreateBlibDb(blibPath))
            {
                blib.CreateLibraryFromSpectra(new BiblioSpecLiteSpec("test", blibPath), spectra, "test", null);
            }
            //System.Console.WriteLine("Time to generate blib: {0:F1}s", sw.Elapsed.TotalSeconds);

            File.Delete(blibPath);
        }
    }
}