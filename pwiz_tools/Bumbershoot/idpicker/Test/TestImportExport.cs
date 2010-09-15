//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Surendra Dasari
//

using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker.DataModel;
using NHibernate.Linq;

namespace Test
{
    /// <summary>
    /// Summary description for TestImportExport
    /// </summary>
    [TestClass]
    public class TestImportExport
    {
        public TestImportExport ()
        {
        }

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        // Use ClassInitialize to run code before running the first test in the class
        [ClassInitialize()]
        public static void ClassInitialize (TestContext testContext)
        {
            TestModel.ClassInitialize(testContext);
        }

        [TestMethod]
        public void TestImportExportIdpXml ()
        {
            var testModel = new TestModel();

            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testModel.idpDB", false, false))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                foreach (var analysis in testModel.session.Query<Analysis>())
                    session.Save(new AnalysisParameter()
                                     {
                                         Analysis = analysis,
                                         Name = "ProteinDatabase",
                                         Value = "testImportExport.fasta"
                                     });

                IList<string> idpXmlPaths;
                using (var exporter = new Exporter(session))
                {
                    idpXmlPaths = exporter.WriteIdpXml(true, true);
                    exporter.WriteSpectra();
                    exporter.WriteProteins("testImportExport.fasta");
                }
                session.Close();

                using (var parser = new Parser("testImportExport.idpDB", ".", idpXmlPaths.ToArray()))
                {
                    // ReadXml should pick up mzML files in the same directory as the idpXMLs
                    parser.Start();
                }

                var merger = new Merger("testImportExport.idpDB", idpXmlPaths.Select(o => Path.ChangeExtension(o, ".idpDB")));
                merger.Start();
            }

            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testImportExport.idpDB", false, false))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                foreach (var analysis in session.Query<Analysis>())
                    session.Delete(session.UniqueResult<AnalysisParameter>(o => o.Analysis.Id == analysis.Id &&
                                                                                o.Name == "ProteinDatabase"));
                session.Flush();

                testModel.TestOverallCounts();
                testModel.TestSanity();
                testModel.TestProteins();
                testModel.TestPeptides();
                testModel.TestPeptideInstances();
                testModel.TestSpectrumSourceGroups();
                testModel.TestSpectrumSources();
                testModel.TestSpectra();
                testModel.TestAnalyses();
                testModel.TestPeptideSpectrumMatches();
                testModel.TestModifications();
            }
        }
    }
}
