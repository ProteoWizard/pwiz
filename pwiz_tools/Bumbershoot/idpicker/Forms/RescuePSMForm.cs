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
// The Initial Developer of the Original Code is Zeqiang Ma
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using System.Threading;
using System.IO;
using System.Security.AccessControl;
using IDPicker.DataModel;

using msdata = pwiz.CLI.msdata;
using pwiz.CLI.analysis;

namespace IDPicker.Forms
{
    public partial class RescuePSMsForm : DockableForm
    {
        public RescuePSMsForm(IDPickerForm owner)
        {
            InitializeComponent();

            this.owner = owner;

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            Text = TabText = "Rescue PSMs";
            Icon = Properties.Resources.BlankIcon;

            btnClustering.Enabled = true;
            btnCancel.Enabled = false;
        }

        private static BackgroundWorker _bgWorkerClustering;
        private bool _bgWorkerCancelled;
        private double similarityThreshold;
        private double precursorMzTolerance;
        private double fragmentMzTolerance;
        private bool backupDB;
        private string searchScore1Name;
        private string searchScore1Order;
        private double searchScore1Threshold;
        private string searchScore2Name;
        private string searchScore2Order;
        private double searchScore2Threshold;
        private string searchScore3Name;
        private string searchScore3Order;
        private double searchScore3Threshold;
        private bool writeLog;
        private int minClusterSize;
        private int maxRank;
        private int rescuedPSMsCount;
        private string logFile;
        private IDPickerForm owner;
        private NHibernate.ISession session;
        private DataFilter basicDataFilter;

        private delegate void reportStatusDelegate(string statusText);
        private void setStatus(string statusText)
        {
             if (this.InvokeRequired)
             {
                 this.BeginInvoke(new reportStatusDelegate(setStatus), statusText);
                 return;
             }
             tbStatus.AppendText(statusText);
        }

