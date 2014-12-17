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
using System.Data;
using System.Linq;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using IDPicker;
using IDPicker.DataModel;
using NHibernate;
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
    using TestProtein = Protein;
    using TestPI = PeptideInstance;
    using TestPSM = PeptideSpectrumMatch;
    using TestMod = Modification;
    using TestPM = PeptideModification;
    using TestSpectrum = Spectrum;
    using TestSource = SpectrumSource;
    using TestGroup = SpectrumSourceGroup;
    using TestGL = SpectrumSourceGroupLink;

    public class TestPeptide : Peptide { public TestPeptide(string sequence) : base(sequence) { } };

    /// <summary>
    /// Summary description for TestModel
    /// </summary>
    [TestClass]
    public class TestModel
    {
        #region Public static methods for easily populating an IDPicker session
        public static void CreateTestProteins(ISession session, IList<string> testProteinSequences)
        {
            var bulkInserter = new BulkInserter(session.Connection);

            for (int i = 0; i < testProteinSequences.Count; ++i)
            {
                int id = i + 1;
                var protein = new TestProtein()
                {
                    Id = id,
                    Accession = "PRO" + id.ToString(),
                    Description = "Protein " + id.ToString(),
                    Sequence = testProteinSequences[i]
                };
                var proteinAccessor = new PrivateObject(protein);
                proteinAccessor.SetProperty("Length", testProteinSequences[i].Length);
                bulkInserter.Add(protein);
            }

            bulkInserter.Execute();
            bulkInserter.Reset("");
        }

        public struct PeptideTuple
        {
            public string Sequence { get; set; }
            public int Charge { get; set; }
            public int ScoreDivider { get; set; }
        }

        public class SpectrumTuple
        {
            public SpectrumTuple (string group, int source, int spectrum, int analysis, int? score, double qvalue, string peptideTuples)
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
            public int Source { get; set; }
            public int Spectrum { get; set; }
            public int Analysis { get; set; }
            public int? Score { get; set; }
            public double QValue { get; set; }
            public string PeptideTuples { get; set; }
        }

        public static void CreateTestData (NHibernate.ISession session, IList<SpectrumTuple> testPsmSummary)
        {
            var dbGroups = new Map<string, SpectrumSourceGroup>();
            foreach (var ssg in session.Query<SpectrumSourceGroup>())
                dbGroups[ssg.Name] = ssg;

            var dbSources = new Map<long, SpectrumSource>();
            foreach (var ss in session.Query<SpectrumSource>())
                dbSources[ss.Id.Value] = ss;

            var dbAnalyses = new Map<long, Analysis>();
            foreach (var a in session.Query<Analysis>())
                dbAnalyses[a.Id.Value] = a;

            var dbPeptides = new Map<string, Peptide>();
            foreach (var pep in session.Query<Peptide>())
                dbPeptides[pep.Sequence] = pep;

            var bulkInserter = new BulkInserter(session.Connection);

            long lastPsmId = session.CreateQuery("SELECT MAX(Id) FROM PeptideSpectrumMatch").UniqueResult<long?>().GetValueOrDefault();
            long lastModId = session.CreateQuery("SELECT MAX(Id) FROM Modification").UniqueResult<long?>().GetValueOrDefault();
            long lastPmId = session.CreateQuery("SELECT MAX(Id) FROM PeptideModification").UniqueResult<long?>().GetValueOrDefault();
            long lastGroupId = session.CreateQuery("SELECT MAX(Id) FROM SpectrumSourceGroup").UniqueResult<long?>().GetValueOrDefault();
            long lastSourceId = session.CreateQuery("SELECT MAX(Id) FROM SpectrumSource").UniqueResult<long?>().GetValueOrDefault();
            long lastSglId = session.CreateQuery("SELECT MAX(Id) FROM SpectrumSourceGroupLink").UniqueResult<long?>().GetValueOrDefault();

            foreach (SpectrumTuple row in testPsmSummary)
            {
                string groupName = row.Group;
                string sourceName = "Source " + row.Source;
                string analysisId = "Engine " + row.Analysis;
                string peptideTuples = row.PeptideTuples;

                SpectrumSourceGroup group = dbGroups[groupName];
                if (String.IsNullOrEmpty(group.Name))
                {
                    group.Id = ++lastGroupId;
                    group.Name = groupName;
                    bulkInserter.Add(group);
                }

                SpectrumSource source = dbSources[row.Source];
                if (String.IsNullOrEmpty(source.Name))
                {
                    source.Id = ++lastSourceId;
                    source.Name = sourceName;
                    source.Group = group;
                    source.Spectra = new List<Spectrum>();
                    bulkInserter.Add(source);

                    // add a source group link for the source's immediate group
                    bulkInserter.Add(new SpectrumSourceGroupLink() { Id = ++lastSglId, Group = group, Source = source });

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
                                parentGroup = new SpectrumSourceGroup() { Id = ++lastGroupId, Name = parentGroupName };
                                bulkInserter.Add(parentGroup);
                            }

                            bulkInserter.Add(new SpectrumSourceGroupLink() { Id = ++lastSglId, Group = parentGroup, Source = source });

                            if (parentGroupName == "/")
                                break;
                            parentGroupName = parentGroupName.Substring(0, parentGroupName.LastIndexOf("/"));
                        }
                    }

                    #endregion
                }

                Spectrum spectrum = source.Spectra.SingleOrDefault(o => o.Source.Id == source.Id &&
                                                                        o.Index == row.Spectrum - 1);
                if (spectrum == null)
                {
                    spectrum = new Spectrum()
                                   {
                                       Id = source.Id * 10000 + row.Spectrum,
                                       Index = row.Spectrum - 1,
                                       NativeID = "scan=" + row.Spectrum,
                                       Source = source,
                                       PrecursorMZ = 42
                                   };
                    source.Spectra.Add(spectrum);
                    bulkInserter.Add(spectrum);
                }

                Analysis analysis = dbAnalyses[row.Analysis];
                if (String.IsNullOrEmpty(analysis.Name))
                {
                    analysis.Id = dbAnalyses.Max(o => o.Value.Id).GetValueOrDefault() + 1;
                    analysis.Name = analysisId + " 1.0";
                    analysis.Software = new AnalysisSoftware() {Name = analysisId, Version = "1.0"};
                    analysis.StartTime = DateTime.Today.AddHours(row.Analysis);
                    analysis.Type = AnalysisType.DatabaseSearch;

                    analysis.Parameters = new Iesi.Collections.Generic.SortedSet<AnalysisParameter>()
                    {
                        new AnalysisParameter()
                        {
                            Id = analysis.Id * 10000,
                            Analysis = analysis,
                            Name = "Parameter 1",
                            Value = "Value 1"
                        }
                    };

                    bulkInserter.Add(analysis);
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
                        using (PwizPeptide pwizPeptide = new PwizPeptide(peptideTuple.Sequence, ModParsing.ModificationParsing_Auto, ModDelimiter.ModificationDelimiter_Brackets))
                        {
                            Peptide peptide = dbPeptides[pwizPeptide.sequence];
                            if (String.IsNullOrEmpty(peptide.Sequence))
                            {
                                peptide = new TestPeptide(pwizPeptide.sequence);
                                peptide.Id = dbPeptides.Max(o => o.Value.Id).GetValueOrDefault() + 1;
                                peptide.MonoisotopicMass = pwizPeptide.monoisotopicMass(false);
                                peptide.MolecularWeight = pwizPeptide.molecularWeight(false);
                                dbPeptides[pwizPeptide.sequence] = peptide;
                                bulkInserter.Add(peptide);
                                createTestPeptideInstances(session, bulkInserter, peptide);
                            }

                            double neutralPrecursorMass = (spectrum.PrecursorMZ*peptideTuple.Charge) - (peptideTuple.Charge*Proton.Mass);

                            var psm = new PeptideSpectrumMatch()
                                          {
                                              Id = ++lastPsmId,
                                              Peptide = peptide,
                                              Spectrum = spectrum,
                                              Analysis = analysis,
                                              ObservedNeutralMass = neutralPrecursorMass,
                                              MonoisotopicMassError = neutralPrecursorMass - pwizPeptide.monoisotopicMass(),
                                              MolecularWeightError = neutralPrecursorMass - pwizPeptide.molecularWeight(),
                                              Charge = peptideTuple.Charge,
                                              Rank = (peptideTuple.ScoreDivider == lastDivider ? rank : ++rank),
                                              QValue = (rank == 1 ? row.QValue : PeptideSpectrumMatch.DefaultQValue),
                                          };

                            if (row.Score != null)
                                psm.Scores = new Dictionary<string, double>()
                                                 {
                                                     {"score1", (double) row.Score/peptideTuple.ScoreDivider},
                                                     {"score2", 1/((double) row.Score/peptideTuple.ScoreDivider)}
                                                 };

                            bulkInserter.Add(psm);
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
                                                      Id = ++lastModId,
                                                      Formula = pwizMod.formula(),
                                                      MonoMassDelta = pwizMod.monoisotopicDeltaMass(),
                                                      AvgMassDelta = pwizMod.averageDeltaMass(),
                                                      Name = pwizMod.formula()
                                                  };
                                        bulkInserter.Add(mod);
                                    }

                                    bulkInserter.Add(new PeptideModification()
                                                         {
                                                             Id = ++lastPmId,
                                                             PeptideSpectrumMatch = psm,
                                                             Modification = mod,
                                                             Offset = itr.Key == ModMap.NTerminus() ? int.MinValue
                                                                    : itr.Key == ModMap.CTerminus() ? int.MaxValue
                                                                    : itr.Key
                                                         });
                                }
                            }
                        }
                    }
            }
            bulkInserter.Execute();
            bulkInserter.Reset("");
        }

        public static void AddSubsetPeakData (NHibernate.ISession session)
        {
            session.Transaction.Begin();
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
            session.Transaction.Commit();
        }
        #endregion

        #region Private methods
        private static void createTestPeptideInstances (NHibernate.ISession session, BulkInserter bulkInserter, Peptide pep)
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
                    };

                    var instanceAccessor = new PrivateObject(instance);
                    instanceAccessor.SetProperty("SpecificTermini", (nTerminusIsSpecific ? 1 : 0) + (cTerminusIsSpecific ? 1 : 0));

                    bulkInserter.Add(instance);
                    pep.Instances.Add(instance);

                    start = pro.Sequence.IndexOf(pep.Sequence, start + 1);
                }
            }

            if (pep.Instances.Count == 0)
                throw new ArgumentException("peptide " + pep.Sequence + " does not occur in any proteins");
        }
        #endregion

        // shared session between TestModel methods
        public NHibernate.ISession session;
        
        #region Example proteins
        static string[] testProteinSequences = new string[]
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
        static List<SpectrumTuple> testPsmSummary = new List<SpectrumTuple>()
            {
                //               Group Source Spectrum Analysis     Score  Q   List of Peptide@Charge/ScoreDivider
                new SpectrumTuple("/A/1", 1,   1,  1,      12, 0, "[C2H2O1]PEPTIDE@2/1 TIDERPEPTIDEK@4/2 EPPIER@1/3"),
                new SpectrumTuple("/A/1", 1,   2,  1,      23, 0, "PEPTIDER@2/1 PETPDETK@3/3 EDITPEPK@2/5"),
                new SpectrumTuple("/A/1", 1,   3,  1,      34, 0, "PEPTIDEK@2/1 TIDER@1/4 PETPDETK@2/8"),
                new SpectrumTuple("/A/1", 2,   1,  1,      43, 0, "PEPTIDE@2/1 E[H-2O-1]DIT[P1O4]PEPR@2/2 EPPIER@1/7"),
                new SpectrumTuple("/A/1", 2,   2,  1,      32, 0, "PEPTIDER@3/1 EDITPEPK@3/4 EDITPEPR@3/5"),
                new SpectrumTuple("/A/1", 2,   3,  1,      21, 0, "PEPT[P1O4]IDEK@3/1 TIDEK@1/7 PETPDETK@2/8"),
                new SpectrumTuple("/A/2", 3,   1,  1,      56, 0, "TIDEK@2/1 TIDE@1/2 P[P1O4]EPTIDE@3/3"),
                new SpectrumTuple("/A/2", 3,   2,  1,      45, 0, "TIDER@2/1 TIDERPEPTIDEK@4/3 PEPTIDEK@3/4"),
                new SpectrumTuple("/A/2", 3,   3,  1,      34, 0, "TIDE@1/1 PEPTIDEK@3/6 TIDEK@1/7"),
                new SpectrumTuple("/B/1", 4,   1,  1,      65, 0, "TIDERPEPTIDEK@4/1 PETPDETK@3/8 EDITPEPR@3/9"),
                new SpectrumTuple("/B/1", 4,   2,  1,      53, 0, "E[H-2O-1]DITPEPK@2/1 PEPTIDEK@3/2 PEPTIDE@2/3"),
                new SpectrumTuple("/B/1", 4,   3,  1,      42, 0, "EDIT@2/1 PEPTIDEK@3/3 EDITPEPR@2/4"),
                new SpectrumTuple("/B/2", 5,   1,  1,      20, 0, "EPPIER@2/1 TIDE@1/7 PEPTIDE@2/9"),
                new SpectrumTuple("/B/2", 5,   2,  1,      24, 0, "PETPDETK@2/1 PEPTIDEK@3/5 EDITPEPR@2/8"),
                new SpectrumTuple("/B/2", 5,   3,  1,      24, 0, "PETPDETK@3/1 EDIT@1/4 TIDER@2/6"),

                new SpectrumTuple("/A/1", 1,   1,  2,     120, 0, "TIDERPEPTIDEK@4/1 PEPTIDE@2/2 EPPIER@1/3"),
                new SpectrumTuple("/A/1", 1,   2,  2,     230, 0, "PEPTIDER@2/1 PETPDETK@3/3 EDITPEPK@2/5"),
                new SpectrumTuple("/A/1", 1,   3,  2,     340, 0, "PEPTIDEK@2/1 TIDER@1/4 PETPDETK@2/8"),
                new SpectrumTuple("/A/1", 2,   1,  2,     430, 0, "PEPTIDE@2/1 EDITPEPR@2/2 EPPIER@1/7"),
                new SpectrumTuple("/A/1", 2,   2,  2,     320, 0, "PEPTIDER@3/1 EDITPEPK@3/4 EDITPEPR@3/5"),
                new SpectrumTuple("/A/1", 2,   3,  2,     210, 0, "PEPT[P1O4]IDEK@3/1 TIDEK@1/7 PETPDETK@2/8"),
                new SpectrumTuple("/A/2", 3,   1,  2,     560, 0, "TIDEK@2/1 TIDE@1/2 PEPTIDE@3/3"),
                new SpectrumTuple("/A/2", 3,   2,  2,     450, 0, "TIDER@2/1 TIDERPEPTIDEK@4/3 PEPTIDEK@3/4"),
                new SpectrumTuple("/A/2", 3,   3,  2,     340, 0, "TIDE@1/1 PEPTIDEK@3/6 TIDEK@1/7"),
                new SpectrumTuple("/B/1", 4,   1,  2,     650, 0, "TIDERPEPTIDEK@4/1 PET[P1O4]PDETK@3/8 EDITPEPR@3/9"),
                new SpectrumTuple("/B/1", 4,   2,  2,     530, 0, "EDITPEPK@2/1 PEPTIDEK@3/2 PEPTIDE@2/3"),
                new SpectrumTuple("/B/1", 4,   3,  2,     420, 0, "EDIT@2/1 PEPTIDEK@3/3 EDITPEPR@2/4"),
                new SpectrumTuple("/B/2", 5,   1,  2,     200, 0, "E[H-2O-1]PPIER@2/1 TIDE@1/7 PEPTIDE@2/9"),
                new SpectrumTuple("/B/2", 5,   2,  2,     240, 0, "PEPTIDEK@2/1 PETPDETK@2/4 EDITPEPR@2/8"),
                new SpectrumTuple("/B/2", 5,   3,  2,     240, 0, "PETPDETK@3/1 EDIT@1/4 TIDER@2/6"),
            };
        #endregion

        public TestModel ()
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
            testContext.SetTestOutputSubdirectory(testContext.FullyQualifiedTestClassName + "\\" + testContext.TestName);
            Directory.CreateDirectory(testContext.TestOutputPath());
            string testModelFilepath = testContext.TestOutputPath("../testModel.idpDB");
            var sessionFactory = SessionFactoryFactory.CreateSessionFactory(testModelFilepath, new SessionFactoryConfig { CreateSchema = true });
            var session = sessionFactory.OpenSession();

            CreateTestProteins(session, testProteinSequences);
            CreateTestData(session, testPsmSummary);
            AddSubsetPeakData(session);

            var qonverterSettings1 = new QonverterSettings()
            {
                Analysis = session.UniqueResult<Analysis>(o => o.Software.Name == "Engine 1"),
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
                Analysis = session.UniqueResult<Analysis>(o => o.Software.Name == "Engine 2"),
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

            session.Save(qonverterSettings1);
            session.Save(qonverterSettings2);
            session.Flush();
                
            session.Close();
            sessionFactory.Close();
        }

        // Use TestInitialize to run code before running each test 
        [TestInitialize()]
        public void TestInitialize ()
        {
            TestContext.SetTestOutputSubdirectory(TestContext.FullyQualifiedTestClassName + "/" + TestContext.TestName);
            Directory.CreateDirectory(TestContext.TestOutputPath());
            File.Copy(TestContext.TestOutputPath("../testModel.idpDB"), TestContext.TestOutputPath("testModel.idpDB"));
            Directory.SetCurrentDirectory(TestContext.TestOutputPath());

            var sessionFactory = SessionFactoryFactory.CreateSessionFactory(TestContext.TestOutputPath("testModel.idpDB"));
            session = sessionFactory.OpenSession();
        }

        [TestCleanup()]
        public void TestCleanup ()
        {
            session.Close();
            session.SessionFactory.Close();
        }

        [TestMethod]
        public void TestAminoAcidSequence ()
        {
            Assert.AreEqual("A", new AminoAcidSequence("A").ToString());
            Assert.AreEqual("Z", new AminoAcidSequence("Z").ToString());
            Assert.AreEqual("AZ", new AminoAcidSequence("AZ").ToString());
            Assert.AreEqual("ABC", new AminoAcidSequence("ABC").ToString());
            Assert.AreEqual("ABCD", new AminoAcidSequence("ABCD").ToString());
            Assert.AreEqual("ZZZZZ", new AminoAcidSequence("ZZZZZ").ToString());
            Assert.AreEqual("RAZAMATAZ", new AminoAcidSequence("RAZAMATAZ").ToString());
        }

        [TestMethod]
        public void TestGhostEntities() // test that no entity type gets dirty simply by querying it
        {
            session.DefaultReadOnly = true;
            Assert.IsFalse(session.IsDirty());

            var pro1 = session.Get<Protein>(1L);
            Assert.IsFalse(session.IsDirty());

            var pep1 = session.Get<Peptide>(1L);
            Assert.IsFalse(session.IsDirty());

            var pi1 = session.Get<PeptideInstance>(1L);
            Assert.IsFalse(session.IsDirty());

            var psm1 = session.Get<PeptideSpectrumMatch>(1L);
            Assert.IsFalse(session.IsDirty());

            var a1 = session.Get<Analysis>(1L);
            Assert.IsFalse(session.IsDirty());

            var ap1 = session.Get<AnalysisParameter>(1L);
            Assert.IsFalse(session.IsDirty());

            var s1 = session.Get<Spectrum>(1L);
            Assert.IsFalse(session.IsDirty());

            var ss1 = session.Get<SpectrumSource>(1L);
            Assert.IsFalse(session.IsDirty());

            var ssg1 = session.Get<SpectrumSourceGroup>(1L);
            Assert.IsFalse(session.IsDirty());

            var ssgl1 = session.Get<SpectrumSourceGroupLink>(1L);
            Assert.IsFalse(session.IsDirty());
        }

        [TestMethod]
        public void TestOverallCounts ()
        {
            Assert.AreEqual(6, session.Query<Protein>().Count());
            Assert.AreEqual(12, session.Query<Peptide>().Count());
            Assert.AreEqual(30, session.Query<PeptideInstance>().Count());
            Assert.AreEqual(90, session.Query<PeptideSpectrumMatch>().Count());
            Assert.AreEqual(180, session.CreateSQLQuery("SELECT COUNT(*) FROM PeptideSpectrumMatchScore").UniqueResult<long>());
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
                Assert.AreEqual(0, pro.ProteinGroup);
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
        public void TestSpectrumSources() { TestSpectrumSources(true); }
        public void TestSpectrumSources(bool testMetadata)
        {
            SpectrumSource ss1 = session.UniqueResult<SpectrumSource>(o => o.Name == "Source 1");
            Assert.AreEqual(session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/A/1"), ss1.Group);
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/"));
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/A"));
            Assert.IsNotNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/A/1"));
            Assert.IsNull(ss1.Groups.SingleOrDefault(o => o.Group.Name == "/B"));
            if (testMetadata)
            {
                Assert.IsNotNull(ss1.Metadata);
                Assert.AreEqual(ss1.Name, ss1.Metadata.id);
                Assert.IsNotInstanceOfType(ss1.Metadata.run.spectrumList, typeof (msdata.SpectrumListSimple));
            }

            SpectrumSource ss4 = session.UniqueResult<SpectrumSource>(o => o.Name == "Source 4");
            Assert.AreEqual(session.UniqueResult<SpectrumSourceGroup>(o => o.Name == "/B/1"), ss4.Group);
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/"));
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/B"));
            Assert.IsNotNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/B/1"));
            Assert.IsNull(ss4.Groups.SingleOrDefault(o => o.Group.Name == "/A"));
            if (testMetadata)
            {
                Assert.IsNotNull(ss4.Metadata);
                Assert.AreEqual(ss4.Name, ss4.Metadata.id);
                Assert.IsNotInstanceOfType(ss4.Metadata.run.spectrumList, typeof (msdata.SpectrumListSimple));
            }
        }

        [TestMethod]
        public void TestSpectra() { TestSpectra(true); }
        public void TestSpectra(bool testMetadata)
        {
            Spectrum ss1s1 = session.UniqueResult<Spectrum>(o => o.Index == 0 &&
                                                                 o.Source.Name == "Source 1");
            Assert.AreEqual("scan=1", ss1s1.NativeID);
            Assert.AreEqual(6, ss1s1.Matches.Count);
            Assert.AreEqual(42, ss1s1.PrecursorMZ, 1e-12);
            if (testMetadata)
            {
                Assert.AreEqual(ss1s1.Index, ss1s1.Metadata.index);
                Assert.AreEqual(ss1s1.NativeID, ss1s1.Metadata.id);
                Assert.AreEqual(5UL, ss1s1.Metadata.defaultArrayLength);
                Assert.AreEqual(100, ss1s1.MetadataWithPeaks.getMZArray().data[0]);
            }

            Spectrum ss1s2 = session.UniqueResult<Spectrum>(o => o.Index == 1 &&
                                                                 o.Source.Name == "Source 1");
            Assert.AreEqual("scan=2", ss1s2.NativeID);
            Assert.AreEqual(6, ss1s2.Matches.Count);
            if (testMetadata)
            {
                Assert.AreEqual(ss1s2.Index, ss1s2.Metadata.index);
                Assert.AreEqual(ss1s2.NativeID, ss1s2.Metadata.id);
                Assert.AreEqual(5UL, ss1s2.Metadata.defaultArrayLength);
                Assert.AreEqual(200, ss1s2.MetadataWithPeaks.getMZArray().data[1]);
            }

            Spectrum ss4s1 = session.UniqueResult<Spectrum>(o => o.Index == 0 &&
                                                                 o.Source.Name == "Source 4");
            Assert.AreEqual("scan=1", ss4s1.NativeID);
            Assert.AreEqual(6, ss4s1.Matches.Count);
            Assert.AreEqual(42, ss4s1.PrecursorMZ, 1e-12);
            if (testMetadata)
            {
                Assert.AreEqual(ss4s1.Index, ss4s1.Metadata.index);
                Assert.AreEqual(ss4s1.NativeID, ss4s1.Metadata.id);
                Assert.AreEqual(5UL, ss4s1.Metadata.defaultArrayLength);
                Assert.AreEqual(300, ss4s1.MetadataWithPeaks.getMZArray().data[2]);
            }
        }

        [TestMethod]
        public void TestAnalyses()
        {
            Analysis a1 = session.UniqueResult<Analysis>(o => o.Software.Name == "Engine 1");
            Assert.AreEqual("Engine 1 1.0", a1.Name);
            Assert.AreEqual("1.0", a1.Software.Version);
            Assert.AreEqual(45, a1.Matches.Count);

            Analysis a2 = session.UniqueResult<Analysis>(o => o.Software.Name == "Engine 2");
            Assert.AreEqual("Engine 2 1.0", a2.Name);
            Assert.AreEqual("1.0", a2.Software.Version);
            Assert.AreEqual(45, a2.Matches.Count);

            AnalysisParameter ap1 = a1.Parameters.First();
            Assert.AreEqual(a1, ap1.Analysis);
            Assert.AreEqual(ap1, a1.Parameters.First());
            Assert.AreEqual("Parameter 1", ap1.Name);
            Assert.AreEqual("Value 1", ap1.Value);

            AnalysisParameter ap2 = a2.Parameters.First();
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
            Assert.AreEqual(pwizPeptide.monoisotopicMass(), ss1s1psm1.ObservedNeutralMass - ss1s1psm1.MonoisotopicMassError, 1e-12);
            Assert.AreEqual(pwizPeptide.molecularWeight(), ss1s1psm1.ObservedNeutralMass - ss1s1psm1.MolecularWeightError, 1e-12);
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
            Assert.AreEqual(pwizPeptide.monoisotopicMass(), ss1s1psm1e2.ObservedNeutralMass - ss1s1psm1e2.MonoisotopicMassError, 1e-12);
            Assert.AreEqual(pwizPeptide.molecularWeight(), ss1s1psm1e2.ObservedNeutralMass - ss1s1psm1e2.MolecularWeightError, 1e-12);
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
            var ss2s1psm2 = session.UniqueResult<PeptideSpectrumMatch>(o => o.Spectrum.Source.Name == "Source 2" &&
                                                                            o.Spectrum.Index == 0 &&
                                                                            o.Analysis.Id == a1.Id &&
                                                                            o.Rank == 2);
            pwizPeptide = new PwizPeptide("E(H-2O-1)DIT(P1O4)PEPR", proteome.ModificationParsing.ModificationParsing_ByFormula);
            Assert.AreEqual(session.UniqueResult<Peptide>(o => o.Sequence == "EDITPEPR"), ss2s1psm2.Peptide);
            Assert.AreEqual(2, ss2s1psm2.Modifications.Count);
            Assert.AreEqual("H-2O-1", ss2s1psm2.Modifications[0].Modification.Formula);
            Assert.AreEqual(0, ss2s1psm2.Modifications[0].Offset);
            Assert.AreEqual("O4P1", ss2s1psm2.Modifications[1].Modification.Formula);
            Assert.AreEqual(3, ss2s1psm2.Modifications[1].Offset);
            Assert.AreEqual(pwizPeptide.monoisotopicMass(), ss2s1psm2.ObservedNeutralMass - ss2s1psm2.MonoisotopicMassError, 1e-12);
            Assert.AreEqual(pwizPeptide.molecularWeight(), ss2s1psm2.ObservedNeutralMass - ss2s1psm2.MolecularWeightError, 1e-12);

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
                                                                                     o.Offset == 0);
            Assert.AreEqual(mod2, pm2.Modification);
            Assert.IsTrue(pm2.PeptideSpectrumMatch.Modifications.Contains(pm2));
            Assert.AreEqual(0, pm2.Offset);
            Assert.AreEqual('E', pm2.Site);

            PeptideModification pm3 = pm2.PeptideSpectrumMatch.Modifications.Where(o => o.Offset == 3).Single();
            Assert.AreEqual(mod3, pm3.Modification);
            Assert.IsTrue(pm3.PeptideSpectrumMatch.Modifications.Contains(pm3));
            Assert.AreEqual('T', pm3.Site);

            // PEPT[P1O4]IDEK (2 PSMs)
            foreach (var pm4 in session.Query<PeptideModification>().Where(o => o.PeptideSpectrumMatch.Peptide.Sequence == "PEPTIDEK" &&
                                                                                o.Modification == mod3 &&
                                                                                o.Offset == 3))
            {
                Assert.IsTrue(pm4.PeptideSpectrumMatch.Modifications.Contains(pm4));
                Assert.AreEqual('T', pm4.Site);
            }
        }

        [TestMethod]
        public void TestQonverterSettings ()
        {
            Assert.AreEqual(2, session.Query<QonverterSettings>().Count());

            var qonverterSettings1 = session.UniqueResult<QonverterSettings>(o => o.Analysis.Software.Name == "Engine 1");
            Assert.AreEqual(Qonverter.QonverterMethod.StaticWeighted, qonverterSettings1.QonverterMethod);
            Assert.AreEqual("quiRKy", qonverterSettings1.DecoyPrefix);
            Assert.AreEqual(true, qonverterSettings1.RerankMatches);
            Assert.AreEqual(2, qonverterSettings1.ScoreInfoByName.Count);
            Assert.AreEqual(1, qonverterSettings1.ScoreInfoByName["score1"].Weight);
            Assert.AreEqual(Qonverter.Settings.Order.Ascending, qonverterSettings1.ScoreInfoByName["score1"].Order);
            Assert.AreEqual(Qonverter.Settings.NormalizationMethod.Linear, qonverterSettings1.ScoreInfoByName["score1"].NormalizationMethod);
            Assert.AreEqual(42, qonverterSettings1.ScoreInfoByName["score2"].Weight);
            Assert.AreEqual(Qonverter.Settings.Order.Descending, qonverterSettings1.ScoreInfoByName["score2"].Order);
            Assert.AreEqual(Qonverter.Settings.NormalizationMethod.Quantile, qonverterSettings1.ScoreInfoByName["score2"].NormalizationMethod);

            var qonverterSettings2 = session.UniqueResult<QonverterSettings>(o => o.Analysis.Software.Name == "Engine 2");
            Assert.AreEqual(Qonverter.QonverterMethod.SVM, qonverterSettings2.QonverterMethod);
            Assert.AreEqual("___---", qonverterSettings2.DecoyPrefix);
            Assert.AreEqual(false, qonverterSettings2.RerankMatches);
            Assert.AreEqual(2, qonverterSettings2.ScoreInfoByName.Count);
            Assert.AreEqual(7, qonverterSettings2.ScoreInfoByName["foo"].Weight);
            Assert.AreEqual(Qonverter.Settings.Order.Ascending, qonverterSettings2.ScoreInfoByName["foo"].Order);
            Assert.AreEqual(Qonverter.Settings.NormalizationMethod.Off, qonverterSettings2.ScoreInfoByName["foo"].NormalizationMethod);
            Assert.AreEqual(11, qonverterSettings2.ScoreInfoByName["bar"].Weight);
            Assert.AreEqual(Qonverter.Settings.Order.Descending, qonverterSettings2.ScoreInfoByName["bar"].Order);
            Assert.AreEqual(Qonverter.Settings.NormalizationMethod.Off, qonverterSettings2.ScoreInfoByName["bar"].NormalizationMethod);
        }
    }
}
