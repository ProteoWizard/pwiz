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

using pwiz.CLI.chemistry;
using proteome = pwiz.CLI.proteome;
using msdata = pwiz.CLI.msdata;
using PwizPeptide = pwiz.CLI.proteome.Peptide;
using PwizMod = pwiz.CLI.proteome.Modification;
using ModList = pwiz.CLI.proteome.ModificationList;
using ModMap = pwiz.CLI.proteome.ModificationMap;
using ModParsing = pwiz.CLI.proteome.ModificationParsing;
using ModDelimiter = pwiz.CLI.proteome.ModificationDelimiter;

namespace Test
{
    using TestProtein = Protein_Accessor;
    using TestPeptide = Peptide_Accessor;
    using TestPI = PeptideInstance_Accessor;
    using TestPSM = PeptideSpectrumMatch_Accessor;
    using TestMod = Modification_Accessor;
    using TestPM = PeptideModification_Accessor;
    using TestSpectrum = Spectrum_Accessor;
    using TestSource = SpectrumSource_Accessor;
    using TestGroup = SpectrumSourceGroup_Accessor;
    using TestGL = SpectrumSourceGroupLink_Accessor;

    /// <summary>
    /// Summary description for TestModel
    /// </summary>
    [TestClass]
    public class TestModel
    {
        #region Public static methods for easily populating an IDPicker session
        public static void CreateTestProteins(NHibernate.ISession session, IList<string> testProteinSequences)
        {
            for (int i = 0; i < testProteinSequences.Count; ++i)
            {
                int id = i + 1;

                session.Save(new TestProtein()
                {
                    Id = id,
                    Accession = "PRO" + id.ToString(),
                    Description = "Protein " + id.ToString(),
                    Sequence = testProteinSequences[i],
                    length = testProteinSequences[i].Length
                }.Target as Protein);
            }
        }

        public struct PeptideTuple
        {
            public string Sequence { get; set; }
            public int Charge { get; set; }
            public int ScoreDivider { get; set; }
        }

        public class SpectrumTuple
        {
            public SpectrumTuple (string group, string source, int spectrum, string analysis, int? score, double qvalue, string peptideTuples)
            {
                Group = group;
                Source = source;
                Spectrum = spectrum;
                Analysis = analysis;
                Score = score;
                QValue = qvalue;
                PeptideTuples = peptideTuples;
            }

            public string Group { get; set; }
            public string Source { get; set; }
            public int Spectrum { get; set; }
            public string Analysis { get; set; }
            public int? Score { get; set; }
            public double QValue { get; set; }
            public string PeptideTuples { get; set; }
        }

