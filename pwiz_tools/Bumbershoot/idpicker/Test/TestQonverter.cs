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
using IDPicker;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker.DataModel;
using NHibernate;
using NHibernate.Linq;

namespace Test
{
    using SpectrumTuple = TestModel.SpectrumTuple;

    /// <summary>
    /// Summary description for TestQonverter
    /// </summary>
    [TestClass]
    public class TestQonverter
    {
        public TestQonverter ()
        {
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
        [TestInitialize()]
        public void TestInitialize()
        {
            Directory.SetCurrentDirectory(TestContext.TestDeploymentDir);
        }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
        [TestCategory("Model")]
        public void TestStaticQonverter ()
        {
            #region Example with forward and reverse proteins
            string[] testProteinSequences = new string[]
            {
                "PEPTIDERPEPTIDEK",
                "TIDERPEPTIDEKPEP",
                "DERPEPTIDEKPEPTI",
                "THEQUICKBRWNFXUMPSVERTHELZYDG",
                "KEDITPEPREDITPEP",
                "PEPKEDITPEPREDIT",
                "ITPEPKEDITPEPRED",
                "GDYZLEHTREVSPMUXFNWRBKCIUQEHT"
            };

            const string decoyPrefix = "r-";
            const int analysisCount = 2;
            const int sourceCount = 2;
            const int chargeCount = 2;

            File.Delete("testStaticQonversion.idpDB");
            var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testStaticQonversion.idpDB", new SessionFactoryConfig { CreateSchema = true });
            var session = sessionFactory.OpenSession();

            TestModel.CreateTestProteins(session, testProteinSequences);

            session.Clear();

            // add decoy prefix to the second half of the proteins
            for (long i = testProteinSequences.LongLength / 2; i < testProteinSequences.LongLength; ++i)
            {
                var pro = session.Get<Protein>(i + 1);
                pro.Accession = decoyPrefix + pro.Accession;
                session.Update(pro);
            }
            #endregion

            #region Example PSMs
            // For each combination of 2 engines, 2 sources, and 2 charges,
            // we test qonversion on this scenario:
            //
            // Scan Score DecoyState Targets Decoys Ambiguous -> Expected QValue
            // 50   6     T          1       0      0            0
            // 30   5     T          2       0      0            0
            // 40   4     T          3       0      0            0
            // 10   3     D          3       1      0            2/4
            // 20   2     D          3       2      0            4/5
            // 60   1     T          4       2      0            4/6

            double q = PeptideSpectrumMatch.DefaultQValue;

            for (int analysis = 1; analysis <= analysisCount; ++analysis)
            for (int source = 1; source <= sourceCount; ++source)
            for (int charge = 1; charge <= chargeCount; ++charge)
            {
                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    //               Group Source Spectrum  Analysis    Score Q   List of Peptide@Charge/ScoreDivider
                    new SpectrumTuple("/",  source,     5, analysis,     6, q, String.Format("TIDERPEPTIDEK@{0}/1 PEPTIDER@{0}/8", charge)),
                    new SpectrumTuple("/",  source,     3, analysis,     5, q, String.Format("TIDERPEPTIDEK@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     4, analysis,     4, q, String.Format("EDITPEP@{0}/1 THEQUICKBR@{0}/4", charge)),
                    new SpectrumTuple("/",  source,     1, analysis,     3, q, String.Format("PEPTIDEKPEP@{0}/1 PEPTIDEK@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     2, analysis,     2, q, String.Format("TIDER@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     6, analysis,     1, q, String.Format("EDITPEPR@{0}/1 TIDERPEPTIDEK@{0}/3", charge))
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            List<SpectrumTuple> testPsmSummaryWithoutScores = new List<SpectrumTuple>()
            {
                new SpectrumTuple("/", 1, 1, analysisCount+1, null, 0.01, "TIDERPEPTIDEK@1/1 PEPTIDER@3/8"),
                new SpectrumTuple("/", 1, 2, analysisCount+1, null, 0.02, "KEDITPEPR@2/1 PEPTIDEK@2/8"),
                new SpectrumTuple("/", 1, 3, analysisCount+1, null, 0.03, "THEQUICKBR@3/1 THELZYDG@1/8"),
            };

            TestModel.CreateTestData(session, testPsmSummaryWithoutScores);

            // force all peptide instances to fully specific
            foreach (PeptideInstance pi in session.Query<PeptideInstance>())
            {
                pi.NTerminusIsSpecific = pi.CTerminusIsSpecific = true;
                session.Update(pi);
            }

            session.Flush();
            session.Close(); // close the connection
            sessionFactory.Close();
            #endregion

            var qonverter = new IDPicker.Qonverter()
            {
                SettingsByAnalysis = new Dictionary<int, Qonverter.Settings>()
            };

            for (int i = 1; i <= analysisCount + 1; ++i)
                qonverter.SettingsByAnalysis[i] = new Qonverter.Settings()
                {
                    QonverterMethod = Qonverter.QonverterMethod.StaticWeighted,
                    ChargeStateHandling = Qonverter.ChargeStateHandling.Ignore,
                    TerminalSpecificityHandling = Qonverter.TerminalSpecificityHandling.Ignore,
                    MassErrorHandling = Qonverter.MassErrorHandling.Ignore,
                    MissedCleavagesHandling = Qonverter.MissedCleavagesHandling.Ignore,
                    DecoyPrefix = decoyPrefix,
                    ScoreInfoByName = new Dictionary<string, Qonverter.Settings.ScoreInfo>()
                    {
                        { "score1", new Qonverter.Settings.ScoreInfo() { Weight = 1 } },
                        { "score2", new Qonverter.Settings.ScoreInfo() { Weight = 0 } }
                    }
                };

            var progressTester = new QonversionProgressTest()
            {
                Qonverter = qonverter,
                ExpectedTotalAnalyses = analysisCount * sourceCount // engine 3 is ignored
            };

            File.Copy("testStaticQonversion.idpDB", "testStaticQonversion2.idpDB", true);

            // test without progress monitor
            qonverter.Qonvert("testStaticQonversion2.idpDB");

            qonverter.QonversionProgress += new IDPicker.Qonverter.QonversionProgressEventHandler(progressTester.qonverter_QonversionProgress);

            File.Copy("testStaticQonversion.idpDB", "testStaticQonversion3.idpDB", true);

            // test progress monitor with cancelling
            // (must before a full qonversion since the QValue test doesn't expect cancellation)
            progressTester.ExpectedQonvertedAnalyses = 0;
            progressTester.CancelAtCount = 4;
            qonverter.Qonvert("testStaticQonversion3.idpDB");

            // test progress monitor without cancelling
            progressTester.ExpectedQonvertedAnalyses = 0;
            progressTester.CancelAtCount = int.MaxValue;
            qonverter.Qonvert("testStaticQonversion.idpDB");

            sessionFactory = SessionFactoryFactory.CreateSessionFactory("testStaticQonversion.idpDB");
            session = sessionFactory.OpenSession();
            session.Clear();

            #region QValue test
            Dictionary<long, double> expectedQValues = new Dictionary<long, double>()
            {
                // with high scoring decoys, Q values can spike and gradually go down again;
                // we squash these spikes such that Q value is monotonically increasing;
                // then we convert Q values to FDR scores using linear interpolation
                //               targets  decoys  ambiguous  unadjusted-Q-value  adjusted-Q-value  FDR-score
                { 4, 0 },        // 1        0       0              0                   0             0
                { 2, 0.2 },      // 2        0       0              0                   0             0.2
                { 3, 0.4 },      // 2        1       0              0.666               0.4           0.4
                { 0, 0.488 },    // 3        1       0              0.5                 0.4           0.488
                { 1, 0.577 },    // 4        1       0              0.4                 0.4           0.577
                { 5, 0.666 },    // 4        2       0              0.666               0.666         0.666
            };

            for (long engine = 1; engine <= 2; ++engine)
            for (long source = 1; source <= 2; ++source)
            for (int charge = 1; charge <= 2; ++charge)
                foreach (var itr in expectedQValues)
                {
                    var topRankedMatch = session.CreateQuery("SELECT psm " +
                                                             "FROM PeptideSpectrumMatch psm " +
                                                             "WHERE psm.Analysis.id = ? " +
                                                             "  AND psm.Spectrum.Source.id = ? " +
                                                             "  AND psm.Spectrum.Index = ? " +
                                                             "  AND psm.Charge = ? " +
                                                             "  AND psm.Rank = 1" +
                                                             "GROUP BY psm.Spectrum.id ")
                                                .SetParameter(0, engine)
                                                .SetParameter(1, source)
                                                .SetParameter(2, itr.Key)
                                                .SetParameter(3, charge)
                                                .List<PeptideSpectrumMatch>().First();

                    Assert.AreEqual(session.Get<Analysis>(engine), topRankedMatch.Analysis);
                    Assert.AreEqual(session.Get<SpectrumSource>(source), topRankedMatch.Spectrum.Source);
                    Assert.AreEqual(charge, topRankedMatch.Charge);
                    Assert.AreEqual(itr.Value, topRankedMatch.QValue, 1e-3);
                }

            var scorelessMatches = session.CreateQuery("SELECT psm " +
                                                       "FROM PeptideSpectrumMatch psm " +
                                                       "WHERE psm.Analysis.id = 3 " +
                                                       "  AND psm.Rank = 1")
                                          .List<PeptideSpectrumMatch>();
            Assert.AreEqual(3, scorelessMatches.Count);
            Assert.AreEqual(0.01, scorelessMatches[0].QValue, 1e-12);
            Assert.AreEqual(0.02, scorelessMatches[1].QValue, 1e-12);
            Assert.AreEqual(0.03, scorelessMatches[2].QValue, 1e-12);
            #endregion
        }

        public class QonversionProgressTest
        {
            public IDPicker.Qonverter Qonverter { get; set; }
            public int CancelAtCount { get; set; }
            public int ExpectedQonvertedAnalyses { get; set; }
            public int ExpectedTotalAnalyses { get; set; }

            public void qonverter_QonversionProgress (object sender, IDPicker.Qonverter.QonversionProgressEventArgs eventArgs)
            {
                Assert.AreEqual(Qonverter, sender);
                Assert.AreEqual(ExpectedQonvertedAnalyses, eventArgs.QonvertedAnalyses);
                Assert.AreEqual(eventArgs.Message.StartsWith("counting") ? 0 : ExpectedTotalAnalyses, eventArgs.TotalAnalyses);

                if (CancelAtCount == eventArgs.QonvertedAnalyses)
                    eventArgs.Cancel = true;
                else
                    Assert.IsTrue(eventArgs.QonvertedAnalyses < CancelAtCount);

                if (eventArgs.Message == "")
                    ++ExpectedQonvertedAnalyses;
            }
        }
    }
}
