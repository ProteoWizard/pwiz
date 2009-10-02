/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
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
using System.Text;
using NHibernate;
using NHibernate.Tool.hbm2ddl;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Lib;
using pwiz.Skyline.Model.Results;

namespace pwiz.Skyline.Model.Hibernate.Query
{
    /// <summary>
    /// In-memory SQLite database that holds all of the queryable information in the document.
    /// </summary>
    public class Database
    {
        private readonly ISessionFactory _sessionFactory;
        private readonly ISession _session;
        public Database()
        {
            var configuration = SessionFactoryFactory.GetConfiguration(":memory:");
            // In-memory SQLite databases disappear the moment that you release the connection,
            // so we have to tell Hibernate not to release the connection until we close the 
            // session.
            configuration.SetProperty("connection.release_mode", "on_close");
            _sessionFactory = configuration.BuildSessionFactory();
            _session = _sessionFactory.OpenSession();
            new SchemaExport(configuration).Execute(false, true, false, true, _session.Connection, null);
        }


        public ISessionFactory SessionFactory
        {
            get { return _sessionFactory; }
        }

        public Schema GetSchema()
        {
            return new Schema(SessionFactory);
        }

        public ResultSet ExecuteQuery(Type table, IList<Identifier> columns)
        {
            Schema schema = new Schema(SessionFactory);
            StringBuilder hql = new StringBuilder("SELECT ");
            String comma = "";
            List<ColumnInfo> columnInfos = new List<ColumnInfo>();
            foreach (Identifier column in columns)
            {
                hql.Append(comma);
                comma = ",";
                hql.Append("T.");
                hql.Append(column);
                columnInfos.Add(schema.GetColumnInfo(table, column));
            }
            hql.Append("\nFROM " + table + " T");
            IQuery query = Session.CreateQuery(hql.ToString());
            return new ResultSet(columnInfos, query.List());
        }

        public ISession Session
        {
            get { return _session; }
        }

        public int SrmDocumentRevisionIndex { get; private set; }
        /// <summary>
        /// Add all of the information from the SrmDocument into the tables.
        /// </summary>
        public void AddSrmDocument(SrmDocument srmDocument)
        {
            SrmDocumentRevisionIndex = srmDocument.RevisionIndex;
            DocInfo docInfo = new DocInfo(srmDocument);
            ITransaction transaction = _session.BeginTransaction();
            SaveResults(_session, docInfo);
            foreach (PeptideGroupDocNode nodeGroup in srmDocument.PeptideGroups)
            {
                PeptideGroup peptideGroup = nodeGroup.PeptideGroup;
                DbProtein dbProtein = new DbProtein
                                          {
                                              Name = nodeGroup.Name,
                                              Description = nodeGroup.Description,
                                              Sequence = peptideGroup.Sequence,
                                              Note = nodeGroup.Note
                                          };
                _session.Save(dbProtein);
                Dictionary<DbResultFile, DbProteinResult> proteinResults = new Dictionary<DbResultFile, DbProteinResult>();
                if (srmDocument.Settings.HasResults)
                {
                    foreach (var replicateFiles in docInfo.ReplicateResultFiles)
                    {
                        foreach (var replicateFile in replicateFiles)
                        {
                            DbProteinResult proteinResult = new DbProteinResult
                                                                {
                                                                    ResultFile = replicateFile,
                                                                    Protein = dbProtein,
                                                                    FileName = replicateFile.FileName,
                                                                    SampleName = replicateFile.SampleName,
                                                                    ReplicateName = replicateFile.Replicate.Replicate
                                                                };
                            _session.Save(proteinResult);
                            proteinResults.Add(replicateFile, proteinResult);
                        }
                    }
                }
                docInfo.ProteinResults.Add(dbProtein, proteinResults);
                foreach (PeptideDocNode nodePeptide in nodeGroup.Children)
                {
                    SavePeptide(_session, docInfo, dbProtein, nodePeptide);
                }
                _session.Flush();
                _session.Clear();
            }
            transaction.Commit();
        }

