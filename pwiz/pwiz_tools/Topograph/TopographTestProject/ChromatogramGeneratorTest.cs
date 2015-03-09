/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
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
        [TestMethod]
        public void TestChromatogramGenerator()
        {
            String dbPath = Path.Combine(TestContext.TestDir, "test" + Guid.NewGuid() + ".tpg");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(dbPath, SessionFactoryFlags.CreateSchema))
            {
                using (var session = sessionFactory.OpenSession())
                {
                    session.BeginTransaction();
                    DbWorkspace dbWorkspace = new DbWorkspace
                    {
                        TracerDefCount = 1,
                    };
                    session.Save(dbWorkspace);
                    DbTracerDef dbTracerDef = TracerDef.GetN15Enrichment();
                    dbTracerDef.Workspace = dbWorkspace;
                    dbTracerDef.Name = "Tracer";

                    session.Save(dbTracerDef);
                    session.Save(new DbSetting
                                     {
                                         Workspace = dbWorkspace,
                                         Name = SettingEnum.data_directory.ToString(),
                                         Value = GetDataDirectory()
                                     });
                    session.Transaction.Commit();
                }
            }
            Workspace workspace = new Workspace(dbPath);
            workspace.SetTaskScheduler(TaskScheduler.Default);
            var dbMsDataFile = new DbMsDataFile
                {
                    Name = "20090724_HT3_0",
                };
            using (var session = workspace.OpenWriteSession())
            {
                session.BeginTransaction();
                session.Save(dbMsDataFile);
                session.Transaction.Commit();
            }
            workspace.DatabasePoller.LoadAndMergeChanges(null);
            var msDataFile = workspace.MsDataFiles.FindByKey(dbMsDataFile.GetId());
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
                };
                session.Save(dbPeptide);
                var searchResult = new DbPeptideSpectrumMatch
                {
                    Peptide = dbPeptide,
                    MsDataFile = session.Load<DbMsDataFile>(msDataFile.Id),
                    PrecursorCharge = 3,
                    RetentionTime = 20.557 * 60,
                };
                session.Save(searchResult);
                session.Transaction.Commit();
            }
            var peptide = new Peptide(workspace, dbPeptide);
            var peptideAnalysis = peptide.EnsurePeptideAnalysis();
            peptideAnalysis.IncChromatogramRefCount();
            var peptideFileAnalysis = PeptideFileAnalysis.EnsurePeptideFileAnalysis(peptideAnalysis, msDataFile);
            workspace.DatabasePoller.LoadAndMergeChanges(null);
            peptideAnalysis.IncChromatogramRefCount();
            var chromatogramGenerator = new ChromatogramGenerator(workspace);
            chromatogramGenerator.Start();
            while (peptideFileAnalysis.ChromatogramSet == null)
            {
                Thread.Sleep(100);
            }
            var chromatogramDatas = peptideFileAnalysis.GetChromatograms();
            Assert.IsFalse(chromatogramDatas.Chromatograms.Count == 0);
            chromatogramGenerator.Stop();
            while (chromatogramGenerator.IsThreadAlive)
            {
                Thread.Sleep(100);
            }
        }
    }
}
