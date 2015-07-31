/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2014 University of Washington - Seattle, WA
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Common.Collections;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls.Editor;
using pwiz.Skyline;
using pwiz.Skyline.Controls.Databinding;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Databinding;
using pwiz.Skyline.Properties;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTestFunctional
{
    /// <summary>
    /// Summary description for ReportSharingTest
    /// </summary>
    [TestClass]
    public class ReportSharingTest : AbstractFunctionalTest
    {
        [TestMethod]
        public void TestReportSharing()
        {
            TestFilesZip = "TestFunctional/ReportSharingTest.zip";
            RunFunctionalTest();
        }

        protected override void DoTest()
        {
            TestDeserializeAllFiles();
            TestCommandLine();
            TestExportReportImport();
            TestDocumentGridManageViewsImport();
            TestResultsGridManageViewsImport();
        }

        protected void TestDeserializeAllFiles()
        {
            var reportInfos = ListTestReportInfos();
            Assert.AreNotEqual(0, reportInfos.Count);
            foreach (var reportInfo in reportInfos)
            {
                Assert.AreNotEqual(reportInfo.HasOldReports, reportInfo.HasNewReports);
            }
        }

        protected void TestCommandLine()
        {
            foreach (var reportInfo in ListTestReportInfos())
            {
                Settings.Default.PersistedViews.Clear();
                StringWriter stringWriter = new StringWriter();
                CommandLineRunner.RunCommand(new[]{"--report-add=" + reportInfo.Path}, new CommandStatusWriter(stringWriter));
                VerifyReportsImported(PersistedViews.MainGroup, reportInfo);
            }
        }

        protected void TestExportReportImport()
        {
            foreach (var reportInfo in ListTestReportInfos())
            {
                Settings.Default.PersistedViews.Clear(); 
                var exportLiveReportDlg = ShowDialog<ExportLiveReportDlg>(SkylineWindow.ShowExportReportDialog);
                string reportInfoPath = reportInfo.Path;
                var manageViewsForm = ShowDialog<ManageViewsForm>(exportLiveReportDlg.EditList);
                RunUI(()=>manageViewsForm.ImportViews(reportInfoPath));
                OkDialog(manageViewsForm, manageViewsForm.Close);
                VerifyReportsImported(PersistedViews.MainGroup, reportInfo);
                OkDialog(exportLiveReportDlg, exportLiveReportDlg.CancelClick);
            }
        }

        protected void TestDocumentGridManageViewsImport()
        {
            DocumentGridForm documentGridForm = ShowDialog<DocumentGridForm>(()=>SkylineWindow.ShowDocumentGrid(true));
            foreach (var reportInfo in ListTestReportInfos())
            {
                Settings.Default.PersistedViews.Clear(); 
                ManageViewsForm manageViewsForm = ShowDialog<ManageViewsForm>(documentGridForm.ManageViews);
                string reportInfoPath = reportInfo.Path;
                RunUI(()=>manageViewsForm.ImportViews(reportInfoPath));
                VerifyReportsImported(PersistedViews.MainGroup, reportInfo);
                OkDialog(manageViewsForm, manageViewsForm.Close);
            }
        }

        protected void TestResultsGridManageViewsImport()
        {
            LiveResultsGrid liveResultsGrid = ShowDialog<LiveResultsGrid>(() => SkylineWindow.ShowResultsGrid(true));
            foreach (var reportInfo in ListTestReportInfos())
            {
                Settings.Default.PersistedViews.Clear();
                ManageViewsForm manageViewsForm = ShowDialog<ManageViewsForm>(liveResultsGrid.ManageViews);
                string reportInfoPath = reportInfo.Path;
                RunUI(() => manageViewsForm.ImportViews(reportInfoPath));
                VerifyReportsImported(PersistedViews.MainGroup, reportInfo);
                OkDialog(manageViewsForm, manageViewsForm.Close);
            }
        }

        protected void VerifyReportsImported(ViewGroup viewGroup, ReportInfo reportInfo)
        {
            foreach (var expectedReport in reportInfo.ReportOrViewSpecs)
            {
                var foundReports = Settings.Default.PersistedViews.GetViewSpecList(viewGroup.Id).ViewSpecs
                    .Where(viewSpec => viewSpec.Name == expectedReport.Name)
                    .ToArray();
                Assert.AreEqual(1, foundReports.Length);
            }
        }

        protected IList<ReportInfo> ListTestReportInfos()
        {
            List<ReportInfo> reportInfos = new List<ReportInfo>();
            foreach (string filename in new[]
            {
                "PeptidesSharedFrom25ResultsGrid.skyr",
                "SharedFrom25DocumentGrid.skyr",
                "Skyline21BuiltInReports.skyr",
                "Skyline31ShareFromDocumentGrid.skyr",
                "Skyline31ShareFromExportReport.skyr",
                "Skyline32Reports.skyr",
            })
            {
                reportInfos.Add(GetReportInfo(TestFilesDir.GetTestPath(filename)));
            }
            return reportInfos;
        }

        protected ReportInfo GetReportInfo(string path)
        {
            using (var stream = File.OpenRead(TestFilesDir.GetTestPath(path)))
            {
                return new ReportInfo(path, ReportSharing.DeserializeReportList(stream));
            }
        }

        protected class ReportInfo
        {
            public ReportInfo(string path, IEnumerable<ReportOrViewSpec> reportOrViewSpecs)
            {
                Path = path;
                ReportOrViewSpecs = ImmutableList.ValueOf(reportOrViewSpecs);
            }

            public string Path { get; private set; }

            public IList<ReportOrViewSpec> ReportOrViewSpecs { get; private set; }

            public bool IsEmpty
            {
                get { return ReportOrViewSpecs.Count == 0; }
            }

            public bool HasOldReports
            {
                get { return ReportOrViewSpecs.Any(reportOrViewSpec => null != reportOrViewSpec.ReportSpec); }
            }

            public bool HasNewReports
            {
                get { return ReportOrViewSpecs.Any(reportSpec => null != reportSpec.ViewSpec); }
            }
        }
    }
}
