using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Results;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.SkylineTestUtil
{
    public class PeakMatcherTestUtil
    {
        public static void SelectAndApplyPeak(string modifiedSequence, double? precursorMz, string resultsName, bool subsequent, double rt)
        {
            bool found = false;
            var skylineWindow = Program.MainWindow;
            var doc = skylineWindow.Document;
            IdentityPath identityPath = null;
            foreach (PeptideGroupDocNode nodePepGroup in doc.MoleculeGroups)
            {
                foreach (var nodePep in nodePepGroup.Peptides.Where(nodePep => nodePep.ModifiedSequence.Equals(modifiedSequence)))
                {
                    var nodeTranGroup = precursorMz.HasValue
                        ? nodePep.TransitionGroups.First(tranGroup => Math.Abs(tranGroup.PrecursorMz - precursorMz.Value) < 0.01)
                        : nodePep.TransitionGroups.First();
                    identityPath = new IdentityPath(nodePepGroup.Id, nodePep.Id, nodeTranGroup.Id);
                    IdentityPath path = identityPath;
                    skylineWindow.SelectedPath = path;
                    found = true;
                    break;
                }
                if (found)
                    break;
            }
            if (!found)
            {
                Assert.Fail("Could not find peptide {0}", modifiedSequence);
            }

            found = false;
            var chromatograms = doc.Settings.MeasuredResults.Chromatograms;
            foreach (var chromatogram in chromatograms.Where(chromSet => chromSet.Name.Equals(resultsName)))
            {
                found = true;
                ChromatogramSet chromSet = chromatogram;
                skylineWindow.SelectedResultsIndex = chromatograms.IndexOf(chromSet);
                skylineWindow.ModifyDocument("change peak", document =>
                    document.ChangePeak(identityPath, chromSet.Name, chromSet.MSDataFilePaths.First(), null, rt, UserSet.TRUE));
                break;
            }
            if (!found)
            {
                Assert.Fail("Could not find results {0}", resultsName);
            }
            skylineWindow.ApplyPeak(subsequent);
        }

        public static void VerifyPeaks(IReadOnlyDictionary<string, double> expected)
        {
            var skylineWindow = Program.MainWindow;
            bool fail = false;

            var expectedBuilder = new StringBuilder();
            var observedBuilder = new StringBuilder();

            var selectedTreeNode = skylineWindow.SelectedNode as PeptideTreeNode;
            TransitionGroupDocNode nodeTranGroup = selectedTreeNode != null
                ? selectedTreeNode.DocNode.TransitionGroups.First(g => g.Results[skylineWindow.SelectedResultsIndex] != null)
                : skylineWindow.SequenceTree.GetNodeOfType<TransitionGroupTreeNode>().DocNode;

            var settings = skylineWindow.Document.Settings;
            float mzMatchTolerance = (float) settings.TransitionSettings.Instrument.MzMatchTolerance;

            var chromatograms = settings.MeasuredResults.Chromatograms;
            for (int resultsIndex = 0; resultsIndex < chromatograms.Count; resultsIndex++)
            {
                var chromSet = chromatograms[resultsIndex];
                Assert.IsTrue(chromSet.FileCount == 1);
                ChromatogramGroupInfo[] chromGroupInfos;
                Assert.IsTrue(settings.MeasuredResults.TryLoadChromatogram(chromSet, null, nodeTranGroup, mzMatchTolerance, false, out chromGroupInfos));
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
