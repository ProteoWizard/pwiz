using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using pwiz.Topograph.MsData;
using pwiz.Topograph.Data;
using pwiz.Topograph.Enrichment;
using pwiz.Topograph.Model;

namespace pwiz.Topograph.Test
{
    /// <summary>
    /// Summary description for UnitTest1
    /// </summary>
    [TestClass]
    public class ChromatogramGeneratorTest : BaseTest
    {
        public ChromatogramGeneratorTest()
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
        public void TestChromatogramGenerator()
        {
            String dbPath = Path.Combine(TestContext.TestDir, "test" + Guid.NewGuid() + ".tpg");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(dbPath, true))
            {
                using (var session = sessionFactory.OpenSession())
                { 
                    DbWorkspace dbWorkspace = new DbWorkspace
                    {
                        TracerDefCount = 1,
                    };
                    session.Save(dbWorkspace);
                    DbTracerDef dbTracerDef = TracerDef.GetD3LeuEnrichment();
                    dbTracerDef.Workspace = dbWorkspace;
                    dbTracerDef.Name = "Tracer";

                    session.Save(dbTracerDef);
                }
            }
            Workspace workspace = new Workspace(dbPath);
            workspace.SetTracerDefs(new List<DbTracerDef>{TracerDef.GetN15Enrichment()});
            workspace.Save();
            MsDataFile msDataFile;
            using (var session = workspace.OpenWriteSession())
            {
                session.BeginTransaction();
                var dbMsDataFile = new DbMsDataFile()
                {
                    Name = "20090724_HT3_0",
                    Path = Path.Combine(GetDataDirectory(), "20090724_HT3_0.mzML"),
                    Workspace = workspace.LoadDbWorkspace(session),
                };
                session.Save(dbMsDataFile);
                session.Transaction.Commit();

                msDataFile = new MsDataFile(workspace, dbMsDataFile);
            }
            Assert.IsTrue(MsDataFileUtil.InitMsDataFile(workspace, msDataFile));
            DbPeptide dbPeptide;
            using (var session = workspace.OpenWriteSession())
            {
                session.BeginTransaction();
                dbPeptide = new DbPeptide
                {
                    Protein = "TestProtein",
                    Sequence = "YLAAYLLLVQGGNAAPSAADIK",
                    FullSequence = "K.YLAAYLLLVQGGNAAPSAADIK.A",
                    Workspace = workspace.LoadDbWorkspace(session),
                };
                session.Save(dbPeptide);
                var searchResult = new DbPeptideSearchResult
                {
                    Peptide = dbPeptide,
                    MsDataFile = session.Load<DbMsDataFile>(msDataFile.Id),
                    MinCharge = 3,
                    MaxCharge = 3,
                    FirstDetectedScan = 2645,
                    LastDetectedScan = 2645
                };
                session.Save(searchResult);
                session.Transaction.Commit();
            }
            var peptide = new Peptide(workspace, dbPeptide);
            var peptideAnalysis = peptide.EnsurePeptideAnalysis();
            var peptideFileAnalysis = PeptideFileAnalysis.EnsurePeptideFileAnalysis(peptideAnalysis, msDataFile);
            var chromatogramGenerator = new ChromatogramGenerator(workspace);
            chromatogramGenerator.Start();
            while (peptideFileAnalysis.Chromatograms.ChildCount == 0)
            {
                Thread.Sleep(100);
            }
            var chromatogramDatas = peptideFileAnalysis.GetChromatograms();
            Assert.IsFalse(chromatogramDatas.GetChildCount() == 0);
            chromatogramGenerator.Stop();
        }
    }
}
