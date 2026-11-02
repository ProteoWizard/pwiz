/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    /// <summary>
    /// Tests the item by item merge of a settings list that both this program and the file its
    /// settings follow have changed since they were the same. External tools are the case that
    /// matters most: an administrator's shared settings add tools, and each user adds their own.
    /// </summary>
    [TestClass]
    public class SettingsListMergeTest : AbstractUnitTest
    {
        [TestMethod]
        public void TestSettingsListMerge()
        {
            var baseList = CreateList(
                Tool(@"Untouched", @"base.exe"),
                Tool(@"ChangedInSource", @"base.exe"),
                Tool(@"RemovedFromSource", @"base.exe"),
                Tool(@"ChangedHere", @"base.exe"),
                Tool(@"RemovedHere", @"base.exe"),
                Tool(@"ChangedInBoth", @"base.exe"),
                Tool(@"ChangedHereRemovedFromSource", @"base.exe"));
            var localList = CreateList(
                Tool(@"Untouched", @"base.exe"),
                Tool(@"ChangedInSource", @"base.exe"),
                Tool(@"RemovedFromSource", @"base.exe"),
                Tool(@"ChangedHere", @"here.exe"),
                Tool(@"ChangedInBoth", @"here.exe"),
                Tool(@"ChangedHereRemovedFromSource", @"here.exe"),
                Tool(@"AddedHere", @"here.exe"));
            var sourceList = CreateList(
                Tool(@"Untouched", @"base.exe"),
                Tool(@"ChangedInSource", @"source.exe"),
                Tool(@"ChangedHere", @"base.exe"),
                Tool(@"RemovedHere", @"base.exe"),
                Tool(@"ChangedInBoth", @"source.exe"),
                Tool(@"AddedToSource", @"source.exe"));

            var merged = (ToolList) localList.ThreeWayMerge(baseList, sourceList);

            Assert.AreEqual(string.Join(@", ", @"Untouched", @"ChangedInSource", @"ChangedHere", @"ChangedInBoth",
                    @"ChangedHereRemovedFromSource", @"AddedHere", @"AddedToSource"),
                string.Join(@", ", merged.Select(tool => tool.Title)));
            Assert.AreEqual(@"source.exe", merged[@"ChangedInSource"].Command);
            Assert.AreEqual(@"here.exe", merged[@"ChangedHere"].Command);
            // A change made here wins over one made in the source
            Assert.AreEqual(@"here.exe", merged[@"ChangedInBoth"].Command);
            // The merge makes a new list, leaving the one in the settings as it was
            Assert.AreEqual(7, localList.Count);
        }

        private static ToolDescription Tool(string title, string command)
        {
            return new ToolDescription(title, command, string.Empty);
        }

        private static ToolList CreateList(params ToolDescription[] tools)
        {
            var list = new ToolList();
            list.AddRange(tools);
            return list;
        }
    }
}
