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
using System.Diagnostics;
using System.Linq;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Controls.SeqNode;
using pwiz.Skyline.Model;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Tests Find Node.
    /// </summary>
    [TestClass]
    public class FindNodeTest
    {
        [TestMethod]
        public void TestFindNode()
        {
            SrmDocument doc = CreateStudy7Doc();
            var displaySettings = new DisplaySettings(null, false, 0, 0);
            // Find every other transition, searching down.
            List<TransitionDocNode> listTransitions = doc.Transitions.ToList();
            var pathFound = doc.GetPathTo(0, 0);
            int i;
            for (i = 0; i < doc.TransitionCount; i += 2)
            {
                pathFound = doc.SearchForString(pathFound, String.Format("{0:F04}", listTransitions[i].Mz), displaySettings, false, false);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Transitions, i), pathFound);
            }
            
            // Test wrapping in search down.
            pathFound = doc.SearchForString(pathFound, String.Format("{0:F04}", listTransitions[0].Mz), displaySettings, false, false);
            Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Transitions, 0), pathFound);

            // Find every other peptide searching up while for each finding one of its children searching down.
            pathFound = doc.LastNodePath;
            List<PeptideDocNode> listPeptides = new List<PeptideDocNode>();
            listPeptides.AddRange(doc.Peptides);
            List<TransitionGroupDocNode> listTransitionGroups = new List<TransitionGroupDocNode>();
            listTransitionGroups.AddRange(doc.TransitionGroups);
            for (int x = doc.PeptideCount; x > 0; x -= 2)
            {
                // Test case insensitivity.
                pathFound = doc.SearchForString(pathFound, listPeptides[x-1].Peptide.Sequence.ToLower(), displaySettings, true, false);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Peptides, x-1), pathFound);
                // Test parents can find children.
                pathFound = doc.SearchForString(pathFound, String.Format("{0:F04}", listTransitionGroups[x * 2 - 1].PrecursorMz), displaySettings, 
                    false, true);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.TransitionGroups, x * 2 - 1), pathFound);
                // Test Children can find parents.
                pathFound = doc.SearchForString(pathFound, listPeptides[x - 1].Peptide.Sequence.ToLower(), displaySettings, true, false);
                Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Peptides, x - 1), pathFound);
            }

            // Test wrapping in search up.
            pathFound = doc.SearchForString(pathFound, String.Format("{0:F04}", listTransitionGroups[listTransitionGroups.Count - 1].PrecursorMz), 
                displaySettings, false, true);
            Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.TransitionGroups, listTransitionGroups.Count - 1), pathFound);
            
            // Test children can find other parents.
            pathFound = doc.SearchForString(pathFound, listPeptides[0].Peptide.Sequence.ToLower(), displaySettings, true, false);
            Assert.AreEqual(doc.GetPathTo((int)SrmDocument.Level.Peptides, 0), pathFound);
        }

        private static SrmDocument CreateStudy7Doc()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            var stream = typeof(PasteTest).Assembly.GetManifestResourceStream("pwiz.SkylineTestA.Study7_0-7.sky");
            Assert.IsNotNull(stream);
            Debug.Assert(stream != null);   // Keep ReSharper from warning
            SrmDocument docStudy7 = (SrmDocument)xmlSerializer.Deserialize(stream);
            return docStudy7;
        }
    }
}
