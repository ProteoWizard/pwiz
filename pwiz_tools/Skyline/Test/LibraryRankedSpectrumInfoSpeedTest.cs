/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2020 University of Washington - Seattle, WA
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
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests how long LibraryRankedSpectrumInfo takes to match predicted ions to library spectra
    /// </summary>
    [TestClass]
    public class LibraryRankedSpectrumInfoSpeedTest : AbstractUnitTest
    {
        protected bool IsRecording
        {
            get { return false; }
        }
        [TestMethod]
        public void TestLibraryRankedSpectrumInfoSpeed()
        {
            var testFilesDir = new TestFilesDir(TestContext, @"Test\LibraryRankedSpectrumInfoSpeedTest.zip");
            SrmDocument srmDocument;
            using (var stream = File.OpenRead(testFilesDir.GetTestPath("LibraryRankedSpectrumInfoSpeedTest.sky")))
            {
                srmDocument = (SrmDocument) new XmlSerializer(typeof(SrmDocument)).Deserialize(stream);
            }

            var unloadedLibrary = srmDocument.Settings.PeptideSettings.Libraries.Libraries.FirstOrDefault();
            Assert.IsNotNull(unloadedLibrary);
            var streamManager = new MemoryStreamManager();
            var loader = new LibraryLoadTest.TestLibraryLoader { StreamManager = streamManager };
            var librarySpec = unloadedLibrary.CreateSpec(testFilesDir.GetTestPath("LibraryRankedSpectrumInfoSpeedTest.blib"));
            var library = librarySpec.LoadLibrary(loader);
            Assert.IsTrue(library.IsLoaded);
            var peptideLibraries = srmDocument.Settings.PeptideSettings.Libraries.ChangeLibraries(new[] {library});

            var settingsWithLibraries =
                srmDocument.Settings.ChangePeptideSettings(
                    srmDocument.Settings.PeptideSettings.ChangeLibraries(peptideLibraries));

            // List of peptides to test with: <peptide sequence, description, expected # of matched ions, expected ranked ions>
            Tuple<string, string, int, double[]>[] testPeptides = new[]
            {
                // ReSharper disable StringLiteralTypo
                Tuple.Create("MEIVK", "Short Peptide", 7, new[] {147.1, 246.1, 359.3, 488.4}),
                Tuple.Create("EIYYTPDPSELAAKEEPAKEEAPAPTPAASAPAPAAAAPAPVAAAAPAAAAAEIADEPVK", "Long Peptide", 9,
                    new[] {1352.3, 1423.8, 1475}),
                Tuple.Create("LVGFT[+80]FR", "Short Peptide with 1 Neutral Loss", 4,
                    new[] {322.0948, 609.278, 707.1737, 708.1829}),
                Tuple.Create("DLKEILSGFHNAGPVAGAGAASGAAAAGGDAAAEEEKEEEAAEES[+80]DDDMGFGLFD",
                    "Long peptide with 1 Neutral Loss", 7,
                    new[] {1203.3427, 1250.8986, 1274.571, 1298.2545, 1329.6705, 1341.2415}),
                Tuple.Create("MLPTS[+80]VS[+80]R", "Short peptide with 2 neutral losses", 8,
                    new[] {343.2071, 403.681, 443.1311, 610.0934, 709.152}),
                Tuple.Create("RNPDFEDDDFLGGDFDEDEIDEESS[+80]EEEEEEKT[+80]QKK", "Long peptide with 2 neutral losses", 12,
                    new[]
                    {
                        682.7864, 1031.3154, 1069.0573, 1090.3293, 1251.3538, 1358.1454, 1431.825, 1489.1063, 1497.3608
                    })
                // ReSharper restore StringLiteralTypo
            };
            Console.Out.WriteLine();
            foreach (var tuple in testPeptides)
            {
                var peptideDocNode = srmDocument.Molecules.FirstOrDefault(pep => pep.ModifiedSequenceDisplay == tuple.Item1);
                Assert.IsNotNull(peptideDocNode, "Could not find peptide {0}", tuple.Item1);
                var transitionGroup = peptideDocNode.TransitionGroups.FirstOrDefault();
                Assert.IsNotNull(transitionGroup);
                var spectrum = library
                    .GetSpectra(new LibKey(peptideDocNode.ModifiedTarget, transitionGroup.PrecursorAdduct),
                        transitionGroup.LabelType, LibraryRedundancy.all).FirstOrDefault();
                Assert.IsNotNull(spectrum);
                
                var startTime = DateTime.UtcNow;
                LibraryRankedSpectrumInfo libraryRankedSpectrum = null;
                int repeatCount = 300;
                for (int i = 0; i < repeatCount; i++)
                {
                    libraryRankedSpectrum = new LibraryRankedSpectrumInfo(spectrum.SpectrumPeaksInfo,
                        transitionGroup.LabelType, transitionGroup, settingsWithLibraries, peptideDocNode.ExplicitMods,
                        false, 3);
                    Assert.IsNotNull(libraryRankedSpectrum);
                }
                var endTime = DateTime.UtcNow;

                Console.Out.WriteLine("Time to get LibraryRankedSpectrumInfo for {0} {1} times: {2}", tuple.Item2, repeatCount, endTime.Subtract(startTime));
                Assert.IsNotNull(libraryRankedSpectrum);
                var rankedMzs = libraryRankedSpectrum.PeaksRanked.Select(peak=>Math.Round(peak.ObservedMz, 4)).ToArray();
                Console.Out.WriteLine("{0} matched peaks. Ranked peaks: {{ {1} }}", libraryRankedSpectrum.PeaksMatched.Count(), string.Join(",", rankedMzs));
                if (!IsRecording)
                {
                    CollectionAssert.AreEqual(tuple.Item4, rankedMzs);
                    Assert.AreEqual(tuple.Item3, libraryRankedSpectrum.PeaksMatched.Count());
                }
            }
        }
    }
}