        private static void SaveResults(ISession session, DocInfo docInfo)
        {
            if (docInfo.MeasuredResult == null)
                return;

            foreach (ChromatogramSet chromatogramSet in docInfo.MeasuredResult.Chromatograms)
            {
                DbReplicate dbReplicate = new DbReplicate {Replicate = chromatogramSet.Name};
                session.Save(dbReplicate);

                var listResultFiles = new List<DbResultFile>();
                docInfo.ReplicateResultFiles.Add(listResultFiles);

                foreach (string filePath in chromatogramSet.MSDataFilePaths)
                {
                    DbResultFile dbResultFile = new DbResultFile
                                                    {
                                                        Replicate = dbReplicate,
                                                        FileName = SampleHelp.GetFileName(filePath),
                                                        SampleName = SampleHelp.GetFileSampleName(filePath)
                                                    };
                    session.Save(dbResultFile);
                    
                    listResultFiles.Add(dbResultFile);
                }
            }
            session.Flush();
            session.Clear();
        }

        /// <summary>
        /// Inserts rows for the peptide and all of its results and children.
        /// </summary>
        private static void SavePeptide(ISession session, DocInfo docInfo,
            DbProtein dbProtein, PeptideDocNode nodePeptide)
        {
            Peptide peptide = nodePeptide.Peptide;
            DbPeptide dbPeptide = new DbPeptide
            {
                Protein = dbProtein,
                Sequence = peptide.Sequence,
                BeginPos = peptide.Begin,
                EndPos = peptide.End,
                Note = nodePeptide.Note,
                AverageMeasuredRetentionTime = nodePeptide.AverageMeasuredRetentionTime,
            };
            if (docInfo.PeptidePrediction.RetentionTime != null)
            {
                double rt = docInfo.PeptidePrediction.RetentionTime.GetRetentionTime(peptide.Sequence);                
                dbPeptide.PredictedRetentionTime = rt;
            }
            session.Save(dbPeptide);
            var peptideResults = new Dictionary<DbResultFile, DbPeptideResult>();
            docInfo.PeptideResults.Add(dbPeptide, peptideResults);
            if (nodePeptide.HasResults)
            {
                var enumReplicates = docInfo.ReplicateResultFiles.GetEnumerator();
                foreach (var results in nodePeptide.Results)
                {
                    bool success = enumReplicates.MoveNext();   // synch with foreach
                    Debug.Assert(success);

                    if (results == null)
                        continue;

                    var resultFiles = enumReplicates.Current;

                    foreach (var chromInfo in results)
                    {
                        if (chromInfo == null)
                            continue;

                        var resultFile = resultFiles[chromInfo.FileIndex];
                        DbPeptideResult dbPeptideResult = new DbPeptideResult
                        {
                            Peptide = dbPeptide,
                            ResultFile = resultFile,
                            PeptidePeakFoundRatio = chromInfo.PeakCountRatio,
                            PeptideRetentionTime = chromInfo.RetentionTime,
                            RatioToStandard = chromInfo.RatioToStandard,
                            ProteinResult = docInfo.ProteinResults[dbProtein][resultFile]
                        };
                        session.Save(dbPeptideResult);
                        peptideResults.Add(resultFile, dbPeptideResult);
                    }
                }
            }
            session.Flush();
            session.Clear();
            foreach (TransitionGroupDocNode nodeGroup in nodePeptide.Children)
            {
                SavePrecursor(session, docInfo, dbPeptide, nodeGroup);
            }
        }

