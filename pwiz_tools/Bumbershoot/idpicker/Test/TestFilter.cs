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
        public void TestAggregation ()
        {
            var lhs = new DataFilter() { MaximumQValue = 1 };
            var rhs = new DataFilter() { Protein =  new List<Protein>()};
            rhs.Protein.Add(new Protein("foo", "bar"));
            var result = lhs + rhs;

            var rhs2 = new DataFilter() { Peptide = new List<Peptide>() };
            rhs2.Peptide.Add((Peptide)new Peptide_Accessor("FOO").Target);
            var result2 = result + rhs2;

            Assert.AreEqual(1, result.Protein.Count);
            Assert.AreEqual(1, result2.Protein.Count);
            Assert.AreEqual(1, result2.Peptide.Count);

            Assert.AreEqual("foo", result.Protein.FirstOrDefault().Description);
            Assert.AreEqual("foo", result2.Protein.FirstOrDefault().Description);
            Assert.AreEqual("FOO", result2.Peptide.FirstOrDefault().Sequence);
        }

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

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, true, false);
            var session = sessionFactory.OpenSession();

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

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, true, true);
            var session = sessionFactory.OpenSession();

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

            Assert.AreEqual(1, session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE").ProteinGroup);
            Assert.AreEqual(2, session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE").ProteinGroup);
            Assert.AreEqual(2, session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP").ProteinGroup);
            Assert.AreEqual(4, session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH").ProteinGroup);
            Assert.AreEqual(3, session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING").ProteinGroup);

            Assert.AreEqual(2, session.Query<Protein>().Where(o => o.ProteinGroup == 2).Count());

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

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, true, false);
            var session = sessionFactory.OpenSession();

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

            // PRO69 -> NNNNRRRR, RRRRUUUU, RRRRUUU = 3 additional peptides
            // PRO70 -> RRRRUUUU, RRRRUUU, UUUUZZZZ = 0 additional peptides
            // PRO71 -> UUUUZZZZ, UZZZZC, ZZZZCCCC = 3 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO69").Id.GetValueOrDefault()]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO70").Id.GetValueOrDefault()]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[session.UniqueResult<Protein>(o => o.Accession == "PRO71").Id.GetValueOrDefault()]);

            // test that the MinimumAdditionalPeptidesPerProtein filter is applied correctly;
            // proteins filtered out by it should cascade to PeptideInstances, Peptides, and PSMs

            dataFilter.MinimumAdditionalPeptidesPerProtein = 1;
            dataFilter.ApplyBasicFilters(session);

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO55"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO56"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO57"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO58"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO59"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO60"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO61"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO62"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO62"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "AAAADDDD"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "KKKKNNNN"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO63"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO64"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO65"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO64"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "EEEEHHHH"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHLLLL"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO66"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO67"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO68"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO69"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO70"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO71"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO70"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "RRRRUUUU"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "RRRRUUU"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "UUUUZZZZ"));

            dataFilter.MinimumAdditionalPeptidesPerProtein = 2;
            dataFilter.ApplyBasicFilters(session);

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO55"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO56"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO57"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO58"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO59"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO60"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO61"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO62"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO62"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO63"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO64"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO65"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO64"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO66"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO67"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO68"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO67"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "FIIIIM"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "FIIIIM"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO69"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO70"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO71"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO70"));

            dataFilter.MinimumAdditionalPeptidesPerProtein = 3;
            dataFilter.ApplyBasicFilters(session);

            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO55"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO56"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO55"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO56"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "QQQQSSSS"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "SSSSUUUU"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "UUUUYYYY"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "QQQQSSSS"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "SSSSUUUU"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "UUUUYYYY"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO57"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO58"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO59"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO60"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO61"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO62"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO62"));

            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO63"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO64"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO65"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO63"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO64"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO65"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "BBBBEEEE"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "EEEEHHHH"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHLLLL"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "LLLLPPPP"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "BBBBEEEE"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "EEEEHHHH"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "HHHHLLLL"));
            Assert.IsNull(session.UniqueResult<PeptideSpectrumMatch>(o => o.Peptide.Sequence == "LLLLPPPP"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO66"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO67"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO68"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO67"));

            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO69"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Accession == "PRO70"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO71"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO70"));

            dataFilter.MinimumAdditionalPeptidesPerProtein = 4;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(1, session.Query<Protein>().Count());
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO39"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession != "PRO39"));

            var remainingPeptides = new string[] { "BBBBBDDDDD", "BBBBDDDD", "BBBBDDD", "BBBDD", "BBDD" };
            Assert.AreEqual(5, session.Query<Peptide>().Count());
            Assert.AreEqual(5, session.Query<Peptide>().Count(o => remainingPeptides.Contains(o.Sequence)));
            Assert.AreEqual(0, session.Query<Peptide>().Count(o => !remainingPeptides.Contains(o.Sequence)));
            Assert.AreNotEqual(0, session.Query<PeptideSpectrumMatch>().Count(o => remainingPeptides.Contains(o.Peptide.Sequence)));
            Assert.AreEqual(0, session.Query<PeptideSpectrumMatch>().Count(o => !remainingPeptides.Contains(o.Peptide.Sequence)));

            dataFilter.MinimumAdditionalPeptidesPerProtein = 5;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(1, session.Query<Protein>().Count());
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO39"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession != "PRO39"));

            dataFilter.MinimumAdditionalPeptidesPerProtein = 6;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(0, session.Query<Protein>().Count());
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count());
            Assert.AreEqual(0, session.Query<Peptide>().Count());
            Assert.AreEqual(0, session.Query<PeptideSpectrumMatch>().Count());

            session.Close();
        }

        [TestMethod]
        public void TestClusters ()
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

                createSimpleProteinSequence("BBBBBDDDDD", 20),
                createSimpleProteinSequence("BBBBDDDD", 24),
                createSimpleProteinSequence("ZBBBBDDD", 24),
                createSimpleProteinSequence("YBBBDDD", 20),
                createSimpleProteinSequence("WBBDD", 21), // PRO20

                createSimpleProteinSequence("DDDDDFFFFF", 20),
                createSimpleProteinSequence("FFFFFHHHHH", 20),

                createSimpleProteinSequence("HHHHHKKKKK", 20),
                createSimpleProteinSequence("HHHHHKKKKK", 30),
                createSimpleProteinSequence("KKKKKMMMMM", 20), // PRO25

                createSimpleProteinSequence("LLLLLNNNNN", 20),
                createSimpleProteinSequence("NNNNNQQQQQ", 20),
                createSimpleProteinSequence("NNNNNQQQQQ", 30),

                createSimpleProteinSequence("MMMMMPPPPP", 20),
                createSimpleProteinSequence("PPPPPRRRRR", 20), // PRO30
                createSimpleProteinSequence("PPPPPRRRR", 24),
            };

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, true, false);
            var session = sessionFactory.OpenSession();

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
                    
                    // 1 protein to 1 peptide to 1 spectrum = 1 additional peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAAAAAAAA@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 1 peptide to 2 spectra = 1 additional peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBBBBBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),

                    // 1 protein to 2 peptides to 1 spectrum (each)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCCCCCCC@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCCCCCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 2 peptides to 2 spectra (each)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDDD@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDDD@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDD@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDD@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 1 peptide to 1 spectrum (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("EEEEEEEEEE@{0}/1 AAAAAAAAAA@{0}/8", charge)),

                    // 2 proteins to 1 peptide to 2 spectra (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFFFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 2 peptides to 1 spectrum (each) (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGGG@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGG@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 2 peptides to 2 spectra (each) (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHHH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHH@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHH@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 2 peptides
                    // 1 protein to 1 of the above peptides (subsumed protein)
                    // 1 protein to the other above peptide (subsumed protein)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAABBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBCCCC@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 5 peptides
                    // 1 protein to 4 of the above peptides
                    // 1 protein to 3 of the above peptides and 1 extra peptides
                    // 1 protein to 2 of the above peptides and 2 extra peptides
                    // 1 protein to 1 of the above peptides and 3 extra peptides
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

                    // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
                    // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDFFFF@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DFFFFF@{0}/1 FFFFFH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    
                    // 2 proteins to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
                    // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHKKKKK@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHKKKK@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HKKKKK@{0}/1 KKKKKM@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("KKKKKMMMMM@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    
                    // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
                    // 2 proteins to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLLNNNNN@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLLNNNN@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LNNNNN@{0}/1 NNNNNQ@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("NNNNNQQQQQ@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    
                    // 1 protein to 3 peptides, 2 of which are evidenced by ambiguous spectra
                    // 1 protein to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide
                    // 1 protein to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MMMMMPPPPP@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MPPPPP@{0}/1 PRRRRR@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("PPPPPRRRRR@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MPPPP@{0}/1 PRRRRP@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("PPPPRRRRP@{0}/1  AAAAAAAAAA@{0}/8", charge)),

                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter()
            {
                MaximumQValue = 1,
                MinimumDistinctPeptidesPerProtein = 0,
                MinimumSpectraPerProtein = 0,
                MinimumAdditionalPeptidesPerProtein = 0
            };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            // 1 protein to 1 peptide to 1 spectrum
            Assert.AreEqual(1, session.UniqueResult<Protein>(o => o.Accession == "PRO1").Cluster);

            // 1 protein to 1 peptide to 2 spectra
            Assert.AreEqual(2, session.UniqueResult<Protein>(o => o.Accession == "PRO2").Cluster);

            // 1 protein to 2 peptides to 1 spectrum (each)
            Assert.AreEqual(3, session.UniqueResult<Protein>(o => o.Accession == "PRO3").Cluster);

            // 1 protein to 2 peptides to 2 spectra (each)
            Assert.AreEqual(4, session.UniqueResult<Protein>(o => o.Accession == "PRO4").Cluster);

            // 2 proteins to 1 peptide to 1 spectrum (ambiguous protein group)
            Assert.AreEqual(5, session.UniqueResult<Protein>(o => o.Accession == "PRO5").Cluster);
            Assert.AreEqual(5, session.UniqueResult<Protein>(o => o.Accession == "PRO6").Cluster);

            // 2 proteins to 1 peptide to 2 spectra (ambiguous protein group)
            Assert.AreEqual(6, session.UniqueResult<Protein>(o => o.Accession == "PRO7").Cluster);
            Assert.AreEqual(6, session.UniqueResult<Protein>(o => o.Accession == "PRO8").Cluster);

            // 2 proteins to 2 peptides to 1 spectrum (each) (ambiguous protein group)
            Assert.AreEqual(7, session.UniqueResult<Protein>(o => o.Accession == "PRO9").Cluster);
            Assert.AreEqual(7, session.UniqueResult<Protein>(o => o.Accession == "PRO10").Cluster);

            // 2 proteins to 2 peptides to 2 spectra (each) (ambiguous protein group)
            Assert.AreEqual(8, session.UniqueResult<Protein>(o => o.Accession == "PRO11").Cluster);
            Assert.AreEqual(8, session.UniqueResult<Protein>(o => o.Accession == "PRO12").Cluster);

            // 1 protein to 2 peptides to 1 spectrum (each)
            // 1 protein to 1 of the above peptides (subsumed protein)
            // 1 protein to the other above peptide (subsumed protein)
            Assert.AreEqual(9, session.UniqueResult<Protein>(o => o.Accession == "PRO13").Cluster);
            Assert.AreEqual(9, session.UniqueResult<Protein>(o => o.Accession == "PRO14").Cluster);
            Assert.AreEqual(9, session.UniqueResult<Protein>(o => o.Accession == "PRO15").Cluster);

            // 1 protein to 5 peptides
            // 1 protein to 4 of the above peptides
            // 1 protein to 3 of the above peptides and 1 extra peptides
            // 1 protein to 2 of the above peptides and 2 extra peptides
            // 1 protein to 1 of the above peptides and 3 extra peptides
            Assert.AreEqual(10, session.UniqueResult<Protein>(o => o.Accession == "PRO16").Cluster);
            Assert.AreEqual(10, session.UniqueResult<Protein>(o => o.Accession == "PRO17").Cluster);
            Assert.AreEqual(10, session.UniqueResult<Protein>(o => o.Accession == "PRO18").Cluster);
            Assert.AreEqual(10, session.UniqueResult<Protein>(o => o.Accession == "PRO19").Cluster);
            Assert.AreEqual(10, session.UniqueResult<Protein>(o => o.Accession == "PRO20").Cluster);

            // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
            // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(11, session.UniqueResult<Protein>(o => o.Accession == "PRO21").Cluster);
            Assert.AreEqual(11, session.UniqueResult<Protein>(o => o.Accession == "PRO22").Cluster);

            // 2 proteins to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
            // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(12, session.UniqueResult<Protein>(o => o.Accession == "PRO23").Cluster);
            Assert.AreEqual(12, session.UniqueResult<Protein>(o => o.Accession == "PRO24").Cluster);
            Assert.AreEqual(12, session.UniqueResult<Protein>(o => o.Accession == "PRO25").Cluster);

            // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
            // 2 proteins to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(13, session.UniqueResult<Protein>(o => o.Accession == "PRO26").Cluster);
            Assert.AreEqual(13, session.UniqueResult<Protein>(o => o.Accession == "PRO27").Cluster);
            Assert.AreEqual(13, session.UniqueResult<Protein>(o => o.Accession == "PRO28").Cluster);

            // 1 protein to 3 peptides, 2 of which are evidenced by ambiguous spectra
            // 1 protein to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide
            // 1 protein to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(14, session.UniqueResult<Protein>(o => o.Accession == "PRO29").Cluster);
            Assert.AreEqual(14, session.UniqueResult<Protein>(o => o.Accession == "PRO30").Cluster);
            Assert.AreEqual(14, session.UniqueResult<Protein>(o => o.Accession == "PRO31").Cluster);

            session.Close();
        }

        private ushort[] createProteinCoverageMask (string mask)
        {
            return new List<ushort>(mask.Select(o => Convert.ToUInt16(o - '0'))).ToArray();
        }

        [TestMethod]
        public void TestCoverage ()
        {
            string[] testProteinSequences = new string[]
            {
                "PEPTIDERPEPTIDEKELVISPEPTIDE",
                "DERPEPTIDEKELVISPEPTIDE",
                "TIDERELVISPEPTIDEKPEP",

                "ELVISKLIVESRTHANKYAVERYMUCH",
                "ELVISISRCKNRLLKING",
            };

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, true, false);
            var session = sessionFactory.OpenSession();

            TestModel.CreateTestProteins(session, testProteinSequences);

            int numSources = 2;
            int numAnalyses = 2;

            for (int source = 1; source <= numSources; ++source)
            for (int analysis = 1; analysis <= numAnalyses; ++analysis)
            {
                int scan = 0;

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    //               Group                        Score  Q   List of Peptide@Charge/ScoreDivider

                    // PEPTIDERPEPTIDEKELVISPEPTIDE
                    // ------- -------      ------- (PEPTIDE)
                    // --------                     (PEPTIDER)
                    //    -----                     (TIDER)
                    //         --------             (PEPTIDEK)
                    //      -----------             (DERPEPTIDEK)
                    // ----------------             (PEPTIDERPEPTIDEK)
                    // 3334455444444443000001111111 (23/28 covered)

                    // DERPEPTIDEKELVISPEPTIDE
                    //    -------      ------- (PEPTIDE)
                    //    --------             (PEPTIDEK)
                    // -----------             (DERPEPTIDEK)
                    // 11133333332000001111111 (18/23 covered)

                    // TIDERELVISPEPTIDEKPEP
                    //           -------     (PEPTIDE)
                    //           --------    (PEPTIDEK)
                    // -----                 (TIDER)
                    // 111110000022222221000 (13/21 covered)

                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "PEPTIDE@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "PEPTIDEK@1/1 TIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "DERPEPTIDEK@1/1 PEPTIDEKPEP@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "TIDER@1/1"),


                    // ELVISKLIVESRTHANKYAVERYMUCH
                    // ------------                (ELVISLIVESR)
                    // ------------                (E[N-1H-3]LVISLIVESR)
                    // 222222222222000000000000000 (12/27 covered)

                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "E[N-1H-3]LVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "ELVISKLIVESR@1/1"),


                    // ELVISISRCKNRLLKING
                    // --------           (ELVISISR)
                    // ------------       (ELVISISRCKNR)
                    //         ---------- (CKNRLLKING)
                    // 222222222222111111 (18/18 covered)

                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "ELVISISRCKNR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, 42, 0, "CKNRLLKING@1/1"),
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter()
            {
                MaximumQValue = 1,
                MinimumDistinctPeptidesPerProtein = 1,
                MinimumSpectraPerProtein = 1,
                MinimumAdditionalPeptidesPerProtein = 0
            };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            Protein pro;
            const double epsilon = 1e-9;

            // PEPTIDERPEPTIDEKELVISPEPTIDE
            // 3334455444444443000001111111 (23/28 covered)
            pro = session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKELVISPEPTIDE");
            Assert.AreEqual(100 * 23.0 / 28, pro.Coverage, epsilon);
            CollectionAssert.AreEqual(createProteinCoverageMask("3334455444444443000001111111"), pro.CoverageMask.ToArray());

            // DERPEPTIDEKELVISPEPTIDE
            // 11133333332000001111111 (18/23 covered)
            pro = session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKELVISPEPTIDE");
            Assert.AreEqual(100 * 18.0 / 23, pro.Coverage, epsilon);
            CollectionAssert.AreEqual(createProteinCoverageMask("11133333332000001111111"), pro.CoverageMask.ToArray());

            // TIDERELVISPEPTIDEKPEP
            // 111110000011111111000 (13/21 covered)
            pro = session.UniqueResult<Protein>(o => o.Sequence == "TIDERELVISPEPTIDEKPEP");
            Assert.AreEqual(100 * 13.0 / 21, pro.Coverage, epsilon);
            CollectionAssert.AreEqual(createProteinCoverageMask("111110000022222221000"), pro.CoverageMask.ToArray());

            // ELVISKLIVESRTHANKYAVERYMUCH
            // 222222222222000000000000000 (12/27 covered)
            pro = session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH");
            Assert.AreEqual(100 * 12.0 / 27, pro.Coverage, epsilon);
            // TODO: make modifications add depth to sequence coverage?
            //CollectionAssert.AreEqual(createProteinCoverageMask("222222222222000000000000000"), pro.CoverageMask.ToArray());

            // ELVISISRCKNRLLKING
            // 222222222222111111 (18/18 covered)
            pro = session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING");
            Assert.AreEqual(100 * 18 / 18, pro.Coverage, epsilon);
            CollectionAssert.AreEqual(createProteinCoverageMask("222222222222111111"), pro.CoverageMask.ToArray());
        }

        [TestMethod]
        public void TestFilteredQueries ()
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

                createSimpleProteinSequence("BBBBBDDDDD", 20),
                createSimpleProteinSequence("BBBBDDDD", 24),
                createSimpleProteinSequence("ZBBBBDDD", 24),
                createSimpleProteinSequence("YBBBDDD", 20),
                createSimpleProteinSequence("WBBDD", 21), // PRO20

                createSimpleProteinSequence("DDDDDFFFFF", 20),
                createSimpleProteinSequence("FFFFFHHHHH", 20),

                createSimpleProteinSequence("HHHHHKKKKK", 20),
                createSimpleProteinSequence("HHHHHKKKKK", 30),
                createSimpleProteinSequence("KKKKKMMMMM", 20), // PRO25

                createSimpleProteinSequence("LLLLLNNNNN", 20),
                createSimpleProteinSequence("NNNNNQQQQQ", 20),
                createSimpleProteinSequence("NNNNNQQQQQ", 30),

                createSimpleProteinSequence("MMMMMPPPPP", 20),
                createSimpleProteinSequence("PPPPPRRRRR", 20), // PRO30
                createSimpleProteinSequence("PPPPPRRRR", 24),

                createSimpleProteinSequence("QQQQSSSSUUUU", 24),
                createSimpleProteinSequence("SSSSUUUUYYYY", 24),

                createSimpleProteinSequence("RRRRTTTTWWWWZZZZ", 32),
                createSimpleProteinSequence("TTTTWWWWZZZZBBBB", 32), // PRO35
                createSimpleProteinSequence("RRRRTTTTZZZZBBBBTTTTWWWW", 24),

                createSimpleProteinSequence("AAAADDDDGGGGKKKK", 32),
                createSimpleProteinSequence("DDDDGGGGKKKKNNNN", 32),
                createSimpleProteinSequence("AAAADDDDKKKKNNNN", 32),

                createSimpleProteinSequence("BBBBEEEEHHHH", 24), // PRO40
                createSimpleProteinSequence("EEEEHHHHLLLL", 24),
                createSimpleProteinSequence("HHHHLLLLPPPP", 24),

                createSimpleProteinSequence("CCCCFFFFIIII", 24),
                createSimpleProteinSequence("FFFFIIIIMMMM", 24),
                createSimpleProteinSequence("IIIIMMMMQQQQ", 24), // PRO45

                createSimpleProteinSequence("NNNNRRRRUUUU", 24),
                createSimpleProteinSequence("RRRRUUUUZZZZ", 24),
                createSimpleProteinSequence("UUUUZZZZCCCC", 24),
            };

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, true, false);
            var session = sessionFactory.OpenSession();

            TestModel.CreateTestProteins(session, testProteinSequences);

            const int analysisCount = 2;
            const int sourceCount = 1;
            const int chargeCount = 2;
            int totalPSMs = 0;

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
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFF[C1H2]FFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFFFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                     // 2 proteins (PRO9,10) to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGGG@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGG@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                     // 2 proteins (PRO11,12) to 2 peptides to 2 spectra (each) = 2 additional peptides (ambiguous protein group)
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHH[O1]HHHHH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHH[O1]HHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("[O1]HHHHHHHHH@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("[O1]HHHHHHHHH@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                     // 1 protein (PRO13) to 2 peptides = 2 additional peptide
                     // 1 protein (PRO14) to 1 of the above peptides = 0 additional peptides (subsumed protein)
                     // 1 protein (PRO15) to the other above peptide = 0 additional peptides (subsumed protein)
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAABBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBCCCC@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                     // 1 protein (PRO16) to 5 peptides = 5 additional peptides
                     // 1 protein (PRO17) to 4 of the above peptides = 0 additional peptides
                     // 1 protein (PRO18) to 3 of the above peptides and 1 extra peptides = 1 additional peptides
                     // 1 protein (PRO19) to 2 of the above peptides and 2 extra peptides = 2 additional peptides
                     // 1 protein (PRO20) to 1 of the above peptides and 3 extra peptides = 3 additional peptides
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

                     // 1 protein (PRO21) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
                     // 1 protein (PRO22) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDFFFF@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DFFFFF@{0}/1 FFFFFH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                     // 2 proteins (PRO23,24) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
                     // 1 protein (PRO25) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHKKKKK@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHKKKK@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HKKKKK@{0}/1 KKKKKM@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("KKKKKMMMMM@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                     // 1 protein (PRO26) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
                     // 2 proteins (PRO27,28) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLLNNNNN@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLLNNNN@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LNNNNN@{0}/1 NNNNNQ@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("NNNNNQQQQQ@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                     // 1 protein (PRO29) to 3 peptides, 2 of which are evidenced by ambiguous spectra = 3 additional peptides
                     // 1 protein (PRO30) to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                     // 1 protein (PRO31) to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide = 1 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MMMMMPPPPP@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MPPPPP@{0}/1 PRRRRR@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("PPPPPRRRRR@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MPPPP@{0}/1 PRRRRP@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("PPPPRRRRP@{0}/1  AAAAAAAAAA@{0}/8", charge)),

                     // PRO32 -> QQQQSSSS, SSSSUUUU = 2 additional peptides
                     // PRO33 -> UUUUYYYY, SSSSUUUU = 2 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("QQQQSSSS@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("SSSSUUUU@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("UUUUYYYY@{0}/1   AAAAAAAAAA@{0}/8", charge)),

                     // PRO34 -> RRRRTTTT, WWWWZZZZ, TTTTWWWW = 3 additional peptides
                     // PRO35 -> ZZZZBBBB, WWWWZZZZ, TTTTWWWW = 3 additional peptides
                     // PRO36 -> RRRRTTTT, ZZZZBBBB, TTTTWWWW = 3 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("RRRRTTTT@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("WWWWZZZZ@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("TTTTWWWW@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("ZZZZBBBB@{0}/1   BBBBBBBBBB@{0}/8", charge)),

                     // PRO37 -> AAAADDDD, DDDDGGGG, GGGGKKKK = 3 additional peptides
                     // PRO38 -> DDDDGGGG, GGGGKKKK, KKKKNNNN = 3 additional peptides
                     // PRO39 -> AAAADDDD, KKKKNNNN = 0 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAADDDD@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDGGGG@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGKKKK@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("KKKKNNNN@{0}/1   BBBBBBBBBB@{0}/8", charge)),

                     // PRO40 -> BBBBEEEE, EEEEHHHH = 2 additional peptides
                     // PRO41 -> EEEEHHHH, HHHHLLLL = 0 additional peptides
                     // PRO42 -> HHHHLLLL, LLLLPPPP = 2 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBEEEE@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("EEEEHHHH@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHLLLL@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("LLLLPPPP@{0}/1   BBBBBBBBBB@{0}/8", charge)),

                     // PRO43 -> CCCCFFFF, CFFFFI, FFFFIIII = 3 additional peptides
                     // PRO44 -> FFFFIIII, FIIIIM, IIIIMMMM = 1 additional peptides
                     // PRO45 -> IIIIMMMM, IMMMMQ, MMMMQQQQ = 3 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCFFFF@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CFFFFI@{0}/1     BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFIIII@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FIIIIM@{0}/1     BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("IIIIMMMM@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("IMMMMQ@{0}/1     BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("MMMMQQQQ@{0}/1   AAAAAAAAAA@{0}/8", charge)),

                     // PRO46 -> NNNNRRRR, RRRRUUUU, RRRRUUU = 3 additional peptides
                     // PRO47 -> RRRRUUUU, RRRRUUU, UUUUZZZZ = 0 additional peptides
                     // PRO48 -> UUUUZZZZ, UZZZZC, ZZZZCCCC = 3 additional peptides
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("NNNNRRRR@{0}/1   AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("RRRRUUUU@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("RRRRUUU@{0}/1    AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("UUUUZZZZ@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("UZZZZC@{0}/1     AAAAAAAAAA@{0}/8", charge)),
                     new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("ZZZZCCCC@{0}/1   BBBBBBBBBB@{0}/8", charge)),
                };

                totalPSMs += scan;
                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter()
                                 {
                                     MaximumQValue = 1,
                                     MinimumDistinctPeptidesPerProtein = 0,
                                     MinimumSpectraPerProtein = 0,
                                     MinimumAdditionalPeptidesPerProtein = 0
                                 };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            // test with default DistinctMatchFormat (charge state and modifications distinct)
            {
                var peptideQuery = session.CreateQuery(IDPicker.Forms.PeptideTableForm.AggregateRow.Selection + ", psm.Peptide, psm, psm.DistinctMatchKey " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToProtein) +
                                                       "GROUP BY psm.Peptide");

                var queryRows = peptideQuery.List<object[]>();
                var peptideRows = queryRows.Select(o => new IDPicker.Forms.PeptideTableForm.DistinctPeptideRow(o, dataFilter)).ToList();
                int row = 0;

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "AAAAAAAAAA"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "BBBBBBBBBB"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "CCCCCCCCCC"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "CCCCCCCCC"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "DDDDDDDDDD"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "DDDDDDDDD"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "EEEEEEEEEE"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "FFFFFFFFFF"), peptideRows[row].Peptide);
                Assert.AreEqual(4, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "GGGGGGGGGG"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "GGGGGGGGG"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHHHHHHH"), peptideRows[row].Peptide);
                Assert.AreEqual(4, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHHHHHH"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);
            }

            // test with charge state and analysis indistinct and modifications distinct
            {
                dataFilter.DistinctMatchFormat.IsChargeDistinct = false;
                dataFilter.DistinctMatchFormat.IsAnalysisDistinct = false;
                dataFilter.ApplyBasicFilters(session);

                var peptideQuery = session.CreateQuery(IDPicker.Forms.PeptideTableForm.AggregateRow.Selection + ", psm.Peptide, psm, psm.DistinctMatchKey " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToProtein) +
                                                       "GROUP BY psm.Peptide");

                var queryRows = peptideQuery.List<object[]>();
                var peptideRows = queryRows.Select(o => new IDPicker.Forms.PeptideTableForm.DistinctPeptideRow(o, dataFilter)).ToList();
                int row = 0;

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "AAAAAAAAAA"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "BBBBBBBBBB"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "CCCCCCCCCC"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "CCCCCCCCC"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "DDDDDDDDDD"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "DDDDDDDDD"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "EEEEEEEEEE"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "FFFFFFFFFF"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "GGGGGGGGGG"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "GGGGGGGGG"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHHHHHHH"), peptideRows[row].Peptide);
                Assert.AreEqual(2, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHHHHHH"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);
            }

            // test with charge state, analysis, and modifications indistinct
            {
                dataFilter.DistinctMatchFormat.IsChargeDistinct = false;
                dataFilter.DistinctMatchFormat.IsAnalysisDistinct = false;
                dataFilter.DistinctMatchFormat.AreModificationsDistinct = false;
                dataFilter.ApplyBasicFilters(session);

                var peptideQuery = session.CreateQuery(IDPicker.Forms.PeptideTableForm.AggregateRow.Selection + ", psm.Peptide, psm, psm.DistinctMatchKey " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToProtein) +
                                                       "GROUP BY psm.Peptide");

                var queryRows = peptideQuery.List<object[]>();
                var peptideRows = queryRows.Select(o => new IDPicker.Forms.PeptideTableForm.DistinctPeptideRow(o, dataFilter)).ToList();
                int row = 0;

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "AAAAAAAAAA"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "BBBBBBBBBB"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "CCCCCCCCCC"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "CCCCCCCCC"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "DDDDDDDDDD"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "DDDDDDDDD"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "EEEEEEEEEE"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "FFFFFFFFFF"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "GGGGGGGGGG"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "GGGGGGGGG"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(1, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHHHHHHH"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);

                Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "HHHHHHHHH"), peptideRows[row].Peptide);
                Assert.AreEqual(1, peptideRows[row].DistinctMatches);
                Assert.AreEqual(2, peptideRows[row++].Spectra);
            }

            session.Close();
        }
    }
}