        public static void CreateTestData (NHibernate.ISession session, IList<SpectrumTuple> testPsmSummary)
        {
            foreach (SpectrumTuple row in testPsmSummary)
            {
                string groupName = row.Group;
                string sourceName = row.Source;
                int spectrumId = row.Spectrum;
                string analysisId = row.Analysis;
                string peptideTuples = row.PeptideTuples;

                SpectrumSourceGroup group = session.UniqueResult<SpectrumSourceGroup>(o => o.Name == groupName);
                if (group == null)
                {
                    group = new SpectrumSourceGroup() { Name = groupName };
                    session.Save(group);
                }

                SpectrumSource source = session.UniqueResult<SpectrumSource>(o => o.Name == sourceName);
                if (source == null)
                {
                    source = new SpectrumSource() { Name = sourceName, Group = group };
                    session.Save(source);

                    // add a source group link for the source's immediate group
                    session.Save(new SpectrumSourceGroupLink() { Group = group, Source = source });

                    #region add source group links for all of the immediate group's parent groups
                    if (groupName != "/")
                    {
                        string parentGroupName = groupName.Substring(0, groupName.LastIndexOf("/"));
                        while (true)
                        {
                            if (String.IsNullOrEmpty(parentGroupName))
                                parentGroupName = "/";

                            // add the parent group if it doesn't exist yet
                            SpectrumSourceGroup parentGroup = session.UniqueResult<SpectrumSourceGroup>(o => o.Name == parentGroupName);
                            if (parentGroup == null)
                            {
                                parentGroup = new SpectrumSourceGroup() { Name = parentGroupName };
                                session.Save(parentGroup);
                            }

                            session.Save(new SpectrumSourceGroupLink()
                            {
                                Group = parentGroup,
                                Source = source
                            });

                            if (parentGroupName == "/")
                                break;
                            parentGroupName = parentGroupName.Substring(0, parentGroupName.LastIndexOf("/"));
                        }
                    }
                    #endregion
                }

                Spectrum spectrum = session.UniqueResult<Spectrum>(o => o.Source.Id == source.Id &&
                                                                        o.Index == spectrumId - 1);
                if (spectrum == null)
                {
                    spectrum = new Spectrum()
                    {
                        Index = spectrumId - 1,
                        NativeID = "scan=" + spectrumId,
                        Source = source,
                        PrecursorMZ = 42
                    };
                    session.Save(spectrum);
                }

                Analysis analysis = session.UniqueResult<Analysis>(o => o.Software.Name == analysisId);
                if (analysis == null)
                {
                    int analysisCount = session.Query<Analysis>().Count();
                    analysis = new Analysis()
                    {
                        Name = analysisId + " 1.0",
                        Software = new AnalysisSoftware() { Name = analysisId, Version = "1.0" },
                        StartTime = DateTime.Today.AddHours(analysisCount),
                        Type = AnalysisType.DatabaseSearch
                    };
                    session.Save(analysis);

                    session.Save(new AnalysisParameter()
                    {
                        Analysis = analysis,
                        Name = "Parameter 1",
                        Value = "Value 1"
                    });
                }

                // make sure peptides are sorted by their score divider (which will determine rank)
                var peptideList = new SortedList<int, List<PeptideTuple>>();
                foreach (string tuple in peptideTuples.Split(" ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries))
                {
                    var peptideTuple = new PeptideTuple()
                    {
                        Sequence = tuple.Split('@', '/')[0],
                        Charge = Convert.ToInt32(tuple.Split('@', '/')[1]),
                        ScoreDivider = Convert.ToInt32(tuple.Split('@', '/')[2])
                    };
                    if (!peptideList.ContainsKey(peptideTuple.ScoreDivider))
                        peptideList[peptideTuple.ScoreDivider] = new List<PeptideTuple>();
                    peptideList[peptideTuple.ScoreDivider].Add(peptideTuple);
                }

                int rank = 1;
                int lastDivider = 1;
                foreach (var peptideTupleList in peptideList.Values)
                    foreach (var peptideTuple in peptideTupleList)
                    {
                        PwizPeptide pwizPeptide = new PwizPeptide(peptideTuple.Sequence, ModParsing.ModificationParsing_Auto, ModDelimiter.ModificationDelimiter_Brackets);

                        Peptide peptide = session.UniqueResult<Peptide>(o => o.Sequence == pwizPeptide.sequence);
                        if (peptide == null)
                        {
                            peptide = new TestPeptide(pwizPeptide.sequence).Target as Peptide;
                            peptide.MonoisotopicMass = pwizPeptide.monoisotopicMass(false);
                            peptide.MolecularWeight = pwizPeptide.molecularWeight(false);
                            session.Save(peptide);
                            createTestPeptideInstances(session, peptide);
                        }

                        double neutralPrecursorMass = (spectrum.PrecursorMZ * peptideTuple.Charge) - (peptideTuple.Charge * Proton.Mass);

                        var psm = new PeptideSpectrumMatch()
                        {
                            Peptide = peptide,
                            Spectrum = spectrum,
                            Analysis = analysis,
                            MonoisotopicMass = pwizPeptide.monoisotopicMass(),
                            MolecularWeight = pwizPeptide.molecularWeight(),
                            MonoisotopicMassError = neutralPrecursorMass - pwizPeptide.monoisotopicMass(),
                            MolecularWeightError = neutralPrecursorMass - pwizPeptide.molecularWeight(),
                            Charge = peptideTuple.Charge,
                            Rank = (peptideTuple.ScoreDivider == lastDivider ? rank : ++rank),
                            QValue = (rank == 1 ? row.QValue : PeptideSpectrumMatch.DefaultQValue),
                        };

                        if (row.Score != null)
                            psm.Scores = new Dictionary<string, double>()
                            {
                                {"score1", (double) row.Score / peptideTuple.ScoreDivider},
                                {"score2", 1 / ((double) row.Score / peptideTuple.ScoreDivider)}
                            };

                        session.Save(psm);
                        lastDivider = peptideTuple.ScoreDivider;

                        // add PeptideModifications and Modifications
                        foreach (KeyValuePair<int, ModList> itr in pwizPeptide.modifications())
                        {
                            foreach (PwizMod pwizMod in itr.Value)
                            {
                                Modification mod = session.UniqueResult<Modification>(o => o.Formula == pwizMod.formula());
                                if (mod == null)
                                {
                                    mod = new Modification()
                                    {
                                        Formula = pwizMod.formula(),
                                        MonoMassDelta = pwizMod.monoisotopicDeltaMass(),
                                        AvgMassDelta = pwizMod.averageDeltaMass(),
                                        Name = pwizMod.formula()
                                    };
                                    session.Save(mod);
                                }

                                session.Save(new PeptideModification()
                                {
                                    PeptideSpectrumMatch = psm,
                                    Modification = mod,
                                    Offset = itr.Key == ModMap.NTerminus() ? int.MinValue
                                           : itr.Key == ModMap.CTerminus() ? int.MaxValue
                                           : itr.Key + 1
                                });
                            }
                        }
                    }
                session.Flush();
            }
        }

        public void AddSubsetPeakData (NHibernate.ISession session)
        {
            session.Clear();
            foreach (SpectrumSource source in session.Query<SpectrumSource>())
            {
                var subsetPeakData = new msdata.MSData();
                subsetPeakData.id = subsetPeakData.run.id = source.Name;
                var spectrumList = new msdata.SpectrumListSimple();
                subsetPeakData.run.spectrumList = spectrumList;

                foreach (Spectrum spectrum in source.Spectra.OrderBy(o => o.Index))
                {
                    var spectrumData = new msdata.Spectrum();
                    spectrumData.id = spectrum.NativeID;
                    spectrumData.index = spectrum.Index;
                    spectrumData.setMZIntensityArrays(new List<double>() { 100, 200, 300, 400, 500 },
                                                      new List<double>() { 10, 20, 30, 40, 50 });
                    spectrumList.spectra.Add(spectrumData);
                }

                session.Evict(source);
                var newSource = new SpectrumSource(subsetPeakData)
                {
                    Id = source.Id,
                    Group = source.Group,
                    Name = source.Name,
                    URL = source.URL
                };
                session.Update(newSource);
            }
        }
        #endregion

        #region Private methods
        private static void createTestPeptideInstances (NHibernate.ISession session, Peptide pep)
        {
            // store instances even though the association is inverse:
            // the PeptideModification.Offset property needs access to the protein sequence
            pep.Instances = new List<PeptideInstance>();

            foreach (Protein pro in session.Query<Protein>())
            {
                int start = pro.Sequence.IndexOf(pep.Sequence, 0);
                while (start >= 0)
                {
                    int end = start + pep.Sequence.Length;
                    bool nTerminusIsSpecific = start == 0 || pro.Sequence[start - 1] == 'K' || pro.Sequence[start - 1] == 'R';
                    bool cTerminusIsSpecific = end == pro.Sequence.Length || pro.Sequence[end - 1] == 'K' || pro.Sequence[end - 1] == 'R';
                    var instance = new TestPI()
                    {
                        Peptide = pep,
                        Protein = pro,
                        Offset = start,
                        Length = pep.Sequence.Length,
                        MissedCleavages = pep.Sequence.ToCharArray(0, pep.Sequence.Length - 1).Count(o => o == 'K' || o == 'R'),
                        NTerminusIsSpecific = nTerminusIsSpecific,
                        CTerminusIsSpecific = cTerminusIsSpecific,
                        specificTermini = (nTerminusIsSpecific ? 1 : 0) + (cTerminusIsSpecific ? 1 : 0),
                    }.Target as PeptideInstance;

                    session.Save(instance);
                    pep.Instances.Add(instance);

                    start = pro.Sequence.IndexOf(pep.Sequence, start + 1);
                }
            }
        }
        #endregion

        // shared session between TestModel methods
        public NHibernate.ISession session;
        
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

        public TestModel ()
        {
            #region Example PSMs
            List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
            {
                //                 Group    Source  Spectrum Analysis     Score  Q   List of Peptide@Charge/ScoreDivider
                new SpectrumTuple("/A/1", "Source 1",   1,  "Engine 1",      12, 0, "[C2H2O1]PEPTIDE@2/1 TIDERPEPTIDEK@4/2 EPPIER@1/3"),
                new SpectrumTuple("/A/1", "Source 1",   2,  "Engine 1",      23, 0, "PEPTIDER@2/1 PETPDETK@3/3 EDITPEPK@2/5"),
                new SpectrumTuple("/A/1", "Source 1",   3,  "Engine 1",      34, 0, "PEPTIDEK@2/1 TIDER@1/4 PETPDETK@2/8"),
                new SpectrumTuple("/A/1", "Source 2",   1,  "Engine 1",      43, 0, "PEPTIDE@2/1 E[H-2O-1]DIT[P1O4]PEPR@2/2 EPPIER@1/7"),
                new SpectrumTuple("/A/1", "Source 2",   2,  "Engine 1",      32, 0, "PEPTIDER@3/1 EDITPEPK@3/4 EDITPEPR@3/5"),
                new SpectrumTuple("/A/1", "Source 2",   3,  "Engine 1",      21, 0, "PEPT[P1O4]IDEK@3/1 TIDEK@1/7 PETPDETK@2/8"),
                new SpectrumTuple("/A/2", "Source 3",   1,  "Engine 1",      56, 0, "TIDEK@2/1 TIDE@1/2 P[P1O4]EPTIDE@3/3"),
                new SpectrumTuple("/A/2", "Source 3",   2,  "Engine 1",      45, 0, "TIDER@2/1 TIDERPEPTIDEK@4/3 PEPTIDEK@3/4"),
                new SpectrumTuple("/A/2", "Source 3",   3,  "Engine 1",      34, 0, "TIDE@1/1 PEPTIDEK@3/6 TIDEK@1/7"),
                new SpectrumTuple("/B/1", "Source 4",   1,  "Engine 1",      65, 0, "TIDERPEPTIDEK@4/1 PETPDETK@3/8 EDITPEPR@3/9"),
                new SpectrumTuple("/B/1", "Source 4",   2,  "Engine 1",      53, 0, "E[H-2O-1]DITPEPK@2/1 PEPTIDEK@3/2 PEPTIDE@2/3"),
                new SpectrumTuple("/B/1", "Source 4",   3,  "Engine 1",      42, 0, "EDIT@2/1 PEPTIDEK@3/3 EDITPEPR@2/4"),
                new SpectrumTuple("/B/2", "Source 5",   1,  "Engine 1",      20, 0, "EPPIER@2/1 TIDE@1/7 PEPTIDE@2/9"),
                new SpectrumTuple("/B/2", "Source 5",   2,  "Engine 1",      24, 0, "PETPDETK@2/1 PEPTIDEK@3/5 EDITPEPR@2/8"),
                new SpectrumTuple("/B/2", "Source 5",   3,  "Engine 1",      24, 0, "PETPDETK@3/1 EDIT@1/4 TIDER@2/6"),

                new SpectrumTuple("/A/1", "Source 1",   1,  "Engine 2",     120, 0, "TIDERPEPTIDEK@4/1 PEPTIDE@2/2 EPPIER@1/3"),
                new SpectrumTuple("/A/1", "Source 1",   2,  "Engine 2",     230, 0, "PEPTIDER@2/1 PETPDETK@3/3 EDITPEPK@2/5"),
                new SpectrumTuple("/A/1", "Source 1",   3,  "Engine 2",     340, 0, "PEPTIDEK@2/1 TIDER@1/4 PETPDETK@2/8"),
                new SpectrumTuple("/A/1", "Source 2",   1,  "Engine 2",     430, 0, "PEPTIDE@2/1 EDITPEPR@2/2 EPPIER@1/7"),
                new SpectrumTuple("/A/1", "Source 2",   2,  "Engine 2",     320, 0, "PEPTIDER@3/1 EDITPEPK@3/4 EDITPEPR@3/5"),
                new SpectrumTuple("/A/1", "Source 2",   3,  "Engine 2",     210, 0, "PEPT[P1O4]IDEK@3/1 TIDEK@1/7 PETPDETK@2/8"),
                new SpectrumTuple("/A/2", "Source 3",   1,  "Engine 2",     560, 0, "TIDEK@2/1 TIDE@1/2 PEPTIDE@3/3"),
                new SpectrumTuple("/A/2", "Source 3",   2,  "Engine 2",     450, 0, "TIDER@2/1 TIDERPEPTIDEK@4/3 PEPTIDEK@3/4"),
                new SpectrumTuple("/A/2", "Source 3",   3,  "Engine 2",     340, 0, "TIDE@1/1 PEPTIDEK@3/6 TIDEK@1/7"),
                new SpectrumTuple("/B/1", "Source 4",   1,  "Engine 2",     650, 0, "TIDERPEPTIDEK@4/1 PET[P1O4]PDETK@3/8 EDITPEPR@3/9"),
                new SpectrumTuple("/B/1", "Source 4",   2,  "Engine 2",     530, 0, "EDITPEPK@2/1 PEPTIDEK@3/2 PEPTIDE@2/3"),
                new SpectrumTuple("/B/1", "Source 4",   3,  "Engine 2",     420, 0, "EDIT@2/1 PEPTIDEK@3/3 EDITPEPR@2/4"),
                new SpectrumTuple("/B/2", "Source 5",   1,  "Engine 2",     200, 0, "E[H-2O-1]PPIER@2/1 TIDE@1/7 PEPTIDE@2/9"),
                new SpectrumTuple("/B/2", "Source 5",   2,  "Engine 2",     240, 0, "PEPTIDEK@2/1 PETPDETK@2/4 EDITPEPR@2/8"),
                new SpectrumTuple("/B/2", "Source 5",   3,  "Engine 2",     240, 0, "PETPDETK@3/1 EDIT@1/4 TIDER@2/6"),
            };
            #endregion

            var sessionFactory = SessionFactoryFactory.CreateSessionFactory(":memory:", true, false);
            session = sessionFactory.OpenSession();

            session.Transaction.Begin();
            CreateTestProteins(session, testProteinSequences);
            CreateTestData(session, testPsmSummary);
            AddSubsetPeakData(session);
            session.Transaction.Commit();

            // clear session so objects are loaded from database
            session.Clear();
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
        public void TestOverallCounts ()
        {
            Assert.AreEqual(6, session.Query<Protein>().Count());
            Assert.AreEqual(12, session.Query<Peptide>().Count());
            Assert.AreEqual(30, session.Query<PeptideInstance>().Count());
            Assert.AreEqual(90, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(180, session.CreateSQLQuery("SELECT COUNT(*) FROM PeptideSpectrumMatchScores").UniqueResult<long>());
            Assert.AreEqual(2, session.Query<Analysis>().Count());
            Assert.AreEqual(2, session.Query<AnalysisParameter>().Count());
            Assert.AreEqual(15, session.Query<Spectrum>().Count());
            Assert.AreEqual(5, session.Query<SpectrumSource>().Count());
            Assert.AreEqual(7, session.Query<SpectrumSourceGroup>().Count());
            Assert.AreEqual(15, session.Query<SpectrumSourceGroupLink>().Count());

            Assert.AreEqual(2, session.Get<PeptideSpectrumMatch>(1L).Scores.Count);

            Assert.AreEqual(1, session.UniqueResult<Analysis>(o => o.Name == "Engine 1 1.0").Parameters.Count);
            Assert.AreEqual(1, session.UniqueResult<Analysis>(o => o.Name == "Engine 2 1.0").Parameters.Count);

            Assert.AreEqual(6, session.UniqueResult<Spectrum>(o => o.Index == 0 && o.Source.Name == "Source 1").Matches.Count);
            Assert.AreEqual(6, session.UniqueResult<Spectrum>(o => o.Index == 1 && o.Source.Name == "Source 1").Matches.Count);
            Assert.AreEqual(6, session.UniqueResult<Spectrum>(o => o.Index == 0 && o.Source.Name == "Source 2").Matches.Count);

            Assert.AreEqual(3, session.UniqueResult<SpectrumSource>(o => o.Name == "Source 1").Groups.Count); // groups: /, /A, /A/1
            Assert.AreEqual(3, session.UniqueResult<SpectrumSource>(o => o.Name == "Source 1").Spectra.Count);

            Assert.AreEqual(3, session.UniqueResult<SpectrumSource>(o => o.Name == "Source 3").Groups.Count); // groups: /, /A, /A/2
            Assert.AreEqual(3, session.UniqueResult<SpectrumSource>(o => o.Name == "Source 3").Spectra.Count);

            Assert.AreEqual(5, session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/").Sources.Count);
            Assert.AreEqual(3, session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/A").Sources.Count);
            Assert.AreEqual(2, session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/A/1").Sources.Count);
            Assert.AreEqual(1, session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/A/2").Sources.Count);

            Assert.AreEqual(30, session.Query<PeptideSpectrumMatch>().Where(o => o.Rank == 1).Count());
            Assert.AreEqual(30, session.Query<PeptideSpectrumMatch>().Where(o => o.Rank == 2).Count());
            Assert.AreEqual(30, session.Query<PeptideSpectrumMatch>().Where(o => o.Rank == 3).Count());

            Assert.AreEqual(18, session.Query<PeptideSpectrumMatch>().Where(o => o.Charge == 1).Count());
            Assert.AreEqual(39, session.Query<PeptideSpectrumMatch>().Where(o => o.Charge == 2).Count());
            Assert.AreEqual(6, session.Query<PeptideSpectrumMatch>().Where(o => o.Charge == 4).Count());
        }

        [TestMethod]
        public void TestSanity()
        {
            for (long i = 1; i <= testProteinSequences.Length; ++i)
            {
                Protein pro = session.Get<Protein>(i);
                Assert.AreEqual(i, pro.Id);
                Assert.AreEqual("PRO" + i, pro.Accession);
                Assert.AreEqual("Protein " + i, pro.Description);
                Assert.AreEqual(testProteinSequences[i - 1], pro.Sequence);

                // these haven't been set yet
                Assert.IsTrue(String.IsNullOrEmpty(pro.ProteinGroup));
                Assert.AreEqual(0, pro.Cluster);
            }
        }

        [TestMethod]
        public void TestProteins()
        {
            Protein pro1 = session.UniqueResult<Protein>(o => o.Accession == "PRO1");
            Assert.AreEqual(11, pro1.Peptides.Count);
            Assert.AreEqual(3, pro1.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDE"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDEK"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDER"));
            Assert.AreEqual(3, pro1.Peptides.Count(o => o.Peptide.Sequence == "TIDE"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "TIDEK"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "TIDER"));
            Assert.AreEqual(0, pro1.Peptides.Count(o => o.Peptide.Sequence == "EDIT"));
            Assert.AreEqual(0, pro1.Peptides.Count(o => o.Peptide.Sequence == "EPPIER"));

            Protein pro5 = session.UniqueResult<Protein>(o => o.Accession == "PRO5");
            Assert.AreEqual(3, pro5.Peptides.Count);
            Assert.AreEqual(2, pro5.Peptides.Count(o => o.Peptide.Sequence == "EDIT"));
            Assert.AreEqual(1, pro5.Peptides.Count(o => o.Peptide.Sequence == "EDITPEPK"));
            Assert.AreEqual(0, pro5.Peptides.Count(o => o.Peptide.Sequence == "EDITPEPR"));
            Assert.AreEqual(0, pro5.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDE"));
            Assert.AreEqual(0, pro5.Peptides.Count(o => o.Peptide.Sequence == "EPPIER"));

            Protein pro6 = session.UniqueResult<Protein>(o => o.Accession == "PRO6");
            Assert.AreEqual(2, pro6.Peptides.Count);
            Assert.AreEqual(1, pro6.Peptides.Count(o => o.Peptide.Sequence == "EPPIER"));
            Assert.AreEqual(1, pro6.Peptides.Count(o => o.Peptide.Sequence == "PETPDETK"));
            Assert.AreEqual(0, pro6.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDE"));
            Assert.AreEqual(0, pro6.Peptides.Count(o => o.Peptide.Sequence == "EDIT"));
        }

        [TestMethod]
        public void TestPeptides()
        {
            Peptide pep1 = session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDE");
            Assert.AreEqual(4, pep1.Instances.Count);
            Assert.AreEqual(10, pep1.Matches.Count);
            Assert.AreEqual(new PwizPeptide(pep1.Sequence).monoisotopicMass(), pep1.MonoisotopicMass, 1e-12);
            Assert.AreEqual(new PwizPeptide(pep1.Sequence).molecularWeight(), pep1.MolecularWeight, 1e-12);

            Peptide pep2 = session.UniqueResult<Peptide>(o => o.Sequence == "TIDERPEPTIDEK");
            Assert.AreEqual(2, pep2.Instances.Count);
            Assert.AreEqual(6, pep2.Matches.Count);

            Peptide pep3 = session.UniqueResult<Peptide>(o => o.Sequence == "EPPIER");
            Assert.AreEqual(1, pep3.Instances.Count);
            Assert.AreEqual(6, pep3.Matches.Count);

            Peptide pep4 = session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDER");
            Assert.AreEqual(1, pep4.Instances.Count);
            Assert.AreEqual(4, pep4.Matches.Count);

            Peptide pep7 = session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDEK");
            Assert.AreEqual(2, pep7.Instances.Count);
            Assert.AreEqual(14, pep7.Matches.Count);

            Peptide pep11 = session.UniqueResult<Peptide>(o => o.Sequence == "TIDE");
            Assert.AreEqual(7, pep11.Instances.Count);
            Assert.AreEqual(6, pep11.Matches.Count);
            Assert.AreEqual(new PwizPeptide(pep11.Sequence).monoisotopicMass(), pep11.MonoisotopicMass, 1e-12);
            Assert.AreEqual(new PwizPeptide(pep11.Sequence).molecularWeight(), pep11.MolecularWeight, 1e-12);
        }

        [TestMethod]
        public void TestPeptideInstances()
        {
            PeptideInstance pi1 = session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO1" &&
                                                                             o.Peptide.Sequence == "PEPTIDE" &&
                                                                             o.Offset == 0);
            Assert.AreEqual(1, pi1.SpecificTermini);
            Assert.AreEqual(true, pi1.NTerminusIsSpecific);
            Assert.AreEqual(false, pi1.CTerminusIsSpecific);
            Assert.AreEqual(0, pi1.Offset);
            Assert.AreEqual(7, pi1.Length);
            Assert.AreEqual(0, pi1.MissedCleavages);

            PeptideInstance pi5 = session.UniqueResult<PeptideInstance>(o => o.Protein.Accession == "PRO1" &&
                                                                             o.Peptide.Sequence == "TIDERPEPTIDEK" &&
                                                                             o.Offset == 3);
            Assert.AreEqual(1, pi5.SpecificTermini);
            Assert.AreEqual(false, pi5.NTerminusIsSpecific);
            Assert.AreEqual(true, pi5.CTerminusIsSpecific);
            Assert.AreEqual(3, pi5.Offset);
            Assert.AreEqual(13, pi5.Length);
            Assert.AreEqual(1, pi5.MissedCleavages);
        }

        [TestMethod]
        public void TestSpectrumSourceGroups()
        {
            SpectrumSourceGroup ssg3 = session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/");
            Assert.AreEqual(5, ssg3.Sources.Count);
            Assert.AreEqual(0, ssg3.GetGroupDepth());

            SpectrumSourceGroup ssg2 = session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/A");
            Assert.AreEqual(3, ssg2.Sources.Count);
            Assert.AreEqual(1, ssg2.GetGroupDepth());
            Assert.IsTrue(ssg2.IsChildOf(ssg3));
            Assert.IsTrue(ssg2.IsImmediateChildOf(ssg3));

            SpectrumSourceGroup ssg1 = session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/A/1");
            Assert.AreEqual(2, ssg1.Sources.Count);
            Assert.AreEqual(2, ssg1.GetGroupDepth());
            Assert.IsTrue(ssg1.IsChildOf(ssg3));
            Assert.IsFalse(ssg1.IsImmediateChildOf(ssg3));
            Assert.IsTrue(ssg1.IsChildOf(ssg2));
            Assert.IsTrue(ssg1.IsImmediateChildOf(ssg2));

            SpectrumSourceGroup ssg5 = session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/B/2");
            Assert.AreEqual(1, ssg5.Sources.Count);
            Assert.AreEqual(2, ssg5.GetGroupDepth());
            Assert.IsFalse(ssg5.IsChildOf(ssg2));
            Assert.IsTrue(ssg5.IsChildOf(ssg3));
            Assert.IsFalse(ssg5.IsImmediateChildOf(ssg3));
        }

        [TestMethod]
        public void TestSpectrumSources()
        {
            SpectrumSource ss1 = session.UniqueResult<SpectrumSource>(o => o.Name == "Source 1");
            Assert.AreEqual(session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/A/1"), ss1.Group);
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/"));
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/A"));
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/A/1"));
            Assert.IsNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/B"));
            Assert.IsNotNull(ss1.Metadata);
            Assert.AreEqual(ss1.Name, ss1.Metadata.id);
            Assert.IsNotInstanceOfType(ss1.Metadata.run.spectrumList, typeof(msdata.SpectrumListSimple));

            SpectrumSource ss4 = session.UniqueResult<SpectrumSource>(o => o.Name == "Source 4");
            Assert.AreEqual(session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/B/1"), ss4.Group);
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/"));
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/B"));
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/B/1"));
            Assert.IsNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/A"));
            Assert.IsNotNull(ss4.Metadata);
            Assert.AreEqual(ss4.Name, ss4.Metadata.id);
            Assert.IsNotInstanceOfType(ss4.Metadata.run.spectrumList, typeof(msdata.SpectrumListSimple));
        }

        [TestMethod]
        public void TestSpectra()
        {
            Spectrum ss1s1 = session.UniqueResult<Spectrum>(o => o.Index == 0 &&
                                                                 o.Source.Name == "Source 1");
            Assert.AreEqual("scan=1", ss1s1.NativeID);
            Assert.AreEqual(6, ss1s1.Matches.Count);
            Assert.AreEqual(42, ss1s1.PrecursorMZ, 1e-12);
            Assert.AreEqual(ss1s1.Index, ss1s1.Metadata.index);
            Assert.AreEqual(ss1s1.NativeID, ss1s1.Metadata.id);
            Assert.AreEqual(5UL, ss1s1.Metadata.defaultArrayLength);
            Assert.AreEqual(100, ss1s1.MetadataWithPeaks.getMZArray().data[0]);

            Spectrum ss1s2 = session.UniqueResult<Spectrum>(o => o.Index == 1 &&
                                                                 o.Source.Name == "Source 1");
            Assert.AreEqual("scan=2", ss1s2.NativeID);
            Assert.AreEqual(6, ss1s2.Matches.Count);
            Assert.AreEqual(ss1s2.Index, ss1s2.Metadata.index);
            Assert.AreEqual(ss1s2.NativeID, ss1s2.Metadata.id);
            Assert.AreEqual(5UL, ss1s2.Metadata.defaultArrayLength);
            Assert.AreEqual(200, ss1s2.MetadataWithPeaks.getMZArray().data[1]);

            Spectrum ss4s1 = session.UniqueResult<Spectrum>(o => o.Index == 0 &&
                                                                 o.Source.Name == "Source 4");
            Assert.AreEqual("scan=1", ss4s1.NativeID);
            Assert.AreEqual(6, ss4s1.Matches.Count);
            Assert.AreEqual(42, ss4s1.PrecursorMZ, 1e-12);
            Assert.AreEqual(ss4s1.Index, ss4s1.Metadata.index);
            Assert.AreEqual(ss4s1.NativeID, ss4s1.Metadata.id);
            Assert.AreEqual(5UL, ss4s1.Metadata.defaultArrayLength);
            Assert.AreEqual(300, ss4s1.MetadataWithPeaks.getMZArray().data[2]);
        }

        [TestMethod]
        public void TestAnalyses()
        {
            Analysis a1 = session.Get<Analysis>(1L);
            Assert.AreEqual("Engine 1 1.0", a1.Name);
            Assert.AreEqual("Engine 1", a1.Software.Name);
            Assert.AreEqual("1.0", a1.Software.Version);
            Assert.AreEqual(45, a1.Matches.Count);

            Analysis a2 = session.Get<Analysis>(2L);
            Assert.AreEqual("Engine 2 1.0", a2.Name);
            Assert.AreEqual("Engine 2", a2.Software.Name);
            Assert.AreEqual("1.0", a2.Software.Version);
            Assert.AreEqual(45, a2.Matches.Count);

            AnalysisParameter ap1 = session.Get<AnalysisParameter>(1L);
            Assert.AreEqual(a1, ap1.Analysis);
            Assert.AreEqual(ap1, a1.Parameters.First());
            Assert.AreEqual("Parameter 1", ap1.Name);
            Assert.AreEqual("Value 1", ap1.Value);

            AnalysisParameter ap2 = session.Get<AnalysisParameter>(2L);
            Assert.AreEqual(a2, ap2.Analysis);
            Assert.AreEqual(ap2, a2.Parameters.First());
            Assert.AreEqual("Parameter 1", ap2.Name);
            Assert.AreEqual("Value 1", ap2.Value);
        }

        [TestMethod]
        public void TestPeptideSpectrumMatches()
        {
            var ss1 = session.UniqueResult<SpectrumSource>(o => o.Name == "Source 1");
            var ss4 = session.UniqueResult<SpectrumSource>(o => o.Name == "Source 4");
            var a1 = session.UniqueResult<Analysis>(o => o.Name == "Engine 1 1.0");
            var a2 = session.UniqueResult<Analysis>(o => o.Name == "Engine 2 1.0");

            // [C2H2O1]PEPTIDE
            var ss1s1psm1 = session.UniqueResult<PeptideSpectrumMatch>(o => o.Spectrum.Source.Id == ss1.Id &&
                                                                            o.Spectrum.Index == 0 &&
                                                                            o.Analysis.Id == a1.Id &&
                                                                            o.Rank == 1);
            var pwizPeptide = new PwizPeptide("(C2H2O1)PEPTIDE", proteome.ModificationParsing.ModificationParsing_ByFormula);
            Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "PEPTIDE"), ss1s1psm1.Peptide);
            Assert.AreEqual(a1, ss1s1psm1.Analysis);
            Assert.IsTrue(a1.Matches.Contains(ss1s1psm1));
            Assert.AreEqual(1, ss1s1psm1.Modifications.Count);
            Assert.AreEqual("C2H2O1", ss1s1psm1.Modifications[0].Modification.Formula);
            Assert.AreEqual(int.MinValue, ss1s1psm1.Modifications[0].Offset);
            Assert.AreEqual(2, ss1s1psm1.Charge);
            Assert.AreEqual(pwizPeptide.monoisotopicMass(), ss1s1psm1.MonoisotopicMass, 1e-12);
            Assert.AreEqual(pwizPeptide.molecularWeight(), ss1s1psm1.MolecularWeight, 1e-12);
            Assert.AreEqual(12.0, ss1s1psm1.Scores["score1"], 1e-12);
            Assert.AreEqual(1 / 12.0, ss1s1psm1.Scores["score2"], 1e-12);

            var ss1s1psm1e2 = session.UniqueResult<PeptideSpectrumMatch>(o => o.Spectrum.Source.Id == ss1.Id &&
                                                                              o.Spectrum.Index == 0 &&
                                                                              o.Analysis.Id == a2.Id &&
                                                                              o.Rank == 1);
            pwizPeptide = new PwizPeptide("TIDERPEPTIDEK");
            Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "TIDERPEPTIDEK"), ss1s1psm1e2.Peptide);
            Assert.AreEqual(a2, ss1s1psm1e2.Analysis);
            Assert.IsTrue(a2.Matches.Contains(ss1s1psm1e2));
            Assert.AreEqual(0, ss1s1psm1e2.Modifications.Count);
            Assert.AreEqual(4, ss1s1psm1e2.Charge);
            Assert.AreEqual(pwizPeptide.monoisotopicMass(), ss1s1psm1e2.MonoisotopicMass, 1e-12);
            Assert.AreEqual(pwizPeptide.molecularWeight(), ss1s1psm1e2.MolecularWeight, 1e-12);
            Assert.AreEqual(120.0, ss1s1psm1e2.Scores["score1"], 1e-12);
            Assert.AreEqual(1 / 120.0, ss1s1psm1e2.Scores["score2"], 1e-12);

            var ss1s1psm2 = session.UniqueResult<PeptideSpectrumMatch>(o => o.Spectrum.Source.Id == ss1.Id &&
                                                                            o.Spectrum.Index == 0 &&
                                                                            o.Analysis.Id == a1.Id &&
                                                                            o.Rank == 2);
            Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "TIDERPEPTIDEK"), ss1s1psm2.Peptide);
            Assert.AreEqual(a1, ss1s1psm1.Analysis);
            Assert.AreEqual(4, ss1s1psm2.Charge);
            Assert.AreEqual(2, ss1s1psm2.Rank);
            Assert.AreEqual(12.0 / 2, ss1s1psm2.Scores["score1"], 1e-12);
            Assert.AreEqual(1 / (12.0 / 2), ss1s1psm2.Scores["score2"], 1e-12);

            var ss1s1psm3 = session.UniqueResult<PeptideSpectrumMatch>(o => o.Spectrum.Source.Id == ss1.Id &&
                                                                            o.Spectrum.Index == 0 &&
                                                                            o.Analysis.Id == a1.Id &&
                                                                            o.Rank == 3);
            Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "EPPIER"), ss1s1psm3.Peptide);
            Assert.AreEqual(a1, ss1s1psm3.Analysis);
            Assert.AreEqual(1, ss1s1psm3.Charge);

            
            // E[H-2O-1]DIT[P1O4]PEPR
            var ss2s1psm2 = session.UniqueResult<PeptideSpectrumMatch>(o => o.Spectrum.Source.Id == 2 &&
                                                                            o.Spectrum.Index == 0 &&
                                                                            o.Analysis.Id == 1 &&
                                                                            o.Rank == 2);
            pwizPeptide = new PwizPeptide("E(H-2O-1)DIT(P1O4)PEPR", proteome.ModificationParsing.ModificationParsing_ByFormula);
            Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "EDITPEPR"), ss2s1psm2.Peptide);
            Assert.AreEqual(2, ss2s1psm2.Modifications.Count);
            Assert.AreEqual("H-2O-1", ss2s1psm2.Modifications[0].Modification.Formula);
            Assert.AreEqual(1, ss2s1psm2.Modifications[0].Offset);
            Assert.AreEqual("O4P1", ss2s1psm2.Modifications[1].Modification.Formula);
            Assert.AreEqual(4, ss2s1psm2.Modifications[1].Offset);
            Assert.AreEqual(pwizPeptide.monoisotopicMass(), ss2s1psm2.MonoisotopicMass, 1e-12);
            Assert.AreEqual(pwizPeptide.molecularWeight(), ss2s1psm2.MolecularWeight, 1e-12);

            var ss4s1psm1 = session.UniqueResult<PeptideSpectrumMatch>(o => o.Spectrum.Source.Id == ss4.Id &&
                                                                            o.Spectrum.Index == 0 &&
                                                                            o.Analysis.Id == a1.Id &&
                                                                            o.Rank == 1);
            Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "TIDERPEPTIDEK"), ss4s1psm1.Peptide);
            Assert.AreEqual(a1, ss4s1psm1.Analysis);
            Assert.AreEqual(4, ss4s1psm1.Charge);
        }

