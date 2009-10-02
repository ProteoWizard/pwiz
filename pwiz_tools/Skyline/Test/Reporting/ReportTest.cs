/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Xml;
using System.Xml.Serialization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.SkylineTest.Reporting
{
    /// <summary>
    /// Tests for reports
    /// </summary>
    [TestClass]
    public class ReportTest
    {
        /// <summary>
        /// Tests that the "built-in" reports are syntactically correct.
        /// </summary>
        [TestMethod]
        public void TestReportSpecList()
        {
            ReportSpecList reportSpecList = new ReportSpecList();
            Database database = new Database();
            ColumnSet columnSet = ColumnSet.GetTransitionsColumnSet(database.GetSchema());
            TreeView treeView = new TreeView();
            treeView.Nodes.AddRange(columnSet.GetTreeNodes().ToArray());

            foreach (ReportSpec reportSpec in reportSpecList.GetDefaults())
            {
                Report report = Report.Load(reportSpec);
                ResultSet resultSet = report.Execute(database);
                List<NodeData> nodeDatas;
                columnSet.GetColumnInfos(report, treeView, out nodeDatas);
                Assert.IsFalse(nodeDatas.Contains(null));
                if (reportSpec.GroupBy == null)
                {
                    SimpleReport simpleReport = (SimpleReport)report;
                    Assert.AreEqual(simpleReport.Columns.Count, resultSet.ColumnInfos.Count);
                    Assert.AreEqual(simpleReport.Columns.Count, nodeDatas.Count);
                }
            }
        }
        [TestMethod]
        public void TestReportSpecListXml()
        {
            ReportSpecList reportSpecList = new ReportSpecList();
            reportSpecList.AddDefaults();
            Assert.AreNotEqual(0, reportSpecList.Count);
            StringBuilder stringBuilder = new StringBuilder();
            using (XmlWriter xmlWriter = XmlWriter.Create(stringBuilder))
            {
                Debug.Assert(xmlWriter != null);
                xmlWriter.WriteStartDocument();
                xmlWriter.WriteStartElement("testElement");
                reportSpecList.WriteXml(xmlWriter);
                xmlWriter.WriteEndElement();
                xmlWriter.WriteEndDocument();
            }
            XmlReader xmlReader = XmlReader.Create(new StringReader(stringBuilder.ToString()));
            ReportSpecList compare = new ReportSpecList();
            compare.ReadXml(xmlReader);
            Assert.AreEqual(reportSpecList.Count, compare.Count);
            for (int i = 0; i < reportSpecList.Count; i++)
            {
                ReportSpec reportSpec = reportSpecList[i];
                ReportSpec reportSpecCompare = compare[i];
                Assert.AreNotSame(reportSpec, reportSpecCompare);
                Assert.AreEqual(reportSpec, reportSpecCompare);
                Assert.AreEqual(reportSpec.GetHashCode(), reportSpecCompare.GetHashCode());
            }
        }

        /// <summary>
        /// Tests a pivot report.  Loads up a document which has some results in it, 
        /// and executes a pivot report.
        /// </summary>
        [TestMethod]
        public void TestIsotopeLabelPivot()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            var stream = typeof(ReportTest).Assembly.GetManifestResourceStream("Test.Reporting.silac_1_to_4.sky");
            Assert.IsNotNull(stream);
            Debug.Assert(stream != null);   // Keep ReSharper from warning
            SrmDocument srmDocument = (SrmDocument)xmlSerializer.Deserialize(stream);
            Database database = new Database();
            database.AddSrmDocument(srmDocument);
            PivotReport pivotReport = new PivotReport
                                          {
                                              Table = typeof(DbTransitionResult),
                                              Columns = new List<Identifier>
                                                            {
                                                                new Identifier("Transition", "Precursor", "Peptide",
                                                                               "Sequence"),
                                                                new Identifier("Transition", "Precursor", "Charge"),
                                                                new Identifier("Transition", "FragmentIon"),
                                                            },
                                              GroupByColumns =
                                                  new List<Identifier>(
                                                  PivotType.ISOTOPE_LABEL.GetGroupByColumns(typeof(DbTransitionResult))),
                                              CrossTabHeaders =
                                                  new List<Identifier> { new Identifier("Transition", "Precursor", "IsotopeLabelType") },
                                              CrossTabValues =
                                                  new List<Identifier> { new Identifier("Area"), new Identifier("Background") }

                                          };
            ResultSet resultSet = pivotReport.Execute(database);
            Assert.AreEqual(7, resultSet.ColumnInfos.Count);
            // Assert that "light" appears earlier in the crosstab columns than "heavy".
            Assert.IsTrue(resultSet.ColumnInfos[3].Caption.ToLower().StartsWith("light"));
            Assert.IsTrue(resultSet.ColumnInfos[4].Caption.ToLower().StartsWith("light"));
            Assert.IsTrue(resultSet.ColumnInfos[5].Caption.ToLower().StartsWith("heavy"));
            Assert.IsTrue(resultSet.ColumnInfos[6].Caption.ToLower().StartsWith("heavy"));
            // TODO(nicksh): write asserts that the rows contain correct data.
        }
        /// <summary>
        /// Regression test for Issue#91.
        /// </summary>
        [TestMethod]
        public void TestColumnsFromResultsTables()
        {
            XmlSerializer xmlSerializer = new XmlSerializer(typeof(SrmDocument));
            var stream = typeof(ReportTest).Assembly.GetManifestResourceStream("Test.Reporting.silac_1_to_4.sky");
            Assert.IsNotNull(stream);
            Debug.Assert(stream != null);   // Keep ReSharper from warning
            SrmDocument srmDocument = (SrmDocument)xmlSerializer.Deserialize(stream);
            Database database = new Database();
            database.AddSrmDocument(srmDocument);
            SimpleReport report = new SimpleReport
                                      {
                                          Table = typeof (DbTransitionResult),
                                          Columns = new List<Identifier>
                                                        {
                                                            new Identifier("PrecursorResult", "PeptideResult",
                                                                           "ProteinResult", "ReplicateName"),
                                                            new Identifier("Transition","Precursor","Peptide","Protein","Name"),
                                                            new Identifier("Transition","Precursor","Peptide","Sequence"),
                                                            new Identifier("Area"),
                                                        },
                                      };
            ColumnSet columnSet = ColumnSet.GetTransitionsColumnSet(database.GetSchema());
            TreeView treeView = new TreeView();
            treeView.Nodes.AddRange(columnSet.GetTreeNodes().ToArray());
            List<NodeData> columnInfos;
            columnSet.GetColumnInfos(report, treeView, out columnInfos);
            Assert.AreEqual(report.Columns.Count, columnInfos.Count);
            SimpleReport reportCompare = (SimpleReport) columnSet.GetReport(columnInfos, new List<PivotType>());
            Assert.IsTrue(ArrayUtil.EqualsDeep(report.Columns, reportCompare.Columns));
        }
    }
}

