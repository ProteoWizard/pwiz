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
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Threading;
using System.IO;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using PopupControl;
using IDPicker;
using IDPicker.DataModel;
using IDPicker.Controls;
using pwiz.Common.Collections;

namespace IDPicker.Forms
{
    public partial class ProteinTableForm : BaseTableForm
    {
        #region Wrapper classes for encapsulating query results

        public class AggregateRow : Row
        {
            protected iTRAQArrayUserType iTRAQArrayUserType = new iTRAQArrayUserType();
            protected TMTArrayUserType TMTArrayUserType = new TMTArrayUserType();

            public int Spectra { get; private set; }
            public int DistinctMatches { get; private set; }
            public int DistinctPeptides { get; private set; }
            public string PeptideSequences { get; private set; }
            public string PeptideGroups { get; private set; }

            public static int ColumnCount = 3;
            public static string Selection = "SELECT " +
                                             "COUNT(DISTINCT psm.Spectrum.id), " +
                                             "COUNT(DISTINCT psm.DistinctMatchId), " +
                                             "COUNT(DISTINCT psm.Peptide.id)";

            protected static IDictionary<TKey, object[]> GetDetailedColumnsByKey<TKey> (NHibernate.ISession session, DataFilter dataFilter, string keyColumn)
            {
                // these columns are not affected by peptide view filters
                var dataFilter2 = new DataFilter(dataFilter) { Peptide = null, PeptideGroup = null, DistinctMatchKey = null };
                return session.CreateQuery("SELECT " + keyColumn +
                                           ", DISTINCT_GROUP_CONCAT(pep.Sequence)" +
                                           ", DISTINCT_GROUP_CONCAT(pep.PeptideGroup)" +
                                           dataFilter2.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToProtein) +
                                           "GROUP BY " + keyColumn)
                              .List<object[]>()
                              .ToDictionary(o => (TKey) o[0]);
            }

            #region Constructor
            public AggregateRow (object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
            {
                int column = -1;
                Spectra = Convert.ToInt32(basicColumns[++column]);
                DistinctMatches = Convert.ToInt32(basicColumns[++column]);
                DistinctPeptides = Convert.ToInt32(basicColumns[++column]);
                column = 0; // skip key column
                PeptideSequences = Convert.ToString(detailedColumns[++column]);
                PeptideGroups = Convert.ToString(detailedColumns[++column]);
                DataFilter = dataFilter;
            }
            #endregion
        }

        public class ClusterRow : AggregateRow
        {
            public int Cluster { get; private set; }
            public int ProteinGroupCount { get; private set; }
            public int ProteinCount { get; private set; }

            public static IList<ClusterRow> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<int, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", COUNT(DISTINCT pro.ProteinGroup)" +
                                                       ", COUNT(DISTINCT pro.Id)" +
                                                       ", pro.Cluster" +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                                         DataFilter.ProteinToPeptideSpectrumMatch) +
                                                       "GROUP BY pro.Cluster").List<object[]>();

