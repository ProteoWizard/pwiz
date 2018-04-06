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
// Copyright 2012 Vanderbilt University
//
// Contributor(s): 
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using IDPicker.DataModel;
using NHibernate;
using NHibernate.Linq;

using pwiz.CLI.analysis;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    public partial class ExportForm : Form
    {
        public ExportForm()
        {
            InitializeComponent();
        }

        internal void toExcel(bool selected, IDictionary<string, TableExporter.ITable> tables, DataFilter viewFilter, DataFilter basicFilter)
        {
            Text = "Generating Excel Report pages (1 of 6)";

            var bg = new BackgroundWorker { WorkerReportsProgress = true };
            bg.ProgressChanged += (x, y) =>
            {
                switch (y.ProgressPercentage)
                {
                    case 1:
                        Text = "Generating Excel Report pages (2 of 6)";
                        break;
                    case 2:
                        Text = "Generating Excel Report pages (3 of 6)";
                        break;
                    case 3:
                        Text = "Generating Excel Report pages (4 of 6)";
                        break;
                    case 4:
                        Text = "Generating Excel Report pages (5 of 6)";
                        break;
                    case 5:
                        Text = "Generating Excel Report pages (6 of 6)";
                        break;
                    default:
                        break;
                }
            };

            bg.RunWorkerCompleted += (x, y) =>
            {
                if (y.Error != null) Program.HandleException(y.Error);
                Close();
            };

            bg.DoWork += (x, y) =>
            {
                var reportDictionary = new Dictionary<string, object>();

                int progress = 0;
                foreach(var nameTablePair in tables)
                {
                    reportDictionary.Add(nameTablePair.Key, nameTablePair.Value);
                    bg.ReportProgress(++progress);
                }

                reportDictionary.Add("Summary", getSummaryList(viewFilter, basicFilter));

                TableExporter.ShowInExcel(tables, false);
            };

            bg.RunWorkerAsync();
        }

        internal void toHTML(bool selected,
                             IDictionary<string, TableExporter.ITable> enumerableTables,
                             IDictionary<string, List<TableExporter.TableTreeNode>> treeTables,
                             DataFilter viewFilter, DataFilter basicFilter, ISession session)
        {
            string progressTitle = "Generating HTML Report";
            Text = progressTitle;

            if (session == null)
            {
                MessageBox.Show("No Report Loaded");
                return;
            }

            string outFolder;
            var fbd = new FolderBrowserDialog() { Description = "Select destination folder" };
            if (fbd.ShowDialog() != DialogResult.OK)
            {
                Close();
                return;
            }

            outFolder = fbd.SelectedPath;

            var textDialog = new TextInputPrompt("Report folder name", false, Path.GetFileNameWithoutExtension(session.Connection.GetDataSource()));
            while (true)
            {
                if (textDialog.ShowDialog() != DialogResult.OK)
                {
                    Close();
                    return;
                }

                var result = textDialog.GetText();
                if (Directory.Exists(Path.Combine(outFolder, result)))
                {
                    var response = MessageBox.Show("Report folder path already exists, overwrite?",
                                                    "Overwrite path?", MessageBoxButtons.YesNoCancel);
                    if (response == DialogResult.Yes)
                    {
                        outFolder = Path.Combine(outFolder, result);
                        var di = new DirectoryInfo(outFolder);
                        try
                        {
                            foreach (var file in di.GetFiles())
                                File.Delete(file.FullName);
                        }
                        catch
                        {
                            MessageBox.Show("Could not overwrite. Please enter a new name" +
                                            " or make sure the report is closed and try again.");
                            continue;
                        }

                        break;
                    }
                    if (response == DialogResult.Cancel) return;
                }
                else
                {
                    outFolder = Path.Combine(outFolder, result);
                    break;
                }
            }

            var bg = new BackgroundWorker { WorkerReportsProgress = true };
            bg.DoWork += delegate
            {
                var textTables = new Dictionary<string, List<List<string>>> { { "Summary", getSummaryList(viewFilter, basicFilter) } };
                CreateHtmlReport(bg, outFolder, enumerableTables, textTables, treeTables, viewFilter, basicFilter, session);
            };

            bg.ProgressChanged += (x, y) =>
            {
                if (y.ProgressPercentage == 100)
                    Close();
                else
                {
                    progressBar.Value = y.ProgressPercentage;
                    string message = y.UserState as string;
                    if (message != null)
                        Text = progressTitle + " - " + message;
                }
            };

            bg.RunWorkerCompleted += (x, y) => { if (y.Error != null) Program.HandleException(y.Error); };
            bg.RunWorkerAsync();
        }

        private List<List<string>> getSummaryList(DataFilter viewFilter, DataFilter basicFilter)
        {
            var summaryList = new List<List<string>>();

            var totalCounts = viewFilter.TotalCounts;
            // TODO: add this - summaryList.Add(new List<string> { "Modifications", totalCounts.Modifications.ToString() });
            summaryList.Add(new List<string> { "Protein Groups", totalCounts.ProteinGroups.ToString() });
            summaryList.Add(new List<string> { "Proteins", totalCounts.Proteins.ToString() });
            summaryList.Add(new List<string> { "Distinct Peptides", totalCounts.DistinctPeptides.ToString() });
            summaryList.Add(new List<string> { "Distinct Peptide Matches", totalCounts.DistinctMatches.ToString() });
            summaryList.Add(new List<string> { "Spectra", totalCounts.FilteredSpectra.ToString() });
            // TODO: add this - summaryList.Add(new List<string> { "Sources", totalCounts.SpectrumSources.ToString() });
            // TODO: add this - summaryList.Add(new List<string> { "Groups", totalCounts.SpectrumSourceGroups.ToString() });

            //Summary Filters
            var filterInfo = new List<List<string>>();
            if (basicFilter != null)
            {
                filterInfo.Add(new List<string> { "Max Q Value %", (viewFilter.MaximumQValue * 100).ToString() });
                filterInfo.Add(new List<string> { "Min Distinct Peptides", viewFilter.MinimumDistinctPeptides.ToString() });
                filterInfo.Add(new List<string> { "Min Additional Peptides", viewFilter.MinimumAdditionalPeptides.ToString() });
                filterInfo.Add(new List<string> { "Min Spectra", viewFilter.MinimumSpectra.ToString() });
                if (viewFilter.GeneLevelFiltering)
                    filterInfo.Add(new List<string> { "Filtering by Gene", viewFilter.GeneLevelFiltering.ToString() });

                #region In-depth filters

                if (viewFilter.SpectrumSourceGroup != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.SpectrumSourceGroup)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Spectrum Source Group", item.Name });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Name });
                    }
                }
                if (viewFilter.SpectrumSource != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.SpectrumSource)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Spectrum Source", item.Name });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Name });
                    }
                }
                if (viewFilter.Spectrum != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Spectrum)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Spectrum", item.NativeID });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.NativeID });
                    }
                }
                if (viewFilter.Charge != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Charge)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Charge", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.PeptideGroup != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.PeptideGroup)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Peptide Group", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.Peptide != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Peptide)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Peptide", item.Sequence });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Sequence });
                    }
                }
                if (viewFilter.DistinctMatchKey != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.DistinctMatchKey)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Distinct Match", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.ModifiedSite != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.ModifiedSite)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Modified Site", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.Modifications != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Modifications)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Modifications", item.MonoMassDelta.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.MonoMassDelta.ToString() });
                    }
                }
                if (viewFilter.ProteinGroup != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.ProteinGroup)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Protein Group", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }
                if (viewFilter.Protein != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Protein)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Protein", item.Accession });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.Accession });
                    }
                }
                if (viewFilter.Cluster != null)
                {
                    bool first = true;
                    foreach (var item in viewFilter.Cluster)
                    {
                        if (first)
                        {
                            filterInfo.Add(new List<string> { "Cluster", item.ToString() });
                            first = false;
                        }
                        else
                            filterInfo.Add(new List<string> { string.Empty, item.ToString() });
                    }
                }

                #endregion
            }

            if (filterInfo.Count > 0)
            {
                summaryList.Add(new List<string> { string.Empty });
                summaryList.Add(new List<string> { string.Empty });
                summaryList.Add(new List<string> { " --- Filters --- " });
                summaryList.AddRange(filterInfo);
            }
            return summaryList;
        }

        private Dictionary<string, List<TableExporter.TableTreeNode>> getSourceContentsForHTML(DataFilter viewFilter, DataFilter dataFilter, ISession session)
        {
            const int decimalPlaces = 4;
            var allContents = new Dictionary<string, List<TableExporter.TableTreeNode>>();

            //get score info
            //score gathering is one of the most time-intensive processes in item retreaval
            //loading everything to memory if possible should speed things up significantly
            //low speed system should stay in place in case memory cant handle the load
            var rawScoreList = session.CreateSQLQuery("select name, id from PeptideSpectrumMatchScoreName").List<object[]>();
            var scoreList = new List<string>();
            var scoreNameToId = new Dictionary<string, string>();
            foreach (var item in rawScoreList)
            {
                scoreList.Add(item[0].ToString());
                if (!scoreNameToId.ContainsKey(item[0].ToString()))
                    scoreNameToId.Add(item[0].ToString(), item[1].ToString());
            }
            var scoresCaptured = true;
            var scoreCache = new Dictionary<string, Dictionary<string, string>>();
            try
            {
                var allScores = session.CreateSQLQuery("Select PsmID, ScoreNameId, CAST(Value AS TEXT) from PeptideSpectrumMatchScore").List();
                foreach (var row in allScores)
                {
                    var rowContents = (object[])row;
                    if (!scoreCache.ContainsKey(rowContents[0].ToString()))
                        scoreCache.Add(rowContents[0].ToString(), new Dictionary<string, string>());
                    if (!scoreCache[rowContents[0].ToString()].ContainsKey(rowContents[1].ToString()))
                        scoreCache[rowContents[0].ToString()].Add(rowContents[1].ToString(), rowContents[2].ToString());
                }
            }
            catch (Exception e)
            {
                scoresCaptured = false;
                var errorMessage =
                        "[SpectrumTableForm] Error when precaching data. " +
                        "Results may be processed slower than expected - " +
                        Environment.NewLine + e.Message;
                if (InvokeRequired)
                    Invoke(new Action(() => MessageBox.Show(errorMessage)));
                else
                    MessageBox.Show(errorMessage);
            }

            var sourceTreeHeaders = new List<string>
                                           {
                                               "'Spectrum'",
                                               "'Distinct Peptides'",
                                               "'Distinct Analyses'",
                                               "'Distinct Charges'",
                                               "'Precursor M/Z'"
                                           };

            var psmHeaders = new List<string>()
                                  {
                                      "'Rank'",
                                      "'Charge'",
                                      "'Observed Mass'",
                                      "'Monoisotopic Mass'",
                                      "'Mass Error'",
                                      "'Q Value'",
                                      "'Sequence'"
                                  };

            psmHeaders.AddRange(scoreList.Select(item => string.Format("'{0}'", item)));

            //get source info
            var sources = session.QueryOver<SpectrumSource>().List();
            foreach (var source in sources)
            {
                var exportTable = new List<TableExporter.TableTreeNode>();
                var sourceFilter = new DataFilter(dataFilter) { SpectrumSource = new List<SpectrumSource> { source } };
                var grouping = new List<Grouping<GroupBy>> { new Grouping<GroupBy>(true) { Mode = GroupBy.Spectrum } };
                var spectraRows = SpectrumTableForm.getSpectrumRows(session, grouping, sourceFilter);

                foreach (SpectrumTableForm.SpectrumRow spectra in spectraRows)
                {
                    var key = spectra.Spectrum.NativeID;
                    try
                    {
                        key = pwiz.CLI.msdata.id.abbreviate(key);
                    }
                    catch
                    {
                    }
                    var newBranch = new TableExporter.TableTreeNode
                    {
                        Text = key,
                        Headers = sourceTreeHeaders,
                        Cells = new List<string>
                                {
                                    "'" + key + "'",
                                    spectra.DistinctPeptides.ToString(),
                                    spectra.DistinctAnalyses.ToString(),
                                    spectra.DistinctCharges.ToString(),
                                    Math.Round(spectra.Spectrum.PrecursorMZ,decimalPlaces).ToString()
                                }
                    };
                    foreach (var match in spectra.Spectrum.Matches)
                    {
                        var observedMass = match.Spectrum.PrecursorMZ * match.Charge -
                                           match.Charge * pwiz.CLI.chemistry.Proton.Mass;
                        var matchNode = new TableExporter.TableTreeNode { Text = match.Rank.ToString(), Headers = psmHeaders };
                        var cells = new List<string>
                                      {
                                          "'" + match.Rank + "'",
                                          match.Charge.ToString(),
                                          Math.Round(observedMass,decimalPlaces).ToString(),
                                          Math.Round(match.ObservedNeutralMass,decimalPlaces).ToString(),
                                          Math.Round(match.MonoisotopicMassError,decimalPlaces).ToString(),
                                          Math.Round(match.QValue,decimalPlaces).ToString(),
                                          "'" + match.Peptide.Sequence + "'"
                                      };
                        foreach (var score in scoreList)
                        {
                            if (scoresCaptured)
                            {
                                var scoreValue = scoreCache[match.Id.ToString()][scoreNameToId[score]];
                                cells.Add(Math.Round(double.Parse(scoreValue), decimalPlaces).ToString());
                            }
                            else
                            {
                                try
                                {
                                    cells.Add(Math.Round(match.Scores[score.Trim("'".ToCharArray())], decimalPlaces).ToString());
                                }
                                catch
                                {
                                    cells.Add(string.Empty);
                                }
                            }
                        }
                        matchNode.Cells = cells;
                        newBranch.Nodes.Add(matchNode);
                    }
                    exportTable.Add(newBranch);
                }
                allContents.Add(source.Name, exportTable);
            }
            return allContents;
        }

        private List<TableExporter.TableTreeNode> getSpectrumSourceGroupTree(DataFilter viewFilter, DataFilter dataFilter, ISession session)
        {
            var groupTreeHeaders = new List<string>
                                           {
                                               "'Group'",
                                               "'Filtered Spectra'",
                                               "'Distinct Peptides'",
                                               "'Distinct Matches'",
                                               "'Distinct Analyses'",
                                               "'Distinct Charges'"
                                           };

            var sourceTreeHeaders = new List<string>
                                           {
                                               "'Source'",
                                               "'Filtered Spectra'",
                                               "'Distinct Peptides'",
                                               "'Distinct Matches'",
                                               "'Distinct Analyses'",
                                               "'Distinct Charges'"
                                           };

            var groupNodes = new List<TableExporter.TableTreeNode>();
            var groups = session.CreateQuery(SpectrumTableForm.AggregateRow.Selection + ", ssgl " +
                                             dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                               DataFilter.PeptideSpectrumMatchToProtein,
                                                                               DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                             "GROUP BY ssgl.Group.id")
                                .List<object[]>()
                                .Select(o => new SpectrumTableForm.SpectrumSourceGroupRow(o, viewFilter));
            foreach (var group in groups)
            {
                var newNode = new TableExporter.TableTreeNode
                {
                    Text = group.SpectrumSourceGroup.Name,
                    Headers = groupTreeHeaders,
                    Cells = new List<string>
                            {
                                "'" + group.SpectrumSourceGroup.Name + "'",
                                group.Spectra.ToString(),
                                group.DistinctPeptides.ToString(),
                                group.DistinctMatches.ToString(),
                                group.DistinctAnalyses.ToString(),
                                group.DistinctCharges.ToString()
                            }
                };
                var groupFilter = new DataFilter(viewFilter)
                {
                    SpectrumSourceGroup = new List<SpectrumSourceGroup> { group.SpectrumSourceGroup }
                };

                var sources = session.CreateQuery(SpectrumTableForm.AggregateRow.Selection + ", s.Source " +
                                                      groupFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                         DataFilter.PeptideSpectrumMatchToProtein,
                                                                                         DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                                      "GROUP BY s.Source.id")
                        .List<object[]>()
                        .Select(o => new SpectrumTableForm.SpectrumSourceRow(o, groupFilter));
                SpectrumTableForm.SpectrumSourceGroupRow ssgr = group;
                sources = from SpectrumTableForm.SpectrumSourceRow s in sources
                          where s.SpectrumSource.Group == ssgr.SpectrumSourceGroup
                          select s;
                foreach (var source in sources)
                {
                    var newSubNode = new TableExporter.TableTreeNode
                    {
                        Text = "source" + source.SpectrumSource.Id ?? string.Empty + ".html",
                        Headers = sourceTreeHeaders,
                        Cells = new List<string>
                                {
                                    String.Format("'<a href=\"{0}.html\">{0}</a>'", source.SpectrumSource.Name),
                                    source.Spectra.ToString(),
                                    source.DistinctPeptides.ToString(),
                                    source.DistinctMatches.ToString(),
                                    source.DistinctAnalyses.ToString(),
                                    source.DistinctCharges.ToString()
                                }
                    };
                    newNode.Nodes.Add(newSubNode);
                }
                groupNodes.Add(newNode);
            }
            return groupNodes;
        }

        private void CreateHtmlReport(BackgroundWorker bg, string outFolder,
                                      IDictionary<string, TableExporter.ITable> enumerableTables,
                                      IDictionary<string, List<List<string>>> textTables,
                                      IDictionary<string, List<TableExporter.TableTreeNode>> treeTables,
                                      DataFilter viewFilter, DataFilter basicFilter, ISession session)
        {
            if (!Directory.Exists(outFolder))
                Directory.CreateDirectory(outFolder);
            var reportName = Path.GetFileName(outFolder);

            //generate resource files
            var css = Properties.Resources.idpicker_style;
            var jsFunctions = Properties.Resources.idpicker_scripts;
            var cssStream = new StreamWriter(Path.Combine(outFolder, "idpicker-style.css"));
            var jsStream = new StreamWriter(Path.Combine(outFolder, "idpicker-scripts.js"));
            cssStream.Write(css);
            cssStream.Flush();
            cssStream.Close();
            jsStream.Write(jsFunctions);
            jsStream.Flush();
            jsStream.Close();
            
            double progress = 5.0;
            bg.ReportProgress((int) Math.Round(progress), "creating tables");

            int totalTables = enumerableTables.Count + textTables.Count + treeTables.Count;
            double progressPerTable = 35.0 / totalTables;

            //generate html files
            foreach(var nameTablePair in enumerableTables)
            {
                var tableList = new List<TableExporter.ITable> { nameTablePair.Value };
                TableExporter.CreateHTMLTablePage(tableList,
                                                  Path.Combine(outFolder, String.Format("{0} {1}.html", reportName, nameTablePair.Key)),
                                                  String.Format("{0} - {1}", reportName, nameTablePair.Key),
                                                  true, false, false);
                progress += progressPerTable;
                bg.ReportProgress((int) Math.Round(progress), "creating tables");
            }

            foreach(var nameTablePair in textTables)
            {
                var tableList = new List<List<List<string>>> { nameTablePair.Value };
                TableExporter.CreateHTMLTablePage(tableList,
                                                  Path.Combine(outFolder, String.Format("{0} {1}.html", reportName, nameTablePair.Key)),
                                                  String.Format("{0} - {1}", reportName, nameTablePair.Key),
                                                  true, false, false);
                progress += progressPerTable;
                bg.ReportProgress((int) Math.Round(progress), "creating tables");
            }

            foreach (var nameTablePair in treeTables)
            {
                var tableList = new List<List<TableExporter.TableTreeNode>> { nameTablePair.Value };
                TableExporter.CreateHTMLTreePage(nameTablePair.Value, Path.Combine(outFolder, String.Format("{0} {1}.html", reportName, nameTablePair.Key)),
                                                 String.Format("{0} - {1}", reportName, nameTablePair.Key));
                progress += progressPerTable;
                bg.ReportProgress((int )Math.Round(progress), "creating tree tables");
            }

            var clusterList = new List<string[]>();
            if (session != null)
            {
                //try to pre-cache data if possible
                //report scales horribly with cluster count (655 clusters took 40 minutes)
                //limiting calls to the database should speed this up considerably
                var clusterIDList = session.CreateSQLQuery("select distinct cluster from protein").List().Cast<int>().ToList();
                var tempFilter = new DataFilter(viewFilter) { Cluster = new List<int>(), Protein = new List<Protein>() };
                var clusterToProteinList = new Dictionary<int, List<ProteinTableForm.ProteinGroupRow>>();
                var proteinAccessionToPeptideList = new Dictionary<string, HashSet<int>>();
                List<PeptideTableForm.DistinctPeptideRow> peptideList;

                progress = 50;
                bg.ReportProgress((int) Math.Round(progress), "precaching cluster data");

                double progressPerCluster = 25.0 / clusterIDList.Count;

                try
                {
                    //seperating proteinList namespace in order to try to conserve memory
                    {
                        var proteinList = ProteinTableForm.ProteinGroupRow.GetRows(session, tempFilter).ToList();
                        foreach (var protein in proteinList)
                        {
                            if (!clusterToProteinList.ContainsKey(protein.FirstProtein.Cluster))
                                clusterToProteinList.Add(protein.FirstProtein.Cluster,
                                                         new List<ProteinTableForm.ProteinGroupRow> { protein });
                            else
                                clusterToProteinList[protein.FirstProtein.Cluster].Add(protein);
                        }
                    }

                    peptideList = PeptideTableForm.DistinctPeptideRow.GetRows(session, tempFilter).ToList();
                    for (var x = 0; x < peptideList.Count; x++)
                    {
                        var peptide = peptideList[x].Peptide;
                        foreach (var instance in peptide.Instances)
                        {
                            var proteinAccession = instance.Protein.Accession;
                            if (!proteinAccessionToPeptideList.ContainsKey(proteinAccession))
                                proteinAccessionToPeptideList.Add(proteinAccession, new HashSet<int> { x });
                            else
                                proteinAccessionToPeptideList[proteinAccession].Add(x);
                        }
                    }
                }
                catch (Exception e)
                {
                    peptideList = null;
                    var errorMessage =
                        "[ClusterInfo] Error when precaching data. " +
                        "Results may be processed slower than expected - " +
                        Environment.NewLine + e.Message;
                    if (InvokeRequired)
                        Invoke(new Action(() => MessageBox.Show(errorMessage)));
                    else
                        MessageBox.Show(errorMessage);
                }

                progress = 60;
                bg.ReportProgress((int) Math.Round(progress), "creating clusters");

                foreach (var clusterID in clusterIDList)
                {
                    ClusterInfo cluster;
                    if (peptideList == null)
                        cluster = getClusterInfo(clusterID, session, viewFilter);
                    else
                        cluster = getClusterInfo(clusterID, clusterToProteinList[clusterID],
                                                 proteinAccessionToPeptideList, peptideList);
                    if (cluster.proteinGroupCount > 0)
                        TableExporter.CreateHTMLTablePage(cluster.clusterTables,
                                                          Path.Combine(outFolder, "cluster" + cluster.clusterID + ".html"),
                                                          reportName + "- Cluster" + cluster.clusterID,
                                                          true, false, true);
                    clusterList.Add(new[]
                                        {
                                            cluster.clusterID.ToString(),
                                            String.Format("'<a href=\"cluster{0}.html\" target=\"mainFrame\">{0}</a>'", cluster.clusterID),
                                            cluster.proteinGroupCount.ToString(),
                                            cluster.peptideCount.ToString(),
                                            cluster.spectraCount.ToString()
                                        });

                    progress += progressPerCluster;
                    bg.ReportProgress((int) Math.Round(progress), "creating clusters");
                }
            }

            bg.ReportProgress(85, "creating source tree tables");
            var sources = getSourceContentsForHTML(viewFilter, basicFilter, session);
            foreach (var source in sources)
                TableExporter.CreateHTMLTreePage(source.Value, Path.Combine(outFolder, source.Key + ".html"), source.Key);

            bg.ReportProgress(90, "creating source group tree table");
            var groups = getSpectrumSourceGroupTree(viewFilter, basicFilter, session);
            TableExporter.CreateHTMLTreePage(groups, Path.Combine(outFolder, reportName + " Source Groups.html"), reportName + " - Source Groups");

            bg.ReportProgress(95, "finishing up");
            //generate navigation page
            var tableNames = new List<string>();
            tableNames.Add("Source Groups");
            tableNames.AddRange(textTables.Keys);
            tableNames.AddRange(enumerableTables.Keys);
            tableNames.AddRange(treeTables.Keys);

            TableExporter.CreateNavigationPage(clusterList, outFolder, reportName, tableNames);
            TableExporter.CreateIndexPage(outFolder, reportName);
            bg.ReportProgress(100);
            if (File.Exists(Path.Combine(outFolder, "index.html")))
                System.Diagnostics.Process.Start(Path.Combine(outFolder, "index.html"));
        }

        private ClusterInfo getClusterInfo(int cluster, ISession session, DataFilter viewFilter)
        {
            var tempFilter = new DataFilter(viewFilter) { Cluster = new List<int> { cluster }, Protein = new List<Protein>() };
            var proteinAccessionToPeptideList = new Dictionary<string, HashSet<int>>();
            var peptideList = new List<PeptideTableForm.DistinctPeptideRow>();
            var peptideFound = new HashSet<long?>();

            var proteinList = ProteinTableForm.ProteinGroupRow.GetRows(session, tempFilter).ToList();
            foreach (var proteinGroup in proteinList)
            {
                //get peptides in protein
                var allGroupedProteins = session.CreateQuery(String.Format(
                    "SELECT pro FROM Protein pro WHERE pro.Accession IN ('{0}')",
                    proteinGroup.Proteins.Replace(",", "','")))
                    .List<Protein>();
                var proteinFilter = new DataFilter(tempFilter) { Protein = allGroupedProteins };
                var peptides = PeptideTableForm.DistinctPeptideRow.GetRows(session, proteinFilter);
                foreach (var peptide in peptides)
                {
                    if (peptideFound.Add(peptide.Peptide.Id))
                        peptideList.Add(peptide);
                }
                for (var x = 0; x < peptideList.Count; x++)
                {
                    var peptide = peptideList[x].Peptide;
                    foreach (var instance in peptide.Instances)
                    {
                        var proteinAccession = instance.Protein.Accession;
                        if (!proteinAccessionToPeptideList.ContainsKey(proteinAccession))
                            proteinAccessionToPeptideList.Add(proteinAccession, new HashSet<int> { x });
                        else
                            proteinAccessionToPeptideList[proteinAccession].Add(x);
                    }
                }

            }

            return getClusterInfo(cluster, proteinList, proteinAccessionToPeptideList, peptideList);
        }

        private ClusterInfo getClusterInfo(int cluster, List<ProteinTableForm.ProteinGroupRow> proteinList,
            Dictionary<string, HashSet<int>> proteinAccessionToPeptideList,
            List<PeptideTableForm.DistinctPeptideRow> peptideList)
        {
            var ci = new ClusterInfo { peptideCount = 0, spectraCount = 0, clusterID = cluster };
            var allTables = new List<List<List<string>>>();

            var sequence2Data = new Dictionary<string, List<string>>();
            var sequence2Group = new Dictionary<string, List<int>>();
            var group2Sequences = new Dictionary<string, List<string>>();
            var peptideGroupList = new List<string>();

            var proteinTable = new List<List<string>>
                                   {
                                       new List<string>
                                           {
                                               "Group",
                                               "Accession",
                                               "Peptides",
                                               "Spectra",
                                               "Description"
                                           }
                                   };
            ci.proteinGroupCount = proteinList.Count;

            for (int x = 0; x < proteinList.Count; x++)
            {
                var proteinGroup = proteinList[x];
                proteinTable.Add(new List<string>
                                     {
                                         TableExporter.IntToColumn(x + 1),
                                         proteinGroup.Proteins,
                                         proteinGroup.DistinctPeptides.ToString(),
                                         proteinGroup.Spectra.ToString(),
                                         proteinGroup.FirstProtein.Description
                                     });

                //get peptides in protein
                var usedPeptides = new HashSet<PeptideTableForm.DistinctPeptideRow>();
                var allGroupedProteins = proteinGroup.Proteins.Split(",".ToCharArray());
                foreach (var protein in allGroupedProteins)
                    foreach (var peptideIndex in proteinAccessionToPeptideList[protein])
                        usedPeptides.Add(peptideList[peptideIndex]);

                foreach (var peptide in usedPeptides)
                {
                    if (sequence2Data.ContainsKey(peptide.Peptide.Sequence))
                        sequence2Group[peptide.Peptide.Sequence].Add(x);
                    else
                    {
                        sequence2Data.Add(peptide.Peptide.Sequence,
                                          new List<string>
                                              {
                                                  peptide.Spectra.ToString(),
                                                  Math.Round(peptide.Peptide.MonoisotopicMass, 4).ToString(),
                                                  Math.Round(peptide.Peptide.Matches.Min(n => n.QValue), 4).ToString()
                                              });
                        sequence2Group.Add(peptide.Peptide.Sequence, new List<int> { x });
                        ci.spectraCount += peptide.Spectra;
                        ci.peptideCount++;
                    }
                }
            }
            allTables.Add(proteinTable);

            foreach (var kvp in sequence2Group)
            {
                kvp.Value.Sort();
                var value = new List<string>();
                foreach (var group in kvp.Value)
                    value.Add(group.ToString());
                var groupName = string.Join(",", value.ToArray());

                if (group2Sequences.ContainsKey(groupName))
                    group2Sequences[groupName].Add(kvp.Key);
                else
                {
                    group2Sequences.Add(groupName, new List<string> { kvp.Key });
                    peptideGroupList.Add(groupName);
                }
            }

            peptideGroupList.Sort();
            var peptideTable = new List<List<string>>
                                   {
                                       new List<string>
                                           {
                                               "PeptideGroup",
                                               "Unique",
                                               "Sequence",
                                               "Spectra",
                                               "Mass",
                                               "Best Q-Value"
                                           }
                                   };
            for (var x = 0; x < peptideGroupList.Count; x++)
            {
                var first = true;
                var unique = peptideGroupList[x].Length == 1;
                foreach (var peptide in group2Sequences[peptideGroupList[x]])
                {
                    peptideTable.Add(new List<string>
                                         {
                                             first ? (x + 1).ToString() : string.Empty,
                                             unique ? "*" : string.Empty,
                                             peptide,
                                             sequence2Data[peptide][0],
                                             sequence2Data[peptide][1],
                                             sequence2Data[peptide][2]
                                         });
                    first = false;
                }
            }
            allTables.Add(peptideTable);

            var associationTable = new List<List<string>>();

            //first row
            var tempList = new List<string> { string.Empty };
            for (var x = 1; x <= peptideGroupList.Count; x++)
                tempList.Add(x.ToString());
            associationTable.Add(tempList);

            //second row
            tempList = new List<string>() { "Peptides" };
            for (var x = 0; x < peptideGroupList.Count; x++)
                tempList.Add(group2Sequences[peptideGroupList[x]].Count.ToString());
            associationTable.Add(tempList);

            //third row
            tempList = new List<string>() { "Spectra" };
            for (var x = 0; x < peptideGroupList.Count; x++)
            {
                var spectraCount = 0;
                foreach (var sequence in group2Sequences[peptideGroupList[x]])
                {
                    int peptideSpectra;
                    int.TryParse(sequence2Data[sequence][0], out peptideSpectra);
                    spectraCount += peptideSpectra;
                }
                tempList.Add(spectraCount.ToString());
            }
            associationTable.Add(tempList);

            //protein rows
            for (var x = 0; x < proteinList.Count; x++)
            {
                tempList = new List<string> { TableExporter.IntToColumn(x + 1) };
                for (var y = 0; y < peptideGroupList.Count; y++)
                {
                    var containedProGroups = peptideGroupList[y].Split(",".ToCharArray());
                    var containNumbers = containedProGroups.Select(item => int.Parse(item));
                    tempList.Add(containNumbers.Contains(x) ? "x" : string.Empty);
                }
                associationTable.Add(tempList);
            }
            allTables.Add(associationTable);

            ci.clusterTables = allTables;
            return ci;
        }

        private static Dictionary<int, string> sptxtMods = new Dictionary<int, string>()
                                                              {
                                                                  {16, "Oxidation"},
                                                                  {57, "Carbamidomethyl"},
                                                                  {227, "ICAT_light"},
                                                                  {236, "ICAT_heavy"},
                                                                  {442, "AB_old_ICATd0"},
                                                                  {450, "AB_old_ICATd8"},
                                                                  {42, "Acetyl"},
                                                                  {1, "Deamidation"},
                                                                  {-17, "Pyro-cmC"},
                                                                  {-18, "Pyro_glu"},
                                                                  {-1, "Amide"},
                                                                  {80, "Phospho"},
                                                                  {14, "Methyl"},
                                                                  {43, "Carbamyl"},
                                                                  {144, "iTRAQ4plex"}
                                                              };

        private static Dictionary<char, double> aminoAcidMass = new Dictionary<char, double>()
                                                                   {
                                                                       {'A', 71.037114},
                                                                       {'R', 156.101111},
                                                                       {'N', 114.042927},
                                                                       {'D', 115.026943},
                                                                       {'C', 103.009185},
                                                                       {'E', 129.042593},
                                                                       {'Q', 128.058578},
                                                                       {'G', 57.021464},
                                                                       {'H', 137.058912},
                                                                       {'I', 113.084064},
                                                                       {'L', 113.084064},
                                                                       {'K', 128.094963},
                                                                       {'M', 131.040485},
                                                                       {'F', 147.068414},
                                                                       {'P', 97.052764},
                                                                       {'S', 87.032028},
                                                                       {'T', 101.047679},
                                                                       {'U', 150.95363},
                                                                       {'W', 186.079313},
                                                                       {'Y', 163.06332},
                                                                       {'V', 99.068414}
                                                                   };

        public struct LibraryExportOptions
        {
            public int minimumSpectra { get; set; }
            public double precursorMzTolerance { get; set; }
            public double fragmentMzTolerance { get; set; }
            public string method { get; set; }
            public string outputFormat { get; set; }
            public bool decoys { get; set; }
            public bool crossPeptide { get; set; }

            public static string DOT_PRODUCT_METHOD = "Dot Product Compare";
        };

        private string _exportLocation;
        private IDPickerForm _owner;
        private ISession _session;
        private LibraryExportOptions _libraryExportSettings;
        private int _minPerPeptide;

        public void toLibrary(IDPickerForm ownerInput, ISession sessionInput, int minPerMatch, int minPerPeptide)
        {
            _owner = ownerInput;
            _session = sessionInput;
            _minPerPeptide = minPerPeptide;

            var tableExists = _session.CreateSQLQuery(
                @"SELECT name FROM sqlite_master WHERE type='table' AND name='SpectralPeaks'")
                                     .List().Count > 0;

            if (tableExists &&
                MessageBox.Show("Previous export found. Would you like to re-use the old results?",
                                "Skip peak calculation?", MessageBoxButtons.YesNo) == DialogResult.Yes)
            {
                Text = string.Format("Exporting to {0}... ", _exportLocation);
                ExportLibrary(_exportLocation);
                Close();
                return;
            }

            Text = "Export Library";
            var settingsDialog = new ExportLibrarySettings(_session, minPerMatch);
            if (settingsDialog.ShowDialog() == DialogResult.OK)
            {
                _libraryExportSettings = settingsDialog.GetSettings();
                var connstr = _session.Connection.ConnectionString;
                var fileBase = Path.GetFileNameWithoutExtension(Regex.Match(connstr, "Data Source=([^;]+)").Groups[1].ToString());

                _exportLocation = string.Empty;
                var sfd = new SaveFileDialog
                              {
                                  AddExtension = true,
                                  DefaultExt = ".sptxt",
                                  FileName = fileBase + ".sptxt",
                                  Filter = "Spectral Library|*.sptxt"
                              };
                if (sfd.ShowDialog() == DialogResult.OK)
                    _exportLocation = sfd.FileName;
                else
                    return;

                string database = _session.Connection.GetDataSource();
                if (MessageBox.Show("Back up database?", "", MessageBoxButtons.YesNo) == DialogResult.Yes)
                {
                    var backupNumber = 1;
                    string dbBackupFile = Path.ChangeExtension(database, ".backup.idpDB");
                    while (File.Exists(dbBackupFile))
                    {
                        backupNumber++;
                        dbBackupFile = Path.ChangeExtension(database, ".backup" + backupNumber + ".idpDB");
                    }
                    Text = (string.Format("Backing up idpDB to {0} ... ", dbBackupFile));
                    File.Copy(database, dbBackupFile, true);
                }

                Text = "Progress";
                progressBar.Visible = true;
                var timer = new System.Diagnostics.Stopwatch();
                timer.Start();

                var bg = new BackgroundWorker {WorkerReportsProgress = true};
                bg.DoWork += (x, y) => CreateNewLibrary((BackgroundWorker) x);
                bg.ProgressChanged += (x, y) =>
                                          {
                                              if (y.ProgressPercentage < 0)
                                                  progressBar.Style = ProgressBarStyle.Marquee;
                                              else
                                              {
                                                  progressBar.Style = ProgressBarStyle.Continuous;
                                                  progressBar.Value = y.ProgressPercentage;
                                              }
                                              if (y.UserState != null && y.UserState.ToString().Length > 0)
                                                  Text = y.UserState.ToString();
                                          };
                bg.RunWorkerCompleted += (x, y) =>
                                             {
                                                 timer.Stop();
                                                 progressBar.Style = ProgressBarStyle.Continuous;
                                                 progressBar.Value = 100;
                                                 var failMessage = string.Empty;
                                                 if (_retrievalFails > 0)
                                                     failMessage += Environment.NewLine + _retrievalFails +
                                                                    " spectra lost from reader error";
                                                 if (_minSpectraFails > 0)
                                                     failMessage += Environment.NewLine + _minSpectraFails +
                                                                    " matches fell below minimum spectra threshhold";
                                                 if (_overlapFails > 0)
                                                     failMessage += Environment.NewLine + _overlapFails +
                                                                    " matches lost due to ambiguous identification";
                                                 if (_decoyFails > 0)
                                                     failMessage += Environment.NewLine + _decoyFails +
                                                                    " matches found unsuitable for decoying and were dropped";
                                                 MessageBox.Show(string.Format("Finished, {0:00}:{1:00}:{2:00} elapsed{3}{4}{5}",
                                                                               timer.Elapsed.Hours,
                                                                               timer.Elapsed.Minutes,
                                                                               timer.Elapsed.Seconds,
                                                                               Environment.NewLine,
                                                                               "Created library with " + _successfulAdds + " matches",
                                                                               failMessage));
                                                 Close();
                                             };

                bg.RunWorkerAsync();
            }
            else
                Close();
        }

        int _successfulAdds;
        int _retrievalFails;
        int _minSpectraFails;
        int _overlapFails;
        int _decoyFails;
        private void CreateNewLibrary(BackgroundWorker bg)
        {
            bg.ReportProgress(-1, "Querying spectra...");
            IList<object[]> queryRows;
            lock (_session)
                queryRows = _session.CreateSQLQuery(@"SELECT s.Id, source.Name, NativeID, PrecursorMZ
                                                        FROM UnfilteredSpectrum s
                                                        JOIN SpectrumSource source ON s.Source = source.Id
                                                        JOIN UnfilteredPeptideSpectrumMatch psm ON s.Id = psm.Spectrum
                                                        JOIN Peptide p ON p.Id = psm.Peptide
                                                        JOIN PeptideSpectrumMatchScore psmScore ON psm.Id = psmScore.PsmId
                                                        JOIN PeptideSpectrumMatchScoreName scoreName ON psmScore.ScoreNameId=scoreName.Id
                                                        GROUP BY s.Id"
                    ).List<object[]>();
            var foundSpectraList =
                _session.CreateSQLQuery(@"SELECT distinct spectrum FROM PeptideSpectrumMatch").List<object>();
            var foundSpectra = new HashSet<long>();
            {
                long tempLong;
                foreach (var item in foundSpectraList)
                    if (long.TryParse(item.ToString(), out tempLong))
                        foundSpectra.Add(tempLong);
            }

            var spectrumRows =
                queryRows.Select(o => new RescuePSMsForm.SpectrumRow(o)).OrderBy(o => o.SourceName).ToList();
            ////converted IOrderedEnumerable to List, the former one may end up with multiple enumeration, each invokes constructor, resulting a fresh set of object

            /*
             * extract peaks for each spectrum, spectrumRows was sorted by SourceName
            */
            string currentSourceName = null;
            string currentSourcePath = null;
            pwiz.CLI.msdata.MSData msd = null;
            int spectrumRowsCount = spectrumRows.Count();
            //Set<long> processedSpectrumIDs = new Set<long>();

            bg.ReportProgress(-1, string.Format("Extracting peaks for {0} spectra ... ", spectrumRowsCount));

            //// create a temp table to store clustered spectrum IDs
            _session.CreateSQLQuery(@"DROP TABLE IF EXISTS SpectralPeaks").ExecuteUpdate();
            _session.CreateSQLQuery(
                @"CREATE TABLE IF NOT EXISTS SpectralPeaks (Id INTEGER PRIMARY KEY, spectra INTEGER, mz STRING, intensity STRING, mods STRING, charge INTEGER, mass REAL, preAA STRING, postAA STRING, sequence STRING, protein STRING, origPeptide STRING, annotations STRING, numSpectraUsed INTEGER)
                                    ").ExecuteUpdate();
            var peaklist = new List<object[]>();
            var currentSource = string.Empty;

            var sourcesSeen = new HashSet<string>();
            var totalSources = spectrumRows.Select(x => x.SourceName).Distinct().Count();
            lock (_owner)
                for (int i = 0; i < spectrumRowsCount; ++i)
                {
                    var row = spectrumRows.ElementAt(i);

                    if (row.SourceName != currentSource)
                    {
                        sourcesSeen.Add(row.SourceName);
                        var saving = _session.BeginTransaction();
                        saving.Begin();
                        var insertPeakscmd = _session.Connection.CreateCommand();
                        insertPeakscmd.CommandText = "INSERT INTO SpectralPeaks (spectra, mz, intensity) VALUES (?,?,?)";
                        var insertPeakParameters = new List<System.Data.IDbDataParameter>();
                        for (int x = 0; x < 3; ++x)
                        {
                            insertPeakParameters.Add(insertPeakscmd.CreateParameter());
                            insertPeakscmd.Parameters.Add(insertPeakParameters[x]);
                        }
                        insertPeakscmd.Prepare();
                        for (var y = 0; y < peaklist.Count; y++)
                        {
                            insertPeakParameters[0].Value = peaklist[y][0];
                            insertPeakParameters[1].Value = peaklist[y][1];
                            insertPeakParameters[2].Value = peaklist[y][2];
                            insertPeakscmd.ExecuteNonQuery();
                        }
                        saving.Commit();
                        currentSource = row != null ? row.SourceName : string.Empty;
                        peaklist = new List<object[]>();
                    }

                    bg.ReportProgress((int) (((i + 1)/(double) spectrumRowsCount)*100),
                                      string.Format("Extracting peaks for {0} spectra (File {1} of {2})", spectrumRowsCount, sourcesSeen.Count, totalSources));

                    //if (processedSpectrumIDs.Contains(row.SpectrumId))
                    //    break;
                    if (row.SourceName != currentSourceName)
                    {
                        currentSourceName = row.SourceName;
                        currentSourcePath = IDPickerForm.LocateSpectrumSource(currentSourceName,
                                                                              _session.Connection.GetDataSource());
                        if (msd != null)
                            msd.Dispose();

                        //var entryCount = session.CreateSQLQuery(string.Format(
                        //    "SELECT count() FROM SpectralPeaks p " +
                        //    "JOIN Spectrum s ON p.Spectra = s.Id " +
                        //    "JOIN SpectrumSource ss ON ss.Id = s.Source " +
                        //    "WHERE ss.Name= '{0}'", currentSourceName)).List<object>().FirstOrDefault() ?? string.Empty;


                        msd = new pwiz.CLI.msdata.MSDataFile(currentSourcePath);
                        SpectrumListFactory.wrap(msd, "threshold count 100 most-intense");
                        //only keep the top 100 peaks
                        //SpectrumListFactory.wrap(msd, "threshold bpi-relative .5 most-intense"); //keep all peaks that are at least 50% of the intensity of the base peak
                        //SpectrumListFactory.wrap(msd, "threshold tic-cutoff .95 most-intense"); //keep all peaks that count for 95% TIC
                        //threshold <count|count-after-ties|absolute|bpi-relative|tic-relative|tic-cutoff> <threshold> <most-intense|least-intense> [int_set(MS levels)]
                    }
                    var spectrumList = msd.run.spectrumList;
                    var pwizSpectrum = spectrumList.spectrum(spectrumList.find(row.SpectrumNativeID), true);
                    //may create indexoutofrange error if no spectrum nativeID                   
                    var mzList = pwizSpectrum.getMZArray().data; //getMZArray().data returns IList<double>
                    var intensityList = pwizSpectrum.getIntensityArray().data;
                    for (var x = 0; x < mzList.Count; x++)
                    {
                        mzList[x] = Math.Round(mzList[x], 4);
                        intensityList[x] = Math.Round(intensityList[x], 1);
                    }
                    if (mzList.Count == intensityList.Count)
                        peaklist.Add(new object[]
                                         {row.SpectrumId, string.Join("|", mzList), string.Join("|", intensityList)});
                    else
                        MessageBox.Show(row.SpectrumId.ToString());
                    //processedSpectrumIDs.Add(row.SpectrumId);
                }

            bg.ReportProgress(-1, string.Format("Saving extracted peaks from {0}... ", currentSource));
            var newTransaction = _session.BeginTransaction();
            newTransaction.Begin();
            var newInsertPeakscmd = _session.Connection.CreateCommand();
            newInsertPeakscmd.CommandText = "INSERT INTO SpectralPeaks (spectra, mz, intensity) VALUES (?,?,?)";
            var newInsertPeakParameters = new List<System.Data.IDbDataParameter>();
            for (int x = 0; x < 3; ++x)
            {
                newInsertPeakParameters.Add(newInsertPeakscmd.CreateParameter());
                newInsertPeakscmd.Parameters.Add(newInsertPeakParameters[x]);
            }
            newInsertPeakscmd.Prepare();
            for (var y = 0; y < peaklist.Count; y++)
            {
                newInsertPeakParameters[0].Value = peaklist[y][0];
                newInsertPeakParameters[1].Value = peaklist[y][1];
                newInsertPeakParameters[2].Value = peaklist[y][2];
                newInsertPeakscmd.ExecuteNonQuery();
            }
            newTransaction.Commit();

            bg.ReportProgress(-1, string.Format("Indexing peaks for {0} spectra ... ", spectrumRowsCount));
            _session.CreateSQLQuery("Create index if not exists SpecraPeakIndex on SpectralPeaks (spectra, mass)")
                   .ExecuteUpdate();

            var peptideList = _session.QueryOver<Peptide>().List();
            var acceptedSpectra = new HashSet<long>();
            var spectraInfo = new Dictionary<long, object[]>();

            var insertDecoyCmd = _session.Connection.CreateCommand();
            insertDecoyCmd.CommandText =
                "INSERT INTO SpectralPeaks (spectra, mz, intensity,mods,charge,mass,preAA,postAA,sequence,protein,origPeptide, annotations, numSpectraUsed) VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?)";
            var insertDecoyParameters = new List<System.Data.IDbDataParameter>();
            for (int x = 0; x < 13; ++x)
            {
                insertDecoyParameters.Add(insertDecoyCmd.CreateParameter());
                insertDecoyCmd.Parameters.Add(insertDecoyParameters[x]);
            }
            insertDecoyCmd.Prepare();
            var annotations = new Dictionary<long, string>();
            _successfulAdds = 0;
            _retrievalFails = 0;
            _minSpectraFails = 0;
            _overlapFails = 0;
            _decoyFails = 0;

            for (var peptideNum = 0; peptideNum < peptideList.Count; peptideNum++)
            {
                if ((peptideNum + 1)%10 == 0)
                    bg.ReportProgress((int) (((peptideNum + 1)/(double) peptideList.Count)*100),
                                      string.Format("Trimming spectra for peptide {0}/{1}", peptideNum + 1,
                                                    peptideList.Count));

                int spectraInPeptide = 0;
                var peptide = peptideList[peptideNum];
                var matchSet = new Dictionary<string, List<PeptideSpectrumMatch>>();
                //TODO: Find mod variations
                foreach (var match in peptide.Matches)
                {
                    if (match.Spectrum.Id == null)
                        continue;

                    string peptideName = insertModsInPeptideString(peptide.Sequence, match.Modifications);
                    if (!matchSet.ContainsKey(peptideName))
                        matchSet.Add(peptideName, new List<PeptideSpectrumMatch>());
                    matchSet[peptideName].Add(match);
                    if (match.Spectrum.Id != null)
                        spectraInPeptide++;
                }
                if (spectraInPeptide < _minPerPeptide)
                    continue;

                    foreach (var distinctMatch in matchSet)
                {
                    //get list of spectra in peptide
                    var chargedSpectraList = new Dictionary<int, List<long>>();
                    var chargedScoreKeeper = new Dictionary<int,Dictionary<long, List<double>>>();
                    var charges = new HashSet<int>();
                    foreach (var psm in distinctMatch.Value.Where(x => x.Spectrum.Id != null))
                    {
                        if (psm.Spectrum.Id == null)
                            continue;
                        var charge = psm.Charge;
                        charges.Add(charge);
                        if (!chargedSpectraList.ContainsKey(charge))
                        {
                            chargedSpectraList.Add(charge, new List<long>());
                            chargedScoreKeeper.Add(charge, new Dictionary<long, List<double>>());
                        }
                        if (!chargedSpectraList[charge].Contains(psm.Spectrum.Id ?? -1))
                        {
                            chargedSpectraList[charge].Add(psm.Spectrum.Id ?? -1);
                            chargedScoreKeeper[charge].Add(psm.Spectrum.Id ?? -1, new List<double>());
                        }
                    }
                    var chargesToRemove = new HashSet<int>();
                    foreach (var charge in charges)
                    {
                        if (chargedSpectraList[charge].Count < _libraryExportSettings.minimumSpectra)
                        {
                            _minSpectraFails++;
                            chargesToRemove.Add(charge);
                        }
                        if (chargedSpectraList[charge].Count(x => !acceptedSpectra.Contains(x)) == 0)
                        {
                            _overlapFails++;
                            chargesToRemove.Add(charge);
                        }
                    }
                    foreach (var charge in chargesToRemove)
                        charges.Remove(charge);
                    if (charges.Count <1)
                        continue;

                    foreach (var charge in charges)
                    {
                        long bestSpectra = -100;
                        double bestScore = -100;
                        var spectraList = chargedSpectraList[charge];

                        //get list of spectra with similar precursor mass
                        var extraList = new List<long>();
                        if (_libraryExportSettings.crossPeptide)
                        {
                            var minValue = distinctMatch.Value[0].ObservedNeutralMass -
                                            _libraryExportSettings.precursorMzTolerance;
                            var maxValue = distinctMatch.Value[0].ObservedNeutralMass +
                                            _libraryExportSettings.precursorMzTolerance;

                            var extraListObj = _session.CreateSQLQuery(string.Format(
                                "SELECT spectra FROM SpectralPeaks WHERE spectra NOT IN ({0}) AND mass > {1} AND Mass < {2} AND spectra > 0",
                                string.Join(",", spectraList), minValue, maxValue)).List<object>();
                            foreach (var item in extraListObj)
                                extraList.Add((long) item);
                        }

                        //get spectra peaks
                        var peakList = _session.CreateSQLQuery(string.Format(
                            "SELECT spectra, mz, intensity FROM SpectralPeaks WHERE spectra IN ({0})",
                            string.Join(",", spectraList.Concat(extraList)))).List<object[]>();
                        if (peakList.Count < _libraryExportSettings.minimumSpectra)
                            continue;

                        //for some reason not all spectra have peaks available
                        for (var x = spectraList.Count - 1; x >= 0; x--)
                        {
                            var found = false;
                            foreach (var peak in peakList)
                                if ((long) peak[0] == spectraList[x])
                                    found = true;
                            if (!found)
                            {
                                _retrievalFails++;
                                spectraList.RemoveAt(x);
                            }
                        }

                        var peakInfo = new Dictionary<long, Peaks>();
                        foreach (var entry in peakList)
                        {
                            var mzValues = entry[1].ToString().Split('|').Select(double.Parse).ToList();
                            var intensityValues = entry[2].ToString().Split('|').Select(double.Parse).ToList();
                            peakInfo.Add((long) entry[0], new Peaks(mzValues, intensityValues));
                        }

                        if (_libraryExportSettings.method == LibraryExportOptions.DOT_PRODUCT_METHOD)
                        {
                            var scoreKeeper = chargedScoreKeeper[charge];
                            spectraList = spectraList.Where(x => !acceptedSpectra.Contains(x)).ToList();
                            if (spectraList.Count == 1)
                                bestSpectra = spectraList.First();
                            else if (spectraList.Count == 2)
                            {
                                if (peakInfo[spectraList[0]].OriginalIntensities.Average() >
                                    peakInfo[spectraList[1]].OriginalIntensities.Average())
                                    bestSpectra = spectraList[0];
                                else
                                    bestSpectra = spectraList[1];
                            }
                            else
                            {
                                //compare spectra in peptide
                                for (var x = 0; x < spectraList.Count; x++)
                                {
                                    for (var y = x + 1; y < spectraList.Count; y++)
                                    {
                                        var similarityScore =
                                            ClusteringAnalysis.DotProductCompareTo(peakInfo[spectraList[x]],
                                                                                    peakInfo[spectraList[y]],
                                                                                    _libraryExportSettings
                                                                                        .fragmentMzTolerance);
                                        scoreKeeper[spectraList[x]].Add(similarityScore);
                                        scoreKeeper[spectraList[y]].Add(similarityScore);
                                    }

                                    if (_libraryExportSettings.crossPeptide)
                                    {
                                        for (var y = 0; y < extraList.Count; y++)
                                        {
                                            var similarityScore =
                                                ClusteringAnalysis.DotProductCompareTo(peakInfo[spectraList[x]],
                                                                                        peakInfo[extraList[y]],
                                                                                        _libraryExportSettings
                                                                                            .fragmentMzTolerance);
                                            scoreKeeper[spectraList[x]].Add(similarityScore);
                                        }
                                    }
                                }

                                foreach (var spectra in spectraList)
                                {
                                    var avg = scoreKeeper[spectra].Average();
                                    if (avg > bestScore)
                                    {
                                        bestScore = avg;
                                        bestSpectra = spectra;
                                    }
                                }
                            }
                        }

                        if (bestSpectra < 0)
                            continue;
                        var modList = new List<string>();
                        foreach (var mod in distinctMatch.Value[0].Modifications.OrderBy(x=>x.Offset))
                        {
                            var closestNumber = (int)Math.Round(mod.Modification.MonoMassDelta);
                            if (sptxtMods.ContainsKey(closestNumber))
                                modList.Add(string.Format("{0},{1},{2}", mod.Offset < 0 ? -1 : mod.Offset, mod.Site, sptxtMods[closestNumber]));
                        }
                        var modstring = modList.Count + (modList.Any()
                                                                    ? "/" + string.Join("/", modList)
                                                                    : string.Empty);
                        var mass = Math.Round(distinctMatch.Value[0].ObservedNeutralMass, 4);
                        var proteinNames = new List<string>();
                        var preAA = "X";
                        var postAA = "X";
                        foreach (var instance in peptide.Instances)
                        {
                            try
                            {
                                proteinNames.Add(instance.Protein.Accession);
                                if (instance.Protein.Sequence == null)
                                {
                                    preAA = "X";
                                    postAA = "X";
                                }
                                else
                                {
                                    preAA = instance.Offset > 0
                                                ? instance.Protein.Sequence[instance.Offset - 1].ToString()
                                                : "X";
                                    postAA = instance.Offset + instance.Length < instance.Protein.Sequence.Length
                                                    ? instance.Protein.Sequence[instance.Offset + instance.Length]
                                                        .ToString()
                                                    : "X";
                                }
                            }
                            catch (Exception)
                            {
                                preAA = "X";
                                postAA = "X";
                            }
                        }

                        //set up annotations
                        string peptideName = distinctMatch.Key;
                        var proteinString = proteinNames.Count + "/" + string.Join(",1/", proteinNames) +
                                            (proteinNames.Count > 1 ? ",1" : string.Empty);
                        var modDict = new Dictionary<int, double>();
                        foreach (var mod in distinctMatch.Value[0].Modifications)
                            modDict.Add(mod.Offset, mod.Modification.MonoMassDelta);
                        var splitPeptide = peptide.Sequence.Select(letter => letter.ToString()).ToList();
                        var FragmentList = CreateFragmentMassReference(splitPeptide, modDict);
                        Dictionary<double, FragmentPeakInfo> annotatedList =
                            AnnotatePeaks(peakInfo[bestSpectra].OriginalMZs.ToList().OrderBy(x => x).ToList(),
                                            FragmentList, charge);
                        if (!annotations.ContainsKey(bestSpectra))
                        {
                            var orderedMZs = peakInfo[bestSpectra].OriginalMZs.OrderBy(x => x).ToList();
                            var tempList = new List<string>();
                            foreach (var mz in orderedMZs)
                            {
                                var tempstring = annotatedList[mz].fragmentID;
                                var closestModValue = Math.Round(annotatedList[mz].relativePosition);
                                if (closestModValue < 0)
                                    tempstring += closestModValue.ToString();
                                else if (closestModValue > 0)
                                    tempstring += "+" + closestModValue.ToString();
                                if (annotatedList[mz].fragmentCharge > 1)
                                    tempstring += "^" + annotatedList[mz].fragmentCharge;
                                tempList.Add(tempstring);
                            }
                            annotations.Add(bestSpectra, string.Join("|", tempList));
                        }

                        //add decoys
                        if (_libraryExportSettings.decoys)
                        {
                            string decoyPeptideName = createDecoyPeptideString(peptide.Sequence,
                                                                                distinctMatch.Value[0]
                                                                                    .Modifications);
                            string annotatedDecoyPeptideName = insertModsInPeptideString(decoyPeptideName,
                                                                                            distinctMatch.Value[0]
                                                                                                .Modifications);
                            if (decoyPeptideName == string.Empty ||
                                peptideName.Length != annotatedDecoyPeptideName.Length)
                            {
                                _decoyFails++;
                                continue;
                            }
                            object[] decoyPeaks = createDecoyPeaks(peptide.Sequence, decoyPeptideName,
                                                                    annotatedList,
                                                                    peakInfo[bestSpectra].OriginalMZs.ToList(),
                                                                    peakInfo[bestSpectra].OriginalIntensities
                                                                                        .ToList(),
                                                                    modDict, charge);


                            var decoyProteinString = proteinNames.Count + "/DECOY_" +
                                                        string.Join(",1/DECOY_", proteinNames) +
                                                        (proteinNames.Count > 1 ? ",1" : string.Empty);
                            var spectraAnnotations = string.Join("|", (List<string>) decoyPeaks[2]);

                            insertDecoyParameters[0].Value = -bestSpectra;
                            insertDecoyParameters[1].Value = string.Join("|", (List<double>) decoyPeaks[0]);
                            insertDecoyParameters[2].Value = string.Join("|", (List<double>) decoyPeaks[1]);
                            insertDecoyParameters[3].Value = modstring;
                            insertDecoyParameters[4].Value = charge;
                            insertDecoyParameters[5].Value = mass;
                            insertDecoyParameters[6].Value = preAA;
                            insertDecoyParameters[7].Value = postAA;
                            insertDecoyParameters[8].Value = annotatedDecoyPeptideName;
                            insertDecoyParameters[9].Value = decoyProteinString;
                            insertDecoyParameters[10].Value = preAA + "." + peptideName + "." + postAA;
                            insertDecoyParameters[11].Value = spectraAnnotations;
                            insertDecoyParameters[12].Value = spectraList.Count;
                            insertDecoyCmd.ExecuteNonQuery();
                        }

                        spectraInfo.Add(bestSpectra,
                                        new object[]
                                            {charge, mass, modstring, preAA, postAA, peptideName, proteinString,spectraList.Count});

                        acceptedSpectra.Add(bestSpectra);
                        _successfulAdds++;
                    }
                }
            }
            _session.CreateSQLQuery(string.Format("DELETE FROM SpectralPeaks WHERE spectra NOT IN ({0}) and spectra > 0",
                                                 string.Join(",", acceptedSpectra))).ExecuteUpdate();

            bg.ReportProgress(-1, "Adding detailed peak data... ");
            var insertPeakInfocmd = _session.Connection.CreateCommand();
            insertPeakInfocmd.CommandText =
                "UPDATE SpectralPeaks SET charge=?, mass=?, mods=?, preAA=?, postAA=?, sequence=?, protein=?, numSpectraUsed=?, annotations=? where spectra=?";
            var insertPeakInfoParameters = new List<System.Data.IDbDataParameter>();
            for (int x = 0; x < 10; ++x)
            {
                insertPeakInfoParameters.Add(insertPeakInfocmd.CreateParameter());
                insertPeakInfocmd.Parameters.Add(insertPeakInfoParameters[x]);
            }
            insertPeakInfocmd.Prepare();
            var addInfo = _session.BeginTransaction();
            addInfo.Begin();
            foreach (var item in acceptedSpectra)
            {
                insertPeakInfoParameters[0].Value = spectraInfo[item][0];
                insertPeakInfoParameters[1].Value = spectraInfo[item][1];
                insertPeakInfoParameters[2].Value = spectraInfo[item][2];
                insertPeakInfoParameters[3].Value = spectraInfo[item][3];
                insertPeakInfoParameters[4].Value = spectraInfo[item][4];
                insertPeakInfoParameters[5].Value = spectraInfo[item][5];
                insertPeakInfoParameters[6].Value = spectraInfo[item][6];
                insertPeakInfoParameters[7].Value = spectraInfo[item][7];
                insertPeakInfoParameters[8].Value = annotations[item];
                insertPeakInfoParameters[9].Value = item;
                insertPeakInfocmd.ExecuteNonQuery();
            }

            addInfo.Commit();
            try
            {
                bg.ReportProgress(-1, "Compressing database... ");
                _session.CreateSQLQuery("VACUUM").ExecuteUpdate();
            }
            catch (Exception e)
            {
                MessageBox.Show("Could not compress database");
            }

            bg.ReportProgress(-1, string.Format("Exporting to {0}... ", _exportLocation));

            ExportLibrary(_exportLocation);
        }

        private struct FragmentPeakInfo
        {
            public double originalMZvalue;
            public double relativePosition;
            public string fragmentID;
            public int complexity;
            public int fragmentCharge;
        };

        private object[] createDecoyPeaks(string peptideName, string decoyPeptideName, Dictionary<double, FragmentPeakInfo> annotatedList , List<double> mzValues, List<double> intensities, Dictionary<int,double> modDict, int charge)
        {
            var splitDecoy = decoyPeptideName.Select(letter => letter.ToString()).ToList();
            var originalPeaks = new Dictionary<double, double>();
            for (var x = 0; x < mzValues.Count; x++)
                originalPeaks.Add(mzValues[x], intensities[x]);
            var DecoyList = CreateFragmentMassReference(splitDecoy, modDict);
            var newPeaks = new Dictionary<double, double>();
            var peakLocations = new List<double>();
            
            var newAnnotations = new Dictionary<double, string>();

            foreach (var peak in annotatedList)
            {
                var mzValue = peak.Value.fragmentID == "?"
                                  ? peak.Key
                                  : Math.Round((DecoyList[peak.Value.fragmentID] + peak.Value.relativePosition) / peak.Value.fragmentCharge, 4);
                var intensity = originalPeaks[peak.Key];
                if (newPeaks.ContainsKey(mzValue))
                    newPeaks[mzValue] += intensity;
                else
                    newPeaks.Add(mzValue, intensity);
                peakLocations.Add(mzValue);
                if (!newAnnotations.ContainsKey(mzValue))
                {
                    var tempstring = peak.Value.fragmentID;
                    var closestModValue = Math.Round(peak.Value.relativePosition);
                    if (closestModValue < 0)
                        tempstring += closestModValue.ToString();
                    else if (closestModValue > 0)
                        tempstring += "+" + closestModValue.ToString();
                    if (peak.Value.fragmentCharge > 1)
                        tempstring += "^" + peak.Value.fragmentCharge;
                    newAnnotations.Add(mzValue, tempstring);
                }
            }

            for (var x = 1; x < peakLocations.Count - 1; x++)
            {
                if (newPeaks[peakLocations[x]] <= 0)
                    continue;
                var closeBehind = false;
                var closeAhead = false;
                if (newPeaks[peakLocations[x - 1]] > 0 &&
                    Math.Abs(peakLocations[x] - peakLocations[x - 1]) < _libraryExportSettings.fragmentMzTolerance)
                    closeBehind = true;
                if (newPeaks[peakLocations[x + 1]] > 0 &&
                    Math.Abs(peakLocations[x] - peakLocations[x + 1]) < _libraryExportSettings.fragmentMzTolerance)
                    closeAhead = true;
                if (closeBehind)
                {
                    newPeaks[peakLocations[x]] += newPeaks[peakLocations[x - 1]];
                    newPeaks[peakLocations[x - 1]] = 0;
                }
                if (closeAhead)
                {
                    newPeaks[peakLocations[x]] += newPeaks[peakLocations[x + 1]];
                    newPeaks[peakLocations[x + 1]] = 0;
                }
            }


            var newPeakMz = new List<double>();
            var newPeakIntensity = new List<double>();
            var possibleAnnotations = new List<string>();
            foreach (var kvp in newPeaks.OrderBy(x => x.Key))
            {
                if (kvp.Value <= 0)
                    continue;
                newPeakMz.Add(kvp.Key);
                newPeakIntensity.Add(kvp.Value);
                possibleAnnotations.Add(newAnnotations[kvp.Key]);
            }

            return new object[] { newPeakMz, newPeakIntensity, possibleAnnotations };
        }

        private Dictionary<double, FragmentPeakInfo> AnnotatePeaks(List<double> originalMZs, Dictionary<string, double> fragmentList, int charge)
        {
            Dictionary<double, FragmentPeakInfo> availableAnnotations = CreatePossibleAnnotations(fragmentList, charge);
            var annotationList = availableAnnotations.Select(item => item.Key).OrderBy(x => x).ToList();
            var possibleExplanations = new Dictionary<double, FragmentPeakInfo>();
            foreach (var peak in originalMZs)
            {
                var peak1 = peak;
                var closestMatch = 0.0;
                var peakExplanationList =
                    annotationList.Where(x => x < peak1 + _libraryExportSettings.fragmentMzTolerance && x > peak1 - _libraryExportSettings.fragmentMzTolerance).ToList();
                if (peakExplanationList.Any())
                {
                    var explanationList = availableAnnotations.Where(x => peakExplanationList.Contains(x.Key)).ToList();
                    if (explanationList.Count < 0)
                        continue;
                    var minComplexity = peakExplanationList.Min(x => availableAnnotations[x].complexity);
                    peakExplanationList =
                        peakExplanationList.Where(x => availableAnnotations[x].complexity == minComplexity).ToList();
                    if (peakExplanationList.Any())
                    {
                        var closestDistance = double.MaxValue;
                        foreach (var match in peakExplanationList)
                        {
                            var distance = Math.Abs(peak - match);
                            if (distance < closestDistance)
                            {
                                closestDistance = distance;
                                closestMatch = match;
                            }
                        }
                    }
                }

                if (peakExplanationList.Any() && availableAnnotations.ContainsKey(closestMatch))
                {
                    possibleExplanations.Add(peak, availableAnnotations[closestMatch]);
                    availableAnnotations.Remove(closestMatch);
                    annotationList.Remove(closestMatch);
                }
                else
                {
                    possibleExplanations.Add(peak,
                                         new FragmentPeakInfo
                                         {
                                             fragmentID = "?",
                                             originalMZvalue = peak,
                                             relativePosition = 0,
                                             complexity = int.MaxValue
                                         }
                    );
                }

            }

            return possibleExplanations;
        }

        private Dictionary<double, FragmentPeakInfo> CreatePossibleAnnotations(Dictionary<string, double> fragmentList, int charge)
        {
            var possibleAnnotations = new Dictionary<double, FragmentPeakInfo>();
            var allList = new List<double> { -1, 0, 1, 17, 18, 34, 35, 36 };
            var bList = new List<double> { 28, 45, 46, 56 };
            var singleComplexity = new List<double> { -1, 1, 17, 18, 28 };
            foreach (var fragment in fragmentList)
            {
                for (var currentCharge = 1; currentCharge <= charge; currentCharge++)
                {
                    foreach (var item in allList)
                    {
                        var itemComplexity = singleComplexity.Contains(item) ? 1 : 2;
                        if (item == 0)
                            itemComplexity = 0;
                        itemComplexity += (currentCharge > 1 ? 1 : 0);
                        if (!possibleAnnotations.ContainsKey((fragment.Value - item) / currentCharge))
                            possibleAnnotations.Add((fragment.Value - item) / currentCharge,
                                                    new FragmentPeakInfo
                                                    {
                                                        complexity = itemComplexity,
                                                        fragmentID = fragment.Key,
                                                        originalMZvalue = fragment.Value,
                                                        relativePosition = -item,
                                                        fragmentCharge = currentCharge
                                                    });
                        else if (possibleAnnotations[(fragment.Value - item) / currentCharge].complexity > itemComplexity)
                            possibleAnnotations[(fragment.Value - item) / currentCharge] =
                                new FragmentPeakInfo
                                {
                                    complexity = itemComplexity,
                                    fragmentID = fragment.Key,
                                    originalMZvalue = fragment.Value,
                                    relativePosition = -item,
                                    fragmentCharge = currentCharge
                                };
                    }
                    if (fragment.Key.StartsWith("b"))
                    {
                        foreach (var item in bList)
                        {
                            var itemComplexity = singleComplexity.Contains(item) ? 1 : 2;
                            if (item == 0)
                                itemComplexity = 0;
                            itemComplexity += (currentCharge > 1 ? 1 : 0);
                            if (!possibleAnnotations.ContainsKey((fragment.Value - item) / currentCharge))
                                possibleAnnotations.Add((fragment.Value - item) / currentCharge,
                                                        new FragmentPeakInfo
                                                        {
                                                            complexity = itemComplexity,
                                                            fragmentID = fragment.Key,
                                                            originalMZvalue = fragment.Value,
                                                            relativePosition = -item,
                                                            fragmentCharge = currentCharge
                                                        });
                            else if (possibleAnnotations[(fragment.Value - item) / currentCharge].complexity >
                                     itemComplexity)
                                possibleAnnotations[(fragment.Value - item) / currentCharge] =
                                    new FragmentPeakInfo
                                    {
                                        complexity = itemComplexity,
                                        fragmentID = fragment.Key,
                                        originalMZvalue = fragment.Value,
                                        relativePosition = -item,
                                        fragmentCharge = currentCharge
                                    };
                        }
                    }
                }
            }
            return possibleAnnotations;
        }

        private Dictionary<string, double> CreateFragmentMassReference(List<string> splitPeptide, Dictionary<int, double> modDict)
        {
            var ionList = new Dictionary<string, double>();
            var totalMass = 1.0;
            for (var x = 0; x < splitPeptide.Count; x++)
            {
                var fragmentMass = aminoAcidMass[splitPeptide[x][0]];
                if (modDict.ContainsKey(x))
                    fragmentMass += modDict[x];
                totalMass += fragmentMass;
                ionList.Add("b" + (x + 1), totalMass);
            }
            totalMass = 19.0;
            splitPeptide.Reverse();
            for (var x = 0; x < splitPeptide.Count; x++)
            {
                var fragmentMass = aminoAcidMass[splitPeptide[x][0]];
                if (modDict.ContainsKey(x))
                    fragmentMass += modDict[x];
                totalMass += fragmentMass;
                ionList.Add("y" + (x + 1), totalMass);
            }

            return ionList;
        }

        private string createDecoyPeptideString(string peptide, IList<PeptideModification> mods)
        {
            var nonTermMods = (from x in mods where x.Offset >= 0 select x).ToList();
            var endingAA = peptide[peptide.Length - 1].ToString();

            var originalSplitPeptide = peptide.Select(letter => letter.ToString()).ToList();
            var splitPeptide = peptide.Select(letter => letter.ToString()).ToList();
            var orderedMods = mods.OrderBy(x => x.Offset).ToList();
            foreach (var mod in orderedMods)
                if (mod.Offset >= 0 && mod.Offset < splitPeptide.Count)
                    splitPeptide[mod.Offset] = string.Empty;
            splitPeptide[splitPeptide.Count - 1] = string.Empty;

            for (var x = splitPeptide.Count - 1; x >= 0; x--)
                if (splitPeptide[x] == string.Empty)
                    splitPeptide.RemoveAt(x);

            var wellScrambled = false;
            var attempts = 0;
            var scrambledPeptide = new List<string>(splitPeptide);
            if (!scrambledPeptide.Any())
                return string.Empty;
            while (!wellScrambled && attempts < 50)
            {
                var match = nonTermMods.Count;
                var nonMatch = 0;
                scrambledPeptide = scrambledPeptide.Shuffle().ToList();
                for (var x = 0; x < splitPeptide.Count; x++)
                    if (splitPeptide[x] == scrambledPeptide[x])
                        match++;
                    else
                        nonMatch++;
                if (nonMatch >= 2 * match)
                    wellScrambled = true;
                attempts++;
            }
            if (!wellScrambled)
                return string.Empty;
            for (var x = 0; x < orderedMods.Count(); x++)
                if (orderedMods[x].Offset >= 0 && orderedMods[x].Offset < scrambledPeptide.Count)
                    scrambledPeptide.Insert(orderedMods[x].Offset, orderedMods[x].Site.ToString());
                else if (orderedMods[x].Offset >= 0 && orderedMods[x].Offset == scrambledPeptide.Count)
                    scrambledPeptide.Add(orderedMods[x].Site.ToString());
            if (scrambledPeptide.Count < originalSplitPeptide.Count) //in odd cases ending AA has already been added back
                scrambledPeptide.Add(endingAA);
            return string.Join(string.Empty, scrambledPeptide);
        }

        private void ExportLibrary(string outputLocation)
        {
            var fileout = new StreamWriter(outputLocation);
            fileout.AutoFlush = false;
            fileout.WriteLine("### Produced by IDPicker's Database To Library functionality");
            var resultList = _session.CreateSQLQuery("select * from spectralPeaks order by sequence").List<object[]>();
            for (var x = 0; x < resultList.Count; x++)
            {
                var mzList = (resultList[x][2].ToString()).Split("|".ToCharArray());
                var intensityList = (resultList[x][3].ToString()).Split("|".ToCharArray());
                var mods = resultList[x][4].ToString();
                var charge = int.Parse(resultList[x][5].ToString());
                var mass = double.Parse(resultList[x][6].ToString());
                var preAA = resultList[x][7].ToString();
                var postAA = resultList[x][8].ToString();
                var sequence = resultList[x][9].ToString();
                var proteinString = resultList[x][10].ToString();
                var origPeptide = resultList[x][11] == null ? string.Empty : resultList[x][11].ToString();
                var annotationList = (resultList[x][12].ToString()).Split("|".ToCharArray());
                var numSpectraUsed = int.Parse(resultList[x][13].ToString());

                if (mzList.Length != intensityList.Length)
                    continue;

                fileout.WriteLine("Name: {0}/{1}", sequence, charge);
                fileout.WriteLine("LibID: {0}", x);
                fileout.WriteLine("MW: {0:f4}", mass);
                fileout.WriteLine("PrecursorMZ: {0:f4}", Math.Round(mass / charge, 4));
                fileout.WriteLine("Status: Normal");
                fileout.WriteLine("FullName: {0}.{1}.{2}/{3}", preAA, sequence, postAA, charge);
                fileout.WriteLine("Comment: Mods={0}{1} NumSpectraUsed={2} Protein={3}", mods,
                                  origPeptide == string.Empty ? string.Empty : " OrigPeptide=" + origPeptide, numSpectraUsed, proteinString);
                fileout.WriteLine("NumPeaks: {0}", mzList.Length);
                for (var y = 0; y < mzList.Length; y++)
                {
                    fileout.WriteLine("{0:f4}\t{1:f1}\t{2}", decimal.Parse(mzList[y]), decimal.Parse(intensityList[y]), annotationList[y]);
                }
                fileout.WriteLine(string.Empty);
                if (x % 1000 == 0)
                    fileout.Flush();
            }
            fileout.Flush();
        }

        //// end of GetRefinedSpectra

        private string insertModsInPeptideString(string baseSequence, IList<PeptideModification> mods)
        {
            var brokenSequence = baseSequence.Select(letter => letter.ToString()).ToList();
            var prefix = string.Empty;
            foreach (var mod in mods)
            {
                var site = mod.Offset;
                if (site < 0)
                {
                    prefix = "n[" + Math.Round(mod.Modification.MonoMassDelta) + "]";
                    continue;
                }
                if (site >= brokenSequence.Count || brokenSequence[site].Length != 1)
                    continue;
                var baseMass = aminoAcidMass[brokenSequence[site][0]];
                var combinedMass = Math.Round(baseMass + mod.Modification.MonoMassDelta);
                brokenSequence[site] = brokenSequence[site] + "[" + combinedMass + "]";
            }
            return prefix + string.Join(string.Empty, brokenSequence);
        }
    }
}