        /// <summary>
        /// Inserts rows for the precursor and all of its results and children.
        /// </summary>
        private static void SavePrecursor(ISession session, DocInfo docInfo,
            DbPeptide dbPeptide, TransitionGroupDocNode nodeGroup)
        {
            TransitionGroup tranGroup = nodeGroup.TransitionGroup;
            DbPrecursor dbPrecursor = new DbPrecursor
                                        {
                                            Peptide = dbPeptide,
                                            Charge = tranGroup.PrecursorCharge,
                                            IsotopeLabelType = tranGroup.LabelType,
                                            NeutralMass = SequenceMassCalc.PersistentNeutral(SequenceMassCalc.GetMH(nodeGroup.PrecursorMz, tranGroup.PrecursorCharge)),
                                            Mz = SequenceMassCalc.PersistentMZ(nodeGroup.PrecursorMz),
                                            Note = nodeGroup.Note
                                        };

            if (nodeGroup.HasLibInfo)
            {
                var libInfo = nodeGroup.LibInfo;
                dbPrecursor.LibraryName = libInfo.LibraryName;
                int iValue = 0;
                if (libInfo is NistSpectrumHeaderInfo)
                    dbPrecursor.LibraryType = "NIST";
                else if (libInfo is XHunterSpectrumHeaderInfo)
                    dbPrecursor.LibraryType = "GPM";
                else if (libInfo is BiblioSpecSpectrumHeaderInfo)
                    dbPrecursor.LibraryType = "BiblioSpec";
                foreach (var pair in libInfo.RankValues)
                {
                    switch (iValue)
                    {
                        case 0:
                            dbPrecursor.LibraryScore1 = libInfo.GetRankValue(pair.Key);
                            break;
                        case 1:
                            dbPrecursor.LibraryScore2 = libInfo.GetRankValue(pair.Key);
                            break;
                        case 2:
                            dbPrecursor.LibraryScore3 = libInfo.GetRankValue(pair.Key);
                            break;
                    }
                    iValue++;
                }
            }
            
            session.Save(dbPrecursor);
            var precursorResults = new Dictionary<DbResultFile, DbPrecursorResult>();
            docInfo.PrecursorResults.Add(dbPrecursor, precursorResults);
            var peptideResults = docInfo.PeptideResults[dbPeptide];

            if (nodeGroup.HasResults)
            {
                var enumReplicates = docInfo.ReplicateResultFiles.GetEnumerator();
                foreach (var results in nodeGroup.Results)
                {
                    bool success = enumReplicates.MoveNext();   // synch with foreach
                    Debug.Assert(success);

                    if (results == null)
                        continue;

                    var resultFiles = enumReplicates.Current;

                    foreach (var chromInfo in results)
                    {
                        if (chromInfo == null)
                            continue;

                        var resultFile = resultFiles[chromInfo.FileIndex];
                        DbPrecursorResult precursorResult = new DbPrecursorResult
                        {
                            Precursor = dbPrecursor,
                            ResultFile = resultFile,
                            PrecursorPeakFoundRatio = chromInfo.PeakCountRatio,
                            BestRetentionTime = chromInfo.RetentionTime,
                            MinStartTime = chromInfo.StartRetentionTime,
                            MaxEndTime = chromInfo.EndRetentionTime,
                            MaxFwhm = chromInfo.Fwhm,
                            TotalArea = chromInfo.Area,
                            TotalBackground = chromInfo.BackgroundArea,
                            TotalAreaRatio = chromInfo.Ratio,
                            // StdevAreaRatio = chromInfo.RatioStdev,
                            LibraryDotProduct = chromInfo.LibraryDotProduct,
                            // TotalSignalToNoise = SignalToNoise(chromInfo.Area, chromInfo.BackgroundArea),
                            UserSetTotal = chromInfo.UserSet,
                            PeptideResult = peptideResults[resultFile],
                        };
                        session.Save(precursorResult);
                        precursorResults.Add(resultFile, precursorResult);
                    }
                }                
            }
            session.Flush();
            session.Clear();
            foreach (TransitionDocNode nodeTran in nodeGroup.Children)
            {
                SaveTransition(session, docInfo, dbPrecursor, nodeTran);
            }
        }

