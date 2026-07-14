/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline;
using pwiz.Skyline.Controls.GroupComparison;
using pwiz.Skyline.ToolsUI;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Verifies that <see cref="JsonToolServer.ClickMainMenuItem"/> reaches an item in a main-menu submenu
    /// that is built on demand: the "View &gt; Live Reports &gt; Group Comparisons" submenu rebuilds its
    /// items (even "Add...") on DropDownOpening, so the walk must open each level before matching.
    /// </summary>
    [TestClass]
    public class LazyMenuMcpConnectorTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestLazyMenuMcpConnector()
        {
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            // Drive the verb through the running JSON tool server (torn down with the window).
            RunUI(() => Program.StartToolService());

            // "Add..." under Group Comparisons exists only after the submenu's DropDownOpening builds it;
            // reaching it confirms the menu walk opens each submenu level. Its click opens the dialog.
            Program.MainJsonToolServer.ClickMainMenuItem(@"View > Live Reports > Group Comparisons > Add");
            var editGroupComparisonDlg = WaitForOpenForm<EditGroupComparisonDlg>();
            Assert.IsNotNull(editGroupComparisonDlg,
                @"ClickMainMenuItem did not reach the lazily-built 'Add' item under Group Comparisons.");
            OkDialog(editGroupComparisonDlg, () => editGroupComparisonDlg.DialogResult = DialogResult.Cancel);
        }
    }
}