        [TestMethod]
        public void TestModifications()
        {
            Modification mod1 = session.UniqueResult<Modification>(o => o.Formula == "C2H2O1");
            Assert.AreEqual("C2H2O1", mod1.Formula);
            Assert.AreEqual(mod1.Formula, mod1.Name);
            Assert.AreEqual(new Formula(mod1.Formula).monoisotopicMass(), mod1.MonoMassDelta);
            Assert.AreEqual(new Formula(mod1.Formula).molecularWeight(), mod1.AvgMassDelta);

            Modification mod2 = session.UniqueResult<Modification>(o => o.Formula == "H-2O-1");
            Assert.AreEqual("H-2O-1", mod2.Formula);
            Assert.AreEqual(mod2.Formula, mod2.Name);
            Assert.AreEqual(new Formula(mod2.Formula).monoisotopicMass(), mod2.MonoMassDelta);
            Assert.AreEqual(new Formula(mod2.Formula).molecularWeight(), mod2.AvgMassDelta);

            Modification mod3 = session.UniqueResult<Modification>(o => o.Formula == "O4P1");
            Assert.AreEqual("O4P1", mod3.Formula);
            Assert.AreEqual(mod3.Formula, mod3.Name);
            Assert.AreEqual(new Formula(mod3.Formula).monoisotopicMass(), mod3.MonoMassDelta);
            Assert.AreEqual(new Formula(mod3.Formula).molecularWeight(), mod3.AvgMassDelta);

            // [C2H2O1]PEPTIDE
            PeptideModification pm1 = session.UniqueResult<PeptideModification>(o => o.PeptideSpectrumMatch.Peptide.Sequence == "PEPTIDE" &&
                                                                                     o.Modification == mod1 &&
                                                                                     o.Offset == int.MinValue);
            Assert.IsTrue(pm1.PeptideSpectrumMatch.Modifications.Contains(pm1));
            Assert.AreEqual('(', pm1.Site);

            // E[H-2O-1]DIT[P1O4]PEPR
            PeptideModification pm2 = session.UniqueResult<PeptideModification>(o => o.PeptideSpectrumMatch.Peptide.Sequence == "EDITPEPR" &&
                                                                                     o.Modification == mod2 &&
                                                                                     o.Offset == 1);
            Assert.AreEqual(mod2, pm2.Modification);
            Assert.IsTrue(pm2.PeptideSpectrumMatch.Modifications.Contains(pm2));
            Assert.AreEqual(1, pm2.Offset);
            Assert.AreEqual('E', pm2.Site);

            PeptideModification pm3 = pm2.PeptideSpectrumMatch.Modifications.Where(o => o.Offset == 4).Single();
            Assert.AreEqual(mod3, pm3.Modification);
            Assert.IsTrue(pm3.PeptideSpectrumMatch.Modifications.Contains(pm3));
            Assert.AreEqual('T', pm3.Site);

            // PEPT[P1O4]IDEK
            PeptideModification pm4 = session.UniqueResult<PeptideModification>(o => o.PeptideSpectrumMatch.Peptide.Sequence == "PEPTIDEK" &&
                                                                                     o.Modification == mod3 &&
                                                                                     o.Offset == 4);
            Assert.IsTrue(pm4.PeptideSpectrumMatch.Modifications.Contains(pm4));
            Assert.AreEqual('T', pm4.Site);
        }

