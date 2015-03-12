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
using IDPicker;
using IDPicker.DataModel;

namespace Test
{
    using SpectrumTuple = TestModel.SpectrumTuple;

    /// <summary>
    /// Summary description for TestMerger
    /// </summary>
    [TestClass]
    public class TestMerger
    {
        // shared session between TestMerger methods
        public NHibernate.ISession session;

        public TestMerger ()
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

        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void TestInitialize ()
        {
        }

        [TestMethod]
        [TestCategory("Model")]
        public void TestMergerModel ()
        {

            #region Example proteins

            string[] testProteinSequences = new string[]
            {
                "PEPTIDERPEPTIDEKPEPTIDE",
                "TIDERPEPTIDEKPEP",
                "RPEPKTIDERPEPKTIDE",
                "EDITPEPKEDITPEPR",
                "PEPREDITPEPKEDIT",
                "EPPIERPETPDETKTDPEPIIRDE"
            };

            #endregion

            #region Example PSMs

            List<SpectrumTuple> mergeSourcePsmSummary1 = new List<SpectrumTuple>()
            {
                 //               Group Source Spectrum Analysis     Score  Q   List of Peptide@Charge/ScoreDivider
                 new SpectrumTuple("/A/1", 1, 1, 1, 12, 0, "[C2H2O1]PEPTIDE@2/1 TIDERPEPTIDEK@4/2 EPPIER@1/3"),
                 new SpectrumTuple("/A/1", 1, 2, 1, 23, 0, "PEPTIDER@2/1 PETPDETK@3/3 EDITPEPK@2/5"),
                 new SpectrumTuple("/A/1", 1, 3, 1, 34, 0, "PEPTIDEK@2/1 TIDER@1/4 PETPDETK@2/8"),
                 new SpectrumTuple("/A/1", 2, 1, 1, 43, 0, "PEPTIDE@2/1 E[H-2O-1]DIT[P1O4]PEPR@2/2 EPPIER@1/7"),
                 new SpectrumTuple("/A/1", 2, 2, 1, 32, 0, "PEPTIDER@3/1 EDITPEPK@3/4 EDITPEPR@3/5"),
                 new SpectrumTuple("/A/1", 2, 3, 1, 21, 0, "PEPT[P1O4]IDEK@3/1 TIDEK@1/7 PETPDETK@2/8"),
                 new SpectrumTuple("/A/2", 3, 1, 1, 56, 0, "TIDEK@2/1 TIDE@1/2 P[P1O4]EPTIDE@3/3"),
                 new SpectrumTuple("/A/2", 3, 2, 1, 45, 0, "TIDER@2/1 TIDERPEPTIDEK@4/3 PEPTIDEK@3/4"),
                 new SpectrumTuple("/A/2", 3, 3, 1, 34, 0, "TIDE@1/1 PEPTIDEK@3/6 TIDEK@1/7"),
                 new SpectrumTuple("/B/1", 4, 1, 1, 65, 0, "TIDERPEPTIDEK@4/1 PETPDETK@3/8 EDITPEPR@3/9"),
                 new SpectrumTuple("/B/1", 4, 2, 1, 53, 0, "E[H-2O-1]DITPEPK@2/1 PEPTIDEK@3/2 PEPTIDE@2/3"),
                 new SpectrumTuple("/B/1", 4, 3, 1, 42, 0, "EDIT@2/1 PEPTIDEK@3/3 EDITPEPR@2/4"),
                 new SpectrumTuple("/B/2", 5, 1, 1, 20, 0, "EPPIER@2/1 TIDE@1/7 PEPTIDE@2/9"),
                 new SpectrumTuple("/B/2", 5, 2, 1, 24, 0, "PETPDETK@2/1 PEPTIDEK@3/5 EDITPEPR@2/8"),
                 new SpectrumTuple("/B/2", 5, 3, 1, 24, 0, "PETPDETK@3/1 EDIT@1/4 TIDER@2/6"),
             };

             List<SpectrumTuple> mergeSourcePsmSummary2 = new List<SpectrumTuple>()
             {
                 new SpectrumTuple("/A/1", 1, 1, 2, 120, 0, "TIDERPEPTIDEK@4/1 PEPTIDE@2/2 EPPIER@1/3"),
                 new SpectrumTuple("/A/1", 1, 2, 2, 230, 0, "PEPTIDER@2/1 PETPDETK@3/3 EDITPEPK@2/5"),
                 new SpectrumTuple("/A/1", 1, 3, 2, 340, 0, "PEPTIDEK@2/1 TIDER@1/4 PETPDETK@2/8"),
                 new SpectrumTuple("/A/1", 2, 1, 2, 430, 0, "PEPTIDE@2/1 EDITPEPR@2/2 EPPIER@1/7"),
                 new SpectrumTuple("/A/1", 2, 2, 2, 320, 0, "PEPTIDER@3/1 EDITPEPK@3/4 EDITPEPR@3/5"),
                 new SpectrumTuple("/A/1", 2, 3, 2, 210, 0, "PEPT[P1O4]IDEK@3/1 TIDEK@1/7 PETPDETK@2/8"),
                 new SpectrumTuple("/A/2", 3, 1, 2, 560, 0, "TIDEK@2/1 TIDE@1/2 PEPTIDE@3/3"),
                 new SpectrumTuple("/A/2", 3, 2, 2, 450, 0, "TIDER@2/1 TIDERPEPTIDEK@4/3 PEPTIDEK@3/4"),
                 new SpectrumTuple("/A/2", 3, 3, 2, 340, 0, "TIDE@1/1 PEPTIDEK@3/6 TIDEK@1/7"),
                 new SpectrumTuple("/B/1", 4, 1, 2, 650, 0, "TIDERPEPTIDEK@4/1 PET[P1O4]PDETK@3/8 EDITPEPR@3/9"),
                 new SpectrumTuple("/B/1", 4, 2, 2, 530, 0, "EDITPEPK@2/1 PEPTIDEK@3/2 PEPTIDE@2/3"),
                 new SpectrumTuple("/B/1", 4, 3, 2, 420, 0, "EDIT@2/1 PEPTIDEK@3/3 EDITPEPR@2/4"),
                 new SpectrumTuple("/B/2", 5, 1, 2, 200, 0, "E[H-2O-1]PPIER@2/1 TIDE@1/7 PEPTIDE@2/9"),
                 new SpectrumTuple("/B/2", 5, 2, 2, 240, 0, "PEPTIDEK@2/1 PETPDETK@2/4 EDITPEPR@2/8"),
                 new SpectrumTuple("/B/2", 5, 3, 2, 240, 0, "PETPDETK@3/1 EDIT@1/4 TIDER@2/6"),
             };

             var qonverterSettings1 = new QonverterSettings()
             {
                 QonverterMethod = Qonverter.QonverterMethod.StaticWeighted,
                 DecoyPrefix = "quiRKy",
                 RerankMatches = true,
                 ScoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>()
                    {
                        {"score1", new Qonverter.Settings.ScoreInfo()
                                    {
                                        Weight = 1,
                                        Order = Qonverter.Settings.Order.Ascending,
                                        NormalizationMethod = Qonverter.Settings.NormalizationMethod.Linear
                                    }},
                        {"score2", new Qonverter.Settings.ScoreInfo()
                                    {
                                        Weight = 42,
                                        Order = Qonverter.Settings.Order.Descending,
                                        NormalizationMethod = Qonverter.Settings.NormalizationMethod.Quantile
                                    }}
                    }
             };

             var qonverterSettings2 = new QonverterSettings()
             {
                 QonverterMethod = Qonverter.QonverterMethod.SVM,
                 DecoyPrefix = "___---",
                 RerankMatches = false,
                 ScoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>()
                    {
                        {"foo", new Qonverter.Settings.ScoreInfo()
                                {
                                    Weight = 7,
                                    Order = Qonverter.Settings.Order.Ascending,
                                    NormalizationMethod = Qonverter.Settings.NormalizationMethod.Off
                                }},
                        {"bar", new Qonverter.Settings.ScoreInfo()
                                {
                                    Weight = 11,
                                    Order = Qonverter.Settings.Order.Descending,
                                    NormalizationMethod = Qonverter.Settings.NormalizationMethod.Off
                                }}
                    }
             };

            #endregion

            File.Delete("testMergeSource1.idpDB");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testMergeSource1.idpDB", new SessionFactoryConfig { CreateSchema = true }))
            using (var session = sessionFactory.OpenSession())
            {
                TestModel.CreateTestProteins(session, testProteinSequences);
                TestModel.CreateTestData(session, mergeSourcePsmSummary1);
                TestModel.AddSubsetPeakData(session);

                qonverterSettings1.Analysis = session.UniqueResult<Analysis>(o => o.Software.Name == "Engine 1");
                session.Save(qonverterSettings1);
                session.Flush();
            }

            File.Delete("testMergeSource2.idpDB");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testMergeSource2.idpDB", new SessionFactoryConfig { CreateSchema = true }))
            using (var session = sessionFactory.OpenSession())
            {
                TestModel.CreateTestProteins(session, testProteinSequences);
                TestModel.CreateTestData(session, mergeSourcePsmSummary2);
                TestModel.AddSubsetPeakData(session);

                // copy is required because session.Save() takes ownership of the instance
                var qonverterSettings2Copy = new QonverterSettings()
                {
                    Analysis = session.UniqueResult<Analysis>(o => o.Software.Name == "Engine 2"),
                    QonverterMethod = qonverterSettings2.QonverterMethod,
                    DecoyPrefix = qonverterSettings2.DecoyPrefix,
                    RerankMatches = qonverterSettings2.RerankMatches,
                    ScoreInfoByName = qonverterSettings2.ScoreInfoByName
                };
                session.Save(qonverterSettings2Copy);
                session.Flush();
            }

            // create a new merged idpDB from two idpDB files
            File.Delete("testMerger.idpDB");
            Merger.Merge("testMerger.idpDB", new string[] { "testMergeSource1.idpDB", "testMergeSource2.idpDB" });

            var testModel = new TestModel();

            // test that testMerger.idpDB passes the TestModel tests
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testMerger.idpDB"))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                testModel.TestOverallCounts();
                testModel.TestSanity();
                testModel.TestProteins();
                testModel.TestPeptides();
                testModel.TestPeptideInstances();
                testModel.TestSpectrumSourceGroups();
                testModel.TestSpectrumSources(false);
                testModel.TestSpectra(false);
                testModel.TestAnalyses();
                testModel.TestPeptideSpectrumMatches();
                testModel.TestModifications();
                testModel.TestQonverterSettings();
            }

