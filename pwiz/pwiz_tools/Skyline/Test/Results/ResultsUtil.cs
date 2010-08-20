/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Results
{
    public class ResultsTestDocumentContainer : TestDocumentContainer
    {
        public ResultsTestDocumentContainer(SrmDocument docInitial, string pathInitial)
        {
            Assert.IsTrue(SetDocument(docInitial, null));
            // Chromatogram loader needs file path to know how to place the .skyd file
            DocumentFilePath = pathInitial;

            ChromatogramManager = new ChromatogramManager();
            ChromatogramManager.Register(this);
            Register(ChromatogramManager);
        }

        public ChromatogramManager ChromatogramManager { get; private set; }

        protected override bool IsComplete(SrmDocument docNew)
        {
            return !docNew.Settings.HasResults || docNew.Settings.MeasuredResults.IsLoaded;
        }
    }

    public static class AssertResult
    {

        public static void IsDocumentResultsState(SrmDocument document, string replicateName,
            int peptides, int tranGroups, int tranGroupsHeavy, int transitions, int transitionsHeavy)
        {
            Assert.IsTrue(document.Settings.HasResults);
            int index;
            ChromatogramSet chromatogramSet;
            document.Settings.MeasuredResults.TryGetChromatogramSet(replicateName, out chromatogramSet, out index);
            int peptidesActual = 0;
            foreach (var nodePep in document.Peptides)
                peptidesActual += nodePep.Results[index][0].PeakCountRatio >= 0.5 ? 1 : 0;
            Assert.AreEqual(peptides, peptidesActual);
            int tranGroupsActual = 0;
            int tranGroupsHeavyActual = 0;
            foreach (var nodeGroup in document.TransitionGroups)
            {
                if (nodeGroup.Results[index][0].PeakCountRatio < 0.5)
                    continue;

                if (nodeGroup.TransitionGroup.LabelType.IsLight)
                    tranGroupsActual++;
                else
                    tranGroupsHeavyActual++;
            }
            Assert.AreEqual(tranGroups, tranGroupsActual);
            Assert.AreEqual(tranGroupsHeavy, tranGroupsHeavyActual);
            int transitionsActual = 0;
            int transitionsHeavyActual = 0;
            foreach (var nodeTran in document.Transitions)
            {
                if (nodeTran.Results[index][0].Area == 0)
                    continue;

                if (nodeTran.Transition.Group.LabelType.IsLight)
                    transitionsActual++;
                else
                    transitionsHeavyActual++;
            }
            Assert.AreEqual(transitions, transitionsActual);
            Assert.AreEqual(transitionsHeavy, transitionsHeavyActual);
        }

        public static void MatchChromatograms(ResultsTestDocumentContainer docContainer,
            string path1, string path2, int delta, int missing)
        {
            var doc = docContainer.Document;
            var listChromatograms = new List<ChromatogramSet>();
            foreach (string path in new[] { path1, path2 })
            {
                listChromatograms.Add(FindChromatogramSet(doc, path) ??
                    new ChromatogramSet(Path.GetFileName(path).Replace('.', '_'), new[] { path }));
            }
            var docResults = doc.ChangeMeasuredResults(new MeasuredResults(listChromatograms));
            Assert.IsTrue(docContainer.SetDocument(docResults, doc, true));
            docContainer.AssertComplete();
            docResults = docContainer.Document;
            MatchChromatograms(docResults, 0, 1, delta, missing);
        }

        private static ChromatogramSet FindChromatogramSet(SrmDocument document, string path)
        {
            if (document.Settings.HasResults)
            {
                foreach (var chromSet in document.Settings.MeasuredResults.Chromatograms)
                {
                    if (chromSet.MSDataFilePaths.Contains(path))
                        return chromSet;
                }
            }
            return null;
        }

        public static void MatchChromatograms(SrmDocument document, int iChrom1, int iChrom2, int delta, int missing)
        {
            float tolerance = (float)document.Settings.TransitionSettings.Instrument.MzMatchTolerance;
            var results = document.Settings.MeasuredResults;
            int missingPeaks = 0;
            foreach (var nodeGroup in document.TransitionGroups)
            {
                ChromatogramGroupInfo[] chromGroupInfo1;
                Assert.IsTrue(results.TryLoadChromatogram(iChrom1, nodeGroup,
                    tolerance, true, out chromGroupInfo1));
                Assert.AreEqual(1, chromGroupInfo1.Length);
                ChromatogramGroupInfo[] chromGroupInfo2;
                Assert.IsTrue(results.TryLoadChromatogram(iChrom2, nodeGroup,
                    tolerance, true, out chromGroupInfo2));
                Assert.AreEqual(1, chromGroupInfo2.Length);
                if (delta != -1)
                {
                    if (chromGroupInfo1[0].NumPeaks != chromGroupInfo2[0].NumPeaks)
                        Assert.AreEqual(chromGroupInfo1[0].NumPeaks, chromGroupInfo2[0].NumPeaks, delta);
                    if (chromGroupInfo1[0].NumPeaks == chromGroupInfo2[0].NumPeaks)
                    {
                        if (chromGroupInfo1[0].MaxPeakIndex != chromGroupInfo2[0].MaxPeakIndex)
                            Assert.AreEqual(MaxPeakTime(chromGroupInfo1[0]), MaxPeakTime(chromGroupInfo2[0]), 0.1);
                    }
                }
                else
                {
                    Assert.IsTrue(chromGroupInfo1[0].NumPeaks >= 1);
                    Assert.IsTrue(chromGroupInfo2[0].NumPeaks >= 1);
                }
                if (chromGroupInfo1[0].MaxPeakIndex < 0 || chromGroupInfo2[0].MaxPeakIndex < 0)
                    missingPeaks++;
            }
            Assert.AreEqual(missing, missingPeaks);
        }

        private static double MaxPeakTime(ChromatogramGroupInfo chromGroupInfo)
        {
            double maxIntensity = 0;
            double maxTime = 0;
            int iBest = chromGroupInfo.BestPeakIndex;
            foreach (var chromInfo in chromGroupInfo.TransitionPointSets)
            {
                var peak = chromInfo.GetPeak(iBest);
                if (!peak.IsForcedIntegration && peak.Height > maxIntensity)
                {
                    maxIntensity = peak.Height;
                    maxTime = peak.RetentionTime;
                }
            }
            return maxTime;
        }        
    }
}