        private string createSimpleProteinSequence(string motif, int length)
        {
            var sequence = new StringBuilder();
            while(sequence.Length < length)
                sequence.Append(motif);
            return sequence.ToString();
        }

#if false
        [TestMethod]
        public void TestCalculateAdditionalPeptides ()
        {
            #region Example with forward and reverse proteins

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
            };

            const int analysisCount = 2;
            const int sourceCount = 2;
            const int chargeCount = 2;

            var sessionFactory = SessionFactoryFactory.CreateSessionFactory(":memory:", true, false);
            session = sessionFactory.OpenSession();

            session.Transaction.Begin();
            createTestProteins(testProteinSequences);
            #endregion

            #region Example PSMs
            // For each combination of 2 engines, 2 sources, and 2 charges,
            // we test qonversion on the scenarios described by the comments:

            for (int analysis = 1; analysis <= analysisCount; ++analysis)
            for (int source = 1; source <= sourceCount; ++source)
            for (int charge = 1; charge <= chargeCount; ++charge)
            {
                string sourceName = "Source " + source.ToString();
                string engineName = "Engine " + analysis.ToString();
                int scan = 0;

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    // Columns:       Group  Source   Spectrum Analysis Score Q  List of Peptide@Charge/ScoreDivider
                    
                    // 1 protein to 1 peptide to 1 spectrum = 1 additional peptide
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("AAAAAAAAAA@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 1 peptide to 2 spectra = 1 additional peptide
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("BBBBBBBBBB@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("BBBBBBBBBB@{0}/1 CCCCCCCCCC@{0}/8", charge)),

                    // 1 protein to 2 peptides to 1 spectrum (each) = 2 additional peptides
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("CCCCCCCCCC@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("CCCCCCCCC@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 1 protein to 2 peptides to 2 spectra (each) = 2 additional peptides
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDDD@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDDD@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDD@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDD@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 1 peptide to 1 spectrum = 1 additional peptide (ambiguous protein group)
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("EEEEEEEEEE@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("FFFFFFFFFF@{0}/1 BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 1 peptide to 2 spectra = 1 additional peptide (ambiguous protein group)
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDDD@{0}/1 AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDDD@{0}/1 BBBBBBBBBB@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDD@{0}/1  AAAAAAAAAA@{0}/8", charge)),
                    new SpectrumTuple("/", sourceName, ++scan, engineName, 1, 1, String.Format("DDDDDDDDD@{0}/1  BBBBBBBBBB@{0}/8", charge)),

                    // 2 proteins to 2 peptides to 1 spectrum (each) = 2 additional peptides (ambiguous protein group)

                    // 2 proteins to 2 peptides to 2 spectra (each) = 2 additional peptides (ambiguous protein group)

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

                createTestData(testPsmSummary);
            }
            session.Transaction.Commit();
            #endregion

            var dataFilter = new DataFilter_Accessor();
            Map<long, long> additionalPeptidesByProteinId = dataFilter.calculateAdditionalPeptides(session);
        }
#endif
    }
}
