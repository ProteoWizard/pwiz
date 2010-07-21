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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker.DataModel;

namespace Test
{
    /// <summary>
    /// Summary description for TestImportExport
    /// </summary>
    [TestClass]
    public class TestImportExport
    {
        TestModel testModel;

        public TestImportExport ()
        {
            testModel = new TestModel();
        }

        /// <summary>
        ///Gets or sets the test context which provides
        ///information about and functionality for the current test run.
        ///</summary>
        public TestContext TestContext { get; set; }

        #region Additional test attributes
        //
        // You can use the following additional attributes as you write your tests:
        //
        // Use ClassInitialize to run code before running the first test in the class
        // [ClassInitialize()]
        // public static void MyClassInitialize(TestContext testContext) { }
        //
        // Use ClassCleanup to run code after all tests in a class have run
        // [ClassCleanup()]
        // public static void MyClassCleanup() { }
        //
        // Use TestInitialize to run code before running each test 
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        public void TestImportExportIdpXml ()
        {
            IList<string> idpXmlPaths;
            using (var exporter = new Exporter(testModel.session))
            {
                idpXmlPaths = exporter.WriteIdpXml(true, true, true);
                exporter.WriteSpectra();
            }
            testModel.session.Close();

            using (var parser = new Parser("testImportExport.idpDB"))
            {
                // ReadXml should pick up mzML files in the same directory as the idpXMLs
                parser.ReadXml(".", idpXmlPaths.ToArray());
            }

            var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testImportExport.idpDB", false, false);
            testModel.session = sessionFactory.OpenSession();

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