        private delegate void reportProgressDelegate(int progress, string progressText);
        private void setProgress(int progress, string progressText)
        {
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new reportProgressDelegate(setProgress), progress, progressText);
                return;
            }
            if (progress == -1)
            {
                progressBar.Style = ProgressBarStyle.Marquee;
            }
            else
            {
                progressBar.Style = ProgressBarStyle.Blocks;
                progressBar.Value = progress;
            }
            lblStatusToolStrip.Text = progressText;
            
        }

        private string reportSecondsElapsed(double seconds)
        {
            return string.Format("{0:0.0} seconds elapsed\r\n", seconds);
        }


        #region Wrapper class for encapsulating query results
        public class SpectrumRow
        {            
            public string SourceName { get; private set; }
            public long SpectrumId { get; private set; }
            public string SpectrumNativeID { get; private set; }
            public double PrecursorMZ { get; private set; }

            public IList<double> OriginalMZs { get;  set; }
            public IList<double> OriginalIntensities { get;  set; }

            #region Constructor
            public SpectrumRow(object[] queryRow)
            {
                SpectrumId = (long)queryRow[0];
                SourceName = (string)queryRow[1];
                SpectrumNativeID = (string)queryRow[2];
                PrecursorMZ = Convert.ToDouble(queryRow[3]);

                OriginalMZs = null;
                OriginalIntensities = null;
            }
            #endregion
        }

        public class ClusterSpectrumRow
        {
            public long PSMId { get; private set; }
            public long PeptideId { get; private set; }
            public long SpectrumId { get; private set; }
            public string SourceName { get; private set; }
            public string SpectrumNativeID { get; private set; }
            public int Charge { get; private set; }
            public string ModifiedSequence { get; private set; }
            public string Protein { get; private set; }
            public double QValue { get; private set; }
            public int Rank { get; private set; }
            public double SearchScore { get; private set; }
            public long Analysis { get; private set; }

            #region Constructor
            public ClusterSpectrumRow(object[] queryRow)
            {
                PSMId = (long)queryRow[0];
                PeptideId = Convert.ToInt64(queryRow[1]);
                SpectrumId = (long)queryRow[2];
                SourceName = (string)queryRow[3];
                SpectrumNativeID = (string)queryRow[4];
                
                Charge = Convert.ToInt32(queryRow[5]);
                

                var mods = new Dictionary<int, List<double>>();
                if (!String.IsNullOrEmpty((string)queryRow[6]))
                {
                    var offsetMassDeltaPairs = ((string)queryRow[6]).Split(',');
                    foreach (var pair in offsetMassDeltaPairs)
                    {
                        var offsetAndMassDelta = pair.Split(':');
                        int offset = Convert.ToInt32(offsetAndMassDelta[0]);
                        if (!mods.ContainsKey(offset))
                            mods[offset] = new List<double>();
                        mods[offset].Add(Convert.ToDouble(offsetAndMassDelta[1]));
                    }
                }

                string format = String.Format("[{{0:f{0}}}]", 0);
                StringBuilder sb = new StringBuilder((string)queryRow[7]);
                foreach (var mod in (from m in mods orderby m.Key descending select m))
                    foreach (var massDelta in mod.Value)
                        if (mod.Key == int.MinValue)
                            sb.Insert(0, String.Format(format, massDelta));
                        else if (mod.Key == int.MaxValue || mod.Key >= sb.Length)
                            sb.AppendFormat(format, massDelta);
                        else
                            sb.Insert(mod.Key + 1, String.Format(format, massDelta));
                ModifiedSequence = sb.ToString();

                Protein = (string)queryRow[8];
                QValue = Convert.ToDouble(queryRow[9]);
                Rank = Convert.ToInt32(queryRow[10]);
                SearchScore = Convert.ToDouble(queryRow[11]);
                Analysis =Convert.ToInt64(queryRow[12]);

            }
            #endregion
        }

        public class PsmIdRow
        {
            public Set<long> PsmIds { get; private set; }

            #region Constructor
            public PsmIdRow(object queryRow)
            {
                PsmIds = new Set<long>();
                var psmIds = ((string)queryRow).Split(',');
                foreach (var psmId in psmIds)
                {
                    PsmIds.Add(Convert.ToInt64(psmId));
                }
            }
            #endregion
        }

        public class qonvertSettingRows
        {
            public long Id { get; private set;}
            public string ScoreOrder { get; private set;}
            public string ScoreName { get; private set; }

            #region Constructor
            public qonvertSettingRows(object[] queryRow)
            {
                Id = (long)queryRow[0];
                var scoreInfoByName = ((string)queryRow[1]).Split();
                ScoreOrder = scoreInfoByName[1];
                ScoreName = scoreInfoByName[3];
            }
            #endregion
        }

        #endregion

        ////use basicFilter to process all source files and PSMs in session
        public void SetData(NHibernate.ISession session, DataFilter basicFilter)  
        {
            this.session = session;
            basicDataFilter = basicFilter;
            ClearData();
        }

        public void ClearData()
        {
            Text = TabText = "Rescue PSMs";
            //tbStatus.Clear();
            //tbStatus.Refresh();
            lblStatusToolStrip.Text = "Ready";
            progressBar.Value = 0;
            //Refresh();
        }

        public void ClearData(bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        public void ClearSession()
        {
            ClearData();
            if (session != null && session.IsOpen)
            {
                session.Close();
                session.Dispose();
                session = null;
            }
        }

        /// <summary>
        /// run clustering, Rescue PSMs, update idpDB
        /// </summary>
        private void RescuePSMsByClustering()
        {
            DateTime startTime = DateTime.Now;
            reportProgressDelegate reportProgress = new reportProgressDelegate(setProgress);
            reportStatusDelegate reportStatus = new reportStatusDelegate(setStatus);

            string database = session.Connection.GetDataSource();
            logFile = Path.ChangeExtension(database, ".log.txt");

            string config = string.Format("Parameters:\r\n" +
                                          "PrecursorMZTol: {0} \r\n" +
                                          "FragmentMZTol: {1} \r\n" +
                                          "Similarity Threshold >= {2} \r\n" +
                                          "Rank <= {3} \r\n" +
                                          "Cluster Size >= {4} \r\n" +
                                          "Search Scores: {5}{6}{7};{8}{9}{10};{11}{12}{13} \r\n\r\n",
                                          precursorMzTolerance,
                                          fragmentMzTolerance,
                                          similarityThreshold,
                                          maxRank,
                                          minClusterSize,
                                          searchScore1Name, searchScore1Order, searchScore1Threshold,
                                          searchScore2Name, searchScore2Order, searchScore2Threshold,
                                          searchScore3Name, searchScore3Order, searchScore3Threshold);
            reportStatus(config);

            //if (writeLog)
            //    File.WriteAllText(logFile, config);

            /*
             * back up original idpDB
             */
            if (backupDB)
            {
                string dbBackupFile = Path.ChangeExtension(database, ".backup.idpDB");
                reportStatus(string.Format("Backing up idpDB to {0} ... ", dbBackupFile));
                reportProgress(-1, "Backing up idpDB");
                File.Copy(database, dbBackupFile, true);
                reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));
            }

            //reportStatus("Dropping filters... \r\n");
            // basicDataFilter.DropFilters(session);  //// this will drop all filtered tables and rename unfiltered tables
            //basicDataFilter.ApplyBasicFilters(session);

            reportStatus("Querying spectra...");
            reportProgress(-1, "Querying spectra...");
            IList<object[]> queryRows;
            lock (session)
                //// SQL query to retrieve spectrum info for unfiltered psm, filter query results by rank1 search score
                //                queryRows = session.CreateSQLQuery(@"SELECT s.Id, source.Name, NativeID, PrecursorMZ
                //                                                        FROM Spectrum s
                //                                                        JOIN SpectrumSource source ON s.Source = source.Id
                //                                                        JOIN UnfilteredPeptideSpectrumMatch psm ON s.Id = psm.Spectrum AND psm.Rank = 1
                //                                                        JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
                //                                                        JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
                //                                                        WHERE (scoreName.Name = " + "'" + searchScore1Name + "'" + " AND psmScore.Value " + searchScore1Order + searchScore1Threshold.ToString() + ") OR (scoreName.Name = " + "'" + searchScore2Name + "'" + " AND psmScore.Value " + searchScore2Order + searchScore2Threshold.ToString() + ") OR (scoreName.Name = " + "'" + searchScore3Name + "'" + " AND psmScore.Value " + searchScore3Order + searchScore3Threshold.ToString() + ")" +
                //                                                        " GROUP BY s.Id"
                //                                                    ).List<object[]>();

                //// SQL query to retrieve spectrum info for unfiltered psm that map to identified peptide, filter by search score 
                queryRows = session.CreateSQLQuery(@"SELECT s.Id, source.Name, NativeID, PrecursorMZ
                                                        FROM UnfilteredSpectrum s
                                                        JOIN SpectrumSource source ON s.Source = source.Id
                                                        JOIN UnfilteredPeptideSpectrumMatch psm ON s.Id = psm.Spectrum
                                                        JOIN Peptide p ON p.Id = psm.Peptide
                                                        JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
                                                        JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
                                                        WHERE (scoreName.Name = " + "'" + searchScore1Name + "'" + " AND psmScore.Value " + searchScore1Order + searchScore1Threshold.ToString() + ") OR (scoreName.Name = " + "'" + searchScore2Name + "'" + " AND psmScore.Value " + searchScore2Order + searchScore2Threshold.ToString() + ") OR (scoreName.Name = " + "'" + searchScore3Name + "'" + " AND psmScore.Value " + searchScore3Order + searchScore3Threshold.ToString() + ")" +
                                                                       " GROUP BY s.Id"
                                                                   ).List<object[]>();
            var foundSpectraList = session.CreateSQLQuery(@"SELECT distinct spectrum FROM PeptideSpectrumMatch").List<object>();
            var foundSpectra = new HashSet<long>();
            {
                long tempLong;
                foreach (var item in foundSpectraList)
                    if (long.TryParse(item.ToString(), out tempLong))
                        foundSpectra.Add(tempLong);
            }

            var spectrumRows = queryRows.Select(o => new SpectrumRow(o)).OrderBy(o => o.SourceName).ToList();
            ////converted IOrderedEnumerable to List, the former one may end up with multiple enumeration, each invokes constructor, resulting a fresh set of object

            /*
             * extract peaks for each spectrum, spectrumRows was sorted by SourceName
            */
            string currentSourceName = null;
            string currentSourcePath = null;
            msdata.MSData msd = null;
            int spectrumRowsCount = spectrumRows.Count();
            //Set<long> processedSpectrumIDs = new Set<long>();

            reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));
            reportStatus(string.Format("Extracting peaks for {0} spectra ... ", spectrumRowsCount));
            lock (owner)
                for (int i = 0; i < spectrumRowsCount; ++i)
                {
                    if (_bgWorkerClustering.CancellationPending)
                    {
                        _bgWorkerCancelled = true;
                        return;
                    }

                    var row = spectrumRows.ElementAt(i);

                    reportProgress((int)(((double)(i + 1) / (double)spectrumRowsCount) * 100), string.Format("Extracting peaks ({0}/{1}) from {2}", i + 1, spectrumRowsCount, row.SourceName));

                    //if (processedSpectrumIDs.Contains(row.SpectrumId))
                    //    break;
                    if (row.SourceName != currentSourceName)
                    {
                        currentSourceName = row.SourceName;
                        currentSourcePath = IDPickerForm.LocateSpectrumSource(currentSourceName, session.Connection.GetDataSource());
                        if (msd != null)
                            msd.Dispose();
                        msd = new pwiz.CLI.msdata.MSDataFile(currentSourcePath);

                        SpectrumListFactory.wrap(msd, "threshold count 100 most-intense"); //only keep the top 100 peaks
                        //SpectrumListFactory.wrap(msd, "threshold bpi-relative .5 most-intense"); //keep all peaks that are at least 50% of the intensity of the base peak
                        //SpectrumListFactory.wrap(msd, "threshold tic-cutoff .95 most-intense"); //keep all peaks that count for 95% TIC
                        //threshold <count|count-after-ties|absolute|bpi-relative|tic-relative|tic-cutoff> <threshold> <most-intense|least-intense> [int_set(MS levels)]
                    }

                    var spectrumList = msd.run.spectrumList;
                    var pwizSpectrum = spectrumList.spectrum(spectrumList.find(row.SpectrumNativeID), true); //may create indexoutofrange error if no spectrum nativeID                   
                    row.OriginalMZs = pwizSpectrum.getMZArray().data; //getMZArray().data returns IList<double>
                    row.OriginalIntensities = pwizSpectrum.getIntensityArray().data;
                    //processedSpectrumIDs.Add(row.SpectrumId);

                }

            /* 
             * re-sort spectrumRows by precursorMZ
             * walk through each spectrum. compare similarity to all other spectra within the precursorMZTolerance 
             * (e.g. compare 1 to 2,3,4, then 2 to 3,4,5, then 3 to 4,5 etc), 
             * if above similarityThreshold, add link edge to BOTH spectra
             * merge all connected spectra to a cluster             
            */
            reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));
            reportStatus("Computing similarities... ");
            var spectrumRowsOrderByPrecursorMZ = (from randomVar in spectrumRows orderby randomVar.PrecursorMZ select randomVar).ToList();
            LinkMap linkMap = new LinkMap(); //// spectrum Id as key, directly linked spectra as value
            double similarityScore = 0;
            lock (owner)
                for (int i = 0; i < spectrumRowsCount; ++i)
                {
                    if (_bgWorkerClustering.CancellationPending)
                    {
                        _bgWorkerCancelled = true;
                        return;
                    }

                    var row = spectrumRowsOrderByPrecursorMZ.ElementAt(i);

                    reportProgress((int)(((double)(i + 1) / (double)spectrumRowsCount) * 100), "Computing similarities");
                    for (int j = i + 1; j < spectrumRowsCount; ++j)
                    {
                        var nextRow = spectrumRowsOrderByPrecursorMZ.ElementAt(j);

                        if (Math.Abs(row.PrecursorMZ - nextRow.PrecursorMZ) > precursorMzTolerance)
                        {
                            break;
                        }
                        else
                        {
                            ////compare pairwise similarity, link spectra passing threshold to both spectrum
                            Peaks rowPeakList = new Peaks(row.OriginalMZs, row.OriginalIntensities);
                            Peaks nextRowPeakList = new Peaks(nextRow.OriginalMZs, nextRow.OriginalIntensities);
                            //// converting peak intensities to sqrt here is 5-fold slower than doing this in DotProductCompareTo function
                            //Peaks rowPeakList = new Peaks(row.OriginalMZs, row.OriginalIntensities.Select(o => Math.Sqrt(o)).ToList());
                            //Peaks nextRowPeakList = new Peaks(nextRow.OriginalMZs, nextRow.OriginalIntensities.Select(o => Math.Sqrt(o)).ToList());
                            similarityScore = ClusteringAnalysis.DotProductCompareTo(rowPeakList, nextRowPeakList, fragmentMzTolerance);
                            //reportStatus("similarity between " + row.SpectrumNativeID + " and " + nextRow.SpectrumNativeID + " is " + similarityScore.ToString() + "\r\n");
                            if (similarityScore >= similarityThreshold)
                            {
                                linkMap[(long)row.SpectrumId].Add((long)nextRow.SpectrumId);
                                linkMap[(long)nextRow.SpectrumId].Add((long)row.SpectrumId); //// if a -> b, then b -> a  
                            }
                        }
                    }
                }
            reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));

            reportStatus("Clustering spectra... ");
            reportProgress(-1, "Clustering spectra");
            linkMap.GetMergedLinkList();
            reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));

            //// print clustered spectra
            //foreach (var cluster in linkMap.MergedLinkList)
            //{
            //    reportStatus("Number of spectra in cluster: " + cluster.Count().ToString() + "\r\n");
            //    foreach (var sID in cluster)
            //    {
            //        var nativeID = (from o in spectrumRows where o.SpectrumId == sID select o.SpectrumNativeID).First();
            //        reportStatus(nativeID.ToString() + "\t");
            //    }
            //    reportStatus("\r\n");
            //}

            ////free some memory
            queryRows.Clear();
            queryRows = null;
            msd.Dispose();
            msd = null;
            spectrumRows.Clear();
            spectrumRows = null;
            spectrumRowsOrderByPrecursorMZ.Clear();
            spectrumRowsOrderByPrecursorMZ = null;

            /* 
             * Go through each cluster, rescue PSMs if spectra in the same cluster were identified as the same peptide (id)
             */
            List<Set<long>> clusterSetList = (from o in linkMap.MergedLinkList where o.Count >= minClusterSize select o).ToList();    //// each element in the list is a set of clustered spectrum Ids, select sets with at least minClusterSize element           
            int clusterSetListCount = clusterSetList.Count();
            var allSpectrumIDs = (from o in clusterSetList from j in o select j).ToList();
            reportStatus(string.Format("Number of clusters: {0} \r\n", clusterSetListCount));
            reportStatus(string.Format("Number of spectra clustered: {0}/{1} ({2:0.0%}) \r\n", allSpectrumIDs.Count, spectrumRowsCount, (double)allSpectrumIDs.Count / spectrumRowsCount));

            IList<object> identPSMQueryRows;
            lock (session)
                identPSMQueryRows = session.CreateSQLQuery(@"SELECT psm.Id FROM PeptideSpectrumMatch psm").List<object>();

            var identPSMIdSet = new Set<long>(identPSMQueryRows.Select(o => (long)o));
            reportStatus(string.Format("Number of PSMs identified: {0} \r\n", identPSMIdSet.Count));

            //// create a temp table to store clustered spectrum IDs
            session.CreateSQLQuery(@"DROP TABLE IF EXISTS TempSpecIds;
                                     CREATE TEMP TABLE TempSpecIds (Id INTEGER PRIMARY KEY)
                                    ").ExecuteUpdate();

            var insertTempSpecIdscmd = session.Connection.CreateCommand();
            insertTempSpecIdscmd.CommandText = "INSERT INTO TempSpecIds VALUES (?)";
            var insertTempSpecIdsParameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 1; ++i)
            {
                insertTempSpecIdsParameters.Add(insertTempSpecIdscmd.CreateParameter());
                insertTempSpecIdscmd.Parameters.Add(insertTempSpecIdsParameters[i]);
            }
            insertTempSpecIdscmd.Prepare();
            foreach (var id in allSpectrumIDs)
            {
                insertTempSpecIdsParameters[0].Value = id;
                insertTempSpecIdscmd.ExecuteNonQuery();
            }


            IList<object> allPsmIdQueryRows;
            lock (session)
                //// SQL query to retrieve all psm id for clustered spectra with score above a threshold
                allPsmIdQueryRows = session.CreateSQLQuery(@"SELECT GROUP_CONCAT(psm.Id)
                                                        FROM TempSpecIds
                                                        JOIN UnfilteredPeptideSpectrumMatch psm ON TempSpecIds.Id = psm.Spectrum
                                                        JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
                                                        JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
                                                        WHERE psm.Rank <= " + maxRank.ToString() + " AND ((scoreName.Name = " + "'" + searchScore1Name + "'" + " AND psmScore.Value " + searchScore1Order + searchScore1Threshold.ToString() + ") OR (scoreName.Name = " + "'" + searchScore2Name + "'" + " AND psmScore.Value " + searchScore2Order + searchScore2Threshold.ToString() + ") OR (scoreName.Name = " + "'" + searchScore3Name + "'" + " AND psmScore.Value " + searchScore3Order + searchScore3Threshold.ToString() + "))" +
                                                        " GROUP BY TempSpecIds.Id, psm.Charge"
                                                    ).List<object>();

            var allPsmIdsRows = allPsmIdQueryRows.Select(o => new PsmIdRow(o)).ToList();

            Set<long> allPsmIds = new Set<long>();
            foreach (var row in allPsmIdsRows)
            {
                allPsmIds.Union(row.PsmIds);
            }

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS TempSpecIds").ExecuteUpdate();

            reportStatus("Querying PSMs...");
            reportProgress(-1, "Querying PSMs");
            IList<object[]> allClusterQueryRows;

            //// create a temp table to store psm IDs
            session.CreateSQLQuery(@"DROP TABLE IF EXISTS TempPsmIds;
                                     CREATE TEMP TABLE TempPsmIds (Id INTEGER PRIMARY KEY)
                                    ").ExecuteUpdate();

            var cmd = session.Connection.CreateCommand();
            cmd.CommandText = "INSERT INTO TempPsmIds VALUES (?)";
            var parameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 1; ++i)
            {
                parameters.Add(cmd.CreateParameter());
                cmd.Parameters.Add(parameters[i]);
            }
            cmd.Prepare();
            foreach (var id in allPsmIds)
            {
                parameters[0].Value = id;
                cmd.ExecuteNonQuery();
            }

            //// qurey string for revison 286, no DecoySequence in Peptide table
            //            string queryCmd = @"SELECT psm.Id as psmId, s.Id, source.Name, s.NativeID, psm.Rank, psm.Charge, psmScore.Value, IFNULL(GROUP_CONCAT(DISTINCT pm.Offset || ':' || mod.MonoMassDelta),''),
            //                                    (SELECT SUBSTR(pro.Sequence, pi.Offset+1, pi.Length)
            //                                                                FROM PeptideInstance pi
            //                                                                JOIN ProteinData pro ON pi.Protein=pro.Id
            //                                                                WHERE pi.Protein=pro.Id AND
            //                                                                  pi.Id=(SELECT MIN(pi2.Id)
            //                                                                         FROM PeptideInstance pi2
            //                                                                         WHERE psm.Peptide=pi2.Peptide))
            //                                    FROM TempIDs tempIDs
            //                                    JOIN Spectrum s ON s.Id = tempIDs.Id
            //                                    JOIN SpectrumSource source ON s.Source = source.Id
            //                                    JOIN PeptideSpectrumMatch psm ON s.Id = psm.Spectrum
            //                                    LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch
            //                                    LEFT JOIN Modification mod ON pm.Modification = mod.Id
            //                                    JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
            //                                    JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
            //                                    WHERE scoreName.Name = " + "'" + searchScoreName + "'" + " AND psm.Rank <= 5" +
            //                                " GROUP BY psm.Id";
            //AND s.Id IN ( " + String.Join(",", allSpectrumIDs.Select(o => o.ToString()).ToArray()) + " ) " +

            //// query string for revison 288, added DecoySequence in Peptide table
            //            string queryCmd = @"SELECT psm.Id as psmId, s.Id, source.Name, s.NativeID, psm.Rank, psm.Charge, psmScore.Value, IFNULL(GROUP_CONCAT(DISTINCT pm.Offset || ':' || mod.MonoMassDelta),''),
            //                                    (SELECT IFNULL(SUBSTR(pro.Sequence, pi.Offset+1, pi.Length), (SELECT DecoySequence FROM Peptide p WHERE p.Id = pi.Peptide))
            //                                            FROM PeptideInstance pi
            //                                            LEFT JOIN ProteinData pro ON pi.Protein=pro.Id
            //                                            WHERE pi.Id=(SELECT pi2.Id FROM PeptideInstance pi2 WHERE pi2.Peptide=psm.Peptide LIMIT 1))
            //                                    FROM TempIDs tempIDs
            //                                    JOIN Spectrum s ON s.Id = tempIDs.Id
            //                                    JOIN SpectrumSource source ON s.Source = source.Id
            //                                    JOIN PeptideSpectrumMatch psm ON s.Id = psm.Spectrum
            //                                    LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch
            //                                    LEFT JOIN Modification mod ON pm.Modification = mod.Id
            //                                    JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
            //                                    JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
            //                                    WHERE scoreName.Name = " + "'" + searchScoreName + "'" + " AND psm.Rank <= 5" +
            //                                " GROUP BY psm.Id";

            ////query string for revision 291, retrive by PSM Ids
            //            string queryCmd = @"SELECT psm.Id as psmId, psm.Peptide,s.Id, source.Name, s.NativeID, psm.Charge, IFNULL(GROUP_CONCAT(DISTINCT pm.Offset || ':' || mod.MonoMassDelta),''),
            //                                    (SELECT IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length), (SELECT DecoySequence FROM UnfilteredPeptide p WHERE p.Id = pi.Peptide))),
            //                                    GROUP_CONCAT(pro.Accession),psm.QValue, psm.Rank, psmScore.Value, analysis.Id
            //                                    FROM TempPsmIds tempPsmIds
            //                                    JOIN UnfilteredPeptideSpectrumMatch psm ON psm.Id = tempPsmIds.Id 
            //                                    JOIN Analysis analysis ON psm.Analysis = analysis.Id
            //                                    JOIN Spectrum s ON s.Id = psm.Spectrum
            //                                    JOIN SpectrumSource source ON s.Source = source.Id
            //                                    JOIN UnfilteredPeptideInstance pi ON psm.Peptide = pi.Peptide
            //                                    JOIN UnfilteredProtein pro ON pi.Protein = pro.Id
            //                                    LEFT JOIN ProteinData pd ON pi.Protein=pd.Id
            //                                    LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch
            //                                    LEFT JOIN Modification mod ON pm.Modification = mod.Id
            //                                    LEFT JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
            //                                    LEFT JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
            //                                    WHERE scoreName.Name = " + "'" + searchScore1Name + "'" +
            //                                    " GROUP BY psm.Id";

            // query for r291, fix no seq for some peptides shared by target and decoy proteins, query seq for target and decoy proteins separately then union
            string queryCmd = @"SELECT psm.Id as psmId, psm.Peptide,s.Id, source.Name, s.NativeID, psm.Charge, 
                                        IFNULL(GROUP_CONCAT(DISTINCT pm.Offset || ':' || mod.MonoMassDelta),''),
                                        IFNULL(IFNULL(SUBSTR(pd.Sequence, pi.Offset+1, pi.Length),(SELECT DecoySequence FROM UnfilteredPeptide p WHERE p.Id = pi.Peptide)),
                                                (SELECT SUBSTR(pd.Sequence, pi.Offset+1, pi.Length)
                                                FROM UnfilteredPeptideInstance pi 
                                                JOIN UnfilteredProtein pro ON pi.Protein = pro.Id AND pro.IsDecoy = 0
                                                LEFT JOIN ProteinData pd ON pi.Protein=pd.Id
                                                WHERE psm.Peptide = pi.Peptide
                                                UNION
                                                SELECT p.DecoySequence
                                                FROM UnfilteredPeptide p
                                                JOIN UnfilteredPeptideInstance pi ON p.Id = pi.Peptide
                                                JOIN UnfilteredProtein pro ON pi.Protein = pro.Id AND pro.IsDecoy = 1
                                                WHERE psm.Peptide = pi.Peptide AND p.DecoySequence is not null)),
                                        GROUP_CONCAT(pro.Accession),
                                        psm.QValue, psm.Rank, psmScore.Value, psm.Analysis
                                        FROM TempPsmIds tempPsmIds
                                        JOIN UnfilteredPeptideSpectrumMatch psm ON psm.Id = tempPsmIds.Id 
                                        JOIN UnfilteredSpectrum s ON s.Id = psm.Spectrum
                                        JOIN SpectrumSource source ON s.Source = source.Id
                                        JOIN UnfilteredPeptideInstance pi ON psm.Peptide = pi.Peptide
                                        JOIN UnfilteredProtein pro ON pi.Protein = pro.Id
                                        LEFT JOIN ProteinData pd ON pi.Protein=pd.Id
                                        LEFT JOIN PeptideModification pm ON psm.Id = pm.PeptideSpectrumMatch
                                        LEFT JOIN Modification mod ON pm.Modification = mod.Id
                                        LEFT JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
                                        LEFT JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
                                        WHERE scoreName.Name in ( " + "'" + searchScore1Name + "','" + searchScore2Name + "','" + searchScore3Name + "')" +
                                        " GROUP BY psm.Id";

            lock (session)
                allClusterQueryRows = session.CreateSQLQuery(queryCmd).List<object[]>();
            var allClusterSpectrumRows = allClusterQueryRows.Select(o => new ClusterSpectrumRow(o)).ToList();

            session.CreateSQLQuery(@"DROP TABLE IF EXISTS TempPsmIds").ExecuteUpdate();
            reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));
            reportStatus(string.Format("Number of PSMs retrieved: {0} \r\n", allClusterSpectrumRows.Count));

            reportStatus("Rescuing PSMs... ");
            if (writeLog)
            {
                string logHeader = string.Join("\t", new string[] { "SourceName", "NativeID", "Charge", "RescuedSequence", "Protein", "ScoreName", "SearchScore", "BAScore", "QValue", "Rank", "Rank1Sequence", "Rank1Protein", "Rank1SearchScore", "Rank1BAScore", "Rank1Qvalue", "\r\n" });
                File.WriteAllText(logFile, logHeader);
            }

            Dictionary<long, UpdateValues> updateDict = new Dictionary<long, UpdateValues>();  ////key: Id in unfiltered psm table, value: reassigned Qvalue and reassinged Rank
            Set<long> rescuedDistinctSpectraIds = new Set<long>();

            //// SQL query to retrieve anlaysis Id and search score order in QonvertSettings table
            IList<object[]> qonvertSettingsQueryRows;
            lock (session)
                qonvertSettingsQueryRows = session.CreateSQLQuery("SELECT Id, ScoreInfoByName FROM QonverterSettings").List<object[]>();
            var qonvertSettingRows = qonvertSettingsQueryRows.Select(o => new qonvertSettingRows(o)).ToList();
            Dictionary<long, string> analysisScoreOrder = new Dictionary<long, string>();
            Dictionary<long, string> analysisScoreName = new Dictionary<long, string>();
            foreach (var qonvertSettingRow in qonvertSettingRows)
            {
                analysisScoreOrder.Add(qonvertSettingRow.Id, qonvertSettingRow.ScoreOrder);
                analysisScoreName.Add(qonvertSettingRow.Id, qonvertSettingRow.ScoreName);
            }

            ////walk through each cluster to rescue PSMs
            for (int i = 0; i < clusterSetListCount; ++i)
            {
                var clusterSet = clusterSetList.ElementAt(i);

                if (_bgWorkerClustering.CancellationPending)
                {
                    _bgWorkerCancelled = true;
                    return;
                }

                //reportStatus("Clustering set: " + String.Join(",",clusterSet.Select(j => j.ToString()).ToArray()) + "\r\n");
                reportProgress((int)(((double)(i + 1) / (double)clusterSetListCount) * 100), "Rescuing PSMs");
                var clusterSpectrumRows = (from o in allClusterSpectrumRows where clusterSet.Contains(o.SpectrumId) select o).ToList();
                //Map<long, Set<long>> peptideIdDict = new Map<long, Set<long>>(); //key: peptide id, value: psm ids
                //Set<long> unprocessedPSMIds = new Set<long>();
                Set<string> unprocessedSpecChargeAnalysisSet = new Set<string>();  //spectrumId.charge.analysis

                var pepSeqDict = new PepDictionary();  //key: modified peptide sequence, value: spectrumId.charge.analysis, score
                //var peptideIdDict = new PepDictionary(); //key: peptide ID, value: PSM Ids and scores

                foreach (var row in clusterSpectrumRows)
                {
                    //peptideIdDict.Add(row.PeptideId,row.PSMId, row.SearchScore);
                    //peptideIdDict[row.PeptideId].Add(row.PSMId);
                    pepSeqDict.Add(row.ModifiedSequence, row.SpectrumId, row.Charge, row.Analysis, row.SearchScore, row.PSMId);
                    //unprocessedPSMIds.Add(row.PSMId);
                    //unprocessedSpectrumCharge.Add(row.SpectrumId.ToString() + "." + row.Charge.ToString());
                    unprocessedSpecChargeAnalysisSet.Add(row.SpectrumId.ToString() + "." + row.Charge.ToString() + "." + row.Analysis.ToString());
                }


                pepSeqDict.ComputeBayesianAverage(analysisScoreOrder); //replace score from sum of search scores to Bayesian Average

                var sortedPepSeqDictKeys = from k in pepSeqDict.Keys orderby pepSeqDict[k].FinalScore descending, pepSeqDict[k].PsmIdSpecDict.Count() descending select k; // sort by score, if tied, second sort by # of linked psms

                foreach (var pepSeq in sortedPepSeqDictKeys)
                {
                    if (unprocessedSpecChargeAnalysisSet.Count == 0)
                        break;

                    if (pepSeqDict[pepSeq].PsmIdSpecDict.Keys.Any(pId => identPSMIdSet.Contains(pId))) ////at least one psm identified as this peptide in this cluster
                    {
                        foreach (var psmId in pepSeqDict[pepSeq].PsmIdSpecDict.Keys)
                        {
                            var row = (from o in clusterSpectrumRows where o.PSMId == psmId select o).First();
                            string spec = row.SpectrumId.ToString() + "." + row.Charge.ToString() + "." + row.Analysis.ToString();
                            if (unprocessedSpecChargeAnalysisSet.Contains(spec))
                            {
                                if (identPSMIdSet.Contains(psmId) || foundSpectra.Contains(row.SpectrumId))
                                {
                                    //// not process ident PSMs
                                    unprocessedSpecChargeAnalysisSet.Remove(spec);
                                }
                                else
                                {
                                    updateDict.Add(psmId, new UpdateValues(-1, 1)); //// update Qvalue = -1, Rank =1
                                    ++rescuedPSMsCount;
                                    rescuedDistinctSpectraIds.Add(row.SpectrumId);
                                    unprocessedSpecChargeAnalysisSet.Remove(spec);

                                    if (writeLog)
                                    {
                                        string originalRank1Seq = "";
                                        string originalRank1Protein = "";
                                        string originalRank1Score = "";
                                        string originalRank1BAScore = "";
                                        string originalRank1Qvalue = "";

                                        if (row.Rank != 1)
                                        {
                                            var originalRank1Rows = (from o in clusterSpectrumRows where o.SpectrumId == row.SpectrumId && o.Rank == 1 && o.Charge == row.Charge && o.Analysis == row.Analysis select new { o.ModifiedSequence, o.Protein, o.SearchScore, o.QValue }).ToList(); ////may exist more than one rank1 hits
                                            foreach (var originalRank1Row in originalRank1Rows)
                                            {
                                                originalRank1Seq += originalRank1Row.ModifiedSequence + ";";
                                                originalRank1Protein += originalRank1Row.Protein + ";";
                                                originalRank1Score += originalRank1Row.SearchScore.ToString("0.0000") + ";";
                                                originalRank1BAScore += pepSeqDict.ContainsKey(originalRank1Row.ModifiedSequence) ? pepSeqDict[originalRank1Row.ModifiedSequence].FinalScore.ToString("0.0000") + ";" : "";
                                                originalRank1Qvalue += originalRank1Row.QValue.ToString("0.0000") + ";";
                                            }
                                        }
                                        string logLine = string.Join("\t", new string[] { row.SourceName, row.SpectrumNativeID, row.Charge.ToString(), row.ModifiedSequence, row.Protein, analysisScoreName[row.Analysis], row.SearchScore.ToString("0.0000"), pepSeqDict[pepSeq].FinalScore.ToString("0.0000"), row.QValue.ToString("0.0000"), row.Rank.ToString(), originalRank1Seq, originalRank1Protein, originalRank1Score, originalRank1BAScore, originalRank1Qvalue });
                                        using (StreamWriter sw = File.AppendText(logFile))
                                        {
                                            sw.WriteLine(logLine);
                                        }
                                    }
                                }
                            }
                        }
                    }
                } //// end of foreach (var pepSeq in sortedPepSeqDictKeys)

            } //// end of for (int i = 0; i < clusterSetListCount; ++i)
            reportStatus(string.Format("{0} seconds elapsed\r\n", (DateTime.Now - startTime).TotalSeconds));

            /*
             *update unfiltered psm table in idpDB
            */
            if (rescuedPSMsCount == 0)
                return;

            reportStatus("Updating idpDB... ");

            session.Transaction.Begin();
            //basicDataFilter.DropFilters(session);  // tables were dropped before querying
            var updateCmd = session.Connection.CreateCommand();
            updateCmd.CommandText = "UPDATE UnfilteredPeptideSpectrumMatch SET QValue = ?, Rank = ? WHERE Id = ?";
            var updateParameters = new List<System.Data.IDbDataParameter>();
            for (int i = 0; i < 3; ++i)
            {
                updateParameters.Add(updateCmd.CreateParameter());
                updateCmd.Parameters.Add(updateParameters[i]);
            }
            updateCmd.Prepare();
            int updateCount = 0;
            int allUpdateCount = updateDict.Count;
            foreach (KeyValuePair<long, UpdateValues> pair in updateDict)
            {
                updateParameters[0].Value = pair.Value.ReassignedQvalue;   //// Qvalue
                updateParameters[1].Value = pair.Value.ReassignedRank;   //// Rank
                updateParameters[2].Value = pair.Key;    //// psm id
                updateCmd.ExecuteNonQuery();
                reportProgress((int)(((double)(updateCount + 1) / (double)allUpdateCount) * 100), "Updating idpDB");
                ++updateCount;
            }
            session.Transaction.Commit();

            //basicDataFilter.ApplyBasicFilters(session);
            reportStatus(reportSecondsElapsed((DateTime.Now - startTime).TotalSeconds));
            reportStatus(string.Format("Rescued {0} PSMs for {1} distinct spectra\r\n", rescuedPSMsCount, rescuedDistinctSpectraIds.Count));
            reportProgress(0, "Ready");
            /*
             * not recompute q values, reload idpDB, implemented in _bgWorkerClustering_RunWorkerCompleted
            */

        } //// end of RescuePSMsByClustering

        private void _bgWorkerClustering_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {                
                RescuePSMsByClustering();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        //private void _bgWorkerClustering_ProgressChanged(object sender, ProgressChangedEventArgs e)
        //{
        //    progressBar.Value = e.ProgressPercentage;
        //    string statusMsg = (string)e.UserState;
        //    lblStatusToolStrip.Text = statusMsg;
        //    tbStatus.AppendText(statusMsg);                    
        //}

        private void _bgWorkerClustering_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
                Program.HandleException(e.Result as Exception);

            Text = TabText = "Rescue PSMs";
            if (_bgWorkerCancelled)
            {
                tbStatus.AppendText("Cancelled\r\n");
            }
            else
            {
                if (rescuedPSMsCount != 0)
                {
                    tbStatus.AppendText("Reloading idpDB...\r\n");
                    owner.ReloadSession(session);  //true if recompute Qvalue
                    owner.ApplyBasicFilter();
                }

                tbStatus.AppendText("Completed\r\n");
            }

            if (writeLog)
            {
                File.AppendAllText(logFile, tbStatus.Text);
            }

            btnClustering.Enabled = true;
            btnCancel.Enabled = false;
            ClearData();
        }

        private void btnClustering_Click(object sender, EventArgs e)
        {
            btnClustering.Enabled = false;
            btnCancel.Enabled = true;
            tbStatus.Clear();

            similarityThreshold = Convert.ToDouble(tbSimilarityThreshold.Text);
            precursorMzTolerance = Convert.ToDouble(tbPrecursorMzTolerance.Text);
            fragmentMzTolerance = Convert.ToDouble(tbFragmentMzTolerance.Text);
            backupDB = cbBackupDB.Checked ? true : false;
            maxRank = Convert.ToInt32(tbRank.Text);
            searchScore1Name = cmbSearchScore1Name.Text;
            searchScore1Order = cmbSearchScore1Order.Text;
            searchScore1Threshold = Convert.ToDouble(tbSearchScore1Threshold.Text);
            searchScore2Name = cmbSearchScore2Name.Text;
            searchScore2Order = cmbSearchScore2Order.Text;
            searchScore2Threshold = Convert.ToDouble(tbSearchScore2Threshold.Text);
            searchScore3Name = cmbSearchScore3Name.Text;
            searchScore3Order = cmbSearchScore3Order.Text;
            searchScore3Threshold = Convert.ToDouble(tbSearchScore3Threshold.Text);
            writeLog = cbWriteLog.Checked ? true : false;
            minClusterSize = Convert.ToInt32(tbMinClusterSize.Text);
            rescuedPSMsCount = 0;

            _bgWorkerClustering = new BackgroundWorker();
            //_bgWorkerClustering.WorkerReportsProgress = true;
            _bgWorkerClustering.WorkerSupportsCancellation = true;

            _bgWorkerCancelled = false;

            _bgWorkerClustering.DoWork += new DoWorkEventHandler(_bgWorkerClustering_DoWork);
            //_bgWorkerClustering.ProgressChanged += new ProgressChangedEventHandler(_bgWorkerClustering_ProgressChanged);
            _bgWorkerClustering.RunWorkerCompleted +=new RunWorkerCompletedEventHandler(_bgWorkerClustering_RunWorkerCompleted);
            _bgWorkerClustering.RunWorkerAsync();
                        
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            tbStatus.AppendText("Cancelling...\r\n");
            if ((null != _bgWorkerClustering) && (_bgWorkerClustering.IsBusy))
            {
                _bgWorkerClustering.CancelAsync();                
            }

        }
    }
}