            // create an in-memory representation of testMergeSource2
            var memoryFactory = SessionFactoryFactory.CreateSessionFactory(":memory:", new SessionFactoryConfig { CreateSchema = true });
            var memoryConnection = SessionFactoryFactory.CreateFile(":memory:");
            var memorySession = memoryFactory.OpenSession(memoryConnection);
            {
                TestModel.CreateTestProteins(memorySession, testProteinSequences);
                TestModel.CreateTestData(memorySession, mergeSourcePsmSummary2);
                TestModel.AddSubsetPeakData(memorySession);

                qonverterSettings2.Analysis = memorySession.UniqueResult<Analysis>(o => o.Software.Name == "Engine 2");
                memorySession.Save(qonverterSettings2);
                memorySession.Flush();
            }

            // merge the in-memory connection into the testMergeSource1 file
            /*Merger.Merge("testMergeSource1.idpDB", memoryConnection as System.Data.SQLite.SQLiteConnection);

            // testMergeSource1.idpDB should pass just like testMerger.idpDB
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testMergeSource1.idpDB"))
            using (var session = testModel.session = sessionFactory.OpenSession())
            {
                testModel.TestOverallCounts();
                testModel.TestSanity();
                testModel.TestProteins();
                testModel.TestPeptides();
                testModel.TestPeptideInstances();
                testModel.TestSpectrumSourceGroups();
                testModel.TestSpectrumSources(false);
                testModel.TestSpectra(false);
                testModel.TestAnalyses();
                testModel.TestPeptideSpectrumMatches();
                testModel.TestModifications();
                testModel.TestQonverterSettings();
            }*/
        }
    }
}