                    // these columns are not affected by the view filter
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<int>(session, dataFilter, "pro.Cluster");
                }

                var rows = new List<ClusterRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new ClusterRow(basicColumns[i], detailedColumnsByKey[(int) basicColumns[i].Last()], dataFilter));
                return rows;
            }

            #region Constructor
            public ClusterRow (object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                ProteinGroupCount = Convert.ToInt32(basicColumns[++column]);
                ProteinCount = Convert.ToInt32(basicColumns[++column]);
                Cluster = Convert.ToInt32(basicColumns[++column]);
            }
            #endregion
        }

        public class ProteinGroupRow : AggregateRow
        {
            public string Proteins { get; private set; }
            public int ProteinGroup { get; private set; }
            public DataModel.Protein FirstProtein { get; set; }
            public int ProteinCount { get; private set; }
            public double? MeanProteinCoverage { get; private set; }
            public double[] iTRAQ_ReporterIonIntensities { get; private set; }
            public double[] TMT_ReporterIonIntensities { get; private set; }
            public string ModifiedSites { get; private set; }

            public static IList<ProteinGroupRow> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<int, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", DISTINCT_GROUP_CONCAT(pro.Accession)" +
                                                       ", pro" +
                                                       ", COUNT(DISTINCT pro.id)" +
                                                       ", AVG(pro.Coverage)" +
                                                       ", " + RollupSQL + "(s.iTRAQ_ReporterIonIntensities)" +
                                                       ", " + RollupSQL + "(s.TMT_ReporterIonIntensities)" +
                                                       ", DISTINCT_GROUP_CONCAT(ROUND_TO_INTEGER(mod.MonoMassDelta) || '@' || pm.Site || PARENS(pi.Offset+pm.Offset+1))" +
                                                       ", pro.ProteinGroup" +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                                         DataFilter.ProteinToSpectrum,
                                                                                         DataFilter.ProteinToModification) +
                                                       "GROUP BY pro.ProteinGroup").List<object[]>();

                    // these columns are not affected by the view filter
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<int>(session, dataFilter, "pro.ProteinGroup");
                }

                var rows = new List<ProteinGroupRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new ProteinGroupRow(basicColumns[i], detailedColumnsByKey[(int) basicColumns[i].Last()], dataFilter));
                return rows;
            }

            #region Constructor
            public ProteinGroupRow (object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Proteins = (string) basicColumns[++column];
                FirstProtein = (DataModel.Protein) basicColumns[++column];
                ProteinCount = Convert.ToInt32(basicColumns[++column]);
                MeanProteinCoverage = (double?)basicColumns[++column];
                iTRAQ_ReporterIonIntensities = (double[])iTRAQArrayUserType.Assemble(basicColumns[++column], null);
                TMT_ReporterIonIntensities = (double[])TMTArrayUserType.Assemble(basicColumns[++column], null);
                ModifiedSites = ProteinRow.RemoveTerminalSites((string)basicColumns[++column] ?? String.Empty);
                ProteinGroup = Convert.ToInt32(basicColumns[++column]);
            }
            #endregion
        }

        public class ProteinRow : AggregateRow
        {
            public DataModel.Protein Protein { get; private set; }
            public double[] iTRAQ_ReporterIonIntensities { get; private set; }
            public double[] TMT_ReporterIonIntensities { get; private set; }
            public string ModifiedSites { get; private set; }

            public static IList<ProteinRow> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<long, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", " + RollupSQL + "(s.iTRAQ_ReporterIonIntensities)" +
                                                       ", " + RollupSQL + "(s.TMT_ReporterIonIntensities)" +
                                                       ", DISTINCT_GROUP_CONCAT(ROUND_TO_INTEGER(mod.MonoMassDelta) || '@' || pm.Site || PARENS(pi.Offset+pm.Offset+1))" +
                                                       ", pro" +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                                         DataFilter.ProteinToSpectrum,
                                                                                         DataFilter.ProteinToModification) +
                                                       "GROUP BY pro.Id").List<object[]>();

                    // these columns are not affected by the view filter
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<long>(session, dataFilter, "pro.Id");
                }

                var rows = new List<ProteinRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new ProteinRow(basicColumns[i], detailedColumnsByKey[(basicColumns[i].Last() as Protein).Id.Value], dataFilter));
                return rows;
            }

            #region Constructor
            public ProteinRow (object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                iTRAQ_ReporterIonIntensities = (double[])iTRAQArrayUserType.Assemble(basicColumns[++column], null);
                TMT_ReporterIonIntensities = (double[])TMTArrayUserType.Assemble(basicColumns[++column], null);
                ModifiedSites = RemoveTerminalSites((string) basicColumns[++column] ?? String.Empty);
                Protein = (DataModel.Protein) basicColumns[++column];
            }
            #endregion

            public static string RemoveTerminalSites(string modifiedSites)
            {
                var sb = new StringBuilder(modifiedSites);
                var matches = Regex.Matches(modifiedSites, @"\d+@[\(\)]-?\d+,?", RegexOptions.Compiled);
                for (int i = matches.Count - 1; i >= 0; --i)
                {
                    var match = matches[i];
                    sb.Remove(Math.Max(0, match.Index - 1), match.Length); // remove the delimiting comma as well
                }
                return sb.ToString();
            }
        }

        public class GeneGroupRow : AggregateRow
        {
            public string Genes { get; private set; }
            public int GeneGroup { get; private set; }
            public DataModel.Protein FirstProtein { get; set; }
            public int GeneCount { get; private set; }
            public int ProteinCount { get; private set; }
            public double? MeanProteinCoverage { get; private set; }
            public double[] iTRAQ_ReporterIonIntensities { get; private set; }
            public double[] TMT_ReporterIonIntensities { get; private set; }

            public static IList<GeneGroupRow> GetRows(NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<int, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", SORT_UNMAPPED_LAST(DISTINCT_GROUP_CONCAT(pro.GeneId))" +
                                                       ", pro" +
                                                       ", COUNT(DISTINCT pro.GeneId)" +
                                                       ", COUNT(DISTINCT pro.Id)" +
                                                       ", AVG(pro.Coverage)" +
                                                       ", " + RollupSQL + "(s.iTRAQ_ReporterIonIntensities)" +
                                                       ", " + RollupSQL + "(s.TMT_ReporterIonIntensities)" +
                                                       ", pro.GeneGroup" +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                                         DataFilter.ProteinToSpectrum) +
                                                       "GROUP BY pro.GeneGroup").List<object[]>();

                    // these columns are not affected by the view filter
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<int>(session, dataFilter, "pro.GeneGroup");
                }

                var rows = new List<GeneGroupRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new GeneGroupRow(basicColumns[i], detailedColumnsByKey[(int)basicColumns[i].Last()], dataFilter));
                return rows;
            }

            #region Constructor
            public GeneGroupRow(object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Genes = (string)basicColumns[++column];
                FirstProtein = (DataModel.Protein)basicColumns[++column];
                GeneCount = Convert.ToInt32(basicColumns[++column]);
                ProteinCount = Convert.ToInt32(basicColumns[++column]);
                MeanProteinCoverage = (double?)basicColumns[++column];
                iTRAQ_ReporterIonIntensities = (double[])iTRAQArrayUserType.Assemble(basicColumns[++column], null);
                TMT_ReporterIonIntensities = (double[])TMTArrayUserType.Assemble(basicColumns[++column], null);
                GeneGroup = Convert.ToInt32(basicColumns[++column]);
            }
            #endregion
        }

        public class GeneRow : AggregateRow
        {
            public int ProteinCount { get; private set; }
            public DataModel.Protein Protein { get; private set; }
            public double[] iTRAQ_ReporterIonIntensities { get; private set; }
            public double[] TMT_ReporterIonIntensities { get; private set; }

            public static IList<GeneRow> GetRows(NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<string, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", COUNT(DISTINCT pro.Id)" +
                                                       ", " + RollupSQL + "(s.iTRAQ_ReporterIonIntensities)" +
                                                       ", " + RollupSQL + "(s.TMT_ReporterIonIntensities)" +
                                                       ", pro" +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                                         DataFilter.ProteinToSpectrum) +
                                                       "GROUP BY pro.GeneId").List<object[]>();

                    // these columns are not affected by the view filter
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<string>(session, dataFilter, "pro.GeneId");
                }

                var rows = new List<GeneRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new GeneRow(basicColumns[i], detailedColumnsByKey[(basicColumns[i].Last() as Protein).GeneId], dataFilter));
                return rows;
            }

            #region Constructor
            public GeneRow(object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                ProteinCount = Convert.ToInt32(basicColumns[++column]);
                iTRAQ_ReporterIonIntensities = (double[])iTRAQArrayUserType.Assemble(basicColumns[++column], null);
                TMT_ReporterIonIntensities = (double[])TMTArrayUserType.Assemble(basicColumns[++column], null);
                Protein = (DataModel.Protein) basicColumns[++column];
            }
            #endregion
        }

        struct TotalCounts
        {
            public int Clusters;
            public int ProteinGroups;
            public int Proteins;
            public double ProteinFDR;
            public int GeneGroups;
            public int Genes;

            #region Constructor
            public TotalCounts (NHibernate.ISession session, DataFilter dataFilter)
            {
                if (dataFilter.IsBasicFilter)
                {
                    var totalCounts = dataFilter.PersistentDataFilter.TotalCounts;
                    Clusters = totalCounts.Clusters;
                    ProteinGroups = totalCounts.ProteinGroups;
                    Proteins = totalCounts.Proteins;
                    GeneGroups = totalCounts.GeneGroups;
                    Genes = totalCounts.Genes;
                    ProteinFDR = totalCounts.ProteinFDR;
                }
                else
                {
                    lock (session)
                    {
                        var total = session.CreateQuery("SELECT " +
                                                        "COUNT(DISTINCT pro.Cluster), " +
                                                        "COUNT(DISTINCT pro.ProteinGroup), " +
                                                        "COUNT(DISTINCT pro.id), " +
                                                        "COUNT(DISTINCT pro.GeneGroup), " +
                                                        "COUNT(DISTINCT pro.GeneId), " +
                                                        "SUM(CASE WHEN pro.IsDecoy = 1 THEN 1 ELSE 0 END) " +
                                                        dataFilter.GetFilteredQueryString(DataFilter.FromProtein))
                                           .UniqueResult<object[]>();

                        Clusters = Convert.ToInt32(total[0]);
                        ProteinGroups = Convert.ToInt32(total[1]);
                        Proteins = Convert.ToInt32(total[2]);
                        GeneGroups = Convert.ToInt32(total[3]);
                        Genes = Convert.ToInt32(total[4]);
                        float decoyProteins = Convert.ToSingle(total[5]);
                        // TODO: use correct target/decoy ratio
                        ProteinFDR = 2 * decoyProteins / Proteins;
                    }
                }
            }
            #endregion
        }
        #endregion

        #region Functions for getting rows

        IList<Row> getChildren (Grouping<GroupBy> grouping, DataFilter parentFilter)
        {
            if (grouping == null)
                return ProteinRow.GetRows(session, parentFilter).Cast<Row>().ToList();

            switch (grouping.Mode)
            {
                case GroupBy.Cluster: return ClusterRow.GetRows(session, parentFilter).Cast<Row>().ToList();
                case GroupBy.ProteinGroup: return ProteinGroupRow.GetRows(session, parentFilter).Cast<Row>().ToList();
                case GroupBy.GeneGroup: return GeneGroupRow.GetRows(session, parentFilter).Cast<Row>().ToList();
                case GroupBy.Gene: return GeneRow.GetRows(session, parentFilter).Cast<Row>().ToList();
                default: throw new NotImplementedException();
            }
        }

        protected override IList<Row> getChildren (Row parentRow)
        {
            if (parentRow.ChildRows != null)
            {
                // cached rows might be re-sorted below
            }
            else if (parentRow is ClusterRow)
            {
                var row = parentRow as ClusterRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Cluster = new List<int>() { row.Cluster } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Cluster);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is ProteinGroupRow)
            {
                var row = parentRow as ProteinGroupRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { ProteinGroup = new List<int>() { row.ProteinGroup } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.ProteinGroup);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is GeneGroupRow)
            {
                var row = parentRow as GeneGroupRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { GeneGroup = new List<int>() { row.GeneGroup } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.GeneGroup);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is GeneRow)
            {
                var row = parentRow as GeneRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Gene = new List<string>() { row.Protein.GeneId } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Gene);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is AggregateRow)
                throw new NotImplementedException();
            else if (parentRow == null)
            {
                return ProteinRow.GetRows(session, dataFilter).Cast<Row>().ToList();
            }

            if (!sortColumns.IsNullOrEmpty())
            {
                var sortColumn = sortColumns.Last();
                parentRow.ChildRows = parentRow.ChildRows.OrderBy(o => getCellValue(sortColumn.Index, o), sortColumn.Order).ToList();
            }

            return parentRow.ChildRows;
        }

        static string pivotHqlFormat = @"SELECT {0}, {1}, {2}
                                         {3}
                                         GROUP BY {0}, {1}
                                         ORDER BY {0}
                                        ";
        Map<long, Map<long, PivotData>> getPivotData (Grouping<GroupBy> group, Pivot<PivotBy> pivot, DataFilter parentFilter, bool rowMajorOrder)
        {
            // ProteinGroup and Cluster are consecutive, 1-based series
            string groupColumn = "pro.ProteinGroup";
            if (group != null && group.Mode != GroupBy.ProteinGroup)
            {
                if (group.Mode == GroupBy.Cluster) groupColumn = "pep.PeptideGroup";
                else if (group.Mode == GroupBy.Gene) groupColumn = "pro.GeneId";
                else if (group.Mode == GroupBy.GeneGroup) groupColumn = "pro.GeneGroup";
                else throw new ArgumentException();
            }
            var pivotColumn = pivot.Text.Contains("Group") ? "ssgl.Group.id" : "s.Source.id";

            string valueColumn;
            if (pivot.Text.Contains("Spectra")) valueColumn = "COUNT(DISTINCT psm.Spectrum.id)";
            else if (pivot.Text.Contains("Matches")) valueColumn = "COUNT(DISTINCT psm.DistinctMatchId)";
            else if (pivot.Text.Contains("Peptides")) valueColumn = "COUNT(DISTINCT psm.Peptide.id)";
            else if (pivot.Text.Contains("iTRAQ")) valueColumn = Row.RollupSQL + "(s.iTRAQ_ReporterIonIntensities)";
            else if (pivot.Text.Contains("TMT")) valueColumn = Row.RollupSQL + "(s.TMT_ReporterIonIntensities)";
            else if (pivot.Text.Contains("MS1")) valueColumn = "DISTINCT_SUM(xic.PeakIntensity)";
            else throw new ArgumentException("unable to handle pivot column " + pivot.Text);

            var pivotHql = String.Format(pivotHqlFormat,
                                         groupColumn, pivotColumn, valueColumn,
                                         parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                             DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink,
                                                                             DataFilter.PeptideSpectrumMatchToProtein));
            if (pivot.Text.Contains("MS1"))
            {
                pivotColumn = pivot.Text.Contains("Group") ? "ssgl.Group.id" : "xic.Source.id";
                pivotHql = String.Format(pivotHqlFormat,
                                         groupColumn, pivotColumn, valueColumn,
                                         parentFilter.GetFilteredQueryString(DataFilter.FromXic,
                                                                             DataFilter.XicToSpectrumSourceGroupLink,
                                                                             DataFilter.XicToProtein));
            }
            var query = session.CreateQuery(pivotHql);
            var pivotData = new Map<long, Map<long, PivotData>>();

            IList<object[]> pivotRows; lock (session) pivotRows = query.List<object[]>();
            if (rowMajorOrder)
            {
                if (group != null && group.Mode == GroupBy.Gene)
                    foreach (var queryRow in pivotRows)
                        pivotData[Convert.ToInt64(Util.Crc32.ComputeChecksum(Encoding.ASCII.GetBytes(queryRow[0].ToString())))][Convert.ToInt64(queryRow[1])] = new PivotData(queryRow);
                else
                    foreach (var queryRow in pivotRows)
                        pivotData[Convert.ToInt64(queryRow[0])][Convert.ToInt64(queryRow[1])] = new PivotData(queryRow);
            }
            else
            {
                if (group != null && group.Mode == GroupBy.Gene)
                    foreach (var queryRow in pivotRows)
                        pivotData[Convert.ToInt64(queryRow[1])][Convert.ToInt64(Util.Crc32.ComputeChecksum(Encoding.ASCII.GetBytes(queryRow[0].ToString())))] = new PivotData(queryRow);
                else
                    foreach (var queryRow in pivotRows)
                        pivotData[Convert.ToInt64(queryRow[1])][Convert.ToInt64(queryRow[0])] = new PivotData(queryRow);
            }
            return pivotData;
        }

        #endregion

        public event EventHandler<ViewFilterEventArgs> ProteinViewFilter;
        public event EventHandler<ProteinViewVisualizeEventArgs> ProteinViewVisualize;

        private TotalCounts totalCounts, basicTotalCounts;

        private DataGridViewColumn ReferenceColumn { get; set; }

        // map source/group id to row index to pivot data
        private Map<long, Map<long, PivotData>> statsBySpectrumSource, basicStatsBySpectrumSource;
        private Map<long, Map<long, PivotData>> statsBySpectrumSourceGroup, basicStatsBySpectrumSourceGroup;

        private Dictionary<long, SpectrumSource> sourceById;
        private Dictionary<long, SpectrumSourceGroup> groupById;

        private ToolStripMenuItem exportNetGestaltAsLogMenuItem = new ToolStripMenuItem("Export values in log scale") { CheckOnClick = true, Checked = true };
        private ToolStripMenuItem exportNetGestaltWithNormalizedColumnsMenuItem = new ToolStripMenuItem("Export values normalized by column") { CheckOnClick = true, Checked = true };
        private ToolStripMenuItem exportNetGestaltWithTrackSampleInfoMenuItem = new ToolStripMenuItem("Create Track Sample Info (TSI) file") { CheckOnClick = true, Checked = false };

        public ProteinTableForm ()
        {
            InitializeComponent();

            Text = TabText = "Protein View";
            Icon = Properties.Resources.ProteinViewIcon;

            treeDataGridView.Columns.AddRange(iTRAQ_ReporterIonColumns.ToArray());
            treeDataGridView.Columns.AddRange(TMT_ReporterIonColumns.ToArray());

            SetDefaults();

            treeDataGridView.CellValueNeeded += treeDataGridView_CellValueNeeded;
            treeDataGridView.CellFormatting += treeDataGridView_CellFormatting;
            treeDataGridView.CellMouseClick += treeDataGridView_CellMouseClick;
            treeDataGridView.CellContentClick += treeDataGridView_CellContentClick;
            treeDataGridView.CellDoubleClick += treeDataGridView_CellDoubleClick;
            treeDataGridView.PreviewKeyDown += treeDataGridView_PreviewKeyDown;
            treeDataGridView.CellIconNeeded += treeDataGridView_CellIconNeeded;
            treeDataGridView.CellPainting += treeDataGridView_CellPainting;
            treeDataGridView.CellMouseMove += treeDataGridView_CellMouseMove;
        }

        protected override void OnLoad(EventArgs e)
        {
            base.OnLoad(e);

            exportMenu.Items.Add(new ToolStripSeparator());

            var exportNetGestaltMenuItem = new ToolStripMenuItem("Export NetGestalt Composite Continuous Track (CCT)", null);
            exportMenu.Items.Add(exportNetGestaltMenuItem);
            exportNetGestaltMenuItem.DropDownItems.Add(exportNetGestaltWithNormalizedColumnsMenuItem);
            exportNetGestaltMenuItem.DropDownItems.Add(exportNetGestaltAsLogMenuItem);
            exportNetGestaltMenuItem.DropDownItems.Add(exportNetGestaltWithTrackSampleInfoMenuItem);
            foreach (var pivotMode in pivotSetupControl.Pivots)
                exportNetGestaltMenuItem.DropDownItems.Add(new ToolStripMenuItem(pivotMode.Text, null, exportNetGestaltCCT) { Tag = pivotMode });

            exportNetGestaltWithNormalizedColumnsMenuItem.Click += (s, e2) => { exportNetGestaltMenuItem.ShowDropDown(); };
            exportNetGestaltAsLogMenuItem.Click += (s, e2) => { exportNetGestaltMenuItem.ShowDropDown(); };
            exportNetGestaltWithTrackSampleInfoMenuItem.Click += (s, e2) => { exportNetGestaltMenuItem.ShowDropDown(); };
        }

        void createNetGestaltTSI(Pivot<PivotBy> pivotMode, List<DataGridViewTextBoxColumn> reporterIonColumns)
        {
            bool pivotIsOnSourceGroup = pivotMode.Text.Contains("Group");
            bool pivotIsITRAQ = pivotMode.Text.Contains("iTRAQ");
            bool pivotIsTMT = pivotMode.Text.Contains("TMT");

            var pivotGroupNames = new List<string>();

            if (pivotIsOnSourceGroup)
                foreach (var group in groupById.OrderBy(o => o.Value.Name))
                {
                    if (group.Value.Name == "/")
                    {
                        // if / is the only group, give it a NetGestalt-friendly name; otherwise skip it
                        if (groupById.Count > 1)
                            continue;
                        else
                        {
                            pivotGroupNames.Add("Root Group");
                            break;
                        }
                    }
                    else if (groupById.Values.Count(o => o.Name.Contains(group.Value.Name)) > 1)
                        continue; // skip non-leaf groups

                    string netgestaltValidName = group.Value.Name.TrimStart('/').Replace('/', '_').Replace(' ', '-');

                    if (reporterIonColumns != null)
                        foreach (var column in reporterIonColumns)
                            pivotGroupNames.Add(String.Format("{0}-{1}", netgestaltValidName, column.HeaderText));
                    else
                        pivotGroupNames.Add(String.Format("{0}", netgestaltValidName));
                }
            else
                foreach (var source in sourceById.OrderBy(o => o.Value.Name))
                {
                    string netgestaltValidName = source.Value.Name.TrimStart('/').Replace('/', '_').Replace(' ', '-');

                    if (reporterIonColumns != null)
                        foreach (var column in reporterIonColumns)
                            pivotGroupNames.Add(String.Format("{0}-{1}", netgestaltValidName, column.HeaderText));
                    else
                        pivotGroupNames.Add(String.Format("{0}", netgestaltValidName));
                }

            using (var form = new NetGestaltTrackSampleInfoForm(Path.GetFileNameWithoutExtension(session.Connection.GetDataSource()))
                                {
                                    PivotGroupNames = pivotGroupNames
                                })
            {
                form.ShowDialog(this);
            }
        }

        void exportNetGestaltCCT(object sender, EventArgs e)
        {
            string outputFilename = null;

            while(true)
                using (var sfd = new SaveFileDialog()
                                     {
                                         OverwritePrompt = true,
                                         FileName = Path.GetFileNameWithoutExtension(session.Connection.GetDataSource()),
                                         DefaultExt = ".cct",
                                         Filter = "NetGestalt CCT|*.cct|All files|*.*"
                                     })
                {
                    if (sfd.ShowDialog(Program.MainWindow) == DialogResult.Cancel)
                        return;

                    outputFilename = sfd.FileName;
                    if (Path.GetFileName(outputFilename).Contains(" "))
                    {
                        MessageBox.Show("NetGestalt does not support spaces in imported filenames.",
                                        "Invalid NetGestalt Filename",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                        continue;
                    }
                    break;
                }

            if (outputFilename == null)
                return;

            var groupMode = new Grouping<GroupBy> { Mode = GroupBy.Gene };
            var pivotMode = (sender as ToolStripMenuItem).Tag as Pivot<PivotBy>;
            var pivotData = getPivotData(groupMode, pivotMode, dataFilter, true);

            bool pivotIsOnSourceGroup = pivotMode.Text.Contains("Group");
            bool pivotIsITRAQ = pivotMode.Text.Contains("iTRAQ");
            bool pivotIsTMT = pivotMode.Text.Contains("TMT");

            var reporterIonColumns = new List<DataGridViewTextBoxColumn>();

            lock (session)
            {
                var quantitationMethods = new Set<QuantitationMethod>(session.Query<SpectrumSource>().Select(o => o.QuantitationMethod).Distinct());
                if (quantitationMethods.All(o => o == QuantitationMethod.None || o == QuantitationMethod.LabelFree))
                    reporterIonColumns = null;
                else if (quantitationMethods.Contains(QuantitationMethod.ITRAQ8plex))
                    // add all iTRAQ columns
                    iTRAQ_ReporterIonColumns.ForEach(o => reporterIonColumns.Add(o));
                else if (quantitationMethods.Contains(QuantitationMethod.ITRAQ4plex))
                    // add iTRAQ4plex-only columns
                    iTRAQ_ReporterIonColumns.GetRange(1, 4).ForEach(o => reporterIonColumns.Add(o));
                else if (quantitationMethods.Contains(QuantitationMethod.TMTpro16plex))
                    // add all TMT columns
                    TMT_ReporterIonColumns.ForEach(o => reporterIonColumns.Add(o));
                else if (quantitationMethods.Contains(QuantitationMethod.TMT11plex))
                    // add TMT11plex-only columns
                    TMT_ReporterIonColumns.GetRange(0, 11).ForEach(o => reporterIonColumns.Add(o));
                else if (quantitationMethods.Contains(QuantitationMethod.TMT10plex))
                    // add TMT10plex-only columns
                    TMT_ReporterIonColumns.GetRange(0, 10).ForEach(o => reporterIonColumns.Add(o));
                else if (quantitationMethods.Contains(QuantitationMethod.TMT6plex))
                    // add TMT6plex-only columns
                    TMT_ReporterIonColumns.Where(o => new List<int> {0, 1, 4, 5, 8, 9}.Contains((int) o.Tag)).ForEach(o => reporterIonColumns.Add(o));
                else if (quantitationMethods.Contains(QuantitationMethod.TMT2plex))
                    // add TMT2plex-only columns
                    TMT_ReporterIonColumns.Where(o => new List<int> {0, 2}.Contains((int) o.Tag)).ForEach(o => reporterIonColumns.Add(o));
                //else if(quantitationMethods.Contains(QuantitationMethod.LabelFree))

            }

            if (exportNetGestaltWithTrackSampleInfoMenuItem.Checked)
                createNetGestaltTSI(pivotMode, reporterIonColumns);

            // getPivotData must return GeneIds in hashed form because the container type is Map<long, Map<long, PivotData>>
            Dictionary<long, string> geneIdByHash;
            Dictionary<long, long> spectraByPivotGroup;
            lock (session)
            {
                var geneIds = session.CreateQuery("SELECT pro.GeneId " + dataFilter.GetFilteredQueryString(DataFilter.FromProtein) + "GROUP BY pro.GeneId").List<string>();
                geneIdByHash = geneIds.ToDictionary(o => (long) Util.Crc32.ComputeChecksum(Encoding.ASCII.GetBytes(o)));

                if (pivotIsOnSourceGroup)
                {
                    var spectraBySourceGroupQuery = session.CreateSQLQuery("SELECT ssg.Id, COUNT(DISTINCT Spectrum) FROM PeptideSpectrumMatch psm JOIN Spectrum s ON psm.Spectrum=s.Id JOIN SpectrumSource ss ON s.Source=ss.Id JOIN SpectrumSourceGroupLink ssgl ON ss.Id=ssgl.Source JOIN SpectrumSourceGroup ssg ON ssgl.Group_=ssg.Id WHERE ssg.Id > 1 GROUP BY ssg.Id").List<object[]>();
                    spectraByPivotGroup = spectraBySourceGroupQuery.ToDictionary(o => Convert.ToInt64(o[0]), o => Convert.ToInt64(o[1]));
                }
                else
                {
                    var spectraBySourceQuery = session.CreateSQLQuery("SELECT s.Source, COUNT(DISTINCT Spectrum) FROM PeptideSpectrumMatch psm JOIN Spectrum s ON psm.Spectrum=s.Id GROUP BY s.Source").List<object[]>();
                    spectraByPivotGroup = spectraBySourceQuery.ToDictionary(o => Convert.ToInt64(o[0]), o => Convert.ToInt64(o[1]));
                }
            }

            long maxSpectraByPivotGroup = spectraByPivotGroup.Max(o => o.Value);

            var columnIds = new List<long>();
            using (var fileStream = new StreamWriter(outputFilename))
            {
                fileStream.Write("GeneSymbol");
                if (pivotIsOnSourceGroup)
                    foreach (var group in groupById.OrderBy(o => o.Value.Name))
                    {
                        if (group.Value.Name == "/")
                            continue;

                        // skip non-leaf groups
                        if (groupById.Values.Count(o => o.Name.Contains(group.Value.Name)) > 1)
                            continue;

                        string netgestaltValidName = group.Value.Name.TrimStart('/').Replace('/', '_').Replace(' ', '-');

                        if (reporterIonColumns != null)
                            foreach (var column in reporterIonColumns)
                                fileStream.Write("\t{0}-{1}", netgestaltValidName, column.HeaderText);
                        else
                            fileStream.Write("\t{0}", netgestaltValidName);
                        columnIds.Add(group.Key);
                    }
                else
                    foreach (var source in sourceById.OrderBy(o => o.Value.Name))
                    {
                        string netgestaltValidName = source.Value.Name.TrimStart('/').Replace('/', '_').Replace(' ', '-');

                        if (reporterIonColumns != null)
                            foreach (var column in reporterIonColumns)
                                fileStream.Write("\t{0}-{1}", netgestaltValidName, column.HeaderText);
                        else
                            fileStream.Write("\t{0}", netgestaltValidName);
                        columnIds.Add(source.Key);
                    }
                fileStream.Write("\n");

                foreach (var kvp in pivotData)
                {
                    string geneId = geneIdByHash[kvp.Key];
                    if (geneId.StartsWith("Unmapped"))
                        continue;

                    // calculate mean value across all columns, but skip the root group if pivoting on source group
                    /*double meanValue = kvp.Value.Values.Skip(pivotIsOnSourceGroup ? 1 : 0)
                                                       .Sum(o => o.Convert.ToDouble(o.Value)) /
                                                       (pivotIsOnSourceGroup ? kvp.Value.Values.Count-1 : kvp.Value.Values.Count);
                    */
                    fileStream.Write(geneId); // NetGestalt doesn't support CRLF line endings!
                    foreach (var column in columnIds)
                    {
                        var findItr = kvp.Value.Find(column);

                        if (reporterIonColumns != null)
                        {
                            double[] array = findItr.IsValid ? (double[]) findItr.Current.Value.Value : null;

                            if (array == null)
                                foreach (var c in reporterIonColumns)
                                    fileStream.Write("\t0");
                            else
                            {
                                if (exportNetGestaltAsLogMenuItem.Checked)
                                    foreach (var c in reporterIonColumns)
                                        fileStream.Write("\t{0}", (array[(int) c.Tag] > 0 ? Math.Log(array[(int) c.Tag]) : 0.0).ToString("f2"));
                                else
                                    foreach (var c in reporterIonColumns)
                                        fileStream.Write("\t{0}", array[(int) c.Tag].ToString("f2"));
                            }
                        }
                        else
                        {
                            string value;
                            if (!findItr.IsValid)
                                value = "0";
                            else
                            {
                                double tmpValue = Convert.ToDouble(findItr.Current.Value.Value);
                                if (exportNetGestaltWithNormalizedColumnsMenuItem.Checked && spectraByPivotGroup[column] > 0)
                                    tmpValue = tmpValue / spectraByPivotGroup[column] * maxSpectraByPivotGroup;
                                if (exportNetGestaltAsLogMenuItem.Checked)
                                    tmpValue = tmpValue > 0 ? Math.Log(tmpValue) : 0.0;
                                value = tmpValue.ToString("f4");
                            }
                            fileStream.Write("\t{0}", value);
                        }
                    }
                    fileStream.Write("\n");
                }
            }
        }

        void treeDataGridView_CellMouseMove(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex >= 0 &&
                (e.ColumnIndex == coverageColumn.Index ||
                 (treeDataGridView.Columns[e.ColumnIndex] is DataGridViewLinkColumn) &&
                  treeDataGridView[e.ColumnIndex, e.RowIndex].ContentBounds.Contains(e.X, e.Y)))
                treeDataGridView.Cursor = Cursors.Hand;
            else
                treeDataGridView.Cursor = Cursors.Default;
        }

        void treeDataGridView_CellPainting (object sender, TreeDataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex != coverageColumn.Index)
                return;

            var baseRow = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            IList<ushort> coverageMask;
            if (baseRow is ProteinRow)
                coverageMask = (baseRow as ProteinRow).Protein.CoverageMask;
            else if (baseRow is ProteinGroupRow && (baseRow as ProteinGroupRow).ProteinCount == 1)
                coverageMask = (baseRow as ProteinGroupRow).FirstProtein.CoverageMask;
            else
                return;

            if (coverageMask == null) // decoy protein
                return;

            var coverageBounds = new Rectangle(e.CellBounds.X + 2, e.CellBounds.Y + 4,
                                               e.CellBounds.Width - 4, e.CellBounds.Height - 9);

            bool isSelected = (e.State & DataGridViewElementStates.Selected) == DataGridViewElementStates.Selected;

            using (var borderPen = new Pen(Color.Black))
            using (var bgBrush = new SolidBrush(isSelected ? treeDataGridView.DefaultCellStyle.SelectionBackColor
                                                           : treeDataGridView.DefaultCellStyle.BackColor))
            using (var noCoverageBrush = new SolidBrush(Color.PapayaWhip))
            using (var lowCoverageBrush = new SolidBrush(Color.Goldenrod))
            using (var mediumCoverageBrush = new SolidBrush(Color.Peru))
            using (var highCoverageBrush = new SolidBrush(Color.SaddleBrown))
            {
                // FIXME: NRE in .NET?!
                //e.PaintBackground(e.ClipBounds, isSelected);

                // draw background
                e.Graphics.FillRectangle(bgBrush, e.CellBounds);

                // draw border
                e.Graphics.DrawRectangle(borderPen,
                                         coverageBounds.X, coverageBounds.Y,
                                         coverageBounds.Width, coverageBounds.Height);

                // draw non-covered region
                e.Graphics.FillRectangle(noCoverageBrush,
                                         coverageBounds.X + 1, coverageBounds.Y + 1,
                                         coverageBounds.Width - 1, coverageBounds.Height - 1);

                int totalPixels = coverageBounds.Width - 3;
                double[] pixelCoverage = new double[totalPixels];
                pixelCoverage.Fill(0);
                for (int i = 0; i < coverageMask.Count; ++i)
                {
                    // with a wide enough coverage column, there can be more pixels than protein residues;
                    // in this case the inter-residue pixels are given the max. coverage of the adjacent residues
                    double pixelIndex = ((double) i / coverageMask.Count) * totalPixels;
                    int nextResiduePixelIndex = Math.Min(totalPixels - 1, (int) Math.Floor(((double) (i + 1)/coverageMask.Count)*totalPixels + 1));
                    for (int j = (int) Math.Floor(pixelIndex); j <= nextResiduePixelIndex; ++j)
                        pixelCoverage[j] = Math.Max(pixelCoverage[j], coverageMask[i]);
                }

                for (int i = 0; i < totalPixels; ++i)
                {
                    if (pixelCoverage[i] > 0)
                    {
                        // iterate until the depth of coverage changes
                        int j = i + 1;
                        while (j < totalPixels && pixelCoverage[j] == pixelCoverage[i]) { ++j; }

                        Brush coverageBrush;
                        if (pixelCoverage[i] > 2)
                            coverageBrush = highCoverageBrush;
                        else if (pixelCoverage[i] > 1)
                            coverageBrush = mediumCoverageBrush;
                        else
                            coverageBrush = lowCoverageBrush;

                        e.Graphics.FillRectangle(coverageBrush,
                                                 coverageBounds.X + 1 + i, coverageBounds.Y + 1,
                                                 j - i, coverageBounds.Height - 1);
                        i = j - 1;
                    }
                }

                e.Handled = true;
            }
        }

        void treeDataGridView_CellIconNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
            if (e.RowIndexHierarchy.First() >= rows.Count)
            {
                e.Value = null;
                return;
            }

            Row baseRow = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            if (baseRow is ClusterRow) e.Value = Properties.Resources.Cluster;
            else if (baseRow is ProteinGroupRow) e.Value = (baseRow as ProteinGroupRow).ProteinCount > 1 ? Properties.Resources.ProteinGroup : Properties.Resources.Protein;
            else if (baseRow is ProteinRow) e.Value = Properties.Resources.Protein;
            else if (baseRow is GeneRow) e.Value = Properties.Resources.Gene;
            else if (baseRow is GeneGroupRow) e.Value = Properties.Resources.Gene;
        }

        private void treeDataGridView_CellValueNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
            var rootGrouping = checkedGroupings.Count > 0 ? checkedGroupings.First() : null;

            if (e.RowIndexHierarchy.First() >= rows.Count)
            {
                e.Value = null;
                return;
            }

            Row baseRow = rows[e.RowIndexHierarchy.First()];
            for (int i = 1; i < e.RowIndexHierarchy.Count; ++i)
            {
                getChildren(baseRow); // populate ChildRows if necessary
                baseRow = baseRow.ChildRows[e.RowIndexHierarchy[i]];
            }

            if (baseRow is ClusterRow)
            {
                var row = baseRow as ClusterRow;
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Cluster);
                if (childGrouping == null)
                    e.ChildRowCount = (int) row.ProteinCount;
                else if (childGrouping.Mode == GroupBy.ProteinGroup)
                    e.ChildRowCount = (int) row.ProteinGroupCount;
            }
            else if (baseRow is ProteinGroupRow)
            {
                var row = baseRow as ProteinGroupRow;
                e.ChildRowCount = (int) row.ProteinCount > 1 ? (int) row.ProteinCount : 0;
            }
            else if (baseRow is GeneGroupRow)
            {
                var row = baseRow as GeneGroupRow;
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.GeneGroup);
                if (childGrouping == null)
                    e.ChildRowCount = (int) row.ProteinCount;
                else if (childGrouping.Mode == GroupBy.Gene)
                    e.ChildRowCount = (int) row.GeneCount;
            }
            else if (baseRow is GeneRow)
            {
                var row = baseRow as GeneRow;
                e.ChildRowCount = row.ProteinCount;
            }

            e.Value = getCellValue(e.ColumnIndex, baseRow);
        }

        protected override object getCellValue (int columnIndex, Row baseRow)
        {
            // TODO: fix child rows so they have their own pivot data
            if (pivotColumns.Count > 0 &&
                pivotColumns.First().Index > 0 &&
                columnIndex >= pivotColumns.First().Index)
            {
                var pivotColumn = pivotColumns[columnIndex - pivotColumns.First().Index];
                var stats = pivotColumn.Tag as Pair<bool, Map<long, PivotData>>;
                if (stats == null)
                    return 0;

                long rowId;
                if (baseRow is ClusterRow)
                    rowId = (baseRow as ClusterRow).Cluster;
                else if (baseRow is ProteinGroupRow)
                    rowId = (baseRow as ProteinGroupRow).ProteinGroup;
                else if (baseRow is ProteinRow)
                    rowId = (baseRow as ProteinRow).Protein.ProteinGroup;
                else if (baseRow is GeneGroupRow)
                    rowId = (baseRow as GeneGroupRow).GeneGroup;
                else if (baseRow is GeneRow)
                    rowId = (baseRow as GeneRow).Protein.GeneId.GetHashCode();
                else
                    throw new NotImplementedException();

                var itr = stats.second.Find(rowId);
                if (itr.IsValid)
                {
                    if (itr.Current.Value.IsArray)
                    {
                        double value = ((double[]) itr.Current.Value.Value)[Convert.ToInt32(pivotColumn.DataPropertyName)];
                        if (ReferenceColumn != null)
                        {
                            var referenceStats = ReferenceColumn.Tag as Pair<bool, Map<long, PivotData>>;
                            var refItr = referenceStats.second.Find(rowId);
                            if (refItr.IsValid)
                            {
                                double refValue = ((double[]) refItr.Current.Value.Value)[Convert.ToInt32(ReferenceColumn.DataPropertyName)];
                                if (refValue > 0)
                                    value /= refValue;
                            }
                        }

                        return value;
                    }

                    return itr.Current.Value.Value;
                }
            }
            else if (baseRow is ClusterRow)
            {
                var row = baseRow as ClusterRow;
                if (columnIndex == keyColumn.Index) return row.Cluster;
                else if (columnIndex == countColumn.Index) return row.ProteinGroupCount;
                else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == peptideSequencesColumn.Index) return row.PeptideSequences;
                else if (columnIndex == peptideGroupsColumn.Index) return row.PeptideGroups;
            }
            else if (baseRow is ProteinGroupRow)
            {
                var row = baseRow as ProteinGroupRow;
                if (columnIndex == keyColumn.Index) return proteinGroupColumn.Visible ? row.Proteins : String.Format("{0} ({1})", row.ProteinGroup, row.Proteins);
                else if (columnIndex == clusterColumn.Index) return row.FirstProtein.Cluster;
                else if (columnIndex == countColumn.Index) return row.ProteinCount;
                else if (columnIndex == coverageColumn.Index) return row.MeanProteinCoverage;
                else if (columnIndex == proteinGroupColumn.Index) return row.ProteinGroup;
                else if (columnIndex == geneIdColumn.Index) return row.FirstProtein.GeneId;
                else if (columnIndex == geneNameColumn.Index) return row.FirstProtein.GeneName;
                else if (columnIndex == chromosomeColumn.Index) return row.FirstProtein.Chromosome;
                else if (columnIndex == geneFamilyColumn.Index) return row.FirstProtein.GeneFamily;
                else if (columnIndex == descriptionColumn.Index) return row.FirstProtein.Description;
                else if (columnIndex == modifiedSitesColumn.Index) return row.ModifiedSites;
                else if (columnIndex == distinctPeptidesColumn.Index)return row.DistinctPeptides;
                else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == peptideSequencesColumn.Index) return row.PeptideSequences;
                else if (columnIndex == peptideGroupsColumn.Index) return row.PeptideGroups;
                else
                {
                    int iTRAQ_ReporterIonIndex = iTRAQ_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                    if (iTRAQ_ReporterIonIndex >= 0) return row.iTRAQ_ReporterIonIntensities[iTRAQ_ReporterIonIndex];
                    else
                    {
                        int TMT_ReporterIonIndex = TMT_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                        if (TMT_ReporterIonIndex >= 0) return row.TMT_ReporterIonIntensities[TMT_ReporterIonIndex];
                    }
                }
            }
            else if (baseRow is ProteinRow)
            {
                var row = baseRow as ProteinRow;
                if (columnIndex == keyColumn.Index) return row.Protein.Accession;
                else if (columnIndex == coverageColumn.Index && !row.Protein.IsDecoy) return row.Protein.Coverage;
                else if (columnIndex == descriptionColumn.Index) return row.Protein.Description;
                else if (columnIndex == modifiedSitesColumn.Index) return row.ModifiedSites;
                else if (checkedGroupings.All(o => o.Mode != GroupBy.ProteinGroup))
                {
                    if (columnIndex == clusterColumn.Index) return row.Protein.Cluster;
                    else if (columnIndex == proteinGroupColumn.Index) return row.Protein.ProteinGroup;
                    else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                    else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                    else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                    else if (columnIndex == peptideSequencesColumn.Index) return row.PeptideSequences;
                    else if (columnIndex == peptideGroupsColumn.Index) return row.PeptideGroups;
                    else
                    {
                        int iTRAQ_ReporterIonIndex = iTRAQ_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                        if (iTRAQ_ReporterIonIndex >= 0) return row.iTRAQ_ReporterIonIntensities[iTRAQ_ReporterIonIndex];
                        else
                        {
                            int TMT_ReporterIonIndex = TMT_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                            if (TMT_ReporterIonIndex >= 0) return row.TMT_ReporterIonIntensities[TMT_ReporterIonIndex];
                        }
                    }
                }

                if (checkedGroupings.All(o => o.Mode != GroupBy.Gene))
                {
                    if (columnIndex == geneIdColumn.Index) return row.Protein.GeneId;
                    else if (columnIndex == geneNameColumn.Index) return row.Protein.GeneName;
                    else if (columnIndex == chromosomeColumn.Index) return row.Protein.Chromosome;
                    else if (columnIndex == geneFamilyColumn.Index) return row.Protein.GeneFamily;
                }
            }
            else if (baseRow is GeneGroupRow)
            {
                var row = baseRow as GeneGroupRow;
                if (columnIndex == keyColumn.Index) return row.Genes;
                else if (columnIndex == countColumn.Index) return row.GeneCount;
                else if (columnIndex == geneFamilyColumn.Index) return row.FirstProtein.GeneFamily;
                else if (columnIndex == descriptionColumn.Index) return row.FirstProtein.GeneDescription;
                //else if (checkedGroupings.Count(o => o.Mode == GroupBy.ProteinGroup) == 0)
                {
                    if (columnIndex == clusterColumn.Index) return row.FirstProtein.Cluster;
                    //else if (columnIndex == proteinGroupColumn.Index) return row.Protein.ProteinGroup; (could be wrong)
                    else if (columnIndex == geneNameColumn.Index) return row.FirstProtein.GeneName;
                    else if (columnIndex == chromosomeColumn.Index) return row.FirstProtein.Chromosome;
                    else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                    else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                    else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                    else
                    {
                        int iTRAQ_ReporterIonIndex = iTRAQ_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                        if (iTRAQ_ReporterIonIndex >= 0) return row.iTRAQ_ReporterIonIntensities[iTRAQ_ReporterIonIndex];
                        else
                        {
                            int TMT_ReporterIonIndex = TMT_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                            if (TMT_ReporterIonIndex >= 0) return row.TMT_ReporterIonIntensities[TMT_ReporterIonIndex];
                        }
                    }
                }
            }
            else if (baseRow is GeneRow)
            {
                var row = baseRow as GeneRow;
                if (columnIndex == keyColumn.Index) return row.Protein.GeneId;
                else if (columnIndex == countColumn.Index) return row.ProteinCount;
                else if (columnIndex == geneFamilyColumn.Index) return row.Protein.GeneFamily;
                else if (columnIndex == descriptionColumn.Index) return row.Protein.GeneDescription;
                //else if (checkedGroupings.Count(o => o.Mode == GroupBy.ProteinGroup) == 0)
                {
                    if (columnIndex == clusterColumn.Index) return row.Protein.Cluster;
                    //else if (columnIndex == proteinGroupColumn.Index) return row.Protein.ProteinGroup; (could be wrong)
                    else if (columnIndex == geneIdColumn.Index) return row.Protein.GeneId;
                    else if (columnIndex == geneNameColumn.Index) return row.Protein.GeneName;
                    else if (columnIndex == chromosomeColumn.Index) return row.Protein.Chromosome;
                    else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                    else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                    else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                    else
                    {
                        int iTRAQ_ReporterIonIndex = iTRAQ_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                        if (iTRAQ_ReporterIonIndex >= 0) return row.iTRAQ_ReporterIonIntensities[iTRAQ_ReporterIonIndex];
                        else
                        {
                            int TMT_ReporterIonIndex = TMT_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                            if (TMT_ReporterIonIndex >= 0) return row.TMT_ReporterIonIntensities[TMT_ReporterIonIndex];
                        }
                    }
                }
            }
            return null;
        }

        protected override RowFilterState getRowFilterState (Row parentRow)
        {
            if (viewFilter.GeneGroup == null && viewFilter.Gene == null && viewFilter.Cluster == null &&
                viewFilter.ProteinGroup == null && viewFilter.Protein == null)
                return RowFilterState.In;

            bool result = false;
            if (parentRow is GeneGroupRow)
            {
                if (viewFilter.GeneGroup != null) result = viewFilter.GeneGroup.Contains((parentRow as GeneGroupRow).GeneGroup);
            }
            else if (parentRow is GeneRow)
            {
                if (viewFilter.Gene != null) result = viewFilter.Gene.Contains((parentRow as GeneRow).Protein.GeneId);
            }
            else if (parentRow is ClusterRow)
            {
                if (viewFilter.Cluster != null) result = viewFilter.Cluster.Contains((parentRow as ClusterRow).Cluster);
            }
            else if (parentRow is ProteinGroupRow)
            {
                if (viewFilter.ProteinGroup != null) result = viewFilter.ProteinGroup.Contains((parentRow as ProteinGroupRow).ProteinGroup);
                if (!result && viewFilter.Cluster != null) result = viewFilter.Cluster.Contains((parentRow as ProteinGroupRow).FirstProtein.Cluster);
            }
            else if (parentRow is ProteinRow)
            {
                if (viewFilter.Protein != null) result = viewFilter.Protein.Contains((parentRow as ProteinRow).Protein);
                if (!result && viewFilter.ProteinGroup != null) result = viewFilter.ProteinGroup.Contains((parentRow as ProteinRow).Protein.ProteinGroup);
                if (!result && viewFilter.Cluster != null) result = viewFilter.Cluster.Contains((parentRow as ProteinRow).Protein.Cluster);
            }
            if (result) return RowFilterState.In;
            if (parentRow.ChildRows == null) return RowFilterState.Out;

            return parentRow.ChildRows.Aggregate(RowFilterState.Unknown, (x, y) => x | getRowFilterState(y));
        }

        private void treeDataGridView_CellFormatting (object sender, TreeDataGridViewCellFormattingEventArgs e)
        {
            var column = treeDataGridView.Columns[e.ColumnIndex];
            ColumnProperty columnProperty;
            if (_columnSettings.TryGetValue(column, out columnProperty))
            {
                if (columnProperty.ForeColor.HasValue)
                    e.CellStyle.ForeColor = _columnSettings[column].ForeColor.Value;

                if (columnProperty.BackColor.HasValue)
                    e.CellStyle.BackColor = _columnSettings[column].BackColor.Value;
            }

            if (viewFilter.GeneGroup == null && viewFilter.Gene == null && viewFilter.Cluster == null &&
                viewFilter.ProteinGroup == null && viewFilter.Protein == null)
                return;

            Row row = rows[e.RowIndexHierarchy.First()];
            for (int i = 1; i < e.RowIndexHierarchy.Count; ++i)
                row = row.ChildRows[e.RowIndexHierarchy[i]];

            switch (getRowFilterState(row))
            {
                case RowFilterState.Out:
                    e.CellStyle.ForeColor = filteredOutColor;
                    break;
                case RowFilterState.Partial:
                    e.CellStyle.ForeColor = filteredPartialColor;
                    break;
            }
        }

        void treeDataGridView_CellMouseClick (object sender, TreeDataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex < 0)
                return;

            // was column header clicked?
            if (e.RowIndexHierarchy.First() < 0)
                Sort(e.ColumnIndex);
            else if (e.ColumnIndex == coverageColumn.Index && ProteinViewVisualize != null)
            {
                Row row = rows[e.RowIndexHierarchy.First()];
                for (int i = 1; i < e.RowIndexHierarchy.Count; ++i)
                    row = row.ChildRows[e.RowIndexHierarchy[i]];

                if (row is ProteinRow)
                    ProteinViewVisualize(this, new ProteinViewVisualizeEventArgs() { Protein = (row as ProteinRow).Protein });
                else if (row is ProteinGroupRow)
                {
                    List<Protein> proteins; lock (session) proteins = session.Query<Protein>().Where(o => o.ProteinGroup == (row as ProteinGroupRow).ProteinGroup).ToList();
                    foreach (var protein in proteins)
                        ProteinViewVisualize(this, new ProteinViewVisualizeEventArgs() { Protein = protein });
                }
            }
        }

        void treeDataGridView_CellContentClick (object sender, TreeDataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndexHierarchy.First() < 0)
                return;

            if (e.ColumnIndex == clusterColumn.Index && ProteinViewFilter != null)
            {
                object value = treeDataGridView[e.ColumnIndex, e.RowIndexHierarchy].Value;
                if (value == null)
                    return;

                var newDataFilter = new DataFilter(dataFilter) { FilterSource = this };
                newDataFilter.Cluster = new List<int> { (int) value };

                ProteinViewFilter(this, new ViewFilterEventArgs(newDataFilter));
            }
            else if (e.ColumnIndex == proteinGroupColumn.Index && ProteinViewFilter != null)
            {
                object value = treeDataGridView[e.ColumnIndex, e.RowIndexHierarchy].Value;
                if (value == null)
                    return;

                var newDataFilter = new DataFilter(dataFilter) { FilterSource = this };
                newDataFilter.ProteinGroup = new List<int> { (int) value };

                ProteinViewFilter(this, new ViewFilterEventArgs(newDataFilter));
            }
        }

        void treeDataGridView_CellDoubleClick (object sender, TreeDataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndexHierarchy.First() < 0)
                return;

            Row row = rows[e.RowIndexHierarchy.First()];
            for (int i = 1; i < e.RowIndexHierarchy.Count; ++i)
                row = row.ChildRows[e.RowIndexHierarchy[i]];

            var newDataFilter = new DataFilter() { FilterSource = this };

            if (row is ClusterRow)
                newDataFilter.Cluster = new List<int>() { (row as ClusterRow).Cluster };
            if (row is ProteinGroupRow)
                newDataFilter.ProteinGroup = new List<int>() { (row as ProteinGroupRow).ProteinGroup };
            else if (row is ProteinRow)
                newDataFilter.Protein = new List<Protein>() { (row as ProteinRow).Protein };
            else if (row is GeneRow)
                newDataFilter.Gene = new List<string>() { (row as GeneRow).Protein.GeneId };
            else if (row is GeneGroupRow)
                newDataFilter.GeneGroup = new List<int>() { (row as GeneGroupRow).GeneGroup };

            if (ProteinViewFilter != null)
                ProteinViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        void treeDataGridView_PreviewKeyDown (object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                treeDataGridView.ClearSelection();

            if (e.KeyCode != Keys.Enter)
                return;

            var newDataFilter = new DataFilter {FilterSource = this};

            if (treeDataGridView.SelectedCells.Count == 0)
                return;

            var selectedProteins = new Set<Protein>(new Comparison<Protein>((x,y) => x.Id.Value.CompareTo(y.Id.Value)));
            var selectedProteinGroups = new Set<int>();
            var selectedClusters = new Set<int>();
            var selectedGenes = new Set<string>();
            var selectedGeneGroups = new Set<int>();

            foreach (DataGridViewCell cell in treeDataGridView.SelectedCells)
            {
                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(cell.RowIndex);

                Row row = rows[rowIndexHierarchy.First()];
                for (int i = 1; i < rowIndexHierarchy.Count; ++i)
                    row = row.ChildRows[rowIndexHierarchy[i]];

                if (row is ClusterRow)
                    selectedClusters.Add((row as ClusterRow).Cluster);
                else if (row is ProteinGroupRow)
                    selectedProteinGroups.Add((row as ProteinGroupRow).ProteinGroup);
                else if (row is ProteinRow)
                    selectedProteins.Add((row as ProteinRow).Protein);
                else if (row is GeneRow)
                    selectedGenes.Add((row as GeneRow).Protein.GeneId);
                else if (row is GeneGroupRow)
                    selectedGeneGroups.Add((row as GeneGroupRow).GeneGroup);
            }

            if (selectedClusters.Count > 0) newDataFilter.Cluster = selectedClusters.ToList();
            if (selectedProteinGroups.Count > 0) newDataFilter.ProteinGroup = selectedProteinGroups.ToList();
            if (selectedProteins.Count > 0) newDataFilter.Protein = selectedProteins.ToList();
            if (selectedGenes.Count > 0) newDataFilter.Gene = selectedGenes.ToList();
            if (selectedGeneGroups.Count > 0) newDataFilter.GeneGroup = selectedGeneGroups.ToList();

            if (ProteinViewFilter != null)
                ProteinViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        public override void SetData(NHibernate.ISession session, DataFilter dataFilter)
        {
            base.SetData(session, dataFilter);

            if (session == null)
                return;

            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) { GeneGroup = null, Gene = null, Protein = null, Cluster = null, ProteinGroup = null };

            // remember the first selected row
            saveSelectionPath();

            ClearData();

            // stored to avoid cross-thread calls on the control
            checkedPivots = pivotSetupControl.CheckedPivots;
            checkedGroupings = groupingSetupControl.CheckedGroupings;

            setColumnVisibility();

            if (!_columnSettings[clusterColumn].Visible.HasValue || _columnSettings[clusterColumn].Visible.Value)
                clusterColumn.Visible = groupingSetupControl.CheckedGroupings.Count(o => o.Mode == GroupBy.Cluster) == 0;

            if (!_columnSettings[countColumn].Visible.HasValue || _columnSettings[countColumn].Visible.Value)
                countColumn.Visible = groupingSetupControl.CheckedGroupings.Count > 0;

            Text = TabText = "Loading protein view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        public override void ClearData ()
        {
            Text = TabText = "Protein View";

            Controls.OfType<Control>().ForEach(o => o.Enabled = false);

            // remember the first selected row
            saveSelectionPath();

            treeDataGridView.RootRowCount = 0;
            Refresh();
        }

        public override void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        void setData(object sender, DoWorkEventArgs e)
        {
            try
            {
                var rootGrouping = checkedGroupings.Count > 0 ? checkedGroupings.First() : null;

                if (dataFilter.IsBasicFilter)
                {
                    // refresh basic data when basicDataFilter is unset or when the basic filter values have changed
                    if (basicDataFilter == null || (viewFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = dataFilter;
                        basicTotalCounts = new TotalCounts(session, viewFilter);
                        basicRows = getChildren(rootGrouping, dataFilter);

                        basicStatsBySpectrumSource = null;
                        Pivot<PivotBy> pivotBySource = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Source"));
                        if (pivotBySource != null)
                            basicStatsBySpectrumSource = getPivotData(rootGrouping, pivotBySource, dataFilter, false);

                        basicStatsBySpectrumSourceGroup = null;
                        Pivot<PivotBy> pivotByGroup = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Group"));
                        if (pivotByGroup != null)
                            basicStatsBySpectrumSourceGroup = getPivotData(rootGrouping, pivotByGroup, dataFilter, false);

                        lock (session)
                        {
                            sourceById = session.Query<SpectrumSource>().Where(o => o.Group != null).ToDictionary(o => o.Id.Value);
                            groupById = session.Query<SpectrumSourceGroup>().ToDictionary(o => o.Id.Value);
                        }
                    }

                    if (viewFilter.IsBasicFilter)
                        totalCounts = basicTotalCounts;
                    else
                        totalCounts = new TotalCounts(session, viewFilter);
                    rows = basicRows;
                    unfilteredRows = null;
                    statsBySpectrumSource = basicStatsBySpectrumSource;
                    statsBySpectrumSourceGroup = basicStatsBySpectrumSourceGroup;
                }
                else
                {
                    totalCounts = new TotalCounts(session, viewFilter);
                    rows = getChildren(rootGrouping, dataFilter);
                    unfilteredRows = null;

                    statsBySpectrumSource = null;
                    Pivot<PivotBy> pivotBySource = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Source"));
                    if (pivotBySource != null)
                        statsBySpectrumSource = getPivotData(rootGrouping, pivotBySource, dataFilter, false);

                    statsBySpectrumSourceGroup = null;
                    Pivot<PivotBy> pivotByGroup = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Group"));
                    if (pivotByGroup != null)
                        statsBySpectrumSourceGroup = getPivotData(rootGrouping, pivotByGroup, dataFilter, false);
                }

                createPivotColumns();
                applyFindFilter();
                applySort();
                

                OnFinishedSetData();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
            {
                Program.HandleException(e.Result as Exception);
                return;
            }

            Controls.OfType<Control>().ForEach(o => o.Enabled = true);

            treeDataGridView.RootRowCount = rows.Count();

            // show total counts in the form title
            if (Embedder.HasGeneMetadata(session.Connection.GetDataSource()))
                Text = TabText = String.Format("Protein View: {0} clusters, {1} protein groups, {2} proteins, {3:0.##%} protein FDR, {4} gene groups, {5} genes",
                                           totalCounts.Clusters, totalCounts.ProteinGroups, totalCounts.Proteins, totalCounts.ProteinFDR, totalCounts.GeneGroups, totalCounts.Genes);
            else
                Text = TabText = String.Format("Protein View: {0} clusters, {1} protein groups, {2} proteins, {3:0.##%} protein FDR",
                                               totalCounts.Clusters, totalCounts.ProteinGroups, totalCounts.Proteins, totalCounts.ProteinFDR);

            addPivotColumns();

            // try to (re)set selected item
            restoreSelectionPath();

            treeDataGridView.Refresh();
        }

        private void createPivotColumns()
        {
            oldPivotColumns = pivotColumns;
            pivotColumns = new List<DataGridViewColumn>();

            if (checkedPivots.Count == 0)
                return;

            var sourceNames = sourceById.Select(o => o.Value.Name);
            var isobaricSampleMapping = Embedder.GetIsobaricSampleMapping(session.Connection.GetDataSource());

            if (statsBySpectrumSource != null)
            foreach (long sourceId in statsBySpectrumSource.Keys)
            {
                string uniqueSubstring;
                Util.UniqueSubstring(sourceById[sourceId].Name, sourceNames, out uniqueSubstring);

                // if pivoting on an isobaric labelling quantitation metric, add one column per reporter ion per source
                if (checkedPivots.Any(o => o.Text.Contains("Source") && (o.Text.Contains("TMT") || o.Text.Contains("iTRAQ"))))
                {
                    var quantColumns = checkedPivots.Any(o => o.Text.Contains("TMT")) ? TMT_ReporterIonColumns : iTRAQ_ReporterIonColumns;

                    for(int i=0; i < quantColumns.Count; ++i)
                    {
                        DataGridViewColumn newColumn = quantColumns[i].Clone() as DataGridViewColumn;
                        newColumn.HeaderText = String.Format("{0} ({1})", uniqueSubstring, newColumn.HeaderText);
                        newColumn.Tag = new Pair<bool, Map<long, PivotData>>(false, statsBySpectrumSource[sourceId]);
                        newColumn.DataPropertyName = i.ToString();
                        newColumn.Name = "pivotQuantColumn" + i.ToString();
                        newColumn.FillWeight = 1;
                        // preserve the visibility of the cloned column
                        pivotColumns.Add(newColumn);
                    }

                    continue;
                }

                // otherwise add a single column for each source

                var column = new DataGridViewTextBoxColumn() { HeaderText = uniqueSubstring, FillWeight = 1 };
                column.Tag = new Pair<bool, Map<long, PivotData>>(false, statsBySpectrumSource[sourceId]);
                column.Name = "pivotColumn" + column.DisplayIndex.ToString();

                var newProperties = new ColumnProperty()
                {
                    Type = typeof(int),
                    Name = column.HeaderText
                };

                var previousForm = _columnSettings.SingleOrDefault(x => x.Value.Name == column.HeaderText);

                if (previousForm.Key != null)
                {
                    _columnSettings.Remove(previousForm.Key);
                    newProperties = previousForm.Value;
                }
                else
                {
                    var possibleSaved = _unusedPivotSettings.SingleOrDefault(x => x.Name == column.HeaderText);
                    if (possibleSaved != null)
                        newProperties = possibleSaved;
                }

                if (newProperties.Visible.HasValue)
                    column.Visible = newProperties.Visible.Value;
                //_columnSettings.Add(column, newProperties);
                if (column.Visible)
                    pivotColumns.Add(column);
            }

            if (statsBySpectrumSourceGroup != null)
            foreach (long groupId in statsBySpectrumSourceGroup.Keys)
            {
                string groupName = groupById[groupId].Name.TrimEnd('/') + '/';
                if (groupName == "/")
                    continue; // skip root group

                // if pivoting on an isobaric labelling quantitation metric, add one column per reporter ion per group
                if (checkedPivots.Any(o => o.Text.Contains("Group") && (o.Text.Contains("TMT") || o.Text.Contains("iTRAQ"))))
                {
                    var quantColumns = checkedPivots.Any(o => o.Text.Contains("TMT")) ? TMT_ReporterIonColumns : iTRAQ_ReporterIonColumns;
                    var sampleNames = isobaricSampleMapping.ContainsKey(groupName.TrimEnd('/')) ? isobaricSampleMapping[groupName.TrimEnd('/')] : null;
                    ReferenceColumn = null;
                    int sampleMapIndex = 0;
                    for (int i = 0; i < quantColumns.Count; ++i)
                    {
                        string sampleName = groupName;
                        bool isReferenceColumn = false;
                        if (quantColumns[i].Visible && sampleNames != null)
                        {
                            sampleName = sampleNames[sampleMapIndex];
                            ++sampleMapIndex;
                            if (sampleName.Equals("Empty", StringComparison.InvariantCultureIgnoreCase))
                                continue;
                            if (sampleName.Equals("Reference", StringComparison.InvariantCultureIgnoreCase))
                                isReferenceColumn = true;
                        }

                        DataGridViewColumn newColumn = quantColumns[i].Clone() as DataGridViewColumn;
                        newColumn.HeaderText = String.Format("{0} ({1})", sampleName, newColumn.HeaderText);
                        newColumn.Tag = new Pair<bool, Map<long, PivotData>>(true, statsBySpectrumSourceGroup[groupId]);
                        newColumn.DataPropertyName = i.ToString();
                        newColumn.Name = "pivotQuantColumn" + i.ToString();
                        newColumn.FillWeight = 1;
                        pivotColumns.Add(newColumn);

                        if (isReferenceColumn)
                            ReferenceColumn = newColumn;
                    }

                    continue;
                }

                // otherwise add a single column for each group

                var column = new DataGridViewTextBoxColumn() { HeaderText = groupName, FillWeight = 1 };
                column.Tag = new Pair<bool, Map<long, PivotData>>(true, statsBySpectrumSourceGroup[groupId]);
                column.Name = "pivotColumn" + column.DisplayIndex.ToString();

                var newProperties = new ColumnProperty()
                {
                    Type = typeof(int),
                    Name = column.HeaderText
                };

                var previousForm = _columnSettings.SingleOrDefault(x => x.Value.Name == column.HeaderText);

                if (previousForm.Key != null)
                {
                    _columnSettings.Remove(previousForm.Key);
                    newProperties = previousForm.Value;
                }
                else
                {
                    var possibleSaved = _unusedPivotSettings.SingleOrDefault(x => x.Name == column.HeaderText);
                    if (possibleSaved != null)
                        newProperties = possibleSaved;
                }

                if (newProperties.Visible.HasValue)
                    column.Visible = newProperties.Visible.Value;
                //_columnSettings.Add(column, newProperties);
                if (column.Visible)
                    pivotColumns.Add(column);
            }

            pivotColumns.Sort((x, y) => x.HeaderText.CompareTo(y.HeaderText));
        }

        private void addPivotColumns ()
        {
            treeDataGridView.SuspendLayout();

            if (oldPivotColumns != null)
                foreach (var pivotColumn in oldPivotColumns)
                    treeDataGridView.Columns.Remove(pivotColumn);

            if (checkedPivots.Count == 0)
            {
                treeDataGridView.ResumeLayout(true);
                return;
            }

            if (statsBySpectrumSourceGroup != null && pivotColumns.Count > 0)
            {
                foreach (var c in TMT_ReporterIonColumns) c.Visible = false;
                foreach (var c in iTRAQ_ReporterIonColumns) c.Visible = false;
            }

            var sourceNames = sourceById.Select(o => o.Value.Name);
            int visibleColumns = treeDataGridView.GetVisibleColumns().Count();
            bool keepDescriptionLastColumn = descriptionColumn.DisplayIndex >= visibleColumns - 1;

            treeDataGridView.Columns.AddRange(pivotColumns.ToArray());

            if (keepDescriptionLastColumn)
                descriptionColumn.DisplayIndex = visibleColumns + pivotColumns.Count - 1;

            treeDataGridView.ResumeLayout(true);
        }

        List<ColumnProperty> _unusedPivotSettings = new List<ColumnProperty>();

        protected override bool updatePivots (FormProperty formProperty)
        {
            if (pivotSetupControl != null && formProperty.PivotModes != null)
                return base.updatePivots(formProperty);
            else
            {
                setPivots(new Pivot<PivotBy>() {Mode = PivotBy.SpectraByGroup, Text = "Spectra by Group"},
                          new Pivot<PivotBy>() {Mode = PivotBy.SpectraBySource, Text = "Spectra by Source"},
                          new Pivot<PivotBy>() {Mode = PivotBy.MatchesByGroup, Text = "Matches by Group"},
                          new Pivot<PivotBy>() {Mode = PivotBy.MatchesBySource, Text = "Matches by Source"},
                          new Pivot<PivotBy>() {Mode = PivotBy.PeptidesByGroup, Text = "Peptides by Group"},
                          new Pivot<PivotBy>() {Mode = PivotBy.PeptidesBySource, Text = "Peptides by Source"},
                          new Pivot<PivotBy>() {Mode = PivotBy.iTRAQByGroup, Text = "iTRAQ by Group"},
                          new Pivot<PivotBy>() {Mode = PivotBy.iTRAQBySource, Text = "iTRAQ by Source"},
                          new Pivot<PivotBy>() {Mode = PivotBy.TMTByGroup, Text = "TMT by Group"},
                          new Pivot<PivotBy>() {Mode = PivotBy.TMTBySource, Text = "TMT by Source"},
                          new Pivot<PivotBy>() {Mode = PivotBy.MS1IntensityByGroup, Text = "MS1 Intensity by Group"},
                          new Pivot<PivotBy>() {Mode = PivotBy.MS1IntensityBySource, Text = "MS1 Intensity by Source"});

                pivotSetupControl.PivotChanged += pivotSetupControl_PivotChanged;
                return false;
            }
        }

        protected override bool updateGroupings(FormProperty formProperty)
        {
            bool groupingChanged = false;
            if (groupingSetupControl != null && formProperty.GroupingModes != null)
                groupingChanged = base.updateGroupings(formProperty);
            else
                setGroupings(new Grouping<GroupBy>() { Mode = GroupBy.GeneGroup, Text = "Gene Group" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Gene, Text = "Gene" },
                             //new Grouping<GroupBy>() { Mode = GroupBy.GeneFamily, Text = "Gene Family" },
                             //new Grouping<GroupBy>() { Mode = GroupBy.Chromosome, Text = "Chromosome" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Cluster, Text = "Cluster" },
                             new Grouping<GroupBy>(true) { Mode = GroupBy.ProteinGroup, Text = "Protein Group" });

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;
            return groupingChanged;
        }

        private void SetDefaults ()
        {
            _columnSettings = new Dictionary<DataGridViewColumn, ColumnProperty>()
            {
                { keyColumn, new ColumnProperty {Type = typeof(string)}},
                { clusterColumn, new ColumnProperty {Type = typeof(int)}},
                { countColumn, new ColumnProperty {Type = typeof(int)}},
                { coverageColumn, new ColumnProperty {Type = typeof(float), Precision = 2 }},
                { geneIdColumn, new ColumnProperty {Type = typeof(string)}},
                { geneNameColumn, new ColumnProperty {Type = typeof(string)}},
                { chromosomeColumn, new ColumnProperty {Type = typeof(string)}},
                { geneFamilyColumn, new ColumnProperty {Type = typeof(string)}},
                { proteinGroupColumn, new ColumnProperty {Type = typeof(int)}},
                { distinctPeptidesColumn, new ColumnProperty {Type = typeof(int)}},
                { distinctMatchesColumn, new ColumnProperty {Type = typeof(int)}},
                { filteredSpectraColumn, new ColumnProperty {Type = typeof(int)}},
                { descriptionColumn, new ColumnProperty {Type = typeof(string)}},
                { modifiedSitesColumn, new ColumnProperty {Type = typeof(string), Visible = false}},
                { peptideGroupsColumn, new ColumnProperty {Type = typeof(string), Visible = false}},
                { peptideSequencesColumn, new ColumnProperty {Type = typeof(string), Visible = false}},
            };

            foreach (var column in iTRAQ_ReporterIonColumns)
                _columnSettings.Add(column, new ColumnProperty { Type = typeof(float), Precision = 2 });

            foreach (var column in TMT_ReporterIonColumns)
                _columnSettings.Add(column, new ColumnProperty { Type = typeof(float), Precision = 2 });

            foreach (var kvp in _columnSettings)
            {
                kvp.Value.Name = kvp.Key.Name;
                kvp.Value.Index = kvp.Key.Index;
                kvp.Value.DisplayIndex = kvp.Key.DisplayIndex;
            }

            initialColumnSortOrders = new Map<int, SortOrder>()
            {
                {keyColumn.Index, SortOrder.Ascending},
                {clusterColumn.Index, SortOrder.Ascending},
                {countColumn.Index, SortOrder.Ascending},
                {coverageColumn.Index, SortOrder.Descending},
                {proteinGroupColumn.Index, SortOrder.Ascending},
                {geneIdColumn.Index, SortOrder.Ascending},
                {geneNameColumn.Index, SortOrder.Ascending},
                {chromosomeColumn.Index, SortOrder.Ascending},
                {geneFamilyColumn.Index, SortOrder.Ascending},
                {distinctPeptidesColumn.Index, SortOrder.Descending},
                {distinctMatchesColumn.Index, SortOrder.Descending},
                {filteredSpectraColumn.Index, SortOrder.Descending},
                {descriptionColumn.Index, SortOrder.Ascending},
                {modifiedSitesColumn.Index, SortOrder.Ascending},
                {peptideSequencesColumn.Index, SortOrder.Ascending},
                {peptideGroupsColumn.Index, SortOrder.Ascending},
            };

            sortColumns = new List<SortColumn>
            {
                new SortColumn { Index = distinctPeptidesColumn.Index, Order = SortOrder.Descending }
            };
        }

        private void groupingSetupControl_GroupingChanging (object sender, GroupingChangingEventArgs<GroupBy> e)
        {
            // if the grouping hasn't been moved, skip the parent checking
            if (groupingSetupControl.Groupings[e.NewIndex].Mode != e.Grouping.Mode)
            {
                // GroupBy.ProteinGroup cannot be before GroupBy.Cluster; GroupBy.Gene cannot be before GroupBy.GeneGroup
                if (e.Grouping.Mode == GroupBy.ProteinGroup || e.Grouping.Mode == GroupBy.Cluster ||
                    e.Grouping.Mode == GroupBy.GeneGroup || e.Grouping.Mode == GroupBy.Gene)
                {
                    var newGroupings = new List<Grouping<GroupBy>>(groupingSetupControl.Groupings);
                    newGroupings.Remove(e.Grouping);
                    newGroupings.Insert(e.NewIndex, e.Grouping);

                    e.Cancel = GroupingSetupControl<GroupBy>.HasParentGrouping(newGroupings, GroupBy.Cluster, GroupBy.ProteinGroup) ||
                               GroupingSetupControl<GroupBy>.HasParentGrouping(newGroupings, GroupBy.GeneGroup, GroupBy.Gene);
                }
            }

            if (e.Grouping.Checked)
            {
                // uncheck mutually exclusive grouping modes
                IEnumerable<Grouping<GroupBy>> conflictingGroupings = null;
                if (e.Grouping.Mode == GroupBy.ProteinGroup || e.Grouping.Mode == GroupBy.Cluster)
                    conflictingGroupings = groupingSetupControl.CheckedGroupings.Where(o => o.Mode == GroupBy.Gene || o.Mode == GroupBy.GeneGroup);
                else
                    conflictingGroupings = groupingSetupControl.CheckedGroupings.Where(o => o.Mode == GroupBy.ProteinGroup || o.Mode == GroupBy.Cluster);

                foreach (var grouping in conflictingGroupings)
                    groupingSetupControl.SetGrouping(grouping.Mode, false);
            }
        }

        protected override void setColumnVisibility ()
        {
            var columnsIrrelevantForGrouping = new Set<DataGridViewColumn>(new Comparison<DataGridViewColumn>((x,y) => x.Name.CompareTo(y.Name)));
            if (checkedGroupings.IsNullOrEmpty())
                columnsIrrelevantForGrouping.Add(countColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Cluster)
                columnsIrrelevantForGrouping.Add(clusterColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Gene)
                columnsIrrelevantForGrouping.Add(geneIdColumn);
            // the protein group column is kept since the keyColumn does not show it if the column is visible
            //else if (checkedGroupings.First().Mode == GroupBy.ProteinGroup)
            //    columnsIrrelevantForGrouping.Add(proteinGroupColumn);

            if (session != null && session.IsOpen)
                lock (session)
                {
                    if (!Embedder.HasGeneMetadata(session.Connection.GetDataSource()))
                    {
                        columnsIrrelevantForGrouping.Add(geneIdColumn);
                        columnsIrrelevantForGrouping.Add(geneNameColumn);
                        columnsIrrelevantForGrouping.Add(chromosomeColumn);
                        columnsIrrelevantForGrouping.Add(geneFamilyColumn);
                    }
                    
                    iTRAQ_ReporterIonColumns.ForEach(o => columnsIrrelevantForGrouping.Add(o));
                    TMT_ReporterIonColumns.ForEach(o => columnsIrrelevantForGrouping.Add(o));

                    var quantitationMethods = new Set<QuantitationMethod>(session.Query<SpectrumSource>().Select(o => o.QuantitationMethod).Distinct());
                    if (quantitationMethods.Contains(QuantitationMethod.ITRAQ8plex))
                        iTRAQ_ReporterIonColumns.ForEach(o => columnsIrrelevantForGrouping.Remove(o));
                    else if (quantitationMethods.Contains(QuantitationMethod.ITRAQ4plex))
                        iTRAQ_ReporterIonColumns.GetRange(1, 4).ForEach(o => columnsIrrelevantForGrouping.Remove(o));

                    if (quantitationMethods.Contains(QuantitationMethod.TMTpro16plex))
                        TMT_ReporterIonColumns.ForEach(o => columnsIrrelevantForGrouping.Remove(o));
                    else if (quantitationMethods.Contains(QuantitationMethod.TMT11plex))
                        TMT_ReporterIonColumns.GetRange(0, 11).ForEach(o => columnsIrrelevantForGrouping.Remove(o));
                    else if (quantitationMethods.Contains(QuantitationMethod.TMT10plex))
                        TMT_ReporterIonColumns.GetRange(0, 10).ForEach(o => columnsIrrelevantForGrouping.Remove(o));
                    else if (quantitationMethods.Contains(QuantitationMethod.TMT6plex))
                        TMT_ReporterIonColumns.Where(o => new List<int> {0, 1, 4, 5, 8, 9}.Contains((int) o.Tag)).ForEach(o => columnsIrrelevantForGrouping.Remove(o));
                    else if (quantitationMethods.Contains(QuantitationMethod.TMT2plex))
                        TMT_ReporterIonColumns.Where(o => new List<int> {0, 2}.Contains((int) o.Tag)).ForEach(o => columnsIrrelevantForGrouping.Remove(o));
                }

            // if visibility is not forced, use grouping mode to set automatic visibility
            foreach (var kvp in _columnSettings)
                kvp.Key.Visible = kvp.Value.Visible ?? !columnsIrrelevantForGrouping.Contains(kvp.Key);

            base.setColumnVisibility();
        }

        protected override void OnGroupingChanged (object sender, EventArgs e)
        {
            unfilteredRows = null;
            setColumnVisibility();
            base.OnGroupingChanged(sender, e);
        }

        private void pivotSetupControl_PivotChanged (object sender, PivotChangedEventArgs<PivotBy> e)
        {
            if (e.Pivot.Checked)
            {
                // uncheck mutually exclusive pivot modes
                string exclusiveMode = e.Pivot.Text.Contains("Source") ? "Source" : "Group";
                var conflictingPivots = pivotSetupControl.CheckedPivots.Where(o => o != e.Pivot && o.Text.Contains(exclusiveMode));
                foreach (var pivot in conflictingPivots)
                    pivotSetupControl.SetPivot(pivot.Mode, false);
            }
        }

        protected override bool filterRowsOnText(string text)
        {
            // filter text is one or more keywords that ContainsOrIsContainedBy will check
            var filterString = new FilterString(text);

            if (rows.First() is ProteinRow)
                rows = rows.OfType<ProteinRow>()
                           .Where(o => o.Protein.Accession.ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneId ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneName ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneFamily ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneDescription ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.Chromosome ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (descriptionColumn.Visible && (o.Protein.Description ?? String.Empty).ContainsOrIsContainedBy(filterString)) ||
                                       (peptideSequencesColumn.Visible && o.PeptideSequences.ContainsOrIsContainedBy(filterString)))
                           .Select(o => o as Row).ToList();
            else if (rows.First() is ProteinGroupRow)
                rows = rows.OfType<ProteinGroupRow>()
                           .Where(o => o.FirstProtein.Accession.ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.GeneId ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.GeneName ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.GeneFamily ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.GeneDescription ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.Chromosome ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (descriptionColumn.Visible && (o.FirstProtein.Description ?? String.Empty).ContainsOrIsContainedBy(filterString)) ||
                                       (peptideSequencesColumn.Visible && o.PeptideSequences.ContainsOrIsContainedBy(filterString)))
                           .Select(o => o as Row).ToList();
            else if (rows.First() is GeneRow)
                rows = rows.OfType<GeneRow>()
                           .Where(o => o.Protein.Accession.ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneId ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneName ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneFamily ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.GeneDescription ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.Protein.Chromosome ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (descriptionColumn.Visible && (o.Protein.Description ?? String.Empty).ContainsOrIsContainedBy(filterString)) ||
                                       (peptideSequencesColumn.Visible && o.PeptideSequences.ContainsOrIsContainedBy(filterString)))
                           .Select(o => o as Row).ToList();
            else if (rows.First() is GeneGroupRow)
                rows = rows.OfType<GeneGroupRow>()
                           .Where(o => o.Genes.ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.GeneName ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.GeneFamily ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.GeneDescription ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (o.FirstProtein.Chromosome ?? String.Empty).ContainsOrIsContainedBy(filterString) ||
                                       (descriptionColumn.Visible && (o.FirstProtein.Description ?? String.Empty).ContainsOrIsContainedBy(filterString)) ||
                                       (peptideSequencesColumn.Visible && o.PeptideSequences.ContainsOrIsContainedBy(filterString)))
                           .Select(o => o as Row).ToList();
            else
                return false;
            return true;
        }
    }

    public class ProteinViewVisualizeEventArgs : EventArgs
    {
        public DataModel.Protein Protein { get; internal set; }
    }
}
