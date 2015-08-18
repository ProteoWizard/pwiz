/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2010 University of Washington - Seattle, WA
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
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Find;
using pwiz.SkylineTestA.Results;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Tests Find Node.
    /// </summary>
    [TestClass]
    public class FindNodeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestFindNode()
        {
            RunTestFindNode(RefinementSettings.ConvertToSmallMoleculesMode.none);
            RunTestFindNode(RefinementSettings.ConvertToSmallMoleculesMode.formulas);
            RunTestFindNode(RefinementSettings.ConvertToSmallMoleculesMode.masses_and_names);
            // RunTestFindNode(RefinementSettings.ConvertToSmallMoleculesMode.masses_only);   Without names or formulas there's no way to associate labeled/unlabeled
        }

        public void RunTestFindNode(RefinementSettings.ConvertToSmallMoleculesMode asSmallMolecules)
        {
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.none)
                TestDirectoryName = asSmallMolecules.ToString();
            TestSmallMolecules = false;  // Don't need the magic test node, we have an explicit test

            SrmDocument doc = CreateStudy7Doc();
            doc = (new RefinementSettings()).ConvertToSmallMolecules(doc, asSmallMolecules);
            var displaySettings = new DisplaySettings(null, false, 0, 0); //, ProteinDisplayMode.ByName);
            // Find every other transition, searching down.
            List<TransitionDocNode> listTransitions = doc.MoleculeTransitions.ToList();
            var pathFound = doc.GetPathTo(0, 0);
            int i;
            for (i = 0; i < doc.MoleculeTransitionCount; i += 2)
            {
                pathFound = doc.SearchDocumentForString(pathFound, String.Format("{0:F04}", listTransitions[i].Mz), displaySettings, false, false);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Transitions, i), pathFound);
            }
            
            // Test wrapping in search down.
            pathFound = doc.SearchDocumentForString(pathFound, String.Format("{0:F04}", listTransitions[0].Mz), displaySettings, false, false);
            Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Transitions, 0), pathFound);

            // Find every other peptide searching up while for each finding one of its children searching down.
            pathFound = doc.LastNodePath;
            List<PeptideDocNode> listPeptides = new List<PeptideDocNode>();
            listPeptides.AddRange(doc.Molecules);
            List<TransitionGroupDocNode> listTransitionGroups = new List<TransitionGroupDocNode>();
            listTransitionGroups.AddRange(doc.MoleculeTransitionGroups);
            for (int x = doc.MoleculeCount; x > 0; x -= 2)
            {
                // Test case insensitivity.
                pathFound = doc.SearchDocumentForString(pathFound, listPeptides[x-1].ToString().ToLower(), displaySettings, true, false);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Molecules, x-1), pathFound);
                // Test parents can find children.
                pathFound = doc.SearchDocumentForString(pathFound, String.Format("{0:F04}", listTransitionGroups[x * 2 - 1].PrecursorMz), displaySettings, 
                    false, true);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.TransitionGroups, x * 2 - 1), pathFound);
                // Test Children can find parents.
                pathFound = doc.SearchDocumentForString(pathFound, listPeptides[x - 1].ToString().ToLower(), displaySettings, true, false);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Molecules, x - 1), pathFound);
            }

            // Test wrapping in search up.
            pathFound = doc.SearchDocumentForString(pathFound, String.Format("{0:F04}", listTransitionGroups[listTransitionGroups.Count - 1].PrecursorMz), 
                displaySettings, false, true);
            Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.TransitionGroups, listTransitionGroups.Count - 1), pathFound);
            
            // Test children can find other parents.
            pathFound = doc.SearchDocumentForString(pathFound, listPeptides[0].ToString().ToLowerInvariant(), displaySettings, true, false);
            Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Molecules, 0), pathFound);

            // Test forward and backward searching in succession
            const string heavyText = "heavy";
            int countHeavyForward = CountOccurrances(doc, heavyText, displaySettings, false, true);
            Assert.IsTrue(countHeavyForward > 0);
            Assert.AreEqual(countHeavyForward, CountOccurrances(doc, heavyText, displaySettings, true, true));
            // More tests of case insensitive searching
            Assert.AreEqual(0, CountOccurrances(doc, heavyText.ToUpperInvariant(), displaySettings, false, true));
            Assert.AreEqual(countHeavyForward, CountOccurrances(doc, heavyText.ToUpperInvariant(), displaySettings, false, false));
            if (asSmallMolecules != RefinementSettings.ConvertToSmallMoleculesMode.masses_only)
                Assert.AreEqual(1, CountOccurrances(doc, "hgflpr", displaySettings, true, false));

            // Test mismatched transitions finder
            var missmatchFinder = new FindOptions().ChangeCustomFinders(new[] {new MismatchedIsotopeTransitionsFinder()});
            Assert.AreEqual(4, CountOccurrances(doc, missmatchFinder, displaySettings));
            var docRemoved = (SrmDocument) doc.RemoveChild(doc.Children[1]).RemoveChild(doc.Children[2]);
            Assert.AreEqual(0, CountOccurrances(docRemoved, missmatchFinder, displaySettings));
            var refineRemoveHeavy = new RefinementSettings {RefineLabelType = IsotopeLabelType.heavy};
            var docLight = refineRemoveHeavy.Refine(doc);
            Assert.AreEqual(0, CountOccurrances(docLight, missmatchFinder, displaySettings));
            var refineRemoveLight = new RefinementSettings {RefineLabelType = IsotopeLabelType.light};
            var docHeavy = refineRemoveLight.Refine(doc);
            Assert.AreEqual(0, CountOccurrances(docHeavy, missmatchFinder, displaySettings));
            var docMulti = ResultsUtil.DeserializeDocument("MultiLabel.sky", typeof(MultiLabelRatioTest));
            docMulti = (new RefinementSettings()).ConvertToSmallMolecules(docMulti, asSmallMolecules);
            Assert.AreEqual(0, CountOccurrances(docMulti, missmatchFinder, displaySettings));
            var pathTranMultiRemove = docMulti.GetPathTo((int) SrmDocument.Level.Transitions, 7);
            var tranMultiRemove = docMulti.FindNode(pathTranMultiRemove);
            var docMultiRemoved = (SrmDocument) docMulti.RemoveChild(pathTranMultiRemove.Parent, tranMultiRemove);
            Assert.AreEqual(2, CountOccurrances(docMultiRemoved, missmatchFinder, displaySettings));
            var tranGroupMultiRemove = docMulti.FindNode(pathTranMultiRemove.Parent);
            var docMultiGroupRemoved = (SrmDocument)
                docMulti.RemoveChild(pathTranMultiRemove.Parent.Parent, tranGroupMultiRemove);
            Assert.AreEqual(0, CountOccurrances(docMultiGroupRemoved, missmatchFinder, displaySettings));
        }

        private static int CountOccurrances(SrmDocument doc, string searchText,
            DisplaySettings displaySettings, bool reverse, bool caseSensitive)
        {
            IdentityPath pathFound = doc.SearchDocumentForString(IdentityPath.ROOT,
                searchText, displaySettings, reverse, caseSensitive);
            if (pathFound == null)
                return 0;

            IdentityPath pathFoundNext = pathFound;

            int i = 0;
            do
            {
                pathFoundNext = doc.SearchDocumentForString(pathFoundNext, searchText,
                    displaySettings, reverse, caseSensitive);
                i++;
            }
            while (!Equals(pathFound, pathFoundNext));
            return i;
        }

        private static int CountOccurrances(SrmDocument doc, FindOptions findOptions,
            DisplaySettings displaySettings)
        {
            var results = doc.SearchDocument(new Bookmark(IdentityPath.ROOT),
                findOptions, displaySettings);
            if (results == null)
                return 0;

            FindResult resultsNext = results;

            int i = 0;
            do
            {
                resultsNext = doc.SearchDocument(resultsNext.Bookmark, findOptions,
                    displaySettings);
                i++;
            }
            while (!Equals(resultsNext.Bookmark, results.Bookmark));
            return i;
        }

        private SrmDocument CreateStudy7Doc()
        {
            return ResultsUtil.DeserializeDocument("Study7_0-7.sky", GetType());
        }
    }
}
