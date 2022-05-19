/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
    /// Tests that extracting chromatograms from a MIDAS wiff file produces a
    /// library where Skyline is able to look up peptides which have modifications,
    /// and is able to annotate the y ions.
    /// </summary>
    [TestClass]
    public class MidasModificationTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestMidasModifications()
        {
            TestFilesZip = @"TestFunctional\MidasModificationTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("PMF Skyline PepCalMix 01.sky")));
            WaitForDocumentLoaded();
            ImportResultsFile(TestFilesDir.GetTestPath("160204 PepCalMix MIDAS01 heavy.wiff"));
            WaitForDocumentLoaded();
            SelectNode(SrmDocument.Level.TransitionGroups, 1);
            RunUI(()=>SkylineWindow.ShowGraphSpectrum(true));
            var graphSpectrum = SkylineWindow.GraphSpectrum;
            Assert.IsNotNull(graphSpectrum);
            WaitForCondition(() => !graphSpectrum.IsGraphUpdatePending && null != graphSpectrum.AvailableSpectra);
            Assert.AreEqual(8, graphSpectrum.AvailableSpectra.Count());
            var goodSpectrum = graphSpectrum.AvailableSpectra.Skip(1).First();
            RunUI(() =>
            {
                graphSpectrum.SelectSpectrum(new SpectrumIdentifier(goodSpectrum.FilePath.ToString(), goodSpectrum.RetentionTime.GetValueOrDefault()));
            });
            WaitForGraphs();
            Assert.AreEqual(7, graphSpectrum.IonLabels.Count());
        }
    }
}
