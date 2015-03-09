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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        // TODO(nicksh): 2013-10-03: Re-enable this test once it reliably passes
        //[TestMethod]
        public void TestPeakFinder()
        {
            String dbPath = Path.Combine(TestContext.TestDir, "PeakFinderTest.tpg");
            using (var sessionFactory = SessionFactoryFactory.CreateSessionFactory(dbPath, SessionFactoryFlags.CreateSchema))
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
            workspace.SetTaskScheduler(TaskScheduler.Default);
            workspace.DatabasePoller.LoadAndMergeChanges(null);

            foreach (var peakFinderPeptide in peptides)
            {
                AddPeptide(workspace, peakFinderPeptide);
            }
            while (true)
            {
                string statusMessage;
                int progress;
                workspace.ChromatogramGenerator.GetProgress(out statusMessage, out progress);
                if ("Idle" == statusMessage)
                {
                    break;
                }
                Thread.Sleep(100);
            }
            workspace.DatabasePoller.LoadAndMergeChanges(null);
            var exceptions = new List<Exception>();
            foreach (var peptide in peptides)
            {
                try
                {
                    TestPeptide(workspace, peptide);
                }
                catch (Exception exception)
                {
                    exceptions.Add(exception);
                    Trace.TraceError("{0}:{1}", peptide.PeptideSequence, exception);
                }
            }
            CollectionAssert.AreEqual(new Exception[0], exceptions);
        }

        private void AddPeptide(Workspace workspace, PeakFinderPeptide peakFinderPeptide)
        {
            var msDataFile = GetMsDataFile(workspace, peakFinderPeptide.DataFile);
            Peptide peptide;
            using (var session = workspace.OpenWriteSession())
            {
                var dbMsDataFile = new DbMsDataFile
                {
                    Name = peakFinderPeptide.DataFile,
                    Label = peakFinderPeptide.DataFile,
                };
                if (msDataFile == null)
                {
                    session.BeginTransaction();
                    session.Save(dbMsDataFile);
                    session.Transaction.Commit();
                }
                workspace.DatabasePoller.LoadAndMergeChanges(null);
                msDataFile = workspace.MsDataFiles.FindByKey(dbMsDataFile.GetId());
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
                    };
                    session.BeginTransaction();
                    session.Save(dbPeptide);
                    session.Transaction.Commit();
                }

                session.BeginTransaction();
                session.Save(new DbPeptideSpectrumMatch
                {
                    RetentionTime = peakFinderPeptide.FirstDetectedTime * 60,
                    PrecursorCharge = peakFinderPeptide.MinCharge,
                    Peptide = dbPeptide,
                    MsDataFile = session.Load<DbMsDataFile>(msDataFile.Id),
                });
                if (peakFinderPeptide.LastDetectedTime != peakFinderPeptide.FirstDetectedTime)
                {
                    session.Save(new DbPeptideSpectrumMatch
                                     {
                                         RetentionTime = peakFinderPeptide.LastDetectedTime * 60,
                                         PrecursorCharge = peakFinderPeptide.MaxCharge,
                                         Peptide = dbPeptide,
                                         MsDataFile = session.Load<DbMsDataFile>(msDataFile.Id),
                                     });
                }
                session.Transaction.Commit();
                peptide = new Peptide(workspace, dbPeptide);
            }
            MsDataFileUtil.InitMsDataFile(workspace, msDataFile);
            var peptideAnalysis = peptide.EnsurePeptideAnalysis();
            if (peptideAnalysis == null)
            {
                Assert.Fail();
            }
            PeptideFileAnalysis.EnsurePeptideFileAnalysis(peptideAnalysis, msDataFile);
            
        }

        private void TestPeptide(Workspace workspace, PeakFinderPeptide peakFinderPeptide)
        {
            DbPeptideFileAnalysis dbPeptideFileAnalysis;
            using (var session = workspace.OpenSession())
            {
                dbPeptideFileAnalysis = (DbPeptideFileAnalysis) session.CreateQuery(
                    "FROM DbPeptideFileAnalysis T WHERE T.MsDataFile.Name = :dataFile AND T.PeptideAnalysis.Peptide.Sequence = :sequence")
                                                                    .SetParameter("dataFile", peakFinderPeptide.DataFile)
                                                                    .SetParameter("sequence",
                                                                                  peakFinderPeptide.PeptideSequence)
                                                                    .UniqueResult();
            }
            PeptideAnalysis peptideAnalysis = workspace.PeptideAnalyses.FindByKey(dbPeptideFileAnalysis.PeptideAnalysis.Id.GetValueOrDefault());
            using (peptideAnalysis.IncChromatogramRefCount())
            {
                workspace.DatabasePoller.LoadAndMergeChanges(new Dictionary<long, bool> {{peptideAnalysis.Id, true}});
                PeptideFileAnalysis peptideFileAnalysis = peptideAnalysis.GetFileAnalysis(dbPeptideFileAnalysis.Id.GetValueOrDefault());
                var peaks = CalculatedPeaks.Calculate(peptideFileAnalysis, new CalculatedPeaks[0]);
                const string format = "0.000";
                Assert.AreEqual(
                    peakFinderPeptide.ExpectedPeakStart.ToString(format) + "-" + peakFinderPeptide.ExpectedPeakEnd.ToString(format),
                    (peaks.StartTime.GetValueOrDefault() / 60).ToString(format) + "-" + (peaks.EndTime.GetValueOrDefault() / 60).ToString(format), peakFinderPeptide.PeptideSequence);
            }
        }

        private MsDataFile GetMsDataFile(Workspace workspace, String name)
        {
            return workspace.GetMsDataFiles().FirstOrDefault(msDataFile => name == msDataFile.Name);
        }

        class PeakFinderPeptide
        {
            public string DataFile;
            public string PeptideSequence;
            public double FirstDetectedTime;
            public double LastDetectedTime;
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
                    FirstDetectedTime = 32.852,
                    LastDetectedTime = 33.034,
                    MinCharge = 2,
                    MaxCharge = 2,
                    ExpectedPeakStart = 32.792,
                    ExpectedPeakEnd = 33.121,
                },
                new PeakFinderPeptide
                {
                   DataFile = "HMELNTYADKIER",
                   PeptideSequence = "HMELNTYADKIER",
                   FirstDetectedTime = 71.131,
                   LastDetectedTime = 71.131,
                   MinCharge = 3,
                   MaxCharge = 3,
                   ExpectedPeakStart = 71.072,
                   ExpectedPeakEnd = 71.219,
                },
                new PeakFinderPeptide
                    {
                        DataFile = "YLTGDLGGR",
                        PeptideSequence = "YLTGDLGGR",
                        FirstDetectedTime = 56.248,
                        LastDetectedTime = 56.457,
                        MinCharge = 2,
                        MaxCharge = 2,
                        ExpectedPeakStart = 56.186,
                        ExpectedPeakEnd = 56.517,
                    },
                new PeakFinderPeptide
                    {
                        DataFile    = "ATEHQIPDRLK",
                        PeptideSequence = "ATEHQIPDRLK",
                        FirstDetectedTime = 30.167,
                        LastDetectedTime = 30.167,
                        MinCharge = 3,
                        MaxCharge = 3,
                        ExpectedPeakStart = 30.135,
                        ExpectedPeakEnd = 30.232,
                    },
                new PeakFinderPeptide
                    {
                        DataFile = "VVDLLAPYAK",
                        PeptideSequence = "VVDLLAPYAK",
                        FirstDetectedTime = 104.238,
                        LastDetectedTime = 104.770,
                        MinCharge = 2,
                        MaxCharge = 2,
                        ExpectedPeakStart = 104.172,
                        ExpectedPeakEnd = 104.804,
                    }
            };
    }
}
