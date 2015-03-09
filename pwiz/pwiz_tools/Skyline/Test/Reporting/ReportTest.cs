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
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest.Reporting
{
    /// <summary>
    /// Tests for reports
    /// </summary>
    [TestClass]
    public class ReportTest : AbstractUnitTest
    {
        /// <summary>
        /// Tests that the "built-in" reports are syntactically correct.
        /// </summary>
        [TestMethod]
        public void TestReportSpecList()
        {
            ReportSpecList reportSpecList = new ReportSpecList();
            using (Database database = new Database())
            {
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
            SrmDocument srmDocument = ResultsUtil.DeserializeDocument("silac_1_to_4.sky", GetType());
            using (Database database = new Database())
            {
                database.AddSrmDocument(srmDocument);
                PivotReport pivotReport = new PivotReport
                {
                    Columns = new[]
                                {
                                    new ReportColumn(typeof(DbTransition), "Precursor", "Peptide",
                                                   "Sequence"),
                                    new ReportColumn(typeof(DbTransition), "Precursor", "Charge"),
                                    new ReportColumn(typeof(DbTransition), "FragmentIon"),
                                },
                    GroupByColumns =
                              PivotType.ISOTOPE_LABEL.GetGroupByColumns(new[] { new ReportColumn(typeof(DbTransitionResult), "Id") }),
                    CrossTabHeaders = new[]
                                        {
                                            new ReportColumn(typeof(DbTransition), "Precursor", "IsotopeLabelType")
                                        },
                    CrossTabValues = new[]
                                       {
                                           new ReportColumn(typeof(DbTransitionResult), "Area"),
                                           new ReportColumn(typeof(DbTransitionResult), "Background")
                                       }
                };
                ResultSet resultSet = pivotReport.Execute(database);
                Assert.AreEqual(7, resultSet.ColumnInfos.Count);
                // Assert that "light" appears earlier in the crosstab columns than "heavy".
                Assert.IsTrue(resultSet.ColumnInfos[3].Caption.ToLowerInvariant().StartsWith("light"));
                Assert.IsTrue(resultSet.ColumnInfos[4].Caption.ToLowerInvariant().StartsWith("light"));
                Assert.IsTrue(resultSet.ColumnInfos[5].Caption.ToLowerInvariant().StartsWith("heavy"));
                Assert.IsTrue(resultSet.ColumnInfos[6].Caption.ToLowerInvariant().StartsWith("heavy"));
                // TODO(nicksh): write asserts that the rows contain correct data.
            }
        }

        /// <summary>
        /// Regression test for Issue#91.
        /// </summary>
        [TestMethod]
        public void TestColumnsFromResultsTables()
        {
            SrmDocument srmDocument = ResultsUtil.DeserializeDocument("silac_1_to_4.sky", GetType());
            using (Database database = new Database())
            {
                database.AddSrmDocument(srmDocument);
                SimpleReport report = new SimpleReport
                {
                    Columns = new[]
                                                        {
                                                            new ReportColumn(typeof (DbTransitionResult),
                                                                "PrecursorResult", "PeptideResult", "ProteinResult", "ReplicateName"),
                                                            new ReportColumn(typeof (DbTransition),"Precursor","Peptide","Protein","Name"),
                                                            new ReportColumn(typeof (DbTransition),"Precursor","Peptide","Sequence"),
                                                            new ReportColumn(typeof (DbTransitionResult), "Area"),
                                                        },
                };
                ColumnSet columnSet = ColumnSet.GetTransitionsColumnSet(database.GetSchema());
                TreeView treeView = new TreeView();
                treeView.Nodes.AddRange(columnSet.GetTreeNodes().ToArray());
                List<NodeData> columnInfos;
                columnSet.GetColumnInfos(report, treeView, out columnInfos);
                Assert.AreEqual(report.Columns.Count, columnInfos.Count);
                SimpleReport reportCompare = (SimpleReport)columnSet.GetReport(columnInfos, null);
                Assert.IsTrue(ArrayUtil.EqualsDeep(report.Columns, reportCompare.Columns));
            }
        }

        /// <summary>
        /// Make sure CPTAC template in original .skyr format loads and works.
        /// </summary>
        [TestMethod]
        public void TestLoadReportFile()
        {
            SrmDocument srmDocument = ResultsUtil.DeserializeDocument("silac_1_to_4.sky", GetType());
            using (Database database = new Database())
            using (var streamR = GetType().Assembly.GetManifestResourceStream(GetType().Namespace + ".Study9p_template_0721_2009_v3.skyr"))
            {
                Assert.IsNotNull(streamR);

                database.AddSrmDocument(srmDocument);

                ReportSpecList reportSpecList = new ReportSpecList();
                var xmlSerializer = new XmlSerializer(reportSpecList.DeserialType);
                reportSpecList = (ReportSpecList)xmlSerializer.Deserialize(streamR);
                Report report = Report.Load(reportSpecList["Study 9p_0721_2009_v6"]);
                ResultSet resultSet = report.Execute(database);
                Assert.AreEqual(26, resultSet.ColumnInfos.Count);
                // The file contains one transition that does not map to the imported results
                Assert.AreEqual(srmDocument.PeptideTransitionCount / 2 - 1, resultSet.RowCount);
            }
        }
    }
}

