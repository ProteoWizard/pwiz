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
                Tuple.Create("MEIVK", "Short Peptide",
                    23, new[] {131.1, 147.1, 187.1}),
                Tuple.Create("EIYYTPDPSELAAKEEPAKEEAPAPTPAASAPAPAAAAPAPVAAAAPAAAAAEIADEPVK", "Long Peptide",
                    61, new[] {587.4, 771.4, 900.5}),
                Tuple.Create("LVGFT[+80]FR", "Short Peptide with 1 Neutral Loss",
                    26, new[] {305.1334, 322.0948, 325.2004}),
                Tuple.Create("DLKEILSGFHNAGPVAGAGAASGAAAAGGDAAAEEEKEEEAAEES[+80]DDDMGFGLFD",
                    "Long peptide with 1 Neutral Loss",
                    43, new[] {790.1161, 825.5901, 889.6103}),
                Tuple.Create("MLPTS[+80]VS[+80]R", "Short peptide with 2 neutral losses",
                    38, new[] {305.6439, 340.5001, 343.2071}),
                Tuple.Create("RNPDFEDDDFLGGDFDEDEIDEESS[+80]EEEEEEKT[+80]QKK", "Long peptide with 2 neutral losses",
                    68, new[] {483.2214, 552.9168, 626.2743})
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
                int repeatCount = 50;
                for (int i = 0; i < repeatCount; i++)
                {
                    libraryRankedSpectrum = LibraryRankedSpectrumInfo.NewLibraryRankedSpectrumInfo(spectrum.SpectrumPeaksInfo,
                        transitionGroup.LabelType, transitionGroup, settingsWithLibraries, peptideDocNode.ExplicitMods,
                        false, TransitionGroup.MAX_MATCHED_MSMS_PEAKS);
                    Assert.IsNotNull(libraryRankedSpectrum);
                }
                var endTime = DateTime.UtcNow;

                Console.Out.WriteLine("Time to get LibraryRankedSpectrumInfo for {0} {1} times: {2}", tuple.Item2, repeatCount, endTime.Subtract(startTime));
                Assert.IsNotNull(libraryRankedSpectrum);
                var rankedMzs = libraryRankedSpectrum.PeaksRanked.Select(peak=>Math.Round(peak.ObservedMz, 4)).Take(3).ToArray();
                Console.Out.WriteLine("{0} matched peaks. First {1}/{2} ranked peaks: {{ {3} }}",
                    libraryRankedSpectrum.PeaksMatched.Count(), rankedMzs.Length,
                    libraryRankedSpectrum.PeaksRanked.Count(), string.Join(",", rankedMzs));
                if (!IsRecording)
                {
                    CollectionAssert.AreEqual(tuple.Item4, rankedMzs);
                    Assert.AreEqual(tuple.Item3, libraryRankedSpectrum.PeaksMatched.Count());
                }
            }
        }
    }
}
