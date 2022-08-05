using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            var libKeyToMatch = LibraryKey.Create(modifiedSequence, 0);
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

        public static void SelectAndApplyPeak(string modifiedSequence, double? precursorMz, string resultsName, bool subsequent, bool group, double rt)
        {
            Select(modifiedSequence, precursorMz, resultsName, out var path, out var chromSet);
            Program.MainWindow.ModifyDocument("change peak", document =>
                document.ChangePeak(path, chromSet.Name, chromSet.MSDataFilePaths.First(), null, rt, UserSet.TRUE));
            Program.MainWindow.ApplyPeak(subsequent, group);
        }

        public static void VerifyPeaks(IReadOnlyDictionary<string, double> expected)
        {
            var skylineWindow = Program.MainWindow;
            bool fail = false;

            var expectedBuilder = new StringBuilder();
            var observedBuilder = new StringBuilder();

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
                ChromatogramGroupInfo[] chromGroupInfos;
                Assert.IsTrue(settings.MeasuredResults.TryLoadChromatogram(chromSet, null, nodeTranGroup, mzMatchTolerance, out chromGroupInfos));
                var rt = nodeTranGroup.Results[resultsIndex][0].RetentionTime;
                Assert.IsTrue(rt.HasValue);
                var chromName = chromSet.Name;
                Assert.IsTrue(expected.ContainsKey(chromName));
                var expectedRt = expected[chromName];
                expectedBuilder.AppendLine(string.Format("{0}", expectedRt));
                observedBuilder.AppendLine(string.Format("{0}", rt.Value));
                if (Math.Abs(expectedRt - rt.Value) > 0.01)
                    fail = true;
            }
            Assert.IsFalse(fail, TextUtil.LineSeparate(
                string.Format("{0}", nodeTranGroup),
                "Expected RTs:", expectedBuilder.ToString(),
                "but found RTs:", observedBuilder.ToString())
            );
        }
    }
}