        /// <summary>
        /// Inserts rows for the transition and all of results.
        /// </summary>
        private static void SaveTransition(ISession session, DocInfo docInfo,
            DbPrecursor dbPrecursor, TransitionDocNode nodeTran)
        {
            Transition transition = nodeTran.Transition;
            DbTransition dbTransition = new DbTransition
                                          {
                                              Precursor = dbPrecursor,
                                              ProductCharge = transition.Charge,
                                              ProductNeutralMass = SequenceMassCalc.PersistentNeutral(SequenceMassCalc.GetMH(nodeTran.Mz, transition.Charge)),
                                              ProductMz = SequenceMassCalc.PersistentMZ(nodeTran.Mz),
                                              FragmentIon = transition.FragmentIonName,
                                              FragmentIonType = transition.IonType.ToString(),
                                              FragmentIonOrdinal = transition.Ordinal,
                                              CleavageAa = transition.AA.ToString(),
                                              Note = nodeTran.Note
                                          };

            if (nodeTran.HasLibInfo)
            {
                dbTransition.LibraryIntensity = nodeTran.LibInfo.Intensity;
                dbTransition.LibraryRank = nodeTran.LibInfo.Rank;
            }

            session.Save(dbTransition);
            var precursorResults = docInfo.PrecursorResults[dbPrecursor];
            if (nodeTran.HasResults)
            {
                var enumReplicates = docInfo.ReplicateResultFiles.GetEnumerator();
                foreach (var results in nodeTran.Results)
                {
                    bool success = enumReplicates.MoveNext();   // synch with foreach
                    Debug.Assert(success);

                    if (results == null)
                        continue;

                    var resultFiles = enumReplicates.Current;

                    foreach (var chromInfo in results)
                    {
                        if (chromInfo == null)
                            continue;

                        var resultFile = resultFiles[chromInfo.FileIndex];
                        DbTransitionResult transitionResult = new DbTransitionResult
                        {
                            Transition = dbTransition,
                            ResultFile = resultFile,
                            AreaRatio = chromInfo.Ratio,
                            UserSetPeak = chromInfo.UserSet,
                            PrecursorResult = precursorResults[resultFile],
                        };
                        if (!chromInfo.IsEmpty)
                        {
                            transitionResult.RetentionTime = chromInfo.RetentionTime;
                            transitionResult.StartTime = chromInfo.StartRetentionTime;
                            transitionResult.EndTime = chromInfo.EndRetentionTime;
                            transitionResult.Area = chromInfo.Area;
                            transitionResult.Background = chromInfo.BackgroundArea;
                            // transitionResult.SignalToNoise = SignalToNoise(chromInfo.Area, chromInfo.BackgroundArea);
                            transitionResult.Height = chromInfo.Height;
                            transitionResult.Fwhm = chromInfo.Fwhm;
                            transitionResult.FwhmDegenerate = chromInfo.IsFwhmDegenerate;
                            transitionResult.PeakRank = chromInfo.Rank;
                        }
                        session.Save(transitionResult);
                    }
                }                                
            }
            session.Flush();
            session.Clear();
        }

        private static double SignalToNoise(float area, float background)
        {
            // TODO: Figure out the real equation for this
            return 20 * Math.Log10(background != 0 ? area / background : 1000000);
        }

        /// <summary>
        /// Holds information about the entire document that is passed around while
        /// we are populating the database.
        /// </summary>
        class DocInfo
        {
            public DocInfo(SrmDocument srmDocument)
            {
                var settings = srmDocument.Settings;
                PeptidePrediction = settings.PeptideSettings.Prediction;
                MeasuredResult = settings.MeasuredResults;

                ReplicateResultFiles = new List<List<DbResultFile>>();
                PeptideResults = new Dictionary<DbPeptide, Dictionary<DbResultFile, DbPeptideResult>>();
                PrecursorResults = new Dictionary<DbPrecursor, Dictionary<DbResultFile, DbPrecursorResult>>();
                ProteinResults = new Dictionary<DbProtein, Dictionary<DbResultFile, DbProteinResult>>();
            }
            public PeptidePrediction PeptidePrediction { get; private set; }
            public MeasuredResults MeasuredResult { get; private set; }
            public List<List<DbResultFile>> ReplicateResultFiles { get; private set; }
            public Dictionary<DbPeptide, Dictionary<DbResultFile, DbPeptideResult>> PeptideResults { get; private set;}
            public Dictionary<DbPrecursor, Dictionary<DbResultFile, DbPrecursorResult>> PrecursorResults { get; private set; }
            public Dictionary<DbProtein, Dictionary<DbResultFile, DbProteinResult>> ProteinResults { get; private set; }
        }

    }
}
