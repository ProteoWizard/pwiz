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

using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.DataBinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;
using System.Linq;
using System.Xml.Serialization;

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

        [TestMethod]
        public void TestPersistedViews()
        {
            var reportListsByVersion = PersistedViews.GetReportListsByVersion().ToList();
            Assert.AreEqual(reportListsByVersion.Count, Settings.Default.PersistedViews.RevisionIndexCurrent, "Number of elements in PersistedViews.GetReportListsByVersion() must be equal to RevisionIndexCurrent");
            var localizedNames = PersistedViews.GetLocalizedReportNames();
            var xmlSerializer = new XmlSerializer(typeof(ViewSpecList));

            foreach (var reportList in reportListsByVersion)
            {
                var viewSpecList = (ViewSpecList)xmlSerializer.Deserialize(new StringReader(reportList));
                foreach (var viewSpec in viewSpecList.ViewSpecs)
                {
                    Assert.IsTrue(localizedNames.ContainsKey(viewSpec.Name), "{0} needs to have entry in PersistedViews.GetLocalizedReportNames", viewSpec.Name);
                }
            }
        }
    }
}
