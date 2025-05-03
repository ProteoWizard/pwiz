/*
 * Original author: Kaipo Tamura <kaipot .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    public static class PeakMatcherTestUtil
    {
        public static void Select(string modifiedSequence, double? precursorMz, string resultsName, out IdentityPath path, out ChromatogramSet chromSet)
        {
            var found = false;
            var skylineWindow = Program.MainWindow;
            var doc = skylineWindow.Document;

            path = null;
            var libKeyToMatch = new PeptideLibraryKey(modifiedSequence, 0);
            foreach (var nodePepGroup in doc.MoleculeGroups)
            {
                foreach (var nodePep in nodePepGroup.Peptides.Where(nodePep => LibKeyIndex.KeysMatch(libKeyToMatch, nodePep.ModifiedTarget.GetLibKey(Adduct.EMPTY))))
                {
                    var nodeTranGroup = precursorMz.HasValue
                        ? nodePep.TransitionGroups.First(tranGroup => Math.Abs(tranGroup.PrecursorMz - precursorMz.Value) < 0.01)
                        : nodePep.TransitionGroups.First();
                    path = new IdentityPath(nodePepGroup.Id, nodePep.Id, nodeTranGroup.Id);
                    skylineWindow.SelectedPath = path;
                    found = true;
                    break;
                }
                if (found)
                    break;
            }
            if (!found)
                Assert.Fail("Could not find peptide {0}", modifiedSequence);

            chromSet = null;
            var chromatograms = doc.Settings.MeasuredResults.Chromatograms;
            foreach (var chromatogram in chromatograms.Where(c => c.Name.Equals(resultsName)))
            {
                chromSet = chromatogram;
                skylineWindow.SelectedResultsIndex = chromatograms.IndexOf(chromSet);
                return;
            }
            Assert.Fail("Could not find results {0}", resultsName);
        }

        public static void SelectPeak(string modifiedSequence, double? precursorMz, string resultsName, double rt)
        {
            Select(modifiedSequence, precursorMz, resultsName, out var path, out var chromSet);
            Program.MainWindow.ModifyDocument("change peak", document =>
                document.ChangePeak(path, chromSet.Name, chromSet.MSDataFilePaths.First(), null, rt, UserSet.TRUE));
        }

        public static void SelectAndApplyPeak(string modifiedSequence, double? precursorMz, string resultsName, bool subsequent, bool group, double rt)
        {
            SelectPeak(modifiedSequence, precursorMz, resultsName, rt);
            Program.MainWindow.ApplyPeak(subsequent, group);
        }

        public static void VerifyPeaks(IReadOnlyDictionary<string, double> expected)
        {
            var skylineWindow = Program.MainWindow;
            bool fail = false;

            var expectedList = new List<double>();
            var observedList = new List<double>();

            var selectedTreeNode = skylineWindow.SelectedNode as PeptideTreeNode;
            TransitionGroupDocNode nodeTranGroup = selectedTreeNode != null
                ? selectedTreeNode.DocNode.TransitionGroups.First(g => !g.Results[skylineWindow.SelectedResultsIndex].IsEmpty)
                : skylineWindow.SequenceTree.GetNodeOfType<TransitionGroupTreeNode>().DocNode;

            var settings = skylineWindow.Document.Settings;
            float mzMatchTolerance = (float) settings.TransitionSettings.Instrument.MzMatchTolerance;

            var chromatograms = settings.MeasuredResults.Chromatograms;
            for (int resultsIndex = 0; resultsIndex < chromatograms.Count; resultsIndex++)
            {
                var chromSet = chromatograms[resultsIndex];
                Assert.IsTrue(chromSet.FileCount == 1);
                Assert.IsTrue(settings.MeasuredResults.TryLoadChromatogram(chromSet, null, nodeTranGroup, mzMatchTolerance, out _));
                var rt = nodeTranGroup.Results[resultsIndex][0].RetentionTime;
                Assert.IsTrue(rt.HasValue);
                var chromName = chromSet.Name;
                Assert.IsTrue(expected.ContainsKey(chromName));
                var expectedRt = expected[chromName];
                expectedList.Add(expectedRt);
                observedList.Add(rt.Value);
                if (Math.Abs(expectedRt - rt.Value) > 0.01)
                    fail = true;
            }

            if (fail)
            {
                var message = TextUtil.LineSeparate(string.Format("{0}", nodeTranGroup),
                    "Expected RTs:",
                    string.Join(",", expectedList.Select(v => v.ToString(CultureInfo.InvariantCulture))),
                    "but found RTs:",
                    string.Join(",", observedList.Select(v => v.ToString("0.#####", CultureInfo.InvariantCulture))));
                Assert.Fail(message);
            }
        }
    }
}
