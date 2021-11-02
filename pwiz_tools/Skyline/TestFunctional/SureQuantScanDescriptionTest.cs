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
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SureQuantScanDescriptionTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSureQuantScanDescription()
        {
            TestFilesZip = @"TestFunctional\SureQuantScanDescriptionTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(()=>SkylineWindow.OpenFile(TestFilesDir.GetTestPath("SureQuantScanDescriptionTest.sky")));
            ImportResultsFile(TestFilesDir.GetTestPath("ScanDescriptionTest.mzML"));
            var expectedFirstTimes = new Dictionary<Tuple<string, string>, double>()
            {
                {Tuple.Create("NYLQSLPSK", "light"), 27.5959},
                {Tuple.Create("NYLQSLPSK", "heavy"), 27.59335},
                {Tuple.Create("LVFIDCPGK", "light"), 34.92232},
                {Tuple.Create("LVFIDCPGK", "heavy"), 15.67547},
                {Tuple.Create("FICEQCGK", "light"), 13.14265},
                {Tuple.Create("FICEQCGK", "heavy"), 13.12781}
            };
            var chromatogramSet = SkylineWindow.Document.Settings.MeasuredResults.Chromatograms[0];
            var tolerance = (float) SkylineWindow.Document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            foreach (var peptide in SkylineWindow.Document.Molecules)
            {
                foreach (var precursor in peptide.TransitionGroups)
                {
                    var key = Tuple.Create(peptide.Target.Sequence, precursor.LabelType.Name);
                    double expectedFirstTime;
                    Assert.IsTrue(expectedFirstTimes.TryGetValue(key, out expectedFirstTime));
                    ChromatogramGroupInfo[] chromatogramGroupInfos;
                    Assert.IsTrue(SkylineWindow.Document.MeasuredResults.TryLoadChromatogram(chromatogramSet, peptide, precursor,
                        tolerance, out chromatogramGroupInfos));
                    Assert.AreEqual(1, chromatogramGroupInfos.Length);
                    Assert.AreEqual(expectedFirstTime, chromatogramGroupInfos[0].TransitionPointSets.First().RawTimes[0], .0001);
                }
            }
        }
    }
}
