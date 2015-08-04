/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    [TestClass]
    public class PersistedViewsTest : AbstractUnitTest
    {
        /// <summary>
        /// Tests that PersistedViews constructed from an old ReportSpecList will end up
        /// having the same views in it as a clean PersistedViews.
        /// </summary>
        [TestMethod]
        public void TestUpgradedReportSpecList()
        {
            var toolList = new ToolList();
            toolList.AddDefaults();
            var cleanPersistedViews = new PersistedViews(null, null, toolList);
            for (int oldRevision = 0; oldRevision < 2; oldRevision++)
            {
                var oldReportSpecList = new ReportSpecList {RevisionIndex = oldRevision};
                oldReportSpecList.AddRange(oldReportSpecList.GetDefaults(oldRevision));
                var upgradedPersistedViews = new PersistedViews(oldReportSpecList, null, toolList);
                foreach (var group in new[] {PersistedViews.MainGroup, PersistedViews.ExternalToolsGroup})
                {
                    var cleanViews = cleanPersistedViews.GetViewSpecList(group.Id).ViewSpecs.ToArray();
                    var upgragedViews = upgradedPersistedViews.GetViewSpecList(group.Id).ViewSpecs.ToArray();
                    
                    CollectionAssert.AreEquivalent(cleanViews, upgragedViews, 
                        "Upgraded from rev {0} in group {1}", oldRevision, group);
                }
            }
        }
    }
}
