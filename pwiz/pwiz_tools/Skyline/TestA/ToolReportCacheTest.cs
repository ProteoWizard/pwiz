/*
 * Original author: Trevor Killeen <killeent .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Tools;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestA
{
    /// <summary>
    /// Summary description for ToolReportCacheTest
    /// </summary>
    [TestClass]
    public class ToolReportCacheTest : AbstractUnitTest 
    {        
        private readonly ReportSpec _report1 = new ReportSpec("Report 1", new QueryDef {Select = new List<ReportColumn>()});
        private readonly ReportSpec _report2 = new ReportSpec("Report 2", new QueryDef {Select = new List<ReportColumn>()});
        private readonly ReportSpec _report3 = new ReportSpec("Report 3", new QueryDef {Select = new List<ReportColumn>()});

        private const string REPORT_STRING = "report";

        private readonly IDocumentContainer _testDocumentContainer = new TestDocumentContainer();
        
        [TestMethod]
        public void AddReportTest()
        {
            using (new ReportCacheTestInitializer(_testDocumentContainer))
            {
                Assert.IsFalse(ToolReportCache.Instance.ContainsKey(_report1));
                Assert.IsFalse(ToolReportCache.Instance.ContainsValue(REPORT_STRING));
                string report = ToolReportCache.Instance.GetReport(null, _report1, null);
                Assert.AreEqual(REPORT_STRING, report);
                Assert.IsTrue(ToolReportCache.Instance.ContainsKey(_report1));
                Assert.IsTrue(ToolReportCache.Instance.ContainsValue(REPORT_STRING));
            }
        }

        [TestMethod]
        public void ReportToFrontTest()
        {
            using (new ReportCacheTestInitializer(_testDocumentContainer))
            {
                // Tests that the most recently used report is brought to the front
                ToolReportCache.Instance.Register(_testDocumentContainer);
                ToolReportCache.Instance.TestReport = REPORT_STRING;

                // add three reports to the cache
                ToolReportCache.Instance.GetReport(null, _report1, null);
                ToolReportCache.Instance.GetReport(null, _report2, null);
                ToolReportCache.Instance.GetReport(null, _report3, null);
                Assert.IsTrue(ToolReportCache.Instance.IsFirst(_report3));
                Assert.IsTrue(ToolReportCache.Instance.IsLast(_report1));

                // access the first report again, bringing it to the front
                ToolReportCache.Instance.GetReport(null, _report1, null);
                Assert.IsTrue(ToolReportCache.Instance.IsFirst(_report1));
            }
        }

        [TestMethod]
        public void CacheCapacityTest()
        {
            using (new ReportCacheTestInitializer(_testDocumentContainer))
            {
                // Tests that the cache kicks out the LRU report when it reaches its capacity
                ToolReportCache.Instance.Register(_testDocumentContainer);
                ToolReportCache.Instance.TestMaximumSize = Convert.ToInt32(ToolReportCache.ReportSize(REPORT_STRING) * 2.5);
                ToolReportCache.Instance.TestReport = REPORT_STRING;

                // add two reports to the cache
                ToolReportCache.Instance.GetReport(null, _report1, null);
                ToolReportCache.Instance.GetReport(null, _report2, null);

                // the cache is now 80% full, adding one more report should cause it to exceed capacity
                Assert.IsTrue(ToolReportCache.Instance.IsLast(_report1));
                ToolReportCache.Instance.GetReport(null, _report3, null);
                Assert.IsFalse(ToolReportCache.Instance.ContainsKey(_report1));
            }
        }

        [TestMethod]
        public void TestClear()
        {
            // Tests that the cache is cleared on document changes
            _testDocumentContainer.SetDocument(new SrmDocument(SrmSettingsList.GetDefault()), _testDocumentContainer.Document);

            using (new ReportCacheTestInitializer(_testDocumentContainer))
            {
                ToolReportCache.Instance.GetReport(_testDocumentContainer.Document, _report1, null);
                Assert.IsTrue(ToolReportCache.Instance.ContainsKey(_report1));

                // change the document
                var oldDocument = _testDocumentContainer.Document;
                _testDocumentContainer.Document.Settings.PeptideSettings.ChangeFilter(
                    _testDocumentContainer.Document.Settings.PeptideSettings.Filter.ChangeAutoSelect(
                        _testDocumentContainer.Document.Settings.PeptideSettings.Filter.AutoSelect));

                _testDocumentContainer.SetDocument(_testDocumentContainer.Document, oldDocument);
                Assert.IsFalse(ToolReportCache.Instance.ContainsKey(_report1));
                Assert.IsFalse(ToolReportCache.Instance.ContainsValue(REPORT_STRING));
            }
        }

        private class ReportCacheTestInitializer : IDisposable
        {
            private readonly ToolReportCache _reportCache;

            public ReportCacheTestInitializer(IDocumentContainer testDocumentContainer)
            {
                _reportCache = ToolReportCache.Instance;
                _reportCache.Register(testDocumentContainer);
                _reportCache.TestReport = REPORT_STRING;
            }

            public void Dispose()
            {
                _reportCache.TestReport = null;
                _reportCache.TestMaximumSize = null;
                _reportCache.Register(null);
            }
        }

        //[TestMethod]
        //public void TestDocumentChangedWhileExporting()
        //{
        //    // Tests that reports aren't stored in the cache if the document changes
        //    // during exporting
        //    ToolReportCache.Instance.Register(_testDocumentContainer);
        //    ToolReportCache.Instance.TestReport = REPORT_STRING;
            
        //    // paused report get
        //    // change document

        //    Assert.IsFalse(ToolReportCache.Instance.ContainsKey(_report1));
        //    Assert.IsFalse(ToolReportCache.Instance.ContainsValue(REPORT_STRING));
        //}

    }
}
