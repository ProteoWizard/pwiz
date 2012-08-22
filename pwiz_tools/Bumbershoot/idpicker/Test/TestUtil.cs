//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using IDPicker.DataModel;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker;

namespace Test
{
    /// <summary>
    /// Summary description for TestUtil
    /// </summary>
    [TestClass]
    public class TestUtil
    {
        public TestUtil (){}
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
        public void TestGetCommonFilename ()
        {
            string[] input;

            input = new string[] {"D:/testfoo.pepXML", "D:/testbar.pepXML"};
            Assert.AreEqual("D:/test.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "D:/test/some/more/foo.pepXML", "D:/test/some/thing/bar.pepXML" };
            Assert.AreEqual("D:/test/some/some.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "D:/test/some/more/foo.pepXML", "D:/test/some/thing/bar.pep.xml" };
            Assert.AreEqual("D:/test/some/some.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "D:/test/some/more/foo.pep.XML", "D:/test/some/thing/bar.pep.xml" };
            Assert.AreEqual("D:/test/some/some.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "foo.pepXML", "foo.idpDB" };
            Assert.AreEqual("foo.idpDB", Util.GetCommonFilename(input));

            input = new string[] { "foo", "bar" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "idpicker-analysis-");

            input = new string[] { "D:/foo.pepXML", "D:/bar.pepXML" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "D:/idpicker-analysis-");

            input = new string[] { "D:/foo.pep.xml", "D:/bar.pepXML" };
            StringAssert.StartsWith(Util.GetCommonFilename(input), "D:/idpicker-analysis-");
        }

        void downgrade_3_to_2(NHibernate.ISession session)
        {
            // delete quantitation tables and quantitative columns from SpectrumSource
            session.CreateSQLQuery(@"DROP TABLE SpectrumQuantitation;
                                     DROP TABLE DistinctMatchQuantitation;
                                     DROP TABLE PeptideQuantitation;
                                     DROP TABLE ProteinQuantitation;
                                     CREATE TABLE TempSpectrumSource (Id INTEGER PRIMARY KEY, Name TEXT, URL TEXT, Group_ INT, MsDataBytes BLOB);
                                     INSERT INTO TempSpectrumSource SELECT Id, Name, URL, Group_, MsDataBytes FROM SpectrumSource;
                                     DROP TABLE SpectrumSource;
                                     ALTER TABLE TempSpectrumSource RENAME TO SpectrumSource;
                                     UPDATE About SET SchemaRevision = 2;
                                    ").ExecuteUpdate();
        }

        void downgrade_2_to_1(NHibernate.ISession session)
        {
            // add an empty ScanTimeInSeconds column
            session.CreateSQLQuery(@"CREATE TABLE NewSpectrum (Id INTEGER PRIMARY KEY, Source INT, Index_ INT, NativeID TEXT, PrecursorMZ NUMERIC, ScanTimeInSeconds NUMERIC);
                                     INSERT INTO NewSpectrum SELECT Id, Source, Index_, NativeID, PrecursorMZ, 0 FROM Spectrum;
                                     DROP TABLE Spectrum;
                                     ALTER TABLE NewSpectrum RENAME TO Spectrum;
                                     UPDATE About SET SchemaRevision = 1;
                                    ").ExecuteUpdate();
        }

        void downgrade_1_to_0(NHibernate.ISession session)
        {
            session.CreateSQLQuery(@"CREATE TABLE NewPeptideSpectrumMatch (Id INTEGER PRIMARY KEY, Spectrum INT, Analysis INT, Peptide INT, QValue NUMERIC, MonoisotopicMass NUMERIC, MolecularWeight NUMERIC, MonoisotopicMassError NUMERIC, MolecularWeightError NUMERIC, Rank INT, Charge INT);
                                     INSERT INTO NewPeptideSpectrumMatch SELECT Id, Spectrum, Analysis, Peptide, QValue, ObservedNeutralMass, ObservedNeutralMass-MolecularWeightError, MonoisotopicMassError, MolecularWeightError, Rank, Charge FROM PeptideSpectrumMatch;
                                     DROP TABLE PeptideSpectrumMatch;
                                     ALTER TABLE NewPeptideSpectrumMatch RENAME TO PeptideSpectrumMatch;
                                     DROP TABLE About;
                                    ").ExecuteUpdate();
        }

        void testModelFile(TestModel testModel, string filepath)
        {
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(filepath))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
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

        [TestMethod]
        public void TestSchemaUpdater()
        {
            Assert.AreEqual(3, SchemaUpdater.CurrentSchemaRevision);

            var testModel = new TestModel();
            TestModel.ClassInitialize(null);
            testModelFile(testModel, "testModel.idpDB");

            File.Copy("testModel.idpDB", "testModel-v2.idpDB");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testModel-v2.idpDB"))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                downgrade_3_to_2(session);
            }
            testModelFile(testModel, "testModel-v2.idpDB");

            File.Copy("testModel.idpDB", "testModel-v1.idpDB");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testModel-v1.idpDB"))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                downgrade_3_to_2(session);
                downgrade_2_to_1(session);
            }
            testModelFile(testModel, "testModel-v1.idpDB");

            File.Copy("testModel.idpDB", "testModel-v0.idpDB");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testModel-v0.idpDB"))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                downgrade_3_to_2(session);
                downgrade_2_to_1(session);
                downgrade_1_to_0(session);
            }
            testModelFile(testModel, "testModel-v0.idpDB");
        }
    }
}
