/*
 * Original author: Alana Killeen <killea .at. u.washington.edu>,
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
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings.Extensions;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Functional test for CopyPaste.
    /// </summary>
    [TestClass]
    public class CopyPasteTest : AbstractFunctionalTest
    {
        private const string TEXT_INDENT = "    ";
        private const string TEXT_LINE_BREAK = "\r\n";

        private const string HTML_INDENT = "&nbsp;&nbsp;&nbsp;&nbsp;";
        private const string HTML_LINE_BREAK = "<br>\r\n";

        [TestMethod]
        public void TestCopyPaste()
        {
            TestFilesZip = @"TestFunctional\CopyPasteTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            RunUI(() => SkylineWindow.OpenFile(TestFilesDir.GetTestPath("CE_Vantage_15mTorr_scheduled_mini_withMod.sky")));
            WaitForGraphs();

            RunUI(() =>
            {
                SequenceTree sequenceTree = SkylineWindow.SequenceTree;

                // Test single node copy.
                sequenceTree.ExpandAll();
                sequenceTree.KeysOverride = Keys.None;
                sequenceTree.SelectedNode = sequenceTree.Nodes[0];
                SkylineWindow.Copy();
                CheckCopiedNodes(0, 1);

                // Test multiple selection copy.
                sequenceTree.KeysOverride = Keys.Shift;
                sequenceTree.SelectedNode = sequenceTree.Nodes[1];
                SkylineWindow.Copy();

                // Test multiple selection disjoint, reverse order copy.
                CheckCopiedNodes(0, 1);
                Assert.AreSame(sequenceTree, SkylineWindow.SequenceTree);
                sequenceTree.KeysOverride = Keys.None;
                sequenceTree.SelectedNode = sequenceTree.Nodes[1].Nodes[0];
                sequenceTree.KeysOverride = Keys.Control;
                sequenceTree.SelectedNode = sequenceTree.Nodes[0].Nodes[0];
                SkylineWindow.Copy();
                // After copying in reverse order, reselect the nodes in sorted order so we don't have to
                // sort them in the test code.
                sequenceTree.KeysOverride = Keys.None;
                sequenceTree.SelectedNode = sequenceTree.Nodes[0];
                sequenceTree.SelectedNode = sequenceTree.Nodes[0].Nodes[0];
                sequenceTree.KeysOverride = Keys.Control;
                sequenceTree.SelectedNode = sequenceTree.Nodes[1].Nodes[0];
                Assert.AreSame(sequenceTree, SkylineWindow.SequenceTree);
                CheckCopiedNodes(1, 2);

                // Test no space between parent and descendents if immediate child is not selected.
                sequenceTree.KeysOverride = Keys.None;
                sequenceTree.SelectedNode = sequenceTree.Nodes[0];
                sequenceTree.KeysOverride = Keys.Control;
                sequenceTree.SelectedNode = sequenceTree.Nodes[0].Nodes[0].Nodes[0].Nodes[0];
                sequenceTree.SelectedNode = sequenceTree.Nodes[0].Nodes[0].Nodes[0].Nodes[2];
                SkylineWindow.Copy();
                CheckCopiedNodes(0, 1);

                // Test paste menu item enabled, copy menu item disabled when dummy node is selected.
                sequenceTree.KeysOverride = Keys.None;
                sequenceTree.SelectedNode =
                  sequenceTree.Nodes[SkylineWindow.SequenceTree.Nodes.Count - 1];
                Assert.IsFalse(SkylineWindow.CopyMenuItemEnabled(), "Copy menu should not be enabled");
                Assert.IsTrue(SkylineWindow.PasteMenuItemEnabled());

                // Test FASTA sequence copy HTML formatting
                sequenceTree.SelectedNode = sequenceTree.Nodes[0];
                SkylineWindow.Copy();
                string clipboardHtml = GetClipboardHtml();
                Assert.AreEqual(4, Regex.Matches(clipboardHtml, "<font style=\"font-weight: bold").Count);
                Assert.AreEqual(4, Regex.Matches(clipboardHtml, "</font>").Count);
                Assert.AreEqual(1, Regex.Matches(clipboardHtml, "color: blue").Count);

                sequenceTree.SelectedNode = sequenceTree.Nodes[1];
                SkylineWindow.Copy();
                clipboardHtml = GetClipboardHtml();
                Assert.AreEqual(19, Regex.Matches(clipboardHtml, "<font style=\"font-weight: bold").Count);
                Assert.AreEqual(19, Regex.Matches(clipboardHtml, "</font>").Count);
                Assert.AreEqual(2, Regex.Matches(clipboardHtml, "color: blue").Count);

                sequenceTree.SelectedNode = sequenceTree.Nodes[2];
                SkylineWindow.Copy();
                clipboardHtml = GetClipboardHtml();
                Assert.AreEqual(18, Regex.Matches(clipboardHtml, "<font style=\"font-weight: bold").Count);
                Assert.AreEqual(18, Regex.Matches(clipboardHtml, "</font>").Count);
                Assert.AreEqual(2, Regex.Matches(clipboardHtml, "color: blue").Count);

                // Test clipboard HTML contains formatting for modified peptides.
                sequenceTree.SelectedNode = sequenceTree.Nodes[3].Nodes[0];
                SkylineWindow.Copy();
                clipboardHtml = GetClipboardHtml();
                Assert.IsTrue(clipboardHtml.Contains("font") && clipboardHtml.Contains("color"));
            });

            // Paste a protein list
            var document = WaitForDocumentLoaded();
            RunUI(() =>
                      {
                          SetClipboardText(PROTEINLIST_CLIPBOARD_TEXT);
                          SkylineWindow.Paste();
                      });
            var docPaste = WaitForDocumentChange(document);
            Assert.AreEqual(document.PeptideGroupCount + 3, docPaste.PeptideGroupCount);
            Assert.AreEqual("P23978", docPaste.PeptideGroups.Last().ProteinMetadata.Accession);  // Did builtin IPI conversion work?

            // Paste an invalid protein list
            RunDlg<MessageDlg>(() =>
                                   {
                                       SetClipboardText(PROTEINLIST_CLIPBOARD_TEXT.Replace("MDAL", "***"));
                                       SkylineWindow.Paste();
                                   },
                                   msgDlg => msgDlg.OkDialog());
            Assert.AreSame(docPaste, WaitForProteinMetadataBackgroundLoaderCompletedUI());

            // Test border case where protein/peptide/transition list contains a blank line
            // Should give generic error, not crash
            RunDlg<MessageDlg>(() =>
                                   {
                                        SetClipboardText(PROTEIN_LIST_BAD_FORMAT);
                                        SkylineWindow.Paste();
                                   },
                msgDlg =>
                {
                    Assert.AreEqual(msgDlg.Message, Resources.CopyPasteTest_DoTest_Could_not_read_the_pasted_transition_list___Transition_list_must_be_in_separated_columns_and_cannot_contain_blank_lines_);
                    msgDlg.OkDialog();
                });

            // Paste peptides
            var precursorAdduct = Adduct.DOUBLY_PROTONATED;
            List<Tuple<string, int>> peptidePaste = new List<Tuple<string, int>>
            {
                new Tuple<string, int>("FVEGLPINDFSR", 3),
                new Tuple<string, int>("FVEGLPINDFSR", 2),
                new Tuple<string, int>("FVEGLPINDFSR", 0),  // Same as charge 2
                new Tuple<string, int>("DLNELQALIEAHFENR", 0),
                new Tuple<string, int>(string.Format("C[+{0:F01}]QPLELAGLGFAELQDLC[+{1:F01}]R", 57.0, 57.0), 3),
                new Tuple<string, int>("PEPTIDER", 5),
                new Tuple<string, int>("PEPTIDER", 15)
            };
            var peptidePasteSb = new StringBuilder();
            foreach (var pep in peptidePaste)
                peptidePasteSb.AppendLine(pep.Item1 + Transition.GetChargeIndicator(Adduct.FromChargeProtonated(pep.Item2)));

            RunUI(() =>
            {
                SkylineWindow.NewDocument(true);
                document = SkylineWindow.Document;
                document = document.ChangeSettings(document.Settings.ChangeTransitionFilter(f => f.ChangePeptidePrecursorCharges(new[] {precursorAdduct})));
                SetClipboardText(peptidePasteSb.ToString());
                SkylineWindow.Paste();
            });
            document = WaitForDocumentChange(document);
            Assert.AreEqual(peptidePaste.Select(t => t.Item1).Distinct().Count(), document.PeptideCount);
            var unseenPasted = new List<Tuple<string, int>>(peptidePaste);
            for (int i = 0; i < document.PeptideTransitionGroupCount; i++)
            {
                var nodeGroup = document.PeptideTransitionGroups.ElementAt(i);
                string seq = nodeGroup.TransitionGroup.Peptide.Sequence;
                var charge = nodeGroup.PrecursorAdduct.AdductCharge;
                var unseenLeft = unseenPasted.Where(t =>
                    !Equals(seq, FastaSequence.StripModifications(t.Item1)) || charge != (t.Item2 == 0 ? 2 : t.Item2)).ToList();
                if (unseenPasted.Count == unseenLeft.Count)
                {
                    Assert.Fail("Unexpected precursor {0}{1} added to document",
                        nodeGroup.TransitionGroup.Peptide.Sequence,
                        Transition.GetChargeIndicator(nodeGroup.TransitionGroup.PrecursorCharge));
                }

                unseenPasted = unseenLeft;
            }
            Assert.AreEqual(0, unseenPasted.Count);
            AssertEx.IsDocumentState(document, null, 1, 4, 6, 21);

            // Undo paste
            RunUI(() => SkylineWindow.Undo());
            document = WaitForDocumentChange(document);
            // Change precursor charges
            Adduct[] precursorCharges = { Adduct.DOUBLY_PROTONATED, Adduct.TRIPLY_PROTONATED, Adduct.QUADRUPLY_PROTONATED };
            RunUI(() => SkylineWindow.ModifyDocument("Change precursor charges", doc => doc.ChangeSettings((document.Settings.ChangeTransitionFilter(f => f.ChangePeptidePrecursorCharges(precursorCharges))))));
            document = WaitForDocumentChange(document);
            // Re-paste in peptides
            RunUI(() => SkylineWindow.Paste());
            document = WaitForDocumentChange(document);
            foreach (var peptide in peptidePaste)
            {
                if (peptide.Item2 > 0)
                {
                    // Pasted peptides with a charge indicator should have a single precursor with the specified charge state
                    VerifyContainsPrecursor(document, peptide.Item1, peptide.Item2);
                }
                else
                {
                    // Pasted peptides with no charge indicator should have a precursor for every charge state in transition filter settings
                    for (int i = 0; i < precursorCharges.Length; i++)
                        VerifyContainsPrecursor(document, peptide.Item1, precursorCharges[i].AdductCharge);
                }
            }
            AssertEx.IsDocumentState(document, null, 1, 4, 9, 31);
        }

        private static void VerifyContainsPrecursor(SrmDocument document, string modSequence, int charge)
        {
            if (!document.PeptideTransitionGroups.Contains(nodeGroup =>
                    Equals(nodeGroup.Peptide.Sequence, FastaSequence.StripModifications(modSequence)) &&
                    nodeGroup.PrecursorCharge == charge))
            {
                Assert.Fail("Precursor {0}{1} not found in the document after paste", modSequence, Transition.GetChargeIndicator(charge));
            }
        }


        // Check that actual clipboard text and HTML match the expected clipboard text and HTML.
        private static void CheckCopiedNodes(int shallowestLevel, int expectedLineBreaks)
        {
            StringBuilder htmlSb = new StringBuilder();
            StringBuilder textSb = new StringBuilder();

            int lineBreaks = 0;

            var seqTree = SkylineWindow.SequenceTree;

            foreach(TreeNodeMS node in seqTree.SelectedNodes)
            {
                IClipboardDataProvider provider = node as IClipboardDataProvider;
                DataObject data = provider == null ? null : provider.ProvideData();
                if (data == null)
                    continue;
                int levels = node.Level - shallowestLevel;
                string providerHtml = (string)data.GetData(DataFormats.Html);
                if (providerHtml != null)
                    AppendText(htmlSb, new HtmlFragment(providerHtml).Fragment,
                        HTML_LINE_BREAK, HTML_INDENT, levels, lineBreaks);
                string providerText = (string)data.GetData("Text");
                if (providerText != null)
                    AppendText(textSb, providerText, TEXT_LINE_BREAK, TEXT_INDENT, levels, lineBreaks);

                lineBreaks = expectedLineBreaks;
            }
            htmlSb.AppendLine();
            textSb.AppendLine();

            Assert.AreEqual(textSb.ToString(), GetClipboardText());
            Assert.AreEqual(htmlSb.ToString(), new HtmlFragment(GetClipboardHtml()).Fragment);
        }

        private static string GetClipboardHtml()
        {
            try
            {
                return (string)ClipboardEx.GetData(DataFormats.Html);
            }
            catch (ExternalException)
            {
                Assert.Fail(ClipboardHelper.GetPasteErrorMessage());
            }
            return null;
        }

        private static string GetClipboardText()
        {
            try
            {
                return ClipboardEx.GetText();
            }
            catch (ExternalException)
            {
                Assert.Fail(ClipboardHelper.GetPasteErrorMessage());
            }
            return null;
        }

        private static void AppendText(StringBuilder sb, string text, string lineSep, string indent, int levels, int lineBreaks)
        {
            for (int i = 0; i < lineBreaks; i++)
                sb.Append(lineSep);
            for (int i = 0; i < levels; i++)
                sb.Append(indent);
            sb.Append(text);
        }

        private const string PROTEINLIST_CLIPBOARD_TEXT =
            @"IPI:IPI00187591.3|SWISS-PROT:Q4V8C5-1|ENSEMBL:ENSRNOP00000023455	MDALEEESFALSFSSASDAEFDAVVGCLEDIIMDAEFQLLQRSFMDKYYQEFEDTEENKLTYTPIFNEYISLVEKYIEEQLLERIPGFNMAAFTTTLQHHKDEVAGDIFDMLLTFTDFLAFKEMFLDYRAEKEGRGLDLSSGLVVTSLCKSSSTPASQNNLRH
IPI:IPI00187593.1|SWISS-PROT:P23977|ENSEMBL:ENSRNOP00000024015;ENSRNOP00000047272|REFSEQ:NP_036826	MSKSKCSVGPMSSVVAPAKESNAVGPREVELILVKEQNGVQLTNSTLINPPQTPVEAQERETWSKKIDFLLSVIGFAVDLANVWRFPYLCYKNGGGAFLVPYLLFMVIAGMPLFYMELALGQFNREGAAGVWKICPVLKGVGFTVILISFYVGFFYNVIIAWALHYFFSSFTMDLPWIHCNNTWNSPNCSDAHASNSSDGLGLNDTFGTTPAAEYFERGVLHLHQSRGIDDLGPPRWQLTACLVLVIVLLYFSLWKGVKTSGKVVWITATMPYVVLTALLLRGVTLPGAMDGIRAYLSVDFYRLCEASVWIDAATQVCFSLGVGFGVLIAFSSYNKFTNNCYRDAIITTSINSLTSFSSGFVVFSFLGYMAQKHNVPIRDVATDGPGLIFIIYPEAIATLPLSSAWAAVFFLMLLTLGIDSAMGGMESVITGLVDEFQLLHRHRELFTLGIVLATFLLSLFCVTNGGIYVFTLLDHFAAGTSILFGVLIEAIGVAWFYGVQQFSDDIKQMTGQRPNLYWRLCWKLVSPCFLLYVVVVSIVTFRPPHYGAYIFPDWANALGWIIATSSMAMVPIYATYKFCSLPGSFREKLAYAITPEKDHQLVDRGEVRQFTLRHWLLL
IPI:IPI00187596.1|SWISS-PROT:P23978|ENSEMBL:ENSRNOP00000009705|REFSEQ:NP_077347	MATDNSKVADGQISTEVSEAPVASDKPKTLVVKVQKKAGDLPDRDTWKGRFDFLMSCVGYAIGLGNVWRFPYLCGKNGGGAFLIPYFLTLIFAGVPLFLLECSLGQYTSIGGLGVWKLAPMFKGVGLAAAVLSFWLNIYYIVIISWAIYYLYNSFTTTLPWKQCDNPWNTDRCFSNYSLVNTTNMTSAVVEFWERNMHQMTDGLDKPGQIRWPLAITLAIAWVLVYFCIWKGVGWTGKVVYFSATYPYIMLIILFFRGVTLPGAKEGILFYITPNFRKLSDSEVWLDAATQIFFSYGLGLGSLIALGSYNSFHNNVYRDSIIVCCINSCTSMFAGFVIFSIVGFMAHVTKRSIADVAASGPGLAFLAYPEAVTQLPISPLWAILFFSMLLMLGIDSQFCTVEGFITALVDEYPRLLRNRRELFIAAVCIVSYLIGLSNITQGGIYVFKLFDYYSASGMSLLFLVFFECVSISWFYGVNRFYDNIQEMVGSRPCIWWKLCWSFFTPIIVAGVFLFSAVQMTPLTMGSYVFPKWGQGVGWLMALSSMVLIPGYMAYMFLTLKGSLKQRLQVMIQPSEDIVRPENGPEQPQAGSSASKEAYI";

        private const string PROTEIN_LIST_BAD_FORMAT =
            "firstColumn , secondColumn\r\n\r\nfirstEntry, 1949";
    }
}
