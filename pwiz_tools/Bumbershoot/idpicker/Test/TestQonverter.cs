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
        // [TestInitialize()]
        // public void MyTestInitialize() { }
        //
        // Use TestCleanup to run code after each test has run
        // [TestCleanup()]
        // public void MyTestCleanup() { }
        //
        #endregion

        [TestMethod]
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

            var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testStaticQonversion.idpDB", true, false);
            var session = sessionFactory.OpenSession();

            session.Transaction.Begin();
            TestModel.CreateTestProteins(session, testProteinSequences);

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
            // 5    99    T          1       0      0            0
            // 11   95    T          2       0      0            0
            // 12   90    T          3       0      0            0
            // 16   85    D          3       1      0            2/4
            // 15   80    D          3       2      0            4/5
            // 17   75    T          4       2      0            4/6
            // 14   70    T          5       2      0            4/7
            // 2    65    A          5       2      1            4/7
            // 7    60    D          5       3      1            6/8
            // 10   55    T          6       3      1            6/9
            // 4    50    T          7       3      1            6/10
            // 8    45    T          8       3      1            6/11
            // 9    40    T          9       3      1            6/12
            // 3    35    D          9       4      1            8/13
            // 1    30    D          9       5      1            10/14
            // 13   25    A          9       5      2            10/14
            // 6    20    T          10      5      2            10/15

            double q = PeptideSpectrumMatch.DefaultQValue;

            for (int analysis = 1; analysis <= analysisCount; ++analysis)
            for (int source = 1; source <= sourceCount; ++source)
            for (int charge = 1; charge <= chargeCount; ++charge)
            {
                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    //               Group Source Spectrum  Analysis    Score Q   List of Peptide@Charge/ScoreDivider
                    new SpectrumTuple("/",  source,     5,  analysis,     99, q, String.Format("TIDERPEPTIDEK@{0}/1 PEPTIDER@{0}/8", charge)),
                    new SpectrumTuple("/",  source,     11, analysis,     95, q, String.Format("TIDERPEPTIDEK@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     12, analysis,     90, q, String.Format("PEPTIDEKPEP@{0}/1 THEQUICKBR@{0}/4", charge)),
                    new SpectrumTuple("/",  source,     16, analysis,     85, q, String.Format("EDITPEP@{0}/1 PEPTIDEK@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     15, analysis,     80, q, String.Format("EDITPEPR@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     17, analysis,     75, q, String.Format("TIDER@{0}/1 TIDERPEPTIDEK@{0}/3", charge)),
                    new SpectrumTuple("/",  source,     14, analysis,     70, q, String.Format("TIDER@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     2,  analysis,     65, q, String.Format("THEQUICKBR@{0}/1 BKCIUQEHT@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     7,  analysis,     60, q, String.Format("KEDITPEPR@{0}/1 DERPEPTIDEK@{0}/4", charge)),
                    new SpectrumTuple("/",  source,     4,  analysis,     50, q, String.Format("THELZYDG@{0}/1 PEPTI@{0}/3", charge)),
                    new SpectrumTuple("/",  source,     10, analysis,     55, q, String.Format("DERPEPTIDEK@{0}/1 PEPTIDEK@{0}/5", charge)),
                    new SpectrumTuple("/",  source,     8,  analysis,     45, q, String.Format("PEPTI@{0}/1 PEPTIDER@{0}/6", charge)),
                    new SpectrumTuple("/",  source,     9,  analysis,     40, q, String.Format("PEPTIDEK@{0}/1 THEQUICKBR@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     3,  analysis,     35, q, String.Format("PEPKEDITPEPR@{0}/1 THELZYDG@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     1,  analysis,     30, q, String.Format("GDYZLEHT@{0}/1 THEQUICKBR@{0}/2", charge)),
                    new SpectrumTuple("/",  source,     13, analysis,     25, q, String.Format("PEPTIDER@{0}/1 PEPKEDITPEPR@{0}/1 BKCIUQEHT@{0}/3", charge)),
                    new SpectrumTuple("/",  source,     6,  analysis,     20, q, String.Format("THEQUICKBR@{0}/1 DERPEPTIDEK@{0}/4", charge)),
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            List<SpectrumTuple> testPsmSummaryWithoutScores = new List<SpectrumTuple>()
            {
                new SpectrumTuple("/", 1, 1, 3, null, 0.01, "TIDERPEPTIDEK@1/1 PEPTIDER@3/8"),
                new SpectrumTuple("/", 1, 2, 3, null, 0.02, "PEPTIDEKPEP@2/1 PEPTIDEK@2/8"),
                new SpectrumTuple("/", 1, 3, 3, null, 0.03, "THEQUICKBR@3/1 THELZYDG@1/8"),
            };

            TestModel.CreateTestData(session, testPsmSummaryWithoutScores);

            session.Transaction.Commit();
            session.Close(); // close the connection
            #endregion

            var qonverter = new IDPicker.StaticWeightQonverter()
            {
                DecoyPrefix = decoyPrefix,
                ScoreWeights = new Dictionary<string, double>() { { "score1", 1 }, { "score2", 0 } }
            };

            var progressTester = new QonversionProgressTest()
            {
                Qonverter = qonverter,
                ExpectedTotalAnalyses = analysisCount * sourceCount * chargeCount // engine 3 is ignored
            };

            System.IO.File.Copy("testStaticQonversion.idpDB", "testStaticQonversion.idpDB-copy");

            // test without progress monitor
            qonverter.Qonvert("testStaticQonversion.idpDB");

            qonverter.QonversionProgress += new IDPicker.StaticWeightQonverter.QonversionProgressEventHandler(progressTester.qonverter_QonversionProgress);

            System.IO.File.Delete("testStaticQonversion.idpDB");
            System.IO.File.Copy("testStaticQonversion.idpDB-copy", "testStaticQonversion.idpDB");

            // test progress monitor with cancelling
            // (must before a full qonversion since the QValue test doesn't expect cancellation)
            progressTester.ExpectedQonvertedAnalyses = 0;
            progressTester.CancelAtCount = 4;
            qonverter.Qonvert("testStaticQonversion.idpDB");

            System.IO.File.Delete("testStaticQonversion.idpDB");
            System.IO.File.Copy("testStaticQonversion.idpDB-copy", "testStaticQonversion.idpDB");

            // test progress monitor without cancelling
            progressTester.ExpectedQonvertedAnalyses = 0;
            progressTester.CancelAtCount = int.MaxValue;
            qonverter.Qonvert("testStaticQonversion.idpDB");

            session = sessionFactory.OpenSession();
            session.Clear();

            #region QValue test
            Dictionary<long, double> expectedQValues = new Dictionary<long, double>()
            {
                { 4, 0 },
                { 10, 0 },
                { 11, 0 },
                { 15, 2/4.0 },
                { 14, 4/5.0 },
                { 16, 4/6.0 },
                { 13, 4/7.0 },
                { 1, 4/7.0 },
                { 6, 6/8.0 },
                { 9, 6/9.0 },
                { 3, 6/10.0 },
                { 7, 6/11.0 },
                { 8, 6/12.0 },
                { 2, 8/13.0 },
                { 0, 10/14.0 },
                { 12, 10/14.0 },
                { 5, 10/15.0 },
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
                    Assert.AreEqual(itr.Value, topRankedMatch.QValue, 1e-12);
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
            public IDPicker.StaticWeightQonverter Qonverter { get; set; }
            public int CancelAtCount { get; set; }
            public int ExpectedQonvertedAnalyses { get; set; }
            public int ExpectedTotalAnalyses { get; set; }

            public void qonverter_QonversionProgress (object sender, IDPicker.StaticWeightQonverter.QonversionProgressEventArgs eventArgs)
            {
                Assert.AreEqual(Qonverter, sender);
                Assert.AreEqual(ExpectedQonvertedAnalyses, eventArgs.QonvertedAnalyses);
                Assert.AreEqual(ExpectedTotalAnalyses, eventArgs.TotalAnalyses);

                if (CancelAtCount == eventArgs.QonvertedAnalyses)
                    eventArgs.Cancel = true;
                else
                    Assert.IsTrue(eventArgs.QonvertedAnalyses < CancelAtCount);

                ++ExpectedQonvertedAnalyses;
            }
        }
    }
}
