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
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Summary description for SrmDocEditTest
    /// </summary>
    [TestClass]
    public class SrmDocEditTest : AbstractUnitTest
    {
        /// <summary>
        /// Test of SrmDocument.ImportFasta functionality
        /// </summary>
        [TestMethod]
        public void ImportFastaTest()
        {
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault0_6());
            IdentityPath path = IdentityPath.ROOT;
            SrmDocument docFasta = document.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST), false, path, out path);
            AssertEx.IsDocumentState(docFasta, 1, 2, 98, 311);
            Assert.AreEqual("YAL001C", ((PeptideGroupDocNode)docFasta.Children[0]).Name);
            Assert.AreEqual("YAL002W", ((PeptideGroupDocNode)docFasta.Children[1]).Name);
            Assert.AreEqual(1, path.Length);
            Assert.IsInstanceOfType(path.GetIdentity(0), typeof(FastaSequence));
            Assert.AreEqual("YAL001C", ((FastaSequence) path.GetIdentity(0)).Name);
            int maxMz = docFasta.Settings.TransitionSettings.Instrument.MaxMz - 120;
            foreach (PeptideGroupDocNode nodeGroup in docFasta.Children)
            {
                Assert.IsInstanceOfType(nodeGroup.Id, typeof(FastaSequence));

                int lastEnd = docFasta.Settings.PeptideSettings.Filter.ExcludeNTermAAs - 1;

                foreach (PeptideDocNode nodePeptide in nodeGroup.Children)
                {
                    Peptide peptide = nodePeptide.Peptide;
                    char prev = peptide.PrevAA;
                    if (prev != 'K' && prev != 'R')
                        Assert.Fail("Unexpected preceding cleavage at {0}", prev);
                    string seq = peptide.Sequence;
                    char last = seq[seq.Length - 1];
                    if (last != 'K' && last != 'R' && peptide.NextAA != '-')
                        Assert.Fail("Unexpected cleavage at {0}", last);
                    Assert.IsNotNull(peptide.Begin);
                    Assert.IsNotNull(peptide.End);
                    
                    // Make sure peptides are ordered, and not overlapping
                    if (peptide.Begin.Value < lastEnd)
                        Assert.Fail("Begin {0} less than last end {1}.", peptide.Begin.Value, lastEnd);
                    lastEnd = peptide.End.Value;

                    IList<DocNode> nodesTrans = ((DocNodeParent) nodePeptide.Children[0]).Children;
                    int trans = nodesTrans.Count;
                    if (trans < 3)
                    {
                        // Might have been cut off by the instrument limit.
                        if ((trans == 0 && ((TransitionGroupDocNode)nodePeptide.Children[0]).PrecursorMz < maxMz) ||
                                (trans > 0 && ((TransitionDocNode)nodesTrans[0]).Mz < maxMz))
                            Assert.Fail("Found {0} transitions, expecting 3.", trans);
                    }
                    // Might have extra proline transitions
                    else if (trans > 3 && peptide.Sequence.IndexOf('P') == -1)
                        Assert.Fail("Found {0} transitions, expecting 3.", trans);

                    // Make sure transitions are ordered correctly
                    IonType lastType = IonType.a;
                    int lastOffset = -1;
                    foreach (TransitionDocNode nodeTran in nodesTrans)
                    {
                        Transition transition = nodeTran.Transition;
                        if (lastType == transition.IonType)
                            Assert.IsTrue(transition.CleavageOffset > lastOffset);
                        else
                            Assert.IsTrue(((int)transition.IonType) > ((int) lastType));
                        lastType = transition.IonType;
                        lastOffset = transition.CleavageOffset;
                    }
                }
            }

            // Make sure old document is unmodified.
            Assert.AreEqual(0, document.RevisionIndex);
            Assert.AreEqual(0, document.PeptideTransitionCount);

            // Re-paste of fasta should have no impact.
            // path = IdentityPath.ROOT; use null as substitute for Root
            SrmDocument docFasta2 = docFasta.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST), false, null, out path);
            // Returns the original document to avoid adding undo record in running app
            Assert.AreSame(docFasta, docFasta2);
            Assert.IsNull(path);

            // Discard double-insert document, and add peptides list into previous document
            path = IdentityPath.ROOT;
            SrmDocument docPeptides = docFasta.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1), true, path, out path);
            AssertEx.IsDocumentState(docPeptides, 2, 3, 111, 352);
            Assert.AreEqual(1, path.Length);
            Assert.IsNotInstanceOfType(path.GetIdentity(0), typeof(FastaSequence));
            Assert.AreEqual("Peptides1", ((PeptideGroupDocNode)docPeptides.FindNode(path)).Name);
            PeptideGroupDocNode nodePepList = (PeptideGroupDocNode) docPeptides.Children[2];
            Assert.IsNotInstanceOfType(nodePepList.Id, typeof(FastaSequence));
            // Make sure other two nodes are unchanged
            Assert.AreSame(docFasta.Children[0], docPeptides.Children[0]);
            Assert.AreSame(docFasta.Children[1], docPeptides.Children[1]);

            foreach (PeptideDocNode nodePeptide in nodePepList.Children)
            {
                char prev = nodePeptide.Peptide.PrevAA;
                char next = nodePeptide.Peptide.NextAA;
                if (prev != 'X' || next != 'X')
                    Assert.Fail("Expected amino acids X, but found {0} or {1}", prev, next);
                string seq = nodePeptide.Peptide.Sequence;
                char last = seq[seq.Length - 1];
                // Just because they are tryptic peptides in the list
                if (last != 'K' && last != 'R' && nodePeptide.Peptide.NextAA != '-')
                    Assert.Fail("Unexpected cleavage at {0}", last);
                Assert.IsNull(nodePeptide.Peptide.Begin);
                Assert.IsNull(nodePeptide.Peptide.End);

                IList<DocNode> nodesTrans = ((DocNodeParent)nodePeptide.Children[0]).Children;
                int trans = nodesTrans.Count;
                if (trans < 3)
                {
                    // Might have been cut off by the instrument limit.
                    if ((trans == 0 && ((TransitionGroupDocNode)nodePeptide.Children[0]).PrecursorMz < maxMz) ||
                            (trans > 0 && ((TransitionDocNode)nodesTrans[0]).Mz < maxMz))
                        Assert.Fail("Found {0} transitions, expecting 3.", trans);
                }
                // Might have extra proline transitions
                else if (trans > 3 && nodePeptide.Peptide.Sequence.IndexOf('P') == -1)
                    Assert.Fail("Found {0} transitions, expecting 3.", trans);
            }

            // Make sure old documents are unmodified.
            AssertEx.IsDocumentState(document, 0, 0, 0, 0);
            AssertEx.IsDocumentState(docFasta, 1, 2, 98, 311);
            AssertEx.IsDocumentState(docPeptides, 2, 3, 111, 352);

            // Add peptides in all possible locations.
            // 1. Root (already done)
            // 1. Before another group
            path = docPeptides.GetPathTo(0);
            SrmDocument docPeptides2 = docPeptides.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1), true, path, out path);
            AssertEx.IsDocumentState(docPeptides2, 3, 4, 124, 393);
            Assert.IsNotInstanceOfType(docPeptides2.Children[0].Id, typeof(FastaSequence));
            Assert.AreEqual(docPeptides2.Children[0].Id, path.GetIdentity(0));
            Assert.IsInstanceOfType(docPeptides2.Children[1].Id, typeof(FastaSequence));
            // Make sure previously existing groups are unchanged
            Assert.AreSame(docPeptides.Children[0], docPeptides2.Children[1]);
            Assert.AreSame(docPeptides.Children[1], docPeptides2.Children[2]);
            Assert.AreSame(docPeptides.Children[2], docPeptides2.Children[3]);

            // 2. Inside a FASTA group
            path = docPeptides2.GetPathTo((int) SrmDocument.Level.Transitions, 100);
            SrmDocument docPeptides3 = docPeptides2.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1), true, path, out path);
            AssertEx.IsDocumentState(docPeptides3, 4, 5, 137, 434);
            Assert.AreEqual(2, docPeptides3.FindNodeIndex(path));
            // Make sure previously existing groups are unchanged
            Assert.AreSame(docPeptides2.Children[1], docPeptides3.Children[1]);
            Assert.AreSame(docPeptides2.Children[2], docPeptides3.Children[3]);

            // 3. To a peptide list
            //    a. Same peptides
            path = docPeptides2.GetPathTo(0);
            docPeptides3 = docPeptides2.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1), true, path, out path);
            // No longer filter repeated peptides, because they are useful for explicit modifictations.
            Assert.AreNotSame(docPeptides2, docPeptides3);
            Assert.IsNotNull(path);

            //    b. Different paptides
            path = docPeptides2.GetPathTo(0);
            IdentityPath pathFirstPep = docPeptides3.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            docPeptides3 = docPeptides2.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES2), true, path, out path);
            AssertEx.IsDocumentState(docPeptides3, 4, 4, 140, 448);
            Assert.AreSame(docPeptides2.Children[0].Id, docPeptides3.Children[0].Id);
            Assert.AreNotSame(docPeptides2.Children[0], docPeptides3.Children[0]);
            Assert.AreEqual("LVTDLTK", ((PeptideDocNode) docPeptides3.FindNode(path)).Peptide.Sequence);
            int index = docPeptides3.FindNodeIndex(path);
            IdentityPath pathPreceding = docPeptides3.GetPathTo(path.Depth, index - 1);
            Assert.AreEqual("IVGYLDEEGVLDQNR", ((PeptideDocNode)docPeptides3.FindNode(pathPreceding)).Peptide.Sequence);
            Assert.AreEqual(0, docPeptides3.FindNodeIndex(pathFirstPep));

            // 4. At a peptide in a peptide list
            path = docPeptides2.GetPathTo((int) SrmDocument.Level.Molecules, 0);
            docPeptides3 = docPeptides2.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES2), true, path, out path);
            AssertEx.IsDocumentState(docPeptides3, 4, 4, 140, 448);
            Assert.AreSame(docPeptides2.Children[0].Id, docPeptides3.Children[0].Id);
            Assert.AreNotSame(docPeptides2.Children[0], docPeptides3.Children[0]);
            Assert.AreEqual(0, docPeptides3.FindNodeIndex(path));
            Assert.AreEqual(16, docPeptides3.FindNodeIndex(pathFirstPep));

            // 5. Inside a peptide in a peptide list
            path = docPeptides2.GetPathTo((int)SrmDocument.Level.Transitions, 0);
            docPeptides3 = docPeptides2.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES2), true, path, out path);
            AssertEx.IsDocumentState(docPeptides3, 4, 4, 140, 448);
            Assert.AreSame(docPeptides2.Children[0].Id, docPeptides3.Children[0].Id);
            Assert.AreNotSame(docPeptides2.Children[0], docPeptides3.Children[0]);
            Assert.AreEqual(1, docPeptides3.FindNodeIndex(path));
            Assert.AreEqual(0, docPeptides3.FindNodeIndex(pathFirstPep));            
        }

        /// <summary>
        /// Test of <see cref="SrmDocument.MoveNode"/> functionality
        /// </summary>
        [TestMethod]
        public void MoveNodeTest()
        {
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault());
            IdentityPath path = IdentityPath.ROOT;
            SrmDocument docFasta = document.ImportFasta(new StringReader(ExampleText.TEXT_FASTA_YEAST), false, path, out path);
            // 1. From peptide group to root
            SrmDocument docMoved = docFasta.MoveNode(docFasta.GetPathTo(0), IdentityPath.ROOT, out path);
            Assert.AreEqual(1, docMoved.FindNodeIndex(path));
            Assert.AreSame(docFasta.Children[0], docMoved.Children[1]);
            Assert.AreSame(docFasta.Children[1], docMoved.Children[0]);
            // 2. From peptide group to before other peptide group
            docMoved = docFasta.MoveNode(docFasta.GetPathTo(1), docFasta.GetPathTo(0), out path);
            Assert.AreEqual(0, docMoved.FindNodeIndex(path));
            Assert.AreSame(docFasta.Children[0], docMoved.Children[1]);
            Assert.AreSame(docFasta.Children[1], docMoved.Children[0]);

            // Some peptide lists
            IdentityPath pathPeptides;
            SrmDocument docPeptides = docFasta.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES2), true,
                                                           docFasta.GetPathTo(1), out pathPeptides);
            docPeptides = docPeptides.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1), true,
                                               IdentityPath.ROOT, out path);
            docPeptides = docPeptides.MoveNode(path, pathPeptides, out pathPeptides);
            Assert.AreEqual(1, docPeptides.FindNodeIndex(pathPeptides));

            // 3. Peptide from one group to another
            IdentityPath fromParent = docPeptides.GetPathTo(2);
            IdentityPath from = new IdentityPath(fromParent, ((DocNodeParent)docPeptides.FindNode(fromParent)).Children[0].Id);
            SrmDocument docPeptides2 = docPeptides.MoveNode(from, pathPeptides, out path);
            Assert.AreEqual(pathPeptides, path.Parent);
            Assert.AreEqual(((DocNodeParent)docPeptides.Children[1]).Children.Count,
                ((DocNodeParent)docPeptides2.Children[1]).Children.Count - 1);
            Assert.AreEqual(((DocNodeParent)docPeptides.Children[2]).Children.Count,
                ((DocNodeParent)docPeptides2.Children[2]).Children.Count + 1);
            // Though moved to a different group, this should not have changed the overall
            // peptide order, since it was moved from the beginning of one group to the end
            // of the group before it.
            Assert.AreEqual(docPeptides.FindNodeIndex(from), docPeptides2.FindNodeIndex(path));

            // 4. To before another peptide
            from = new IdentityPath(fromParent, ((DocNodeParent)docPeptides2.FindNode(fromParent)).Children[0].Id);
            IdentityPath path2;
            SrmDocument docPeptides3 = docPeptides2.MoveNode(from, path, out path2);
            Assert.AreEqual(pathPeptides, path.Parent);
            Assert.AreEqual(((DocNodeParent)docPeptides2.Children[1]).Children.Count,
                ((DocNodeParent)docPeptides3.Children[1]).Children.Count - 1);
            Assert.AreEqual(((DocNodeParent)docPeptides2.Children[2]).Children.Count,
                ((DocNodeParent)docPeptides3.Children[2]).Children.Count + 1);
            // Relative to all peptides, index should be 1 less than before
            Assert.AreEqual(docPeptides2.FindNodeIndex(from), docPeptides3.FindNodeIndex(path2) + 1);

            // 5. To within another peptide
            IdentityPath to = new IdentityPath(path, ((DocNodeParent)docPeptides3.FindNode(path)).Children[0].Id);
            SrmDocument docPeptides4 = docPeptides3.MoveNode(path2, to, out path);
            // Should not have changed to count in the group
            Assert.AreEqual(((DocNodeParent)docPeptides3.Children[1]).Children.Count,
                ((DocNodeParent)docPeptides4.Children[1]).Children.Count);
            // Relative to all peptides, should have been returned to original order
            Assert.AreEqual(docPeptides2.FindNodeIndex(from), docPeptides4.FindNodeIndex(path));

            // Make sure expected exceptions are thrown
            Assert.IsNull(docPeptides4.FindNode(from));
            AssertEx.ThrowsException<IdentityNotFoundException>(() =>
                docPeptides4.MoveNode(from, to, out path));
            AssertEx.ThrowsException<InvalidOperationException>(() =>
                docPeptides2.MoveNode(from, docPeptides2.GetPathTo(0), out path));
            AssertEx.ThrowsException<InvalidOperationException>(() =>
                docPeptides3.MoveNode(docPeptides2.GetPathTo((int) SrmDocument.Level.Molecules, 0), to, out path));
            AssertEx.ThrowsException<InvalidOperationException>(() =>
                docPeptides3.MoveNode(docPeptides2.GetPathTo((int)SrmDocument.Level.Transitions, 0), to, out path));
        }

        /// <summary>
        /// Test of functionality once performed by SrmDocument.RemoveDuplicates,
        /// now refinement.
        /// </summary>
        [TestMethod]
        public void RemoveDuplicatePeptidesTest()
        {
            // First try removals with no impact
            SrmDocument document = new SrmDocument(SrmSettingsList.GetDefault0_6());
            SrmDocument docFasta = document.ImportFasta(new StringReader(string.Format(TEXT_FASTA_YEAST_FRAGMENT, 1)),
                false, IdentityPath.ROOT, out _);
            AssertEx.IsDocumentState(docFasta, 1, 1, 11, 36);
            var refinementSettings = new RefinementSettings {RemoveDuplicatePeptides = true};
            SrmDocument docFasta2 = refinementSettings.Refine(docFasta);
            Assert.AreSame(docFasta, docFasta2);

            docFasta2 = docFasta.ImportFasta(new StringReader(string.Format(TEXT_FASTA_YEAST_FRAGMENT, 2)),
                false, IdentityPath.ROOT, out _);
            // Adding same sequence twice, even with different custom names is ignored
            Assert.AreSame(docFasta, docFasta2);

            // Try a successful removal of duplicates that leaves no peptides
            SrmDocument docPeptides = document.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1),
                true, IdentityPath.ROOT, out _);
            SrmDocument docPeptides2 = docPeptides.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1),
                true, IdentityPath.ROOT, out _);            
            AssertEx.IsDocumentState(docPeptides2, 2, 2, 26, 82);
            SrmDocument docPeptides3 = refinementSettings.Refine(docPeptides2);
            Assert.AreNotSame(docPeptides2, docPeptides3);
            AssertEx.IsDocumentState(docPeptides3, 3, 2, 0, 0);

            // Try again leaving a single peptide
            docPeptides2 = docPeptides.ImportFasta(new StringReader(TEXT_BOVINE_PEPTIDES1 + "\n" + TEXT_BOVINE_SINGLE_PEPTIDE),
                true, IdentityPath.ROOT, out _);
            docPeptides3 = refinementSettings.Refine(docPeptides2);
            Assert.AreNotSame(docPeptides2, docPeptides3);
            AssertEx.IsDocumentState(docPeptides3, 3, 2, 1, 3);
        }

        public const string TEXT_FASTA_YEAST_FRAGMENT = PeptideGroupBuilder.PEPTIDE_LIST_PREFIX + "Sequence{0}\n" +
            "MVLTIYPDELVQIVSDKIASNKGKITLNQLWDISGKYFDLSDKKVKQFVLSCVILKKDIE\n" +
            "VYCDGAITTKNVTDIIGDANHSYSVGITEDSLWTLLTGYTKKESTIGNSAFELLLEVAKS\n" +
            "GEKGINTMDLAQVTGQDPRSVTGRIKKINHLLTSSQLIYKGHVVKQLKLKKFSHDGVDSN\n" +
            "PYINIRDHLATIVEVVKRSKNGIRQIIDLKRELKFDKEKRLSKAFIAAIAWLDEKEYLKK\n" +
            "VLVVSPKNPAIKIRCVKYVKDIPDSKGSPSFEYDSNSADEDSVSDSKAAFEDEDLVEGLD\n" +
            "NFNATDLLQNQGLVMEEKEDAVKNEVLLNRFYPLQNQTYDIADKSGLKGISTMDVVNRIT";

        public const string TEXT_BOVINE_PEPTIDES1 = PeptideGroupBuilder.PEPTIDE_LIST_PREFIX + "Peptides1\n" +
                                                "FALPQYLK\n" +
                                                "ALNEINQFYQK\n" +
                                                "ALPMHIR\n" +
                                                "VLDALDSIK\n" +
                                                "YNLGLDLR\n" +
                                                "TAAYVNAIEK\n" +
                                                "LQHGTILGFPK\n" +
                                                "YSTDVSVDEVK\n" +
                                                "HGGTIPIVPTAEFQDR\n" +
                                                "DGGIDPLVR\n" +
                                                "IHGFDLAAINLQR\n" +
                                                "FWWENPGVFTEK\n" +
                                                "IVGYLDEEGVLDQNR";

        public const string TEXT_BOVINE_PEPTIDES2 = PeptideGroupBuilder.PEPTIDE_LIST_PREFIX + "Peptides2\n" +
                                                "LVTDLTK\n" +
                                                "AEFVEVTK\n" +
                                                "LVNELTEFAK\n" +
                                                "HLVDEPQNLIK\n" +
                                                "LGEYGFQNALIVR\n" +
                                                "DAIPENLPPLTADFAEDK\n" +
                                                "ALASLMTYK\n" +
                                                "CAVVDVPFGGAK\n" +
                                                "DIVHSGLAYTMER\n" +
                                                "VPCFLAGDFR\n" +
                                                "DYLPIVLGSEMQK\n" +
                                                "AGFVCPTPPYQSLAR\n" +
                                                "VPQLEIVPNSAEER\n" +
                                                "LPQEVLNENLLR\n" +
                                                "YLGYLEQLLR\n" +
                                                "FFVAPFPEVFGK";

        public const string TEXT_BOVINE_SINGLE_PEPTIDE = "YLGYLEQLLR";
    }
}