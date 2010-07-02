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

using pwiz.CLI.chemistry;
using PwizPeptide = pwiz.CLI.proteome.Peptide;
using PwizMod = pwiz.CLI.proteome.Modification;
using ModList = pwiz.CLI.proteome.ModificationList;
using ModMap = pwiz.CLI.proteome.ModificationMap;
using ModParsing = pwiz.CLI.proteome.ModificationParsing;
using ModDelimiter = pwiz.CLI.proteome.ModificationDelimiter;

namespace TestModel
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
        NHibernate.ISession session;

        private void createTestProteins(IList<string> testProteinSequences)
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

        private void createTestPeptideInstances (Peptide pep)
        {
            // store instances even though the association is inverse:
            // the PeptideModification.Offset property needs access to the protein sequence
            pep.Instances = new List<PeptideInstance>();

            foreach (Protein pro in session.QueryOver<Protein>().List<Protein>())
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

                    start = pro.Sequence.IndexOf(pep.Sequence, start+1);
                }
            }
        }

        private Peptide addOrGetPeptide(string sequenceWithoutMods)
        {
            Peptide peptide = session.QueryOver<Peptide>().Where(o => o.Sequence == sequenceWithoutMods).SingleOrDefault();
            if (peptide == null)
            {
                peptide = new TestPeptide(sequenceWithoutMods).Target as Peptide;
                session.Save(peptide);
                createTestPeptideInstances(peptide);
            }

            return peptide;
        }

        struct PeptideTuple
        {
            public string Sequence { get; set; }
            public int Charge { get; set; }
            public int ScoreDivider { get; set; }
        }

        class SpectrumTuple
        {
            public SpectrumTuple (string group, string source, int spectrum, string analysis, int score, double qvalue, string peptideTuples)
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
            public int Score { get; set; }
            public double QValue { get; set; }
            public string PeptideTuples { get; set; }
        }

        private void createTestData (IList<SpectrumTuple> testPsmSummary)
        {
            // Group, Source, Spectrum, Peptide@Charge, Score, Alternatives (Peptide@Charge/ScoreDivider)
            foreach (SpectrumTuple row in testPsmSummary)
            {
                string groupName = row.Group;
                string sourceName = row.Source;
                int spectrumId = row.Spectrum;
                string analysisId = row.Analysis;
                double score = (double) row.Score;
                string peptideTuples = row.PeptideTuples;

                SpectrumSourceGroup group = session.QueryOver<SpectrumSourceGroup>().Where(o => o.Name == groupName).SingleOrDefault();
                if (group == null)
                {
                    group = new SpectrumSourceGroup() { Name = groupName };
                    session.Save(group);
                }

                SpectrumSource source = session.QueryOver<SpectrumSource>().Where(o => o.Name == sourceName).SingleOrDefault();
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
                            SpectrumSourceGroup parentGroup = session.QueryOver<SpectrumSourceGroup>().Where(o => o.Name == parentGroupName).SingleOrDefault();
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

                Spectrum spectrum = session.QueryOver<Spectrum>().Where(o => o.Source.Id == source.Id &&
                                                                             o.Index == spectrumId - 1).SingleOrDefault();
                if (spectrum == null)
                {
                    spectrum = new Spectrum()
                    {
                        Index = spectrumId - 1,
                        NativeID = "scan=" + spectrumId,
                        Source = source
                    };
                    session.Save(spectrum);
                }

                Analysis analysis = session.QueryOver<Analysis>().Where(o => o.Name == analysisId).SingleOrDefault();
                if (analysis == null)
                {
                    int analysisCount = session.QueryOver<Analysis>().RowCount();
                    analysis = new Analysis()
                    {
                        Name = analysisId,
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
                foreach (string tuple in peptideTuples.Split(' '))
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

                        Peptide peptide = addOrGetPeptide(pwizPeptide.sequence);

                        var psm = new PeptideSpectrumMatch()
                        {
                            Peptide = peptide,
                            Spectrum = spectrum,
                            Analysis = analysis,
                            Charge = peptideTuple.Charge,
                            Rank = (peptideTuple.ScoreDivider == lastDivider ? rank : ++rank),
                            QValue = row.QValue,
                            Scores = new Dictionary<string, double>()
                        {
                            {"score1", score / peptideTuple.ScoreDivider},
                            {"score2", 1 / (score / peptideTuple.ScoreDivider)}
                        }
                        };
                        session.Save(psm);
                        lastDivider = peptideTuple.ScoreDivider;

                        // add PeptideModifications and Modifications
                        foreach (KeyValuePair<int, ModList> itr in pwizPeptide.modifications())
                        {
                            foreach (PwizMod pwizMod in itr.Value)
                            {
                                Modification mod = session.QueryOver<Modification>().Where(o => o.Formula == pwizMod.formula()).SingleOrDefault();
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

        public TestModel ()
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
        public void TestExampleData ()
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
            List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
            {
                //                 Group    Source  Spectrum Analysis     Score      List of Peptide@Charge/ScoreDivider
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

            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(":memory:", true, false))
            {
                session = sessionFactory.OpenSession();
            }

            session.Transaction.Begin();
            createTestProteins(testProteinSequences);
            createTestData(testPsmSummary);
            session.Transaction.Commit();

            // clear session so objects are loaded from database
            session.Clear();

            #region Overall count tests
            Assert.AreEqual(6, session.QueryOver<Protein>().RowCount());
            Assert.AreEqual(12, session.QueryOver<Peptide>().RowCount());
            Assert.AreEqual(30, session.QueryOver<PeptideInstance>().RowCount());
            Assert.AreEqual(90, session.QueryOver<PeptideSpectrumMatch>().RowCount());
            Assert.AreEqual(180, session.CreateSQLQuery("SELECT COUNT(*) FROM PeptideSpectrumMatchScores").UniqueResult<long>());
            Assert.AreEqual(2, session.QueryOver<Analysis>().RowCount());
            Assert.AreEqual(2, session.QueryOver<AnalysisParameter>().RowCount());
            Assert.AreEqual(15, session.QueryOver<Spectrum>().RowCount());
            Assert.AreEqual(5, session.QueryOver<SpectrumSource>().RowCount());
            Assert.AreEqual(7, session.QueryOver<SpectrumSourceGroup>().RowCount());
            Assert.AreEqual(15, session.QueryOver<SpectrumSourceGroupLink>().RowCount());
            #endregion

            #region Sanity tests
            for (long i = 1; i <= testProteinSequences.LongLength; ++i)
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
            #endregion

            #region Protein-centric tests
            Protein pro1 = session.Get<Protein>(1L);
            Assert.AreEqual(3, pro1.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDE"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDEK"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDER"));
            Assert.AreEqual(3, pro1.Peptides.Count(o => o.Peptide.Sequence == "TIDE"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "TIDEK"));
            Assert.AreEqual(1, pro1.Peptides.Count(o => o.Peptide.Sequence == "TIDER"));
            Assert.AreEqual(0, pro1.Peptides.Count(o => o.Peptide.Sequence == "EDIT"));
            Assert.AreEqual(0, pro1.Peptides.Count(o => o.Peptide.Sequence == "EPPIER"));

            Protein pro5 = session.Get<Protein>(5L);
            Assert.AreEqual(2, pro5.Peptides.Count(o => o.Peptide.Sequence == "EDIT"));
            Assert.AreEqual(1, pro5.Peptides.Count(o => o.Peptide.Sequence == "EDITPEPK"));
            Assert.AreEqual(0, pro5.Peptides.Count(o => o.Peptide.Sequence == "EDITPEPR"));
            Assert.AreEqual(0, pro5.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDE"));
            Assert.AreEqual(0, pro5.Peptides.Count(o => o.Peptide.Sequence == "EPPIER"));

            Protein pro6 = session.Get<Protein>(6L);
            Assert.AreEqual(1, pro6.Peptides.Count(o => o.Peptide.Sequence == "EPPIER"));
            Assert.AreEqual(1, pro6.Peptides.Count(o => o.Peptide.Sequence == "PETPDETK"));
            Assert.AreEqual(0, pro6.Peptides.Count(o => o.Peptide.Sequence == "PEPTIDE"));
            Assert.AreEqual(0, pro6.Peptides.Count(o => o.Peptide.Sequence == "EDIT"));
            #endregion

            #region Peptide-centric tests
            Peptide pep1 = session.Get<Peptide>(1L);
            Assert.AreEqual("PEPTIDE", pep1.Sequence);
            Assert.AreEqual(4, pep1.Instances.Count);
            Assert.AreEqual(10, pep1.Matches.Count);

            Peptide pep2 = session.Get<Peptide>(2L);
            Assert.AreEqual("TIDERPEPTIDEK", pep2.Sequence);
            Assert.AreEqual(2, pep2.Instances.Count);
            Assert.AreEqual(6, pep2.Matches.Count);

            Peptide pep3 = session.Get<Peptide>(3L);
            Assert.AreEqual("EPPIER", pep3.Sequence);
            Assert.AreEqual(1, pep3.Instances.Count);
            Assert.AreEqual(6, pep3.Matches.Count);

            Peptide pep4 = session.Get<Peptide>(4L);
            Assert.AreEqual("PEPTIDER", pep4.Sequence);
            Assert.AreEqual(1, pep4.Instances.Count);
            Assert.AreEqual(4, pep4.Matches.Count);

            Peptide pep7 = session.Get<Peptide>(7L);
            Assert.AreEqual("PEPTIDEK", pep7.Sequence);
            Assert.AreEqual(2, pep7.Instances.Count);
            Assert.AreEqual(14, pep7.Matches.Count);

            Peptide pep11 = session.Get<Peptide>(11L);
            Assert.AreEqual("TIDE", pep11.Sequence);
            Assert.AreEqual(7, pep11.Instances.Count);
            Assert.AreEqual(6, pep11.Matches.Count);
            #endregion

            #region PeptideInstance-centric tests
            PeptideInstance pi1 = session.Get<PeptideInstance>(1L);
            Assert.AreEqual(pep1, pi1.Peptide);
            Assert.AreEqual(pro1, pi1.Protein);
            Assert.AreEqual(1, pi1.SpecificTermini);
            Assert.AreEqual(true, pi1.NTerminusIsSpecific);
            Assert.AreEqual(false, pi1.CTerminusIsSpecific);
            Assert.AreEqual(0, pi1.Offset);
            Assert.AreEqual(7, pi1.Length);
            Assert.AreEqual(0, pi1.MissedCleavages);

            PeptideInstance pi5 = session.Get<PeptideInstance>(5L);
            Assert.AreEqual(pep2, pi5.Peptide);
            Assert.AreEqual(pro1, pi5.Protein);
            Assert.AreEqual(1, pi5.SpecificTermini);
            Assert.AreEqual(false, pi5.NTerminusIsSpecific);
            Assert.AreEqual(true, pi5.CTerminusIsSpecific);
            Assert.AreEqual(3, pi5.Offset);
            Assert.AreEqual(13, pi5.Length);
            Assert.AreEqual(1, pi5.MissedCleavages);
            #endregion

            #region SpectrumSourceGroup-centric tests
            SpectrumSourceGroup ssg3 = session.Get<SpectrumSourceGroup>(3L);
            Assert.AreEqual("/", ssg3.Name);
            Assert.AreEqual(5, ssg3.Sources.Count);
            Assert.AreEqual(0, ssg3.GetGroupDepth());

            SpectrumSourceGroup ssg2 = session.Get<SpectrumSourceGroup>(2L);
            Assert.AreEqual("/A", ssg2.Name);
            Assert.AreEqual(3, ssg2.Sources.Count);
            Assert.AreEqual(1, ssg2.GetGroupDepth());
            Assert.IsTrue(ssg2.IsChildOf(ssg3));
            Assert.IsTrue(ssg2.IsImmediateChildOf(ssg3));

            SpectrumSourceGroup ssg1 = session.Get<SpectrumSourceGroup>(1L);
            Assert.AreEqual("/A/1", ssg1.Name);
            Assert.AreEqual(2, ssg1.Sources.Count);
            Assert.AreEqual(2, ssg1.GetGroupDepth());
            Assert.IsTrue(ssg1.IsChildOf(ssg3));
            Assert.IsFalse(ssg1.IsImmediateChildOf(ssg3));
            Assert.IsTrue(ssg1.IsChildOf(ssg2));
            Assert.IsTrue(ssg1.IsImmediateChildOf(ssg2));

            SpectrumSourceGroup ssg5 = session.Get<SpectrumSourceGroup>(5L);
            Assert.AreEqual("/B/1", ssg5.Name);
            Assert.AreEqual(1, ssg5.Sources.Count);
            Assert.AreEqual(2, ssg5.GetGroupDepth());
            Assert.IsFalse(ssg5.IsChildOf(ssg2));
            Assert.IsTrue(ssg5.IsChildOf(ssg3));
            Assert.IsFalse(ssg5.IsImmediateChildOf(ssg3));
            #endregion

            #region SpectrumSource-centric tests
            SpectrumSource ss1 = session.Get<SpectrumSource>(1L);
            Assert.AreEqual("Source 1", ss1.Name);
            Assert.AreEqual(ssg1, ss1.Group);
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group == ssg1));
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group == ssg2));
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group == ssg3));
            Assert.IsNull(ss1.Groups.SingleOrDefault(o => o.Group == ssg5));

            SpectrumSource ss4 = session.Get<SpectrumSource>(4L);
            Assert.AreEqual("Source 4", ss4.Name);
            Assert.AreEqual(ssg5, ss4.Group);
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group == ssg5));
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group == ssg3));
            Assert.IsNull(ss4.Groups.SingleOrDefault(o => o.Group == ssg1));
            #endregion

            #region Spectrum-centric tests
            Spectrum ss1s1 = ss1.Spectra[0];
            Assert.AreEqual(ss1, ss1s1.Source);
            Assert.AreEqual(0, ss1s1.Index);
            Assert.AreEqual("scan=1", ss1s1.NativeID);
            Assert.AreEqual(6, ss1s1.Matches.Count);

            Spectrum ss1s2 = ss1.Spectra[1];
            Assert.AreEqual(ss1, ss1s2.Source);
            Assert.AreEqual(1, ss1s2.Index);
            Assert.AreEqual("scan=2", ss1s2.NativeID);
            Assert.AreEqual(6, ss1s2.Matches.Count);

            Spectrum ss4s1 = ss4.Spectra[0];
            Assert.AreEqual(ss4, ss4s1.Source);
            Assert.AreEqual(0, ss4s1.Index);
            Assert.AreEqual("scan=1", ss4s1.NativeID);
            Assert.AreEqual(6, ss4s1.Matches.Count);
            #endregion

            #region Analysis-centric tests
            Analysis a1 = session.Get<Analysis>(1L);
            Assert.AreEqual("Engine 1", a1.Name);
            Assert.AreEqual("Engine 1", a1.Software.Name);
            Assert.AreEqual("1.0", a1.Software.Version);
            Assert.AreEqual(45, a1.Matches.Count);

            Analysis a2 = session.Get<Analysis>(2L);
            Assert.AreEqual("Engine 2", a2.Name);
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
            #endregion

            #region PSM-centric tests
            PeptideSpectrumMatch ss1s1psm1 = ss1s1.Matches[0];
            Assert.AreEqual(pep1, ss1s1psm1.Peptide);
            Assert.AreEqual(ss1s1, ss1s1psm1.Spectrum);
            Assert.AreEqual(a1, ss1s1psm1.Analysis);
            Assert.AreEqual(ss1s1psm1, a1.Matches[0]);
            Assert.AreEqual(2, ss1s1psm1.Charge);
            Assert.AreEqual(1, ss1s1psm1.Rank);
            Assert.AreEqual(12.0, ss1s1psm1.Scores["score1"], 1e-12);
            Assert.AreEqual(1 / 12.0, ss1s1psm1.Scores["score2"], 1e-12);

            PeptideSpectrumMatch ss1s1psm1e2 = ss1s1.Matches[3];
            Assert.AreEqual(pep2, ss1s1psm1e2.Peptide);
            Assert.AreEqual(ss1s1, ss1s1psm1e2.Spectrum);
            Assert.AreEqual(a2, ss1s1psm1e2.Analysis);
            Assert.AreEqual(ss1s1psm1e2, a2.Matches[0]);
            Assert.AreEqual(4, ss1s1psm1e2.Charge);
            Assert.AreEqual(1, ss1s1psm1e2.Rank);
            Assert.AreEqual(120.0, ss1s1psm1e2.Scores["score1"], 1e-12);
            Assert.AreEqual(1 / 120.0, ss1s1psm1e2.Scores["score2"], 1e-12);

            PeptideSpectrumMatch ss1s1psm2 = ss1s1.Matches[1];
            Assert.AreEqual(pep2, ss1s1psm2.Peptide);
            Assert.AreEqual(ss1s1, ss1s1psm2.Spectrum);
            Assert.AreEqual(a1, ss1s1psm1.Analysis);
            Assert.AreEqual(4, ss1s1psm2.Charge);
            Assert.AreEqual(2, ss1s1psm2.Rank);
            Assert.AreEqual(12.0 / 2, ss1s1psm2.Scores["score1"], 1e-12);
            Assert.AreEqual(1 / (12.0 / 2), ss1s1psm2.Scores["score2"], 1e-12);

            PeptideSpectrumMatch ss1s1psm3 = ss1s1.Matches[2];
            Assert.AreEqual(pep3, ss1s1psm3.Peptide);
            Assert.AreEqual(ss1s1, ss1s1psm3.Spectrum);
            Assert.AreEqual(a1, ss1s1psm3.Analysis);
            Assert.AreEqual(1, ss1s1psm3.Charge);
            Assert.AreEqual(3, ss1s1psm3.Rank);
            Assert.AreEqual(12.0 / 3, ss1s1psm3.Scores["score1"], 1e-12);
            Assert.AreEqual(1 / (12.0 / 3), ss1s1psm3.Scores["score2"], 1e-12);

            PeptideSpectrumMatch ss4s1psm1 = ss4s1.Matches[0];
            Assert.AreEqual(pep2, ss4s1psm1.Peptide);
            Assert.AreEqual(ss4s1, ss4s1psm1.Spectrum);
            Assert.AreEqual(a1, ss4s1psm1.Analysis);
            Assert.AreEqual(4, ss4s1psm1.Charge);
            Assert.AreEqual(1, ss4s1psm1.Rank);

            Assert.AreEqual(30, session.QueryOver<PeptideSpectrumMatch>().Where(o => o.Rank == 1).RowCount());
            Assert.AreEqual(30, session.QueryOver<PeptideSpectrumMatch>().Where(o => o.Rank == 2).RowCount());
            Assert.AreEqual(30, session.QueryOver<PeptideSpectrumMatch>().Where(o => o.Rank == 3).RowCount());

            Assert.AreEqual(18, session.QueryOver<PeptideSpectrumMatch>().Where(o => o.Charge == 1).RowCount());
            Assert.AreEqual(39, session.QueryOver<PeptideSpectrumMatch>().Where(o => o.Charge == 2).RowCount());
            Assert.AreEqual(6, session.QueryOver<PeptideSpectrumMatch>().Where(o => o.Charge == 4).RowCount());
            #endregion

            #region Modification-centric tests
            Modification mod1 = session.Get<Modification>(1L);
            Assert.AreEqual("C2H2O1", mod1.Formula);
            Assert.AreEqual(mod1.Formula, mod1.Name);
            Assert.AreEqual(new Formula(mod1.Formula).monoisotopicMass(), mod1.MonoMassDelta);
            Assert.AreEqual(new Formula(mod1.Formula).molecularWeight(), mod1.AvgMassDelta);

            Modification mod2 = session.Get<Modification>(2L);
            Assert.AreEqual("H-2O-1", mod2.Formula);
            Assert.AreEqual(mod2.Formula, mod2.Name);
            Assert.AreEqual(new Formula(mod2.Formula).monoisotopicMass(), mod2.MonoMassDelta);
            Assert.AreEqual(new Formula(mod2.Formula).molecularWeight(), mod2.AvgMassDelta);

            Modification mod3 = session.Get<Modification>(3L);
            Assert.AreEqual("O4P1", mod3.Formula);
            Assert.AreEqual(mod3.Name, mod3.Name);
            Assert.AreEqual(new Formula(mod3.Formula).monoisotopicMass(), mod3.MonoMassDelta);
            Assert.AreEqual(new Formula(mod3.Formula).molecularWeight(), mod3.AvgMassDelta);

            // [C2H2O1]PEPTIDE
            PeptideModification pm1 = session.Get<PeptideModification>(1L);
            Assert.AreEqual(mod1, pm1.Modification);
            Assert.AreEqual(1, pm1.PeptideSpectrumMatch.Id);
            Assert.IsTrue(pm1.PeptideSpectrumMatch.Modifications.Contains(pm1));
            Assert.AreEqual(int.MinValue, pm1.Offset);
            Assert.AreEqual('(', pm1.Site);

            // E[H-2O-1]DIT[P1O4]PEPR
            PeptideModification pm2 = session.Get<PeptideModification>(2L);
            Assert.AreEqual(mod2, pm2.Modification);
            Assert.AreEqual(11, pm2.PeptideSpectrumMatch.Id);
            Assert.IsTrue(pm2.PeptideSpectrumMatch.Modifications.Contains(pm2));
            Assert.AreEqual(1, pm2.Offset);
            Assert.AreEqual('E', pm2.Site);

            PeptideModification pm3 = session.Get<PeptideModification>(3L);
            Assert.AreEqual(mod3, pm3.Modification);
            Assert.AreEqual(11, pm3.PeptideSpectrumMatch.Id);
            Assert.IsTrue(pm3.PeptideSpectrumMatch.Modifications.Contains(pm3));
            Assert.AreEqual(4, pm3.Offset);
            Assert.AreEqual('T', pm3.Site);

            // PEPT[P1O4]IDEK
            PeptideModification pm4 = session.Get<PeptideModification>(4L);
            Assert.AreEqual(mod3, pm4.Modification);
            Assert.AreEqual(16, pm4.PeptideSpectrumMatch.Id);
            Assert.IsTrue(pm4.PeptideSpectrumMatch.Modifications.Contains(pm4));
            Assert.AreEqual(4, pm4.Offset);
            Assert.AreEqual('T', pm4.Site);
            #endregion
        }

        [TestMethod]
        public void TestStaticQonversion ()
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

            string decoyPrefix = "r-";

            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory("testStaticQonversion.idpDB", true, false))
            {
                session = sessionFactory.OpenSession();
            }

            session.Transaction.Begin();
            createTestProteins(testProteinSequences);

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

            for (int engine = 1; engine <= 2; ++engine)
            for (int source = 1; source <= 2; ++source)
            for (int charge = 1; charge <= 2; ++charge)
            {
                string sourceName = "Source " + source.ToString();
                string engineName = "Engine " + engine.ToString();

                List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
                {
                    //               Group    Source   Spectrum  Analysis     Score      List of Peptide@Charge/ScoreDivider
                    new SpectrumTuple("/",  sourceName,     5,  engineName,     99, 1, String.Format("TIDERPEPTIDEK@{0}/1 PEPTIDER@{0}/8", charge)),
                    new SpectrumTuple("/",  sourceName,     11, engineName,     95, 1, String.Format("TIDERPEPTIDEK@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     12, engineName,     90, 1, String.Format("PEPTIDEKPEP@{0}/1 THEQUICKBR@{0}/4", charge)),
                    new SpectrumTuple("/",  sourceName,     16, engineName,     85, 1, String.Format("EDITPEP@{0}/1 PEPTIDEK@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     15, engineName,     80, 1, String.Format("EDITPEPR@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     17, engineName,     75, 1, String.Format("TIDER@{0}/1 TIDERPEPTIDEK@{0}/3", charge)),
                    new SpectrumTuple("/",  sourceName,     14, engineName,     70, 1, String.Format("TIDER@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     2,  engineName,     65, 1, String.Format("THEQUICKBR@{0}/1 BKCIUQEHT@{0}/1 PEPTIDER@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     7,  engineName,     60, 1, String.Format("KEDITPEPR@{0}/1 DERPEPTIDEK@{0}/4", charge)),
                    new SpectrumTuple("/",  sourceName,     4,  engineName,     50, 1, String.Format("THELZYDG@{0}/1 PEPTI@{0}/3", charge)),
                    new SpectrumTuple("/",  sourceName,     10, engineName,     55, 1, String.Format("DERPEPTIDEK@{0}/1 PEPTIDEK@{0}/5", charge)),
                    new SpectrumTuple("/",  sourceName,     8,  engineName,     45, 1, String.Format("PEPTI@{0}/1 PEPTIDER@{0}/6", charge)),
                    new SpectrumTuple("/",  sourceName,     9,  engineName,     40, 1, String.Format("PEPTIDEK@{0}/1 THEQUICKBR@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     3,  engineName,     35, 1, String.Format("PEPKEDITPEPR@{0}/1 THELZYDG@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     1,  engineName,     30, 1, String.Format("GDYZLEHT@{0}/1 THEQUICKBR@{0}/2", charge)),
                    new SpectrumTuple("/",  sourceName,     13, engineName,     25, 1, String.Format("PEPTIDER@{0}/1 PEPKEDITPEPR@{0}/1 BKCIUQEHT@{0}/3", charge)),
                    new SpectrumTuple("/",  sourceName,     6,  engineName,     20, 1, String.Format("THEQUICKBR@{0}/1 DERPEPTIDEK@{0}/4", charge)),
                };

                createTestData(testPsmSummary);
            }
            session.Transaction.Commit();
            #endregion

            var qonverter = new IDPicker.StaticWeightQonverter()
            {
                DecoyPrefix = decoyPrefix,
                ScoreWeights = new Dictionary<string, double>() { { "score1", 1 }, { "score2", 0 } }
            };
            qonverter.Qonvert("testStaticQonversion.idpDB");

            // clear session so qonverted objects are loaded from database
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
                                                             "WHERE psm.Analysis.id = ? AND " +
                                                             "      psm.Spectrum.Source.id = ? AND " +
                                                             "      psm.Spectrum.Index = ? AND " +
                                                             "      psm.Charge = ?) " +
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
            #endregion
        }

        /*[TestMethod]
        public void TestCalculateAdditionalPeptides ()
        {
            var dataFilter = new DataFilter_Accessor();
            Map<long, long> additionalPeptidesByProteinId = dataFilter.calculateAdditionalPeptides(session);
        }*/
    }
}
