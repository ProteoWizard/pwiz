/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2022 University of Washington - Seattle, WA
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Model;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Tests that the Library Match window displays the best spectrum from the EncyclopeDia library.
    /// </summary>
    [TestClass]
    public class EncyclopeDiaLibraryMatchTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestEncyclopeDiaLibraryMatch()
        {
            TestFilesZip = @"TestFunctional\EncyclopeDiaLibraryMatchTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() =>
            {
                SkylineWindow.OpenFile(TestFilesDir.GetTestPath("EncyclopeDiaLibraryMatchTest.sky"));
            });
            WaitForDocumentLoaded();
            RunUI(() => SkylineWindow.ShowGraphSpectrum(true));
            GraphSpectrum graphSpectrum = FindOpenForm<GraphSpectrum>();
            // Iterate through all of the peptides in the document
            foreach (var protein in SkylineWindow.Document.MoleculeGroups)
            {
                foreach (PeptideDocNode peptide in protein.Children)
                {
                    SkylineWindow.SelectedPath = new IdentityPath(protein.PeptideGroup, peptide.Peptide);
                    WaitForGraphs();
                    var availableSpectra = graphSpectrum.AvailableSpectra.ToList();
                    var bestSpectrum = availableSpectra.First();
                    Assert.IsTrue(bestSpectrum.IsBest);

                    var mostSpectrumPeaks = availableSpectra.Max(CountSpectrumPeaks);
                    var fewestSpectrumPeaks = availableSpectra.Min(CountSpectrumPeaks);
                    // For all of the peptides in this document, we expect that not all of the spectra have the same number of peaks.
                    Assert.AreNotEqual(mostSpectrumPeaks, fewestSpectrumPeaks);

                    // The best spectrum usually has more peaks than all of the other spectra, but the
                    // only thing we can really assert in this test is that it has more peaks than the worst spectrum
                    Assert.AreNotEqual(fewestSpectrumPeaks, CountSpectrumPeaks(bestSpectrum));
                }
            }
        }
        /// <summary>
        /// Returns the number of nonzero intensities in the spectrum.
        /// </summary>
        private int CountSpectrumPeaks(SpectrumDisplayInfo spectrumDisplayInfo)
        {
            return spectrumDisplayInfo.SpectrumPeaksInfo.Intensities.Count(intensity=>intensity > 0);
        }
    }
}
