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
        public void TestAggregation ()
        {
            var lhs = new DataFilter() { MaximumQValue = 1 };
            var rhs = new DataFilter() { Protein =  new List<Protein>()};
            rhs.Protein.Add(new Protein("foo", "bar"));
            var result = lhs + rhs;

            var rhs2 = new DataFilter() { Peptide = new List<Peptide>() };
            rhs2.Peptide.Add(new TestPeptide("FOO"));
            var result2 = result + rhs2;

            Assert.AreEqual(1, result.Protein.Count);
            Assert.AreEqual(1, result2.Protein.Count);
            Assert.AreEqual(1, result2.Peptide.Count);

            Assert.AreEqual("foo", result.Protein.FirstOrDefault().Description);
            Assert.AreEqual("foo", result2.Protein.FirstOrDefault().Description);
            Assert.AreEqual("FOO", result2.Peptide.FirstOrDefault().Sequence);
        }

        [TestMethod]
        [TestCategory("Model")]
        public void TestBasicFilters ()
        {
            string[] testProteinSequences = new string[]
            {
                "PEPTIDERPEPTIDEKPEPTIDE",
                "DERPEPTIDEKPEPTIDE",
                "TIDERPEPTIDEKPEP",

                "ELVISKLIVESRTHANKYAVERYMUCH",
                "ELVISISRCKNRLLKING",

                "THEQUICKBRWNFXJUMPSVERTHELAZYDG",
            };

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true });
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
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "PEPTIDEK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "PEPTIDEK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "PEPTIDEK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "DERPEPTIDEK@1/1 PEPTIDER@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "DERPEPTIDEK@1/1 PEPTIDER@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "DERPEPTIDEK@1/1 PEPTIDER@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "DERPEPTIDEK@1/1 PEPTIDER@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "TIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "TIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "TIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 2, "TIDER@1/1"),

                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDEK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "DERPEPTIDEK@1/1 PEPTIDER@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "TIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 1, "PEPTIDER@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@1/1 PEPTIDEK@1/2"),

                    // not enough distinct peptides for ELVISKLIVESRTHANKYAVERYMUCH
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"),

                    // not enough spectra for ELVISISRCKNRLLKING
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"),

                    // not enough spectra for distinct peptide THEQUICK
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "THEQUICK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "THEQUICK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "THEQU[H3C2N1O1]ICK@1/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "THEQU[H3C2N1O1]ICK@1/1"),

                    // not enough spectra for distinct matches THEQUICK@2 and THEQUICK@3
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "THEQUICK@2/1"),
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "THEQUICK@3/1"),
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            // Spectral counts must be multiplied by numSources.

            // Distinct Matches:
            // PEPTIDEK(+1): 1 spectrum
            // DERPEPTIDEK(+1): 1 spectrum
            // TIDER(+1): 1 spectrum
            // PEPTIDER(+1): 5 spectra
            // PEPTIDERPEPTIDEK(+1): 5 spectra
            // ELVISKLIVESR(+1): 6 spectra
            // ELVISISR(+1): 5 spectra
            // CKNRLLKING(+1): 5 spectra
            // THEQUICK(+1): 2 spectra
            // THEQUICK(+2): 1 spectrum
            // THEQUICK(+3): 1 spectrum
            // THEQU[H3C2N1O1]ICK(+1): 2 spectra

            // Proteins:
            // PEPTIDERPEPTIDEKPEPTIDE: 5 distinct peptides over 13 (passing) spectra
            // DERPEPTIDEKPEPTIDE: 2 distinct peptides, 2 spectra
            // TIDERPEPTIDEKPEP: 3 distinct peptides, 3 spectra
            // ELVISKLIVESRTHANKYAVERYMUCH: 1 distinct peptide, 6 spectra
            // ELVISISRCKNRLLKING: 2 distinct peptides, 10 spectra
            // THEQUICKBRWNFXJUMPSVERTHELAZYDG: 1 distinct peptide, 6 spectra

            var dataFilter = new DataFilter()
            {
                MaximumQValue = 1,
                MinimumDistinctPeptides = 1,
                MinimumSpectra = 1,
                MinimumAdditionalPeptides = 0
            };
            dataFilter.ApplyBasicFilters(session);
            
            // clear session so objects are loaded from database
            session.Clear();

            for (int sourceId = 1; sourceId <= numSources; ++sourceId)
            for (int analysisId = 1; analysisId <= numAnalyses; ++analysisId)
            {
                string source = "Source " + sourceId.ToString();
                string analysis = "Engine " + analysisId.ToString();
                var sourceAnalysisPSMs = session.Query<PeptideSpectrumMatch>().Where(o => o.Analysis.Software.Name == analysis && o.Spectrum.Source.Name == source);

                // test that PSMs with QValue > MaximumQValue are filtered out
                Assert.AreEqual(0, sourceAnalysisPSMs.Count(o => o.QValue > 1));
            }

            // test that non-rank-1 PSMs are filtered out
            Assert.AreEqual(0, session.Query<PeptideSpectrumMatch>().Where(o => o.Rank > 1).Count());

            // test that nothing else is filtered out
            Assert.AreEqual(6, session.Query<Protein>().Count());
            Assert.AreEqual(9, session.Query<Peptide>().Count());
            Assert.AreEqual(35 * (numSources + numAnalyses), session.Query<PeptideSpectrumMatch>().Count());

            dataFilter.MinimumDistinctPeptides = 3;
            dataFilter.MinimumSpectra = 1;
            dataFilter.ApplyBasicFilters(session);
            session.Clear();

            // test that proteins without at least MinimumPeptidesPerProtein peptides are filtered out
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "THEQUICKBRWNFXJUMPSVERTHELAZYDG"));

            // test that protein filters cascade to peptide instances
            Assert.AreEqual(5, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));
            Assert.AreEqual(3, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "DERPEPTIDEKPEPTIDE"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "ELVISISRCKNRLLKING"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "THEQUICKBRWNFXJUMPSVERTHELAZYDG"));

            // test that protein filters cascade to peptides
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDEK"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "DERPEPTIDEK"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "TIDER"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDER"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDERPEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISKLIVESR"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISISR"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "CKNRLLKING"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "THEQUICK"));

            // test that protein filters cascade to PSMs 
            Assert.AreEqual(13 * (numSources + numAnalyses), session.Query<PeptideSpectrumMatch>().Count());

            dataFilter.MinimumDistinctPeptides = 1;
            dataFilter.MinimumSpectra = 4 * numSources;
            dataFilter.ApplyBasicFilters(session);
            session.Clear();

            // test that proteins without at least MinimumSpectraPerProtein spectra are filtered out
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "THEQUICKBRWNFXJUMPSVERTHELAZYDG"));

            dataFilter.MinimumDistinctPeptides = 1;
            dataFilter.MinimumSpectra = 1;
            dataFilter.MinimumSpectraPerDistinctMatch = 2 * numSources;
            dataFilter.ApplyBasicFilters(session);
            session.Clear();

            // test that distinct matches without at least MinimumSpectraPerDistinctMatch spectra are filtered out
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDER"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDERPEPTIDEK"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISKLIVESR"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISISR"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "CKNRLLKING"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "THEQUICK"));
            Assert.AreEqual(4 * (numSources + numAnalyses), session.Query<PeptideSpectrumMatch>().Count(o => o.Peptide.Sequence == "THEQUICK" && o.Charge == 1));
            Assert.AreEqual(0, session.Query<PeptideSpectrumMatch>().Count(o => o.Charge == 2));
            Assert.AreEqual(0, session.Query<PeptideSpectrumMatch>().Count(o => o.Charge == 3));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "DERPEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "TIDER"));

            // test that peptide filters cascade to proteins
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "THEQUICKBRWNFXJUMPSVERTHELAZYDG"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE"));

            dataFilter.MinimumSpectraPerDistinctMatch = 1;
            dataFilter.MinimumSpectraPerDistinctPeptide = 6 * numSources;
            dataFilter.ApplyBasicFilters(session);
            session.Clear();

            // test that distinct peptides without at least MinimumSpectraPerDistinctPeptide spectra are filtered out
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISKLIVESR"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "THEQUICK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDER"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDERPEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISISR"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "CKNRLLKING"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "DERPEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "TIDER"));

            // test that peptide filters cascade to proteins
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "THEQUICKBRWNFXJUMPSVERTHELAZYDG"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING"));

            // test that peptide filters cascade to peptide instances
            Assert.AreEqual(1, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.AreEqual(1, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "THEQUICKBRWNFXJUMPSVERTHELAZYDG"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "DERPEPTIDEKPEPTIDE"));
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count(o => o.Protein.Sequence == "ELVISISRCKNRLLKING"));

            dataFilter.MinimumSpectraPerDistinctMatch = 1;
            dataFilter.MinimumSpectraPerDistinctPeptide = 1;
            dataFilter.MaximumProteinGroupsPerPeptide = 1;
            dataFilter.ApplyBasicFilters(session);
            session.Clear();

            // test that distinct peptides linking to more than MaximumProteinGroupsPerPeptide protein groups are filtered out
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISKLIVESR"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "THEQUICK"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "ELVISISR"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "CKNRLLKING"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDER"));
            Assert.IsNotNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDERPEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "DERPEPTIDEK"));
            Assert.IsNull(session.UniqueResult<Peptide>(o => o.Sequence == "TIDER"));

            // test that MaximumProteinGroupsPerPeptide filter cascades to proteins
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISKLIVESRTHANKYAVERYMUCH"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "ELVISISRCKNRLLKING"));
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Sequence == "THEQUICKBRWNFXJUMPSVERTHELAZYDG"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "TIDERPEPTIDEKPEP"));
            Assert.IsNull(session.UniqueResult<Protein>(o => o.Sequence == "DERPEPTIDEKPEPTIDE"));

            session.Close();
        }

        [TestMethod]
        [TestCategory("Model")]
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
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true });
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
                MinimumDistinctPeptides = 2,
                MinimumSpectra = 3 * numSources,
                MinimumAdditionalPeptides = 0
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

        [TestMethod]
        [TestCategory("Model")]
        public void TestAminoAcidOffsets ()
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
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true });
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
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDER@1/1"), // [0,7]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDEK@1/1"), // [8,15] [5,12]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@2/1"), // [0,15]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "PEPTIDERPEPTIDEK@3/1"), // [0,15]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "DERPEPTIDEK@1/1"), // [0,10] [5,14]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "TIDER@1/1"), // [0,4] [3,7]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "TIDEK@2/1"), // [6,10] [8,12] [10,14]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "TIDEK@3/1"), // [6,10] [8,12] [10,14]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVIS@1/1"), // [0,4]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISK@1/1"), // [0,5]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISKLIVESR@1/1"), // [0,11]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "LIVESR@1/1"), // [5,11]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "ELVISISR@1/1"), // [0,7]
                    new SpectrumTuple("/", source, ++scan, analysis, scan*2, 0, "CKNRLLKING@1/1"), // [8,18]
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter() { MinimumAdditionalPeptides = 0 };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            // get all peptides at the protein N-terminus
            dataFilter.AminoAcidOffset = new List<int> { 0 };
            Assert.AreEqual(8, Convert.ToInt32(session.CreateQuery("SELECT COUNT(DISTINCT pi.Peptide) " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein)).UniqueResult()));

            // get all peptides of PEPTIDERPEPTIDEKPEPTIDE at the N-terminus
            dataFilter.Protein = new List<Protein> { session.UniqueResult<Protein>(o => o.Sequence == "PEPTIDERPEPTIDEKPEPTIDE") };
            Assert.AreEqual(2, Convert.ToInt32(session.CreateQuery("SELECT COUNT(DISTINCT pi.Peptide) " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein)).UniqueResult()));

            // get all peptides of PEPTIDERPEPTIDEKPEPTIDE that cover offset 8 (PEPTIDERPEPTIDEK, DERPEPTIDEK, PEPTIDEK)
            dataFilter.AminoAcidOffset = new List<int> { 8 };
            Assert.AreEqual(3, Convert.ToInt32(session.CreateQuery("SELECT COUNT(DISTINCT pi.Peptide) " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein)).UniqueResult()));

            // get all peptides of PEPTIDERPEPTIDEKPEPTIDE that cover offset 3 or 8 (PEPTIDER, PEPTIDERPEPTIDEK, DERPEPTIDEK, TIDER, PEPTIDEK)
            dataFilter.AminoAcidOffset = new List<int> { 3, 8 };
            Assert.AreEqual(5, Convert.ToInt32(session.CreateQuery("SELECT COUNT(DISTINCT pi.Peptide) " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein)).UniqueResult()));

            // get all peptides of PEPTIDERPEPTIDEKPEPTIDE at the C-terminus
            dataFilter.AminoAcidOffset = new List<int> { Int32.MaxValue };
            Assert.AreEqual(0, Convert.ToInt32(session.CreateQuery("SELECT COUNT(DISTINCT pi.Peptide) " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein)).UniqueResult()));

            // get all peptides at the protein C-terminus
            dataFilter.Protein = null;
            Assert.AreEqual(1, Convert.ToInt32(session.CreateQuery("SELECT COUNT(DISTINCT pi.Peptide) " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein)).UniqueResult()));

            // get all peptides at either protein terminus
            dataFilter.AminoAcidOffset = new List<int> { 0, Int32.MaxValue };
            Assert.AreEqual(9, Convert.ToInt32(session.CreateQuery("SELECT COUNT(DISTINCT pi.Peptide) " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein)).UniqueResult()));

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
        [TestCategory("Model")]
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
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true });
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

            var dataFilter = new DataFilter()
            {
                MaximumQValue = 1,
                MinimumDistinctPeptides = 0,
                MinimumSpectra = 0,
                MinimumAdditionalPeptides = 1
            };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            var additionalPeptidesByProteinId = new Map<long, int>();
            foreach (object[] row in session.CreateSQLQuery("SELECT ProteinId, AdditionalMatches FROM AdditionalMatches").List<object[]>())
                additionalPeptidesByProteinId[Convert.ToInt64(row[0])] = Convert.ToInt32(row[1]);

            // 1 protein to 1 peptide to 1 spectrum = 1 additional peptide
            Assert.AreEqual(1, additionalPeptidesByProteinId[1]);

            // 1 protein to 1 peptide to 2 spectra = 1 additional peptide
            Assert.AreEqual(1, additionalPeptidesByProteinId[2]);

            // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[3]);

            // 1 protein to 2 peptides to 2 spectra (each) = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[4]);

            // 2 proteins to 1 peptide to 1 spectrum = 1 additional peptide (ambiguous protein group)
            Assert.AreEqual(1, additionalPeptidesByProteinId[5]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[6]);

            // 2 proteins to 1 peptide to 2 spectra = 1 additional peptide (ambiguous protein group)
            Assert.AreEqual(1, additionalPeptidesByProteinId[7]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[8]);

            // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[9]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[10]);

            // 2 proteins to 2 peptides to 2 spectra (each) = 2 additional peptides (ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[11]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[12]);

            // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptide
            // 1 protein to 1 of the above peptides = 0 additional peptides (subsumed protein)
            // 1 protein to the other above peptide = 0 additional peptides (subsumed protein)
            Assert.AreEqual(2, additionalPeptidesByProteinId[13]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[14]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[15]);

            // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptide
            // 2 proteins to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[16]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[17]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[18]);

            // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptide (ambiguous protein group)
            // 1 protein to 1 of the above peptides = 0 additional peptides (subsumed protein)
            // 1 protein to the other above peptide = 0 additional peptides (subsumed protein)
            Assert.AreEqual(2, additionalPeptidesByProteinId[19]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[20]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[21]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[22]);

            // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)
            // 2 proteins to 1 of the above peptides = 0 additional peptides (subsumed ambiguous protein group)
            Assert.AreEqual(2, additionalPeptidesByProteinId[23]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[24]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[25]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[26]);

            // 1 protein to 3 peptides to 1 spectrum (each) = 3 additional peptides
            // 1 protein to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[27]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[28]);

            // 1 protein to 3 peptides to 1 spectrum (each) = 3 additional peptides
            // 2 proteins to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides (ambiguous protein group)
            Assert.AreEqual(3, additionalPeptidesByProteinId[29]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[30]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[31]);

            // 2 proteins to 3 peptides to 1 spectrum (each) = 3 additional peptides (ambiguous protein group)
            // 1 protein to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[32]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[33]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[34]);

            // 2 proteins to 3 peptides to 1 spectrum (each) = 3 additional peptides (ambiguous protein group)
            // 2 proteins to 1 of the above peptides and 1 extra peptide to 1 spectrum = 1 additional peptides (ambiguous protein group)
            Assert.AreEqual(3, additionalPeptidesByProteinId[35]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[36]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[37]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[38]);

            // 1 protein (PRO39) to 5 peptides = 5 additional peptides
            // 1 protein (PRO40) to 4 of the above peptides = 0 additional peptides
            // 1 protein (PRO41) to 3 of the above peptides and 1 extra peptides = 1 additional peptides
            // 1 protein (PRO42) to 2 of the above peptides and 2 extra peptides = 2 additional peptides
            // 1 protein (PRO43) to 1 of the above peptides and 3 extra peptides = 3 additional peptides
            Assert.AreEqual(5, additionalPeptidesByProteinId[39]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[40]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[41]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[42]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[43]);

            // 1 protein (PRO44) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
            // 1 protein (PRO45) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[44]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[45]);
                    
            // 2 proteins (PRO46,47) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
            // 1 protein (PRO48) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[46]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[47]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[48]);
            
            // 1 protein (PRO49) to 3 peptides, 1 of which is evidenced by an ambiguous spectrum = 3 additional peptides
            // 2 proteins (PRO50,51) to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[49]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[50]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[51]);

            // 1 protein (PRO52) to 3 peptides, 2 of which are evidenced by ambiguous spectra = 3 additional peptides
            // 1 protein (PRO53) to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            // 1 protein (PRO54) to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide = 1 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[52]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[53]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[54]);
            
            // PRO55 -> QQQQSSSS, SSSSUUUU = 2 additional peptides
            // PRO56 -> UUUUYYYY, SSSSUUUU = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[55]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[56]);

            // PRO57 -> RRRRTTTT, WWWWZZZZ, TTTTWWWW = 3 additional peptides
            // PRO58 -> QQQQKKKK, WWWWZZZZ, TTTTWWWW = 3 additional peptides
            // PRO59 -> RRRRTTTT, TTTTZZZZ, TTTTWWWW = 3 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[57]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[58]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[59]);

            // PRO60 -> AAAADDDD, DDDDGGGG, GGGGKKKK = 3 additional peptides
            // PRO61 -> DDDDGGGG, GGGGKKKK, KKKKNNNN = 3 additional peptides
            // PRO62 -> AAAADDDD, KKKKNNNN = 0 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[60]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[61]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[62]);

            // PRO63 -> BBBBEEEE, EEEEHHHH = 2 additional peptides
            // PRO64 -> EEEEHHHH, HHHHLLLL = 0 additional peptides
            // PRO65 -> HHHHLLLL, LLLLPPPP = 2 additional peptides
            Assert.AreEqual(2, additionalPeptidesByProteinId[63]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[64]);
            Assert.AreEqual(2, additionalPeptidesByProteinId[65]);

            // PRO66 -> CCCCFFFF, CFFFFI, FFFFIIII = 3 additional peptides
            // PRO67 -> FFFFIIII, FIIIIM, IIIIMMMM = 1 additional peptides
            // PRO68 -> IIIIMMMM, IMMMMQ, MMMMQQQQ = 3 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[66]);
            Assert.AreEqual(1, additionalPeptidesByProteinId[67]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[68]);

            // PRO69 -> NNNNRRRR, RRRRUUUU, RRRRUUU = 3 additional peptides
            // PRO70 -> RRRRUUUU, RRRRUUU, UUUUZZZZ = 0 additional peptides
            // PRO71 -> UUUUZZZZ, UZZZZC, ZZZZCCCC = 3 additional peptides
            Assert.AreEqual(3, additionalPeptidesByProteinId[69]);
            Assert.AreEqual(0, additionalPeptidesByProteinId[70]);
            Assert.AreEqual(3, additionalPeptidesByProteinId[71]);

            // test that the MinimumAdditionalPeptidesPerProtein filter is applied correctly;
            // proteins filtered out by it should cascade to PeptideInstances, Peptides, and PSMs

            dataFilter.MinimumAdditionalPeptides = 1;
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

            dataFilter.MinimumAdditionalPeptides = 2;
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

            dataFilter.MinimumAdditionalPeptides = 3;
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

            dataFilter.MinimumAdditionalPeptides = 4;
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

            dataFilter.MinimumAdditionalPeptides = 5;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(1, session.Query<Protein>().Count());
            Assert.IsNotNull(session.UniqueResult<Protein>(o => o.Accession == "PRO39"));
            Assert.IsNull(session.UniqueResult<PeptideInstance>(o => o.Protein.Accession != "PRO39"));

            dataFilter.MinimumAdditionalPeptides = 6;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(0, session.Query<Protein>().Count());
            Assert.AreEqual(0, session.Query<PeptideInstance>().Count());
            Assert.AreEqual(0, session.Query<Peptide>().Count());
            Assert.AreEqual(0, session.Query<PeptideSpectrumMatch>().Count());

            session.Close();
        }

        [TestMethod]
        [TestCategory("Model")]
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
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true });
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
                MinimumDistinctPeptides = 0,
                MinimumSpectra = 0,
                MinimumAdditionalPeptides = 0
            };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            // 1 protein to 1 peptide to 1 spectrum
            Assert.AreEqual(1, session.UniqueResult<Protein>(o => o.Accession == "PRO1").Cluster);

            // 1 protein to 1 peptide to 2 spectra
            Assert.AreEqual(6, session.UniqueResult<Protein>(o => o.Accession == "PRO2").Cluster);

            // 1 protein to 2 peptides to 1 spectrum (each)
            Assert.AreEqual(10, session.UniqueResult<Protein>(o => o.Accession == "PRO3").Cluster);

            // 1 protein to 2 peptides to 2 spectra (each)
            Assert.AreEqual(12, session.UniqueResult<Protein>(o => o.Accession == "PRO4").Cluster);

            // 2 proteins to 1 peptide to 1 spectrum (ambiguous protein group)
            Assert.AreEqual(13, session.UniqueResult<Protein>(o => o.Accession == "PRO5").Cluster);
            Assert.AreEqual(13, session.UniqueResult<Protein>(o => o.Accession == "PRO6").Cluster);

            // 2 proteins to 1 peptide to 2 spectra (ambiguous protein group)
            Assert.AreEqual(14, session.UniqueResult<Protein>(o => o.Accession == "PRO7").Cluster);
            Assert.AreEqual(14, session.UniqueResult<Protein>(o => o.Accession == "PRO8").Cluster);

            // 2 proteins to 2 peptides to 1 spectrum (each) (ambiguous protein group)
            Assert.AreEqual(2, session.UniqueResult<Protein>(o => o.Accession == "PRO9").Cluster);
            Assert.AreEqual(2, session.UniqueResult<Protein>(o => o.Accession == "PRO10").Cluster);

            // 2 proteins to 2 peptides to 2 spectra (each) (ambiguous protein group)
            Assert.AreEqual(3, session.UniqueResult<Protein>(o => o.Accession == "PRO11").Cluster);
            Assert.AreEqual(3, session.UniqueResult<Protein>(o => o.Accession == "PRO12").Cluster);

            // 1 protein to 2 peptides to 1 spectrum (each)
            // 1 protein to 1 of the above peptides (subsumed protein)
            // 1 protein to the other above peptide (subsumed protein)
            Assert.AreEqual(4, session.UniqueResult<Protein>(o => o.Accession == "PRO13").Cluster);
            Assert.AreEqual(4, session.UniqueResult<Protein>(o => o.Accession == "PRO14").Cluster);
            Assert.AreEqual(4, session.UniqueResult<Protein>(o => o.Accession == "PRO15").Cluster);

            // 1 protein to 5 peptides
            // 1 protein to 4 of the above peptides
            // 1 protein to 3 of the above peptides and 1 extra peptides
            // 1 protein to 2 of the above peptides and 2 extra peptides
            // 1 protein to 1 of the above peptides and 3 extra peptides
            Assert.AreEqual(5, session.UniqueResult<Protein>(o => o.Accession == "PRO16").Cluster);
            Assert.AreEqual(5, session.UniqueResult<Protein>(o => o.Accession == "PRO17").Cluster);
            Assert.AreEqual(5, session.UniqueResult<Protein>(o => o.Accession == "PRO18").Cluster);
            Assert.AreEqual(5, session.UniqueResult<Protein>(o => o.Accession == "PRO19").Cluster);
            Assert.AreEqual(5, session.UniqueResult<Protein>(o => o.Accession == "PRO20").Cluster);

            // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
            // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(7, session.UniqueResult<Protein>(o => o.Accession == "PRO21").Cluster);
            Assert.AreEqual(7, session.UniqueResult<Protein>(o => o.Accession == "PRO22").Cluster);

            // 2 proteins to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
            // 1 protein to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(8, session.UniqueResult<Protein>(o => o.Accession == "PRO23").Cluster);
            Assert.AreEqual(8, session.UniqueResult<Protein>(o => o.Accession == "PRO24").Cluster);
            Assert.AreEqual(8, session.UniqueResult<Protein>(o => o.Accession == "PRO25").Cluster);

            // 1 protein to 3 peptides, 1 of which is evidenced by an ambiguous spectrum
            // 2 proteins to 1 peptide evidenced by the ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(9, session.UniqueResult<Protein>(o => o.Accession == "PRO26").Cluster);
            Assert.AreEqual(9, session.UniqueResult<Protein>(o => o.Accession == "PRO27").Cluster);
            Assert.AreEqual(9, session.UniqueResult<Protein>(o => o.Accession == "PRO28").Cluster);

            // 1 protein to 3 peptides, 2 of which are evidenced by ambiguous spectra
            // 1 protein to 1 peptide evidenced by an ambiguous spectrum above and 1 extra peptide
            // 1 protein to 1 peptide evidenced by the other ambiguous spectrum above and 1 extra peptide
            Assert.AreEqual(11, session.UniqueResult<Protein>(o => o.Accession == "PRO29").Cluster);
            Assert.AreEqual(11, session.UniqueResult<Protein>(o => o.Accession == "PRO30").Cluster);
            Assert.AreEqual(11, session.UniqueResult<Protein>(o => o.Accession == "PRO31").Cluster);

            session.Close();
        }

        private ushort[] createProteinCoverageMask (string mask)
        {
            return new List<ushort>(mask.Select(o => Convert.ToUInt16(o - '0'))).ToArray();
        }

        [TestMethod]
        [TestCategory("Model")]
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
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true });
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
                MinimumDistinctPeptides = 1,
                MinimumSpectra = 1,
                MinimumAdditionalPeptides = 0
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
        [TestCategory("Model")]
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
            var log = new StringWriter();
            Console.SetOut(log);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true, WriteSqlToConsoleOut = true});
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
                };

                totalPSMs += scan;
                TestModel.CreateTestData(session, testPsmSummary);
            }

            var dataFilter = new DataFilter()
                                 {
                                     MaximumQValue = 1,
                                     MinimumDistinctPeptides = 1,
                                     MinimumSpectra = 1,
                                     MinimumAdditionalPeptides = 1
                                 };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            // test with default DistinctMatchFormat (charge state and modifications distinct)
            {
                var peptideRows = IDPicker.Forms.PeptideTableForm.DistinctPeptideRow.GetRows(session, dataFilter);
                int row = 0;
                Assert.AreEqual(12, peptideRows.Count);

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

                var peptideRows = IDPicker.Forms.PeptideTableForm.DistinctPeptideRow.GetRows(session, dataFilter);
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

                var peptideRows = IDPicker.Forms.PeptideTableForm.DistinctPeptideRow.GetRows(session, dataFilter);
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

        
        [TestMethod]
        [TestCategory("Model")]
        public void TestCropAssembly ()
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
            };

            string idpDbName = System.Reflection.MethodInfo.GetCurrentMethod().Name + ".idpDB";
            File.Delete(idpDbName);
            sessionFactory = SessionFactoryFactory.CreateSessionFactory(idpDbName, new SessionFactoryConfig { CreateSchema = true });
            var session = sessionFactory.OpenSession();

            TestModel.CreateTestProteins(session, testProteinSequences);

            const int analysisCount = 1;
            const int sourceCount = 1;
            const int chargeCount = 1;

            for (int analysis = 1; analysis <= analysisCount; ++analysis)
            for (int source = 1; source <= sourceCount; ++source)
            for (int charge = 1; charge <= chargeCount; ++charge)
            {
                int scan = 0;

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    // Columns:     Group  Source Spectrum Analysis Score Q List of Peptide@Charge/ScoreDivider
                    
                    // 1 protein (PRO1) to 1 peptide to 1 spectrum @ 1% FDR
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("AAAAAAAAAA@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO2) to 1 peptide to 2 spectra @ 1% FDR, 3 PSMS @ 2.4% FDR
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("BBBBBBBBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.024, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.024, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.024, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),

                    // 1 protein (PRO3) to 2 peptides to 1 spectrum (each) @ 1% FDR, 1 PSM @ 4.2% FDR, 1 PSM @ 2.4% FDR
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("CCCCCCCCCC@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("CCCCCCCCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.024, String.Format("CCCCCCCCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("CCCCCCCCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein (PRO4) to 2 peptides to 2 spectra (each) @ 2.4% FDR, 2 PSMs @ 4.2% FDR
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.024, String.Format("DDDDDDDDDD@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.024, String.Format("DDDDDDDDDD@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("DDDDDDDDD@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("DDDDDDDDD@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO5,6) to 1 peptide to 1 spectrum @ 1% FDR (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("EEEEEEEEEE@{0}/1 AAAAAAAAAA@{0}/8", charge)),

                    // 2 proteins (PRO7,8) to 1 peptide to 2 spectra @ 4.2% FDR (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("FFFFFFFFFF@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("FFFFFFFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO9,10) to 2 peptides to 1 spectrum (each) @ 4.2% FDR (ambiguous protein group)
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("GGGGGGGGGG@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("GGGGGGGGG@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins (PRO11,12) to 2 peptides to 2 spectra (each) @ 1% FDR (ambiguous protein group), 2 PSMs @ 4.2% FDR
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("HHHHHHHHHH@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("HHHHHHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("HHHHHHHHH@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.001, String.Format("HHHHHHHHH@{0}/1  BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("HHHHHHHHHH@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", source, ++scan, analysis, 1, 0.042, String.Format("HHHHHHHHH@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                };

                TestModel.CreateTestData(session, testPsmSummary);
            }

            // first filter at 5% FDR: all PSMs should be included
            var dataFilter = new DataFilter()
            {
                MaximumQValue = 0.05,
                MinimumDistinctPeptides = 1,
                MinimumSpectra = 1,
                MinimumAdditionalPeptides = 0
            };
            dataFilter.ApplyBasicFilters(session);

            // clear session so objects are loaded from database
            session.Clear();

            Assert.AreEqual(25, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(12, session.Query<Protein>().Count());


            // filter at 3% FDR: PSMs at 4.2% should be excluded
            dataFilter.MaximumQValue = 0.03;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(16, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(8, session.Query<Protein>().Count());


            // filter at 1% FDR: PSMs at 2.4% and 4.2% should be excluded
            dataFilter.MaximumQValue = 0.01;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(10, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(7, session.Query<Protein>().Count());


            // back to 3%, then crop
            dataFilter.MaximumQValue = 0.03;
            dataFilter.ApplyBasicFilters(session);
            dataFilter.CropAssembly(session);

            Assert.AreEqual(16, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(8, session.Query<Protein>().Count());


            // back at 5%, PSMs at 2.4% and 4.2% FDR for the 8 remaining proteins should be recovered
            dataFilter.MaximumQValue = 0.05;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(21, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(8, session.Query<Protein>().Count());


            // back to 1%, then crop
            dataFilter.MaximumQValue = 0.01;
            dataFilter.ApplyBasicFilters(session);
            dataFilter.CropAssembly(session);

            Assert.AreEqual(10, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(7, session.Query<Protein>().Count());


            // back at 5%, PSMs at 2.4% and 4.2% FDR for the 7 remaining proteins should be recovered
            dataFilter.MaximumQValue = 0.05;
            dataFilter.ApplyBasicFilters(session);

            Assert.AreEqual(17, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(7, session.Query<Protein>().Count());
        }
    }
}
