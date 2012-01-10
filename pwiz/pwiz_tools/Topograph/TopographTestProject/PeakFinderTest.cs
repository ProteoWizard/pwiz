using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using NHibernate.Criterion;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Test
{
    /// <summary>
    /// Summary description for PeakFinderTest
    /// </summary>
    [TestClass]
    public class PeakFinderTest : BaseTest
    {
        public PeakFinderTest()
        {
            //
            // TODO: Add constructor logic here
            //
        }

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
        public void TestPeakFinder()
        {
            String dbPath = Path.Combine(TestContext.TestDir, "PeakFinderTest.tpg");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(dbPath, SessionFactoryFlags.create_schema))
            {
                using (var session = sessionFactory.OpenSession())
                {
                    session.BeginTransaction();
                    DbWorkspace dbWorkspace = new DbWorkspace
                    {
                        TracerDefCount = 1,
                        SettingCount = 2,
                        SchemaVersion = WorkspaceUpgrader.CurrentVersion
                    };
                    session.Save(dbWorkspace);
                    DbTracerDef dbTracerDef = TracerDef.GetD3LeuEnrichment();
                    dbTracerDef.Workspace = dbWorkspace;
                    dbTracerDef.Name = "Tracer";

                    session.Save(dbTracerDef);
                    session.Save(new DbSetting
                                     {
                                         Workspace = dbWorkspace,
                                         Name = SettingEnum.data_directory.ToString(),
                                         Value = GetDataDirectory()
                                     });
                    session.Save(new DbSetting
                                     {
                                         Workspace = dbWorkspace,
                                         Name = SettingEnum.mass_accuracy.ToString(),
                                         Value = "100000"
                                     });
                    session.Transaction.Commit();
                }
            }
            Workspace workspace = new Workspace(dbPath);
            workspace.Reconciler.ReconcileNow();

            foreach (var peakFinderPeptide in peptides)
            {
                AddPeptide(workspace, peakFinderPeptide);
            }
            var chromatogramGenerator = new ChromatogramGenerator(workspace);
            chromatogramGenerator.Start();
            while (true)
            {
                string statusMessage;
                int progress;
                chromatogramGenerator.GetProgress(out statusMessage, out progress);
                if ("Idle" == statusMessage)
                {
                    break;
                }
                Thread.Sleep(100);
            }
            workspace.Reconciler.ReconcileNow();
            foreach (var peptide in peptides)
            {
                TestPeptide(workspace, peptide);
            }
        }

        private void AddPeptide(Workspace workspace, PeakFinderPeptide peakFinderPeptide)
        {
            var msDataFile = GetMsDataFile(workspace, peakFinderPeptide.DataFile);
            Peptide peptide;
            using (var session = workspace.OpenWriteSession())
            {
                var dbWorkspace = workspace.LoadDbWorkspace(session);
                if (msDataFile == null)
                {
                    var dbMsDataFile = new DbMsDataFile
                    {
                        Name = peakFinderPeptide.DataFile,
                        Label = peakFinderPeptide.DataFile,
                        Workspace = dbWorkspace,
                    };
                    session.BeginTransaction();
                    session.Save(dbMsDataFile);
                    session.Transaction.Commit();
                    msDataFile = new MsDataFile(workspace, dbMsDataFile);
                }
                workspace.MsDataFiles.AddChild(msDataFile.Id.Value, msDataFile);
                var dbPeptide = (DbPeptide)
                    session.CreateCriteria<DbPeptide>().Add(Restrictions.Eq("Sequence",
                                                                            peakFinderPeptide.PeptideSequence)).
                        UniqueResult();
                if (dbPeptide == null)
                {
                    dbPeptide = new DbPeptide
                    {
                        FullSequence = peakFinderPeptide.PeptideSequence,
                        Sequence = peakFinderPeptide.PeptideSequence,
                        Workspace = dbWorkspace,
                    };
                    session.BeginTransaction();
                    session.Save(dbPeptide);
                    session.Transaction.Commit();
                }

                var dbPeptideSearchResult = new DbPeptideSearchResult
                {
                    FirstDetectedScan = peakFinderPeptide.FirstDetectedScan,
                    LastDetectedScan = peakFinderPeptide.LastDetectedScan,
                    MinCharge = peakFinderPeptide.MinCharge,
                    MaxCharge = peakFinderPeptide.MaxCharge,
                    Peptide = dbPeptide,
                    MsDataFile = session.Load<DbMsDataFile>(msDataFile.Id),
                };
                session.BeginTransaction();
                session.Save(dbPeptideSearchResult);
                session.Transaction.Commit();
                peptide = new Peptide(workspace, dbPeptide);
            }
            MsDataFileUtil.InitMsDataFile(workspace, msDataFile);
            var peptideAnalysis = peptide.EnsurePeptideAnalysis();
            var peptideFileAnalysis = PeptideFileAnalysis.EnsurePeptideFileAnalysis(peptideAnalysis, msDataFile);
        }

        private void TestPeptide(Workspace workspace, PeakFinderPeptide peakFinderPeptide)
        {
            PeptideFileAnalysis peptideFileAnalysis;
            using (var session = workspace.OpenSession())
            {
                var dbPeptideFileAnalysis = (DbPeptideFileAnalysis) session.CreateQuery(
                    "FROM DbPeptideFileAnalysis T WHERE T.MsDataFile.Name = :dataFile AND T.PeptideAnalysis.Peptide.Sequence = :sequence")
                    .SetParameter("dataFile", peakFinderPeptide.DataFile)
                    .SetParameter("sequence", peakFinderPeptide.PeptideSequence)
                    .UniqueResult();
                var peptideAnalysis =
                    workspace.Reconciler.LoadPeptideAnalysis(dbPeptideFileAnalysis.PeptideAnalysis.Id.Value);
                peptideFileAnalysis = peptideAnalysis.GetFileAnalysis(dbPeptideFileAnalysis.Id.Value);
            }
            var peaks = new Peaks(peptideFileAnalysis);
            peaks.CalcIntensities(new Peaks[0]);
            const string format = "0.000";
            Assert.AreEqual(
                peakFinderPeptide.ExpectedPeakStart.ToString(format) + "-" + peakFinderPeptide.ExpectedPeakEnd.ToString(format),
                peaks.StartTime.Value.ToString(format) + "-" + peaks.EndTime.Value.ToString(format));
        }

        private MsDataFile GetMsDataFile(Workspace workspace, String name)
        {
            return workspace.GetMsDataFiles().FirstOrDefault(msDataFile => name == msDataFile.Name);
        }

        class PeakFinderPeptide
        {
            public string DataFile;
            public string PeptideSequence;
            public int FirstDetectedScan;
            public int LastDetectedScan;
            public int MinCharge;
            public int MaxCharge;
            public double ExpectedPeakStart;
            public double ExpectedPeakEnd;
        }

        private static PeakFinderPeptide[] peptides =
            new[]
            {
                new PeakFinderPeptide
                {
                    DataFile = "ADLEETGR",
                    PeptideSequence = "ADLEETGR",
                    FirstDetectedScan = 298,
                    LastDetectedScan = 304,
                    MinCharge = 2,
                    MaxCharge = 2,
                    ExpectedPeakStart = 1.935,
                    ExpectedPeakEnd = 2.154,
                },
                new PeakFinderPeptide
                {
                   DataFile = "HMELNTYADKIER",
                   PeptideSequence = "HMELNTYADKIER",
                   FirstDetectedScan = 137,
                   LastDetectedScan = 137,
                   MinCharge = 3,
                   MaxCharge = 3,
                   ExpectedPeakStart = .594,
                   ExpectedPeakEnd = 1.088,
                },
                new PeakFinderPeptide
                    {
                        DataFile = "YLTGDLGGR",
                        PeptideSequence = "YLTGDLGGR",
                        FirstDetectedScan = 162,
                        LastDetectedScan = 169,
                        MinCharge = 2,
                        MaxCharge = 2,
                        ExpectedPeakStart = .993,
                        ExpectedPeakEnd = 2.209,
                    },
                new PeakFinderPeptide
                    {
                        DataFile    = "ATEHQIPDRLK",
                        PeptideSequence = "ATEHQIPDRLK",
                        FirstDetectedScan = 243,
                        LastDetectedScan = 243,
                        MinCharge = 3,
                        MaxCharge = 3,
                        ExpectedPeakStart = 1.564,
                        ExpectedPeakEnd = 1.746,
                    },
                new PeakFinderPeptide
                    {
                        DataFile = "VVDLLAPYAK",
                        PeptideSequence = "VVDLLAPYAK",
                        FirstDetectedScan = 71,
                        LastDetectedScan = 87,
                        MinCharge = 2,
                        MaxCharge = 2,
                        ExpectedPeakStart = .268,
                        ExpectedPeakEnd = .590,
                    }
            };
    }
}
