/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2023 University of Washington - Seattle, WA
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
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.ProteowizardWrapper;
using pwiz.Skyline.Model.Results.Spectra.Alignment;
using pwiz.Skyline.Model.RetentionTimes;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    [TestClass]
    public class SpectrumRetentionTimeAlignmentTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestSpectrumRetentionTimeAlignment()
        {
            TestFilesZip = @"TestFunctional\RetentionTimeFilterTest.zip";
            RunFunctionalTest();
        }


        protected override void DoTest()
        {
            var spectrumSummaries1 = LoadSpectrumSummaryList(TestFilesDir.GetTestPath("200fmol.mzML"));
            Assert.IsNotNull(spectrumSummaries1);
            Assert.IsNotNull(spectrumSummaries1[0].SummaryValue);
            var spectrumSummaries2 = LoadSpectrumSummaryList(TestFilesDir.GetTestPath("8fmol.mzML"));
            var similarityMatrix = spectrumSummaries1.GetSimilarityGrid(spectrumSummaries2);
            Assert.IsNotNull(similarityMatrix);
            var pointsToAlign = SimilarityGrid.FilterBestPoints(similarityMatrix.GetBestPointCandidates(null, null));
            Assert.AreNotEqual(0, pointsToAlign.Count);
            var kdeAligner = new KdeAligner();
            kdeAligner.Train(pointsToAlign.Select(pt=>pt.XRetentionTime).ToArray(), pointsToAlign.Select(pt=>pt.YRetentionTime).ToArray(), CancellationToken.None);
            kdeAligner.GetSmoothedValues(out var smoothedX, out var smoothedY);
            Assert.IsTrue(smoothedY.Length > 2, "Length {0} should be greater than 2", smoothedX.Length);
            Assert.AreEqual(smoothedX.Length, smoothedY.Length);
            VerifySorted(smoothedX);
            VerifySorted(smoothedY);
        }

        private void VerifySorted(IList<double> values)
        {
            for (int i = 1; i < values.Count; i++)
            {
                var previous = values[i - 1];
                var current = values[i];
                if (current < previous)
                {
                    Assert.Fail("Value {0} at position {1} should not be less than previous value {2}", current, i, previous);
                }
            }
        }

        private SpectrumSummaryList LoadSpectrumSummaryList(string path)
        {
            var spectra = new List<SpectrumSummary>();
            using (var file = new MsDataFileImpl(path))
            {
                for (int spectrumIndex = 0; spectrumIndex < file.SpectrumCount; spectrumIndex++)
                {
                    spectra.Add(SpectrumSummary.FromSpectrum(file.GetSpectrum(spectrumIndex)));
                }
            }

            return new SpectrumSummaryList(spectra);
        }
    }
}
