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

            for (int sourceId = 1; sourceId <= numSources; ++sourceId)
            for (int analysisId = 1; analysisId <= numAnalyses; ++analysisId)
            {
                int scan = 0;
                string source = "Source " + sourceId.ToString();
                string analysis = "Engine " + analysisId.ToString();

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

            for (int sourceId = 1; sourceId <= numSources; ++sourceId)
            for (int analysisId = 1; analysisId <= numAnalyses; ++analysisId)
            {
                int scan = 0;
                string source = "Source " + sourceId.ToString();
                string analysis = "Engine " + analysisId.ToString();

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
                createSimpleProteinSequence("A", 20),

                createSimpleProteinSequence("B", 20),

                createSimpleProteinSequence("C", 20),

                createSimpleProteinSequence("D", 20),

                createSimpleProteinSequence("E", 20),
                createSimpleProteinSequence("E", 21),

                createSimpleProteinSequence("F", 20),
                createSimpleProteinSequence("F", 21),

                createSimpleProteinSequence("G", 20),
                createSimpleProteinSequence("G", 21),
                
                createSimpleProteinSequence("H", 20),
                createSimpleProteinSequence("H", 21),
                
                createSimpleProteinSequence("I", 20),
                createSimpleProteinSequence("I", 21),
            };
            
            var session = sessionFactory.OpenSession();

            session.Transaction.Begin();
            TestModel.CreateTestProteins(session, testProteinSequences);

            const int analysisCount = 2;
            const int sourceCount = 1;
            const int chargeCount = 2;

            for (int analysisId = 1; analysisId <= analysisCount; ++analysisId)
            for (int sourceId = 1; sourceId <= sourceCount; ++sourceId)
            for (int charge = 1; charge <= chargeCount; ++charge)
            {
                string source = "Source " + sourceId.ToString();
                string analysis = "Engine " + analysisId.ToString();
                int scan = 0;

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    // Columns:     Group  Source Spectrum Analysis Score Q List of Peptide@Charge/ScoreDivider
                    
                    // 1 protein to 1 peptide to 1 spectrum = 1 additional peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("AAAAAAAAAA@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 1 peptide to 2 spectra = 1 additional peptide
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBBBBBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),

                    // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCCCCCCC@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("CCCCCCCCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 2 peptides to 2 spectra (each) = 2 additional peptides
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDDD@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDDD@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDD@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("DDDDDDDDD@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 1 peptide to 1 spectrum = 1 additional peptide (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("EEEEEEEEEE@{0}/1 AAAAAAAAAA@{0}/8", charge)),

                    // 2 proteins to 1 peptide to 2 spectra = 1 additional peptide (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("FFFFFFFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGGG@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("GGGGGGGGG@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 2 peptides to 2 spectra (each) = 2 additional peptides (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHHH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHH@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 1, String.Format("HHHHHHHHH@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptide
                    // 1 protein to 1 of the above peptides = 0 additional peptides (subset protein)
                    // 1 protein to the other above peptide = 0 additional peptides (subset protein)

                    // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptide
                    // 2 proteins to 1 of the above peptides = 0 additional peptides (subset ambiguous protein group)

                    // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptide (ambiguous protein group)
                    // 1 protein to 1 of the above peptides = 0 additional peptides (subset protein)
                    // 1 protein to the other above peptide = 0 additional peptides (subset protein)

                    // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
                    // 2 proteins to 1 of the above peptides = 0 additional peptides (subset ambiguous protein group)

                    // 1 protein to 3 peptides to 1 spectrum (each) = 3 additional peptides
                    // 1 protein to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides

                    // 1 protein to 3 peptides to 1 spectrum (each) = 3 additional peptides
                    // 2 proteins to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides (ambiguous protein group)

                    // 2 proteins to 3 peptides to 1 spectrum (each) = 3 additional peptides (ambiguous protein group)
                    // 1 protein to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides

                    // 2 proteins to 3 peptides to 1 spectrum (each) = 3 additional peptides (ambiguous protein group)
                    // 2 proteins to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides (ambiguous protein group)

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

            Map<long, long> additionalPeptidesByProteinId = dataFilter.calculateAdditionalPeptides(session);

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

            session.Transaction.Rollback();
            session.Close();
        }
    }
}
