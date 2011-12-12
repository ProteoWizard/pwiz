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
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using PopupControl;
using IDPicker;
using IDPicker.DataModel;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    public partial class ProteinTableForm : BaseTableForm
    {
        #region Wrapper classes for encapsulating query results

        public class AggregateRow : Row
        {
            public int Spectra { get; private set; }
            public int DistinctMatches { get; private set; }
            public int DistinctPeptides { get; private set; }
            public string PeptideSequences { get; private set; }
            public string PeptideGroups { get; private set; }

            public static int ColumnCount = 5;
            public static string Selection = "SELECT " +
                                             "COUNT(DISTINCT psm.Spectrum.id), " +
                                             "COUNT(DISTINCT psm.DistinctMatchKey), " +
                                             "COUNT(DISTINCT psm.Peptide.id), " +
                                             "DISTINCT_GROUP_CONCAT(pep.Sequence), " +
                                             "DISTINCT_GROUP_CONCAT(pep.PeptideGroup)";

            #region Constructor
            public AggregateRow (object[] queryRow, DataFilter dataFilter)
            {
                int column = -1;
                Spectra = Convert.ToInt32(queryRow[++column]);
                DistinctMatches = Convert.ToInt32(queryRow[++column]);
                DistinctPeptides = Convert.ToInt32(queryRow[++column]);
                PeptideSequences = Convert.ToString(queryRow[++column]);
                PeptideGroups = Convert.ToString(queryRow[++column]);
                DataFilter = dataFilter;
            }
            #endregion
        }

        public class ClusterRow : AggregateRow
        {
            public int Cluster { get; private set; }
            public int ProteinGroupCount { get; private set; }
            public int ProteinCount { get; private set; }

            #region Constructor
            public ClusterRow (object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                ProteinGroupCount = Convert.ToInt32(queryRow[++column]);
                ProteinCount = Convert.ToInt32(queryRow[++column]);
                Cluster = Convert.ToInt32(queryRow[++column]);
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

            #region Constructor
            public ProteinGroupRow (object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Proteins = (string) queryRow[++column];
                ProteinGroup = Convert.ToInt32(queryRow[++column]);
                FirstProtein = (DataModel.Protein) queryRow[++column];
                ProteinCount = Convert.ToInt32(queryRow[++column]);
                MeanProteinCoverage = (double?) queryRow[++column];
            }
            #endregion
        }

        public class ProteinRow : AggregateRow
        {
            public DataModel.Protein Protein { get; private set; }

            #region Constructor
            public ProteinRow (object[] queryRow, DataFilter dataFilter)
            : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Protein = (DataModel.Protein) queryRow[++column];
            }
            #endregion
        }

        class PivotData
        {
            public int Spectra { get; private set; }
            public int DistinctMatches { get; private set; }
            public int DistinctPeptides { get; private set; }

            #region Constructor
            public PivotData () { }
            public PivotData (object[] queryRow)
            {
                Spectra = Convert.ToInt32(queryRow[2]);
                DistinctMatches = Convert.ToInt32(queryRow[3]);
                DistinctPeptides = Convert.ToInt32(queryRow[4]);
            }
            #endregion
        }

        struct TotalCounts
        {
            public int Clusters;
            public int ProteinGroups;
            public int Proteins;
            public double ProteinFDR;

            #region Constructor
            public TotalCounts (NHibernate.ISession session, DataFilter dataFilter)
            {
                lock (session)
                {
                    var total = session.CreateQuery("SELECT " +
                                                    "COUNT(DISTINCT pro.Cluster), " +
                                                    "COUNT(DISTINCT pro.ProteinGroup), " +
                                                    "COUNT(DISTINCT pro.id), " +
                                                    "SUM(pro.IsDecoy) " +
                                                    dataFilter.GetFilteredQueryString(DataFilter.FromProtein))
                        .UniqueResult<object[]>();

                    Clusters = Convert.ToInt32(total[0]);
                    ProteinGroups = Convert.ToInt32(total[1]);
                    Proteins = Convert.ToInt32(total[2]);
                    float decoyProteins = Convert.ToSingle(total[3]);

                    // TODO: use correct target/decoy ratio
                    ProteinFDR = 2 * decoyProteins / Proteins;
                }
            }
            #endregion
        }
        #endregion

        #region Functions for getting rows
        IList<Row> getClusterRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", " +
                                       "       COUNT(DISTINCT pro.ProteinGroup), " +
                                       "       COUNT(DISTINCT pro.Id), " +
                                       "       pro.Cluster " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                           DataFilter.ProteinToPeptideSpectrumMatch) +
                                       "GROUP BY pro.Cluster " +
                                       "ORDER BY COUNT(DISTINCT psm.Peptide.id) DESC")//, COUNT(DISTINCT psm.id) DESC, COUNT(DISTINCT psm.Spectrum.id) DESC")
                          .List<object[]>()
                          .Select(o => new ClusterRow(o, parentFilter) as Row)
                          .ToList();
        }

        public IList<Row> getProteinGroupRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", " +
                                       "       DISTINCT_GROUP_CONCAT(pro.Accession), " +
                                       "       pro.ProteinGroup, " +
                                       "       pro, " +
                                       "       COUNT(DISTINCT pro.id), " +
                                       "       AVG(pro.Coverage) " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                           DataFilter.ProteinToPeptideSpectrumMatch) +
                                       "GROUP BY pro.ProteinGroup " +
                                       "ORDER BY COUNT(DISTINCT psm.Peptide.id) DESC")//, COUNT(DISTINCT psm.id) DESC, COUNT(DISTINCT psm.Spectrum.id) DESC")
                          .List<object[]>()
                          .Select(o => new ProteinGroupRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getProteinRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", pro " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                                           DataFilter.ProteinToPeptideSpectrumMatch) +
                                       "GROUP BY pro.Id " +
                                       "ORDER BY COUNT(DISTINCT psm.Peptide.id) DESC")//, COUNT(DISTINCT psm.id) DESC, COUNT(DISTINCT psm.Spectrum.id) DESC")
                          .List<object[]>()
                          .Select(o => new ProteinRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getChildren (Grouping<GroupBy> grouping, DataFilter parentFilter)
        {
            if (grouping == null)
                return getProteinRows(parentFilter);

            switch (grouping.Mode)
            {
                case GroupBy.Cluster: return getClusterRows(parentFilter);
                case GroupBy.ProteinGroup: return getProteinGroupRows(parentFilter);
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
            else if (parentRow is AggregateRow)
                throw new NotImplementedException();
            else if (parentRow == null)
            {
                return getProteinRows(dataFilter);
            }

            if (!sortColumns.IsNullOrEmpty())
            {
                var sortColumn = sortColumns.Last();
                parentRow.ChildRows = parentRow.ChildRows.OrderBy(o => getCellValue(sortColumn.Index, o), sortColumn.Order).ToList();
            }

            return parentRow.ChildRows;
        }

        static string pivotHqlFormat = @"SELECT {0}, {1},
                                                COUNT(DISTINCT psm.Spectrum.id),
                                                COUNT(DISTINCT psm.DistinctMatchKey),
                                                COUNT(DISTINCT psm.Peptide.id)
                                         {2}
                                         GROUP BY {0}, {1}
                                         ORDER BY {0}
                                        ";
        Map<long, Map<long, PivotData>> getPivotData (Grouping<GroupBy> group, Pivot<PivotBy> pivot, DataFilter parentFilter)
        {
            // ProteinGroup and Cluster are consecutive, 1-based series
            var groupColumn = group != null && group.Mode == GroupBy.Cluster ? "pro.Cluster" : "pro.ProteinGroup";
            var pivotColumn = pivot.Text.Contains("Group") ? "ssgl.Group.id" : "s.Source.id";

            var pivotHql = String.Format(pivotHqlFormat,
                                         groupColumn, pivotColumn,
                                         parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                             DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink,
                                                                             DataFilter.PeptideSpectrumMatchToProtein));
            var query = session.CreateQuery(pivotHql);
            var pivotData = new Map<long, Map<long, PivotData>>();

            IList<object[]> pivotRows; lock (session) pivotRows = query.List<object[]>();
            foreach (var queryRow in pivotRows)
                pivotData[Convert.ToInt64(queryRow[1])][Convert.ToInt64(queryRow[0])] = new PivotData(queryRow);
            return pivotData;
        }

        #endregion

        public event ProteinViewFilterEventHandler ProteinViewFilter;
        public event EventHandler<ProteinViewVisualizeEventArgs> ProteinViewVisualize;

        private TotalCounts totalCounts, basicTotalCounts;

        // map source/group id to row index to pivot data
        private Map<long, Map<long, PivotData>> statsBySpectrumSource, basicStatsBySpectrumSource;
        private Map<long, Map<long, PivotData>> statsBySpectrumSourceGroup, basicStatsBySpectrumSourceGroup;

        private Dictionary<long, SpectrumSource> sourceById;
        private Dictionary<long, SpectrumSourceGroup> groupById;

        // TODO: support multiple selected objects
        string[] oldSelectionPath = new string[] { };

        public ProteinTableForm ()
        {
            InitializeComponent();

            Text = TabText = "Protein View";

            SetDefaults();

            treeDataGridView.CellValueNeeded += treeDataGridView_CellValueNeeded;
            treeDataGridView.CellFormatting += treeDataGridView_CellFormatting;
            treeDataGridView.CellMouseClick += treeDataGridView_CellMouseClick;
            treeDataGridView.CellContentClick += treeDataGridView_CellContentClick;
            treeDataGridView.CellDoubleClick += treeDataGridView_CellDoubleClick;
            treeDataGridView.PreviewKeyDown += treeDataGridView_PreviewKeyDown;
            treeDataGridView.CellIconNeeded += treeDataGridView_CellIconNeeded;
            treeDataGridView.CellPainting += treeDataGridView_CellPainting;
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

            e.Value = getCellValue(e.ColumnIndex, baseRow);
        }

        protected override object getCellValue (int columnIndex, Row baseRow)
        {
            // TODO: fix child rows so they have their own pivot data
            if (pivotColumns.Count > 0 && columnIndex >= pivotColumns.First().Index)
            {
                var stats = treeDataGridView.Columns[columnIndex].Tag as Map<long, PivotData>;
                if (stats == null)
                    return 0;

                long rowId;
                if (baseRow is ClusterRow)
                    rowId = (baseRow as ClusterRow).Cluster;
                else if (baseRow is ProteinGroupRow)
                    rowId = (baseRow as ProteinGroupRow).ProteinGroup;
                else if (baseRow is ProteinRow)
                    rowId = (baseRow as ProteinRow).Protein.ProteinGroup;
                else
                    throw new NotImplementedException();

                var itr = stats.Find(rowId);
                if (itr.IsValid)
                {
                    if (treeDataGridView.Columns[columnIndex].HeaderText.EndsWith("/"))
                    {
                        if (checkedPivots.Count(o => o.Mode == PivotBy.SpectraByGroup) > 0)
                            return itr.Current.Value.Spectra;
                        else if (checkedPivots.Count(o => o.Mode == PivotBy.MatchesByGroup) > 0)
                            return itr.Current.Value.DistinctMatches;
                        else if (checkedPivots.Count(o => o.Mode == PivotBy.PeptidesByGroup) > 0)
                            return itr.Current.Value.DistinctPeptides;
                    }
                    else
                    {
                        if (checkedPivots.Count(o => o.Mode == PivotBy.SpectraBySource) > 0)
                            return itr.Current.Value.Spectra;
                        else if (checkedPivots.Count(o => o.Mode == PivotBy.MatchesBySource) > 0)
                            return itr.Current.Value.DistinctMatches;
                        else if (checkedPivots.Count(o => o.Mode == PivotBy.PeptidesBySource) > 0)
                            return itr.Current.Value.DistinctPeptides;
                    }
                }
            }
            else if (baseRow is ClusterRow)
            {
                var row = baseRow as ClusterRow;
                if (columnIndex == keyColumn.Index) return row.Cluster;
                else if (columnIndex == countColumn.Index) return row.ProteinCount;
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
                else if (columnIndex == descriptionColumn.Index) return row.FirstProtein.Description;
                else if (columnIndex == distinctPeptidesColumn.Index)return row.DistinctPeptides;
                else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == peptideSequencesColumn.Index) return row.PeptideSequences;
                else if (columnIndex == peptideGroupsColumn.Index) return row.PeptideGroups;
            }
            else if (baseRow is ProteinRow)
            {
                var row = baseRow as ProteinRow;
                if (columnIndex == keyColumn.Index) return row.Protein.Accession;
                else if (columnIndex == coverageColumn.Index && !row.Protein.IsDecoy) return row.Protein.Coverage;
                else if (columnIndex == descriptionColumn.Index) return row.Protein.Description;
                else if (checkedGroupings.Count(o => o.Mode == GroupBy.ProteinGroup) == 0)
                {
                    if (columnIndex == clusterColumn.Index) return row.Protein.Cluster;
                    else if (columnIndex == proteinGroupColumn.Index) return row.Protein.ProteinGroup;
                    else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                    else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                    else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                    else if (columnIndex == peptideSequencesColumn.Index) return row.PeptideSequences;
                    else if (columnIndex == peptideGroupsColumn.Index) return row.PeptideGroups;
                }
            }
            return null;
        }

        protected override RowFilterState getRowFilterState (Row parentRow)
        {
            bool result = false;
            if (parentRow is ClusterRow)
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
                result = result || viewFilter.Cluster == null && viewFilter.ProteinGroup == null && viewFilter.Protein == null;
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

            if (viewFilter.Cluster == null && viewFilter.ProteinGroup == null && viewFilter.Protein == null)
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

                ProteinViewFilter(this, newDataFilter);
            }
            else if (e.ColumnIndex == proteinGroupColumn.Index && ProteinViewFilter != null)
            {
                object value = treeDataGridView[e.ColumnIndex, e.RowIndexHierarchy].Value;
                if (value == null)
                    return;

                var newDataFilter = new DataFilter(dataFilter) { FilterSource = this };
                newDataFilter.ProteinGroup = new List<int> { (int) value };

                ProteinViewFilter(this, newDataFilter);
            }
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
                        ProteinViewVisualize(this, new ProteinViewVisualizeEventArgs() {Protein = protein});
                }
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

            if (ProteinViewFilter != null)
                ProteinViewFilter(this, newDataFilter);
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
            }

            if (selectedClusters.Count > 0) newDataFilter.Cluster = selectedClusters.ToList();
            if (selectedProteinGroups.Count > 0) newDataFilter.ProteinGroup = selectedProteinGroups.ToList();
            if (selectedProteins.Count > 0) newDataFilter.Protein = selectedProteins.ToList();

            if (ProteinViewFilter != null)
                ProteinViewFilter(this, newDataFilter);
        }

        public override void SetData (NHibernate.ISession session, DataFilter viewFilter)
        {
            this.session = session;
            this.viewFilter = viewFilter;
            this.dataFilter = new DataFilter(viewFilter) { Protein = null, Cluster = null, ProteinGroup = null };

            /*if (treeListView.SelectedObject is ProteinGroupRow)
            {
                oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
            }
            else if (treeListView.SelectedObject is ProteinRow)
            {
                var proteinGroup = (treeListView.GetParent(treeListView.SelectedObject) as ProteinGroupRow).Proteins;
                oldSelectionPath = new string[] { proteinGroup, treeListView.SelectedItem.Text };
            }*/

            ClearData();

            // stored to avoid cross-thread calls on the control
            checkedPivots = pivotSetupControl.CheckedPivots;
            checkedGroupings = groupingSetupControl.CheckedGroupings;

            clusterColumn.Visible = groupingSetupControl.CheckedGroupings.Count(o => o.Mode == GroupBy.Cluster) == 0;
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
                        basicTotalCounts = new TotalCounts(session, dataFilter);
                        basicRows = getChildren(rootGrouping, dataFilter);

                        basicStatsBySpectrumSourceGroup = null;
                        Pivot<PivotBy> pivotBySource = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Source"));
                        if (pivotBySource != null)
                            basicStatsBySpectrumSource = getPivotData(rootGrouping, pivotBySource, dataFilter);

                        basicStatsBySpectrumSourceGroup = null;
                        Pivot<PivotBy> pivotByGroup = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Group"));
                        if (pivotByGroup != null)
                            basicStatsBySpectrumSourceGroup = getPivotData(rootGrouping, pivotByGroup, dataFilter);

                        lock (session)
                        {
                            sourceById = session.Query<SpectrumSource>().Where(o => o.Group != null).ToDictionary(o => o.Id.Value);
                            groupById = session.Query<SpectrumSourceGroup>().ToDictionary(o => o.Id.Value);
                        }
                    }

                    totalCounts = basicTotalCounts;
                    rows = basicRows;
                    statsBySpectrumSource = basicStatsBySpectrumSource;
                    statsBySpectrumSourceGroup = basicStatsBySpectrumSourceGroup;
                }
                else
                {
                    totalCounts = new TotalCounts(session, dataFilter);
                    rows = getChildren(rootGrouping, dataFilter);

                    statsBySpectrumSource = null;
                    Pivot<PivotBy> pivotBySource = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Source"));
                    if (pivotBySource != null)
                        statsBySpectrumSource = getPivotData(rootGrouping, pivotBySource, dataFilter);

                    statsBySpectrumSourceGroup = null;
                    Pivot<PivotBy> pivotByGroup = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Group"));
                    if (pivotByGroup != null)
                        statsBySpectrumSourceGroup = getPivotData(rootGrouping, pivotByGroup, dataFilter);
                }

                applySort();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
                Program.HandleException(e.Result as Exception);

            treeDataGridView.RootRowCount = rows.Count();

            // show total counts in the form title
            Text = TabText = String.Format("Protein View: {0} clusters, {1} protein groups, {2} proteins, {3:0.##%} protein FDR",
                                           totalCounts.Clusters, totalCounts.ProteinGroups, totalCounts.Proteins, totalCounts.ProteinFDR * 100);

            addPivotColumns();

            // try to (re)set selected item
            /*OLVListItem selectedItem = null;
            foreach (string branch in oldSelectionPath)
            {
                int index = 0;
                if (selectedItem != null)
                {
                    treeListView.Expand(selectedItem.RowObject);
                    index = selectedItem.Index;
                }

                index = treeListView.FindMatchingRow(branch, index, SearchDirectionHint.Down);
                if (index < 0)
                    break;
                selectedItem = treeListView.Items[index] as OLVListItem;
            }

            if (selectedItem != null)
            {
                treeListView.SelectedItem = selectedItem;
                selectedItem.EnsureVisible();
            }*/

            treeDataGridView.Refresh();
        }

        private void addPivotColumns ()
        {
            treeDataGridView.SuspendLayout();
            foreach (var pivotColumn in pivotColumns)
                treeDataGridView.Columns.Remove(pivotColumn);

            pivotColumns = new List<DataGridViewColumn>();

            if (checkedPivots.Count == 0)
            {
                treeDataGridView.ResumeLayout(true);
                return;
            }

            var sourceNames = sourceById.Select(o => o.Value.Name);
            int visibleColumns = treeDataGridView.GetVisibleColumns().Count();
            bool keepDescriptionLastColumn = descriptionColumn.DisplayIndex == visibleColumns - 1;

            if (statsBySpectrumSource != null)
            foreach (long sourceId in statsBySpectrumSource.Keys)
            {
                string uniqueSubstring;
                Util.UniqueSubstring(sourceById[sourceId].Name, sourceNames, out uniqueSubstring);
                var column = new DataGridViewTextBoxColumn() { HeaderText = uniqueSubstring};
                column.Tag = statsBySpectrumSource[sourceId];

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
                var column = new DataGridViewTextBoxColumn() { HeaderText = groupById[groupId].Name.TrimEnd('/') + '/' };
                column.Tag = statsBySpectrumSourceGroup[groupId];

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
                          new Pivot<PivotBy>() {Mode = PivotBy.PeptidesBySource, Text = "Peptides by Source"});
                pivotSetupControl.PivotChanged += pivotSetupControl_PivotChanged;
                return false;
            }
        }

        protected override bool updateGroupings (FormProperty formProperty)
        {
            bool groupingChanged = false;
            if (groupingSetupControl != null && formProperty.GroupingModes != null)
                groupingChanged = base.updateGroupings(formProperty);
            else
                setGroupings(new Grouping<GroupBy>() {Mode = GroupBy.Cluster, Text = "Cluster"},
                             new Grouping<GroupBy>() {Mode = GroupBy.ProteinGroup, Text = "Protein Group"});

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;
            return groupingChanged;
        }

        private void SetDefaults ()
        {
            _columnSettings = new Dictionary<DataGridViewColumn, ColumnProperty>()
            {
                { keyColumn, new ColumnProperty() {Type = typeof(string)}},
                { clusterColumn, new ColumnProperty() {Type = typeof(int)}},
                { countColumn, new ColumnProperty() {Type = typeof(int)}},
                { coverageColumn, new ColumnProperty() {Type = typeof(float)}},
                { proteinGroupColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctPeptidesColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctMatchesColumn, new ColumnProperty() {Type = typeof(int)}},
                { filteredSpectraColumn, new ColumnProperty() {Type = typeof(int)}},
                { descriptionColumn, new ColumnProperty() {Type = typeof(string)}},
                { peptideGroupsColumn, new ColumnProperty() {Type = typeof(string), Visible = false}},
                { peptideSequencesColumn, new ColumnProperty() {Type = typeof(string), Visible = false}},
            };

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
                {distinctPeptidesColumn.Index, SortOrder.Descending},
                {distinctMatchesColumn.Index, SortOrder.Descending},
                {filteredSpectraColumn.Index, SortOrder.Descending},
                {descriptionColumn.Index, SortOrder.Ascending},
                {peptideSequencesColumn.Index, SortOrder.Ascending},
                {peptideGroupsColumn.Index, SortOrder.Ascending},
            };
        }

        private void groupingSetupControl_GroupingChanging (object sender, GroupingChangingEventArgs<GroupBy> e)
        {
            // GroupBy.ProteinGroup cannot be before GroupBy.Cluster

            if (e.Grouping.Mode != GroupBy.ProteinGroup && e.Grouping.Mode != GroupBy.Cluster)
                return;

            var newGroupings = new List<Grouping<GroupBy>>(groupingSetupControl.Groupings);
            newGroupings.Remove(e.Grouping);
            newGroupings.Insert(e.NewIndex, e.Grouping);

            e.Cancel = GroupingSetupControl<GroupBy>.HasParentGrouping(newGroupings, GroupBy.Cluster, GroupBy.ProteinGroup);
        }

        protected override void setColumnVisibility ()
        {
            var columnsIrrelevantForGrouping = new Set<DataGridViewColumn>(new Comparison<DataGridViewColumn>((x,y) => x.Name.CompareTo(y.Name)));
            if (checkedGroupings.IsNullOrEmpty())
                columnsIrrelevantForGrouping.Add(countColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Cluster)
                columnsIrrelevantForGrouping.Add(clusterColumn);
            // the protein group column is kept since the keyColumn does not show it if the column is visible
            //else if (checkedGroupings.First().Mode == GroupBy.ProteinGroup)
            //    columnsIrrelevantForGrouping.Add(proteinGroupColumn);

            // if visibility is not forced, use grouping mode to set automatic visibility
            foreach (var kvp in _columnSettings)
                kvp.Key.Visible = kvp.Value.Visible ?? !columnsIrrelevantForGrouping.Contains(kvp.Key);

            base.setColumnVisibility();
        }

        protected override void OnGroupingChanged (object sender, EventArgs e)
        {
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
    }

    public delegate void ProteinViewFilterEventHandler (ProteinTableForm sender, DataFilter proteinViewFilter);

    public class ProteinViewVisualizeEventArgs : EventArgs
    {
        public DataModel.Protein Protein { get; internal set; }
    }
}
