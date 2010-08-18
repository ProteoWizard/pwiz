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

using System.IO;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
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

            SequenceTree sequenceTree;

            RunUI(() =>
            {
                sequenceTree = SkylineWindow.SequenceTree;

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
                Assert.IsFalse(SkylineWindow.CopyMenuItemEnabled());
                Assert.IsTrue(SkylineWindow.PasteMenuItemEnabled());

                // Test clipboard HTML contains formatting for modified peptides.
                sequenceTree.SelectedNode = sequenceTree.Nodes[3].Nodes[0];
                SkylineWindow.Copy();
                var dataObject = (MemoryStream)Clipboard.GetData("HTML format");
                string clipboardHtml = new StreamReader(dataObject).ReadToEnd();
                Assert.IsTrue(clipboardHtml.Contains("font") && clipboardHtml.Contains("color"));
          });
         

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
                string providerHtml = (string) data.GetData("HTML Format");
                if (providerHtml != null)
                    AppendText(htmlSb, new HtmlFragment(providerHtml).Fragment,
                        HTML_LINE_BREAK, HTML_INDENT, levels, lineBreaks);
                string providerText = (string)data.GetData("Text");
                if (providerText != null)
                    AppendText(textSb, providerText, TEXT_LINE_BREAK, TEXT_INDENT, levels, lineBreaks);

                lineBreaks = expectedLineBreaks;
            }

            Assert.AreEqual(textSb.AppendLine().ToString(), (string)Clipboard.GetData("Text"));

            var dataObject = (MemoryStream) Clipboard.GetData("HTML format");
            string clipboardHtml = new StreamReader(dataObject).ReadToEnd();
            Assert.AreEqual(new HtmlFragment(clipboardHtml).Fragment, htmlSb.AppendLine().ToString());
        }

        private static void AppendText(StringBuilder sb, string text, string lineSep, string indent, int levels, int lineBreaks)
        {
            for (int i = 0; i < lineBreaks; i++)
                sb.Append(lineSep);
            for (int i = 0; i < levels; i++)
                sb.Append(indent);
            sb.Append(text);
        }

    }
}
