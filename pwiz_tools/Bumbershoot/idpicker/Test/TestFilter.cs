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
using NHibernate.Linq;

namespace Test
{
    using SpectrumTuple = TestModel.SpectrumTuple;

    /// <summary>
    /// Summary description for TestFilter
    /// </summary>
    [TestClass]
    public class TestFilter
    {
        NHibernate.ISessionFactory sessionFactory;

        public TestFilter ()
        {
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(":memory:", true, false);
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
        public void TestBasicFilters ()
        {
            string[] testProteinSequences = new string[]
            {
                "PEPTIDERPEPTIDEKPEPTIDE",
                "DERPEPTIDEKPEPTIDE",
                "TIDERPEPTIDEKPEP",

                "ELVISKLIVESRTHANKYAVERYMUCH",
                "ELVISISRCKNRLLKING",
            };
            
            var session = sessionFactory.OpenSession();

            session.Transaction.Begin();
            TestModel.CreateTestProteins(session, testProteinSequences);

            int numSources = 2;
            int numAnalyses = 2;

            for (int source = 1; source <= numSources; ++source)
            for (int analysis = 1; analysis <= numAnalyses; ++analysis)
            {
                int scan = 0;

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    //               Group                            Score  Q   List of Peptide@Charge/ScoreDivider

                    // not enough passing peptides for DERPEPTIDEKPEPTIDE or TIDERPEPTIDEKPEP
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "PEPTIDEK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "DERPEPTIDEK@1/1 PEPTIDER@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "TIDER@1/1"),

                    // not enough distinct peptides for ELVISKLIVESRTHANKYAVERYMUCH
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),

                    // not enough spectra for ELVISISRCKNRLLKING
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"),
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter()
            {
                MaximumQValue = 1,
                MinimumDistinctPeptidesPerProtein = 2,
                MinimumSpectraPerProtein = 3 * numSources,
            };
            dataFilter.ApplyBasicFilters(session);
            
            // clear session so objects are loaded from database
            session.Clear();

            for (int sourceId = 1; sourceId <= numSources; ++sourceId)
            for (int analysisId = 1; analysisId <= numAnalyses; ++analysisId)
            {
                string source = "Source " + sourceId.ToString();
                string analysis = "Engine " + analysisId.ToString();
                // test that PSMs with QValue > MaximumQValue are filtered out
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 0));
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 4));
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 5));
                Assert.IsNotNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 1));
                Assert.IsNotNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 2));
                Assert.IsNotNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 3));
            }

            // test that non-rank-1 PSMs are filtered out
            Assert.AreEqual(0, session.Query<PeptideSpectrumMatch>().Where(o => o.Rank > 1).Count());

            // test that proteins without at least MinimumPeptidesPerProtein peptides are filtered out
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));

            // test that proteins without at least MinimumSpectraPerProtein spectra are filtered out
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING"));

            // test that protein filters cascade to peptide instances
            Assert.AreEqual(0, session.Query<PeptideInstance>().Where(o => o.Protein.Sequence == "DERPEPTIDEKPEPTIDE").Count());
            Assert.AreEqual(0, session.Query<PeptideInstance>().Where(o => o.Protein.Sequence == "TIDERPEPTIDEKPEP").Count());
            Assert.AreEqual(0, session.Query<PeptideInstance>().Where(o => o.Protein.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH").Count());
            Assert.AreEqual(0, session.Query<PeptideInstance>().Where(o => o.Protein.Sequence == "ELVISISRCKNRLLKING").Count());
            Assert.AreEqual(2, session.Query<PeptideInstance>().Where(o => o.Protein.Sequence == "PEPTIDERPEPTIDEKPEPTIDE").Count());

            // test that protein filters cascade to peptides
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "DERPEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "TIDER"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISKLIVESR"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISISR"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "CKNRLLKING"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDER"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDERPEPTIDEK"));

            for (int sourceId = 1; sourceId <= numSources; ++sourceId)
            for (int analysisId = 1; analysisId <= numAnalyses; ++analysisId)
            {
                string source = "Source " + sourceId.ToString();
                string analysis = "Engine " + analysisId.ToString();
                // test that protein filters cascade to PSMs 
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 6));
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 7));
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 8));
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 9));
                Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source && o.Spectrum.Index == 10));
            }

            session.Transaction.Rollback();
            session.Close();
        }

        [TestMethod]
        public void TestProteinGroups ()
        {
            string[] testProteinSequences = new string[]
            {
                "PEPTIDERPEPTIDEKPEPTIDE",
                "DERPEPTIDEKPEPTIDE",
                "TIDERPEPTIDEKPEP",

                "ELVISKLIVESRTHANKYAVERYMUCH",
                "ELVISISRCKNRLLKING",
            };

            var session = sessionFactory.OpenSession();

            session.Transaction.Begin();
            TestModel.CreateTestProteins(session, testProteinSequences);

            int numSources = 2;
            int numAnalyses = 2;

            for (int source = 1; source <= numSources; ++source)
            for (int analysis = 1; analysis <= numAnalyses; ++analysis)
            {
                int scan = 0;

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    //               Group                            Score  Q   List of Peptide@Charge/ScoreDivider

                    // PEPTIDERPEPTIDEKPEPTIDE in protein group: DERPEPTIDEK,PEPTIDEK,PEPTIDERPEPTIDEK,TIDEK
                    // DERPEPTIDEKPEPTIDE in protein group: DERPEPTIDEK,PEPTIDEK,TIDEK
                    // TIDERPEPTIDEKPEP in protein group: DERPEPTIDEK,PEPTIDEK,TIDEK
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDEK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "DERPEPTIDEK@1/1 PEPTIDER@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "TIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "TIDEK@1/1"),

                    // ELVISKLIVESRTHANKYAVERYMUCH in protein group: ELVIS,ELVISK,ELVISKLIVESR
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVIS@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "LIVESR@1/1"),

                    // ELVISISRCKNRLLKING in protein group: CKNRLLKING,ELVIS,ELVISISR
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"),
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter()
            {
                MaximumQValue = 1,
                MinimumDistinctPeptidesPerProtein = 2,
                MinimumSpectraPerProtein = 3 * numSources,
                MinimumAdditionalPeptidesPerProtein = 0
            };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            Assert.AreEqual("2,3,4,6", session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE").ProteinGroup);
            Assert.AreEqual("2,4,6", session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE").ProteinGroup);
            Assert.AreEqual("2,4,6", session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP").ProteinGroup);
            Assert.AreEqual("7,8,9", session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH").ProteinGroup);
            Assert.AreEqual("7,11,12", session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING").ProteinGroup);

            Assert.AreEqual(2, session.Query<Protein>().Where(o => o.ProteinGroup == "2,4,6").Count());

            session.Transaction.Rollback();
            session.Close();
        }

        private string createSimpleProteinSequence (string motif, int length)
        {
            var sequence = new StringBuilder();
            while (sequence.Length < length)
                sequence.Append(motif);
            return sequence.ToString();
        }

        [TestMethod]
        public void TestAdditionalPeptides ()
        {
            // each protein in the test scenarios is created from simple repeating motifs
            var testProteinSequences = new string[]
            {
                createSimpleProteinSequence("A", 20), // PRO1

                createSimpleProteinSequence("B", 20),

                createSimpleProteinSequence("C", 20),

                createSimpleProteinSequence("D", 20),

                createSimpleProteinSequence("E", 20), // PRO5
                createSimpleProteinSequence("E", 21),

                createSimpleProteinSequence("F", 20),
                createSimpleProteinSequence("F", 21),

                createSimpleProteinSequence("G", 20),
                createSimpleProteinSequence("G", 21), // PRO10
                
                createSimpleProteinSequence("H", 20),
                createSimpleProteinSequence("H", 21),
                
                createSimpleProteinSequence("AAAABBBBCCCC", 24),
                createSimpleProteinSequence("AAAABBBB", 24),
                createSimpleProteinSequence("BBBBCCCC", 24), // PRO15
                
                createSimpleProteinSequence("DDDDEEEEFFFF", 24),
                createSimpleProteinSequence("DDDDEEEE", 24),
                createSimpleProteinSequence("DDDDEEEE", 16),

                createSimpleProteinSequence("GGGGHHHHIIII", 24),
                createSimpleProteinSequence("GGGGHHHHIIII", 36), // PRO20
                createSimpleProteinSequence("GGGGHHHH", 24),
                createSimpleProteinSequence("HHHHIIII", 24),
                
                createSimpleProteinSequence("KKKKLLLLMMMM", 24),
                createSimpleProteinSequence("KKKKLLLLMMMM", 36),
                createSimpleProteinSequence("LLLLMMMM", 24), // PRO25
                createSimpleProteinSequence("LLLLMMMM", 16),
                
                createSimpleProteinSequence("NNNNPPPPQQQQ", 24),
                createSimpleProteinSequence("PPPPQQQQRRRR", 24),
                
                createSimpleProteinSequence("SSSSTTTTUUUU", 24),
                createSimpleProteinSequence("TTTTUUUUVVVV", 24), // PRO30
                createSimpleProteinSequence("TTTTUUUUVVVV", 36),
                
                createSimpleProteinSequence("WWWWYYYYZZZZ", 24),
                createSimpleProteinSequence("WWWWYYYYZZZZ", 36),
                createSimpleProteinSequence("YYYYZZZZFFFF", 24),
                
                createSimpleProteinSequence("AAAACCCCEEEE", 24), // PRO35
                createSimpleProteinSequence("AAAACCCCEEEE", 36),
                createSimpleProteinSequence("CCCCEEEEGGGG", 24),
                createSimpleProteinSequence("CCCCEEEEGGGG", 36),

                createSimpleProteinSequence("BBBBBDDDDD", 20),
                createSimpleProteinSequence("BBBBDDDD", 24), // PRO40
                createSimpleProteinSequence("ZBBBBDDD", 24),
                createSimpleProteinSequence("YBBBDDD", 20),
                createSimpleProteinSequence("WBBDD", 21),

                createSimpleProteinSequence("DDDDDFFFFF", 20),
                createSimpleProteinSequence("FFFFFHHHHH", 20), // PRO45

                createSimpleProteinSequence("HHHHHKKKKK", 20),
                createSimpleProteinSequence("HHHHHKKKKK", 30),
                createSimpleProteinSequence("KKKKKMMMMM", 20),

                createSimpleProteinSequence("LLLLLNNNNN", 20),
                createSimpleProteinSequence("NNNNNQQQQQ", 20), // PRO50
                createSimpleProteinSequence("NNNNNQQQQQ", 30),

                createSimpleProteinSequence("MMMMMPPPPP", 20),
                createSimpleProteinSequence("PPPPPRRRRR", 20),
                createSimpleProteinSequence("PPPPPRRRR", 24),

                createSimpleProteinSequence("QQQQSSSSUUUU", 24), // PRO55
                createSimpleProteinSequence("SSSSUUUUYYYY", 24),

                createSimpleProteinSequence("RRRRTTTTWWWWZZZZ", 32),
                createSimpleProteinSequence("TTTTWWWWZZZZBBBB", 32),
                createSimpleProteinSequence("RRRRTTTTZZZZBBBBTTTTWWWW", 24),

                createSimpleProteinSequence("AAAADDDDGGGGKKKK", 32), // PRO60
                createSimpleProteinSequence("DDDDGGGGKKKKNNNN", 32),
                createSimpleProteinSequence("AAAADDDDKKKKNNNN", 32),

                createSimpleProteinSequence("BBBBEEEEHHHH", 24),
                createSimpleProteinSequence("EEEEHHHHLLLL", 24),
                createSimpleProteinSequence("HHHHLLLLPPPP", 24), // PRO65

                createSimpleProteinSequence("CCCCFFFFIIII", 24),
                createSimpleProteinSequence("FFFFIIIIMMMM", 24),
                createSimpleProteinSequence("IIIIMMMMQQQQ", 24),

                createSimpleProteinSequence("NNNNRRRRUUUU", 24),
                createSimpleProteinSequence("RRRRUUUUZZZZ", 24), // PRO70
                createSimpleProteinSequence("UUUUZZZZCCCC", 24),
            };
            
            var session = sessionFactory.OpenSession();

            session.Transaction.Begin();
            TestModel.CreateTestProteins(session, testProteinSequences);

            const int analysisCount = 2;
            const int sourceCount = 1;
            const int chargeCount = 2;

            for (int analysis = 1; analysis <= analysisCount; ++analysis)
            for (int source = 1; source <= sourceCount; ++source)
            for (int charge = 1; charge <= chargeCount; ++charge)
            {
                int scan = 0;

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    // Columns:     Group  Source Spectrum Analysis Score Q List of Peptide@Charge/ScoreDivider
                    
                    // 1 protein (PRO1) to 1 peptide to 1 spectrum = 1 additional peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAAAAAAAA@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO2) to 1 peptide to 2 spectra = 1 additional peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBBBBBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),

                    // 1 protein (PRO3) to 2 peptides to 1 spectrum (each) = 2 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCCCCCCC@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCCCCCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO4) to 2 peptides to 2 spectra (each) = 2 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDDD@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDDD@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDD@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDD@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO5,6) to 1 peptide to 1 spectrum = 1 additional peptide (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("EEEEEEEEEE@{0}/1 AAAAAAAAAA@{0}/8", charge)),

                    // 2 proteins (PRO7,8) to 1 peptide to 2 spectra = 1 additional peptide (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFFFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO9,10) to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGGG@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGG@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO11,12) to 2 peptides to 2 spectra (each) = 2 additional peptides (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHHH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHH@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHH@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO13) to 2 peptides = 2 additional peptide
                    // 1 protein (PRO14) to 1 of the above peptides = 0 additional peptides (subsumed protein)
                    // 1 protein (PRO15) to the other above peptide = 0 additional peptides (subsumed protein)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAABBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBCCCC@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO16) to 2 peptides = 2 additional peptide
                    // 2 proteins (PRO17,18) to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDEEEE@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("EEEEFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO19,20) to 2 peptides = 2 additional peptide (ambiguous protein group)
                    // 1 protein (PRO21) to 1 of the above peptides = 0 additional peptides (subsumed protein)
                    // 1 protein (PRO22) to the other above peptide = 0 additional peptides (subsumed protein)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGHHHH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHIIII@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO23,24) to 2 peptides = 2 additional peptides (ambiguous protein group)
                    // 2 proteins (PRO25,26) to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("KKKKLLLL@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLMMMM@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO27) to 3 peptides = 3 additional peptides
                    // 1 protein (PRO28) to 1 of the above peptides and 1 extra peptide = 1 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("NNNNPPPP@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("NNNNPPP@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("PPPPQQQQ@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("QQQQRRRR@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO29) to 3 peptides = 3 additional peptides
                    // 2 proteins (PRO30,31) to 1 of the above peptides and 1 extra peptide = 1 additional peptides (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("SSSSTTTT@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("SSSSTTT@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("TTTTUUUU@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("UUUUVVVV@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO32,33) to 3 peptides = 3 additional peptides (ambiguous protein group)
                    // 1 protein (PRO34) to 1 of the above peptides and 1 extra peptide = 1 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("WWWWYYYY@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("WWWWYYY@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("YYYYZZZZ@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("ZZZZFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO35,36) to 3 peptides = 3 additional peptides (ambiguous protein group)
                    // 2 proteins (PRO37,38) to 1 of the above peptides and 1 extra peptide = 1 additional peptides (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAACCCC@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAACCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCEEEE@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("EEEEGGGG@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO39) to 5 peptides = 5 additional peptides
                    // 1 protein (PRO40) to 4 of the above peptides = 0 additional peptides
                    // 1 protein (PRO41) to 3 of the above peptides and 1 extra peptides = 1 additional peptides
                    // 1 protein (PRO42) to 2 of the above peptides and 2 extra peptides = 2 additional peptides
                    // 1 protein (PRO43) to 1 of the above peptides and 3 extra peptides = 3 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBBDDDDD@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBDDDD@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBDDD@{0}/1    AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBDD@{0}/1      BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBDD@{0}/1       AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("ZBBBBDDD@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("YBBBDD@{0}/1     AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("YBBBDDD@{0}/1    BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("WBB@{0}/1        AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("WBBD@{0}/1       BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("WBBDD@{0}/1      AAAAAAAAAA@{0}/8", charge)),

                    // 1 protein (PRO44) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
                    // 1 protein (PRO45) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDFFFF@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DFFFFF@{0}/1 FFFFFH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    
                    // 2 proteins (PRO46,47) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
                    // 1 protein (PRO48) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHKKKKK@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHKKKK@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HKKKKK@{0}/1 KKKKKM@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("KKKKKMMMMM@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    
                    // 1 protein (PRO49) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
                    // 2 proteins (PRO50,51) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLLNNNNN@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLLNNNN@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LNNNNN@{0}/1 NNNNNQ@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("NNNNNQQQQQ@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    
                    // 1 protein (PRO52) to 3 peptides, 2 of which are evidenced by ambiguous spectra = 3 additional peptides
                    // 1 protein (PRO53) to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                    // 1 protein (PRO54) to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MMMMMPPPPP@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MPPPPP@{0}/1 PRRRRR@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("PPPPPRRRRR@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MPPPP@{0}/1 PRRRRP@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("PPPPRRRRP@{0}/1  AAAAAAAAAA@{0}/8", charge)),

                    // PRO55 -> QQQQSSSS, SSSSUUUU = 2 additional peptides
                    // PRO56 -> UUUUYYYY, SSSSUUUU = 2 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("QQQQSSSS@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("SSSSUUUU@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("UUUUYYYY@{0}/1   AAAAAAAAAA@{0}/8", charge)),

                    // PRO57 -> RRRRTTTT, WWWWZZZZ, TTTTWWWW = 3 additional peptides
                    // PRO58 -> ZZZZBBBB, WWWWZZZZ, TTTTWWWW = 3 additional peptides
                    // PRO59 -> RRRRTTTT, ZZZZBBBB, TTTTWWWW = 3 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("RRRRTTTT@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("WWWWZZZZ@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("TTTTWWWW@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("ZZZZBBBB@{0}/1   BBBBBBBBBB@{0}/8", charge)),

                    // PRO60 -> AAAADDDD, DDDDGGGG, GGGGKKKK = 3 additional peptides
                    // PRO61 -> DDDDGGGG, GGGGKKKK, KKKKNNNN = 3 additional peptides
                    // PRO62 -> AAAADDDD, KKKKNNNN = 0 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAADDDD@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDGGGG@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGKKKK@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("KKKKNNNN@{0}/1   BBBBBBBBBB@{0}/8", charge)),

                    // PRO63 -> BBBBEEEE, EEEEHHHH = 2 additional peptides
                    // PRO64 -> EEEEHHHH, HHHHLLLL = 0 additional peptides
                    // PRO65 -> HHHHLLLL, LLLLPPPP = 2 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBEEEE@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("EEEEHHHH@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHLLLL@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLPPPP@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    
                    // PRO66 -> CCCCFFFF, CFFFFI, FFFFIIII = 3 additional peptides
                    // PRO67 -> FFFFIIII, FIIIIM, IIIIMMMM = 1 additional peptides
                    // PRO68 -> IIIIMMMM, IMMMMQ, MMMMQQQQ = 3 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCFFFF@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CFFFFI@{0}/1     BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFIIII@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FIIIIM@{0}/1     BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("IIIIMMMM@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("IMMMMQ@{0}/1     BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MMMMQQQQ@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    
                    // PRO69 -> NNNNRRRR, RRRRUUUU, RRRRUUU = 3 additional peptides
                    // PRO70 -> RRRRUUUU, RRRRUUU, UUUUZZZZ = 0 additional peptides
                    // PRO71 -> UUUUZZZZ, UZZZZC, ZZZZCCCC = 3 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("NNNNRRRR@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("RRRRUUUU@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("RRRRUUU@{0}/1    AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("UUUUZZZZ@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("UZZZZC@{0}/1     AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("ZZZZCCCC@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter_Accessor()
            {
                MaximumQValue = 1,
                MinimumDistinctPeptidesPerProtein = 0,
                MinimumSpectraPerProtein = 0,
                MinimumAdditionalPeptidesPerProtein = 0
            };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            Map<long, long> additionalPeptidesByProteinId = DataFilter_Accessor.CalculateAdditionalPeptides(session);

            // 1 protein to 1 peptide to 1 spectrum = 1 additional peptide
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO1").Id.GetValueOrDefault()]);

            // 1 protein to 1 peptide to 2 spectra = 1 additional peptide
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO2").Id.GetValueOrDefault()]);

            // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO3").Id.GetValueOrDefault()]);

            // 1 protein to 2 peptides to 2 spectra (each) = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO4").Id.GetValueOrDefault()]);

            // 2 proteins to 1 peptide to 1 spectrum = 1 additional peptide (ambiguous protein group)
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO5").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO6").Id.GetValueOrDefault()]);

            // 2 proteins to 1 peptide to 2 spectra = 1 additional peptide (ambiguous protein group)
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO7").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO8").Id.GetValueOrDefault()]);

            // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO9").Id.GetValueOrDefault()]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO10").Id.GetValueOrDefault()]);

            // 2 proteins to 2 peptides to 2 spectra (each) = 2 additional peptides (ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO11").Id.GetValueOrDefault()]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO12").Id.GetValueOrDefault()]);

            // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptide
            // 1 protein to 1 of the above peptides = 0 additional peptides (subsumed protein)
            // 1 protein to the other above peptide = 0 additional peptides (subsumed protein)
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO13").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO14").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO15").Id.GetValueOrDefault()]);

            // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptide
            // 2 proteins to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO16").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO17").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO18").Id.GetValueOrDefault()]);

            // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptide (ambiguous protein group)
            // 1 protein to 1 of the above peptides = 0 additional peptides (subsumed protein)
            // 1 protein to the other above peptide = 0 additional peptides (subsumed protein)
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO19").Id.GetValueOrDefault()]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO20").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO21").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO22").Id.GetValueOrDefault()]);

            // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
            // 2 proteins to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO23").Id.GetValueOrDefault()]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO24").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO25").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO26").Id.GetValueOrDefault()]);

            // 1 protein to 3 peptides to 1 spectrum (each) = 3 additional peptides
            // 1 protein to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO27").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO28").Id.GetValueOrDefault()]);

            // 1 protein to 3 peptides to 1 spectrum (each) = 3 additional peptides
            // 2 proteins to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides (ambiguous protein group)
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO29").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO30").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO31").Id.GetValueOrDefault()]);

            // 2 proteins to 3 peptides to 1 spectrum (each) = 3 additional peptides (ambiguous protein group)
            // 1 protein to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO32").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO33").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO34").Id.GetValueOrDefault()]);

            // 2 proteins to 3 peptides to 1 spectrum (each) = 3 additional peptides (ambiguous protein group)
            // 2 proteins to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides (ambiguous protein group)
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO35").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO36").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO37").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO38").Id.GetValueOrDefault()]);

            // 1 protein (PRO39) to 5 peptides = 5 additional peptides
            // 1 protein (PRO40) to 4 of the above peptides = 0 additional peptides
            // 1 protein (PRO41) to 3 of the above peptides and 1 extra peptides = 1 additional peptides
            // 1 protein (PRO42) to 2 of the above peptides and 2 extra peptides = 2 additional peptides
            // 1 protein (PRO43) to 1 of the above peptides and 3 extra peptides = 3 additional peptides
            Assert.AreEqual(5, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO39").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO40").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO41").Id.GetValueOrDefault()]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO42").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO43").Id.GetValueOrDefault()]);

            // 1 protein (PRO44) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
            // 1 protein (PRO45) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO44").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO45").Id.GetValueOrDefault()]);
                    
            // 2 proteins (PRO46,47) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
            // 1 protein (PRO48) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO46").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO47").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO48").Id.GetValueOrDefault()]);
            
            // 1 protein (PRO49) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
            // 2 proteins (PRO50,51) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO49").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO50").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO51").Id.GetValueOrDefault()]);

            // 1 protein (PRO52) to 3 peptides, 2 of which are evidenced by ambiguous spectra = 3 additional peptides
            // 1 protein (PRO53) to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            // 1 protein (PRO54) to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO52").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO53").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO54").Id.GetValueOrDefault()]);
            
            // PRO55 -> QQQQSSSS, SSSSUUUU = 2 additional peptides
            // PRO56 -> UUUUYYYY, SSSSUUUU = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO55").Id.GetValueOrDefault()]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO56").Id.GetValueOrDefault()]);

            // PRO57 -> RRRRTTTT, WWWWZZZZ, TTTTWWWW = 3 additional peptides
            // PRO58 -> QQQQKKKK, WWWWZZZZ, TTTTWWWW = 3 additional peptides
            // PRO59 -> RRRRTTTT, TTTTZZZZ, TTTTWWWW = 3 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO57").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO58").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO59").Id.GetValueOrDefault()]);

            // PRO60 -> AAAADDDD, DDDDGGGG, GGGGKKKK = 3 additional peptides
            // PRO61 -> DDDDGGGG, GGGGKKKK, KKKKNNNN = 3 additional peptides
            // PRO62 -> AAAADDDD, KKKKNNNN = 0 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO60").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO61").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO62").Id.GetValueOrDefault()]);

            // PRO63 -> BBBBEEEE, EEEEHHHH = 2 additional peptides
            // PRO64 -> EEEEHHHH, HHHHLLLL = 0 additional peptides
            // PRO65 -> HHHHLLLL, LLLLPPPP = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO63").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO64").Id.GetValueOrDefault()]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO65").Id.GetValueOrDefault()]);

            // PRO66 -> CCCCFFFF, CFFFFI, FFFFIIII = 3 additional peptides
            // PRO67 -> FFFFIIII, FIIIIM, IIIIMMMM = 1 additional peptides
            // PRO68 -> IIIIMMMM, IMMMMQ, MMMMQQQQ = 3 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO66").Id.GetValueOrDefault()]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO67").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO68").Id.GetValueOrDefault()]);

            // PRO69 -> NNNNRRRR, RRRRU, RRRRUUUU = 3 additional peptides
            // PRO70 -> RRRRUUUU, RUUUUZ, UUUUZZZZ = 0 additional peptides
            // PRO71 -> UUUUZZZZ, UZZZZC, ZZZZCCCC = 3 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO69").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO70").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO71").Id.GetValueOrDefault()]);

            session.Transaction.Rollback();
            session.Close();
        }
    }
}
