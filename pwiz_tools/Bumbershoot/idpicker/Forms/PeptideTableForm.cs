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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using BrightIdeasSoftware;
using PopupControl;
using IDPicker.DataModel;
using IDPicker.Controls;
using pwiz.Common.Collections;

namespace IDPicker.Forms
{
    public partial class PeptideTableForm : BaseTableForm
    {
        #region Wrapper classes for encapsulating query results

        public class AggregateRow : Row
        {
            protected static readonly iTRAQArrayUserType itraqArrayType = new iTRAQArrayUserType();
            protected static readonly TMTArrayUserType tmtArrayType = new TMTArrayUserType();

            public int Spectra { get; private set; }
            public int DistinctMatches { get; private set; }
            public int DistinctPeptides { get; private set; }
            public int Proteins { get; private set; }
            public string ProteinAccessions { get; private set; }
            public string ProteinGroups { get; private set; }
            public double[] iTRAQ_ReporterIonIntensities { get; protected set; }
            public double[] TMT_ReporterIonIntensities { get; protected set; }

            public static int ColumnCount = 3;
            public static string Selection = "SELECT " +
                                             "COUNT(DISTINCT psm.Spectrum.id), " +
                                             "COUNT(DISTINCT psm.DistinctMatchId), " +
                                             "COUNT(DISTINCT psm.Peptide.id)";

            protected static IDictionary<TKey, object[]> GetDetailedColumnsByKey<TKey> (NHibernate.ISession session, DataFilter dataFilter, string keyColumn)
            {
                // these columns are not affected by protein view filters
                var dataFilter2 = new DataFilter(dataFilter) { Cluster = null, Protein = null, ProteinGroup = null };
                return session.CreateQuery("SELECT " + keyColumn +
                                           ", COUNT(DISTINCT pro.id)" +
                                           ", DISTINCT_GROUP_CONCAT(pro.Accession)" +
                                           ", DISTINCT_GROUP_CONCAT(pro.ProteinGroup)" +
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
                Proteins = Convert.ToInt32(detailedColumns[++column]);
                ProteinAccessions = Convert.ToString(detailedColumns[++column]);
                ProteinGroups = Convert.ToString(detailedColumns[++column]);
                DataFilter = dataFilter;
            }
            #endregion
        }

        public class PeptideGroupRow : AggregateRow
        {
            public int PeptideGroup { get; private set; }

            public static IList<PeptideGroupRow> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<int, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", " + RollupSQL + "(s.iTRAQ_ReporterIonIntensities)" +
                                                       ", " + RollupSQL + "(s.TMT_ReporterIonIntensities)" +
                                                       ", pep.PeptideGroup " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToProtein, DataFilter.PeptideSpectrumMatchToSpectrum) +
                                                       "GROUP BY pep.PeptideGroup").List<object[]>();

                    // these columns are not affected by the view filter
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<int>(session, dataFilter, "pep.PeptideGroup");
                }

                var rows = new List<PeptideGroupRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new PeptideGroupRow(basicColumns[i], detailedColumnsByKey[(int) basicColumns[i].Last()], dataFilter));
                return rows;
            }

            #region Constructor
            public PeptideGroupRow (object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                iTRAQ_ReporterIonIntensities = (double[]) itraqArrayType.Assemble((byte[]) basicColumns[++column], null);
                TMT_ReporterIonIntensities = (double[]) tmtArrayType.Assemble((byte[]) basicColumns[++column], null);
                PeptideGroup = Convert.ToInt32(basicColumns[++column]);
            }
            #endregion
        }

        public class DistinctPeptideRow : AggregateRow
        {
            public Peptide Peptide { get; private set; }

            public static IList<DistinctPeptideRow> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<long, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", " + RollupSQL + "(s.iTRAQ_ReporterIonIntensities)" +
                                                       ", " + RollupSQL + "(s.TMT_ReporterIonIntensities)" +
                                                       ", psm.Peptide " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToProtein, DataFilter.PeptideSpectrumMatchToSpectrum) +
                                                       "GROUP BY psm.Peptide.id").List<object[]>();

                    // these columns are not affected by the view filter
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<long>(session, dataFilter, "psm.Peptide.id");
                }

                var rows = new List<DistinctPeptideRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new DistinctPeptideRow(basicColumns[i], detailedColumnsByKey[(basicColumns[i].Last() as Peptide).Id.Value], dataFilter));
                return rows;
            }

            #region Constructor
            public DistinctPeptideRow (object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                iTRAQ_ReporterIonIntensities = (double[]) itraqArrayType.Assemble((byte[]) basicColumns[++column], null);
                TMT_ReporterIonIntensities = (double[]) tmtArrayType.Assemble((byte[]) basicColumns[++column], null);
                Peptide = (DataModel.Peptide)basicColumns[++column];
            }
            #endregion
        }

        public class DistinctMatchRow : AggregateRow
        {
            public Peptide Peptide { get; private set; }
            public DistinctMatchKey DistinctMatch { get; private set; }
            public PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }

            public static IList<DistinctMatchRow> GetRows (NHibernate.ISession session, DataFilter dataFilter)
            {
                IList<object[]> basicColumns;
                IDictionary<long, object[]> detailedColumnsByKey;
                lock (session)
                {
                    basicColumns = session.CreateQuery(AggregateRow.Selection +
                                                       ", " + RollupSQL + "(s.iTRAQ_ReporterIonIntensities)" +
                                                       ", " + RollupSQL + "(s.TMT_ReporterIonIntensities)" +
                                                       ", psm.Peptide, psm, psm.DistinctMatchKey, psm.DistinctMatchId " +
                                                       dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToProtein, DataFilter.PeptideSpectrumMatchToSpectrum) +
                                                       "GROUP BY psm.DistinctMatchId").List<object[]>();
                    detailedColumnsByKey = AggregateRow.GetDetailedColumnsByKey<long>(session, dataFilter, "psm.DistinctMatchId");
                }

                var rows = new List<DistinctMatchRow>(basicColumns.Count);
                for (int i = 0; i < basicColumns.Count; ++i)
                    rows.Add(new DistinctMatchRow(basicColumns[i], detailedColumnsByKey[(long) basicColumns[i].Last()], dataFilter));
                return rows;
            }

            #region Constructor
            public DistinctMatchRow (object[] basicColumns, object[] detailedColumns, DataFilter dataFilter)
                : base(basicColumns, detailedColumns, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                iTRAQ_ReporterIonIntensities = (double[]) itraqArrayType.Assemble((byte[]) basicColumns[++column], null);
                TMT_ReporterIonIntensities = (double[]) tmtArrayType.Assemble((byte[]) basicColumns[++column], null);
                Peptide = (Peptide) basicColumns[++column];
                PeptideSpectrumMatch = (PeptideSpectrumMatch) basicColumns[++column];
                DistinctMatch = new DistinctMatchKey(Peptide, PeptideSpectrumMatch,
                                                     dataFilter.DistinctMatchFormat,
                                                     (string) basicColumns[++column],
                                                     (long) basicColumns[++column]);
            }

            #endregion
        }

        struct TotalCounts
        {
            public int DistinctPeptides;
            public int DistinctMatches;

            #region Constructor
            public TotalCounts (NHibernate.ISession session, DataFilter dataFilter)
            {
                if (dataFilter.IsBasicFilter)
                {
                    var totalCounts = dataFilter.PersistentDataFilter.TotalCounts;
                    DistinctPeptides = totalCounts.DistinctPeptides;
                    DistinctMatches = totalCounts.DistinctMatches;
                }
                else
                {
                    lock (session)
                    {
                        var total = session.CreateQuery("SELECT " +
                                                        "COUNT(DISTINCT psm.Peptide.id), " +
                                                        "COUNT(DISTINCT psm.DistinctMatchId) " +
                                                        dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch))
                                           .UniqueResult<object[]>();

                        DistinctPeptides = Convert.ToInt32(total[0]);
                        DistinctMatches = Convert.ToInt32(total[1]);
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
                return DistinctMatchRow.GetRows(session, parentFilter).Cast<Row>().ToList();

            switch (grouping.Mode)
            {
                case GroupBy.PeptideGroup: return PeptideGroupRow.GetRows(session, parentFilter).Cast<Row>().ToList();
                case GroupBy.Peptide: return DistinctPeptideRow.GetRows(session, parentFilter).Cast<Row>().ToList();
                default: throw new NotImplementedException();
            }
        }

        protected override IList<Row> getChildren (Row parentRow)
        {
            if (parentRow.ChildRows != null)
            {
                // cached rows might be re-sorted below
            }
            else if (parentRow is PeptideGroupRow)
            {
                var row = parentRow as PeptideGroupRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { PeptideGroup = new List<int>() { row.PeptideGroup } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.PeptideGroup);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is DistinctPeptideRow)
            {
                var row = parentRow as DistinctPeptideRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Peptide = new List<Peptide>() { row.Peptide } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Peptide);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is AggregateRow)
                throw new NotImplementedException();
            else if (parentRow == null)
            {
                return DistinctMatchRow.GetRows(session, dataFilter).Cast<Row>().ToList();
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
        Map<long, Map<long, PivotData>> getPivotData (Grouping<GroupBy> group, Pivot<PivotBy> pivot, DataFilter parentFilter)
        {
            // ProteinGroup and Cluster are consecutive, 1-based series
            string groupColumn = "psm.DistinctMatchId";
            if (group != null)
            {
                if (group.Mode == GroupBy.PeptideGroup) groupColumn = "pep.PeptideGroup";
                else if (group.Mode == GroupBy.Peptide) groupColumn = "psm.Peptide.id";
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
                                                                             DataFilter.PeptideSpectrumMatchToPeptide));
            if (pivot.Text.Contains("MS1"))
            {
                pivotColumn = pivot.Text.Contains("Group") ? "ssgl.Group.id" : "xic.Source.id";
                groupColumn = "xic.DistinctMatch";
                if (group != null)
                {
                    if (group.Mode == GroupBy.PeptideGroup) groupColumn = "pep.PeptideGroup";
                    else if (group.Mode == GroupBy.Peptide) groupColumn = "xic.Peptide.id"; 
                    else throw new ArgumentException();
                }
                pivotHql = String.Format(pivotHqlFormat,
                                         groupColumn, pivotColumn, valueColumn,
                                         parentFilter.GetFilteredQueryString(DataFilter.FromXic,
                                                                             DataFilter.XicToSpectrumSourceGroupLink,
                                                                             DataFilter.XicToPeptide));
            }

            var query = session.CreateQuery(pivotHql);
            var pivotData = new Map<long, Map<long, PivotData>>();

            IList<object[]> pivotRows; lock (session) pivotRows = query.List<object[]>();
            foreach (var queryRow in pivotRows)
                pivotData[Convert.ToInt64(queryRow[1])][Convert.ToInt64(queryRow[0])] = new PivotData(queryRow);
            return pivotData;
        }

        #endregion

        public event EventHandler<ViewFilterEventArgs> PeptideViewFilter;

        private TotalCounts totalCounts, basicTotalCounts;

        private Dictionary<long, SpectrumSource> sourceById;
        private Dictionary<long, SpectrumSourceGroup> groupById;

        // map source/group id to row index to pivot data
        private Map<long, Map<long, PivotData>> statsBySpectrumSource, basicStatsBySpectrumSource;
        private Map<long, Map<long, PivotData>> statsBySpectrumSourceGroup, basicStatsBySpectrumSourceGroup;

        public PeptideTableForm ()
        {
            InitializeComponent();

            Text = TabText = "Peptide View";
            Icon = Properties.Resources.PeptideViewIcon;

            treeDataGridView.Columns.AddRange(iTRAQ_ReporterIonColumns.ToArray());
            treeDataGridView.Columns.AddRange(TMT_ReporterIonColumns.ToArray());

            SetDefaults();

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;
            pivotSetupControl.PivotChanged += pivotSetupControl_PivotChanged;

            treeDataGridView.CellValueNeeded += treeDataGridView_CellValueNeeded;
            treeDataGridView.CellFormatting += treeDataGridView_CellFormatting;
            treeDataGridView.CellMouseClick += treeDataGridView_CellMouseClick;
            treeDataGridView.CellContentClick += treeDataGridView_CellContentClick;
            treeDataGridView.CellDoubleClick += treeDataGridView_CellDoubleClick;
            treeDataGridView.PreviewKeyDown += treeDataGridView_PreviewKeyDown;
            treeDataGridView.CellIconNeeded += treeDataGridView_CellIconNeeded;
        }

        void treeDataGridView_CellIconNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
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

            if (baseRow is PeptideGroupRow) e.Value = Properties.Resources.PeptideGroup;
            else if (baseRow is DistinctPeptideRow) e.Value = Properties.Resources.Peptide;
            else if (baseRow is DistinctMatchRow) e.Value = Properties.Resources.DistinctMatch;
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

            if (baseRow is PeptideGroupRow)
            {
                var row = baseRow as PeptideGroupRow;
                
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.PeptideGroup);
                if (childGrouping == null)
                    e.ChildRowCount = row.DistinctMatches;
                else if (childGrouping.Mode == GroupBy.Peptide)
                    e.ChildRowCount = row.DistinctPeptides;
            }
            else if (baseRow is DistinctPeptideRow)
            {
                var row = baseRow as DistinctPeptideRow;
                e.ChildRowCount = row.DistinctMatches;
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
                if (baseRow is PeptideGroupRow)
                    rowId = (baseRow as PeptideGroupRow).PeptideGroup;
                else if (baseRow is DistinctPeptideRow)
                    rowId = (baseRow as DistinctPeptideRow).Peptide.Id.Value;
                else if (baseRow is DistinctMatchRow)
                    rowId = (baseRow as DistinctMatchRow).PeptideSpectrumMatch.DistinctMatchId;
                else
                    throw new NotImplementedException();
                var itr = stats.second.Find(rowId);

                if (itr.IsValid)
                {
                    if (stats.first)
                    {
                        if (itr.Current.Value.IsArray)
                            return ((double[]) itr.Current.Value.Value)[Convert.ToInt32(pivotColumn.DataPropertyName)];
                        else
                            return itr.Current.Value.Value;
                    }
                    else
                    {
                        if (itr.Current.Value.IsArray)
                            return ((double[]) itr.Current.Value.Value)[Convert.ToInt32(pivotColumn.DataPropertyName)];
                        else
                            return itr.Current.Value.Value;
                    }
                }
            }
            else if (baseRow is PeptideGroupRow)
            {
                var row = baseRow as PeptideGroupRow;
                if (columnIndex == keyColumn.Index) return row.PeptideGroup;
                else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == proteinsColumn.Index) return row.Proteins;
                else if (columnIndex == proteinAccessionsColumn.Index) return row.ProteinAccessions;
                else if (columnIndex == proteinGroupsColumn.Index) return row.ProteinGroups;
            }
            else if (baseRow is DistinctPeptideRow)
            {
                var row = baseRow as DistinctPeptideRow;
                if (columnIndex == keyColumn.Index) return row.Peptide.Sequence;
                else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == monoisotopicMassColumn.Index) return row.Peptide.MonoisotopicMass;
                else if (columnIndex == molecularWeightColumn.Index) return row.Peptide.MolecularWeight;
                else if (columnIndex == peptideGroupColumn.Index) return row.Peptide.PeptideGroup;
                else if (columnIndex == proteinsColumn.Index) return row.Proteins;
                else if (columnIndex == proteinAccessionsColumn.Index) return row.ProteinAccessions;
                else if (columnIndex == proteinGroupsColumn.Index) return row.ProteinGroups;
            }
            else if (baseRow is DistinctMatchRow)
            {
                var row = baseRow as DistinctMatchRow;
                if (columnIndex == keyColumn.Index) return row.DistinctMatch;
                else if (columnIndex == monoisotopicMassColumn.Index) return row.PeptideSpectrumMatch.ObservedNeutralMass - row.PeptideSpectrumMatch.MonoisotopicMassError;
                else if (columnIndex == molecularWeightColumn.Index) return row.PeptideSpectrumMatch.ObservedNeutralMass - row.PeptideSpectrumMatch.MolecularWeightError;
                else if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == peptideGroupColumn.Index) return row.Peptide.PeptideGroup;
                else if (columnIndex == proteinsColumn.Index) return row.Proteins;
                else if (columnIndex == proteinAccessionsColumn.Index) return row.ProteinAccessions;
                else if (columnIndex == proteinGroupsColumn.Index) return row.ProteinGroups;
            }

            // if we get this far, try the iTRAQ/TMT total columns
            var arow = baseRow as AggregateRow;
            int iTRAQ_ReporterIonIndex = iTRAQ_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
            if (iTRAQ_ReporterIonIndex >= 0) return arow.iTRAQ_ReporterIonIntensities[iTRAQ_ReporterIonIndex];
            else
            {
                int TMT_ReporterIonIndex = TMT_ReporterIonColumns.FindIndex(o => o.Index == columnIndex);
                if (TMT_ReporterIonIndex >= 0) return arow.TMT_ReporterIonIntensities[TMT_ReporterIonIndex];
            }

            return null;
        }

        protected override RowFilterState getRowFilterState (Row parentRow)
        {
            if (viewFilter.PeptideGroup == null && viewFilter.Peptide == null && viewFilter.DistinctMatchKey == null)
                return RowFilterState.In;

            bool result = false;
            if (parentRow is PeptideGroupRow)
            {
                if (viewFilter.PeptideGroup != null) result = viewFilter.PeptideGroup.Contains((parentRow as PeptideGroupRow).PeptideGroup);
            }
            else if (parentRow is DistinctPeptideRow)
            {
                if (viewFilter.Peptide != null) result = viewFilter.Peptide.Contains((parentRow as DistinctPeptideRow).Peptide);
                if (!result && viewFilter.PeptideGroup != null) result = viewFilter.PeptideGroup.Contains((parentRow as DistinctPeptideRow).Peptide.PeptideGroup);
            }
            else if (parentRow is DistinctMatchRow)
            {
                if (viewFilter.DistinctMatchKey != null) result = viewFilter.DistinctMatchKey.Contains((parentRow as DistinctMatchRow).DistinctMatch);
                if (!result && viewFilter.Peptide != null) result = viewFilter.Peptide.Contains((parentRow as DistinctMatchRow).Peptide);
                if (!result && viewFilter.PeptideGroup != null) result = viewFilter.PeptideGroup.Contains((parentRow as DistinctMatchRow).Peptide.PeptideGroup);
            }
            if (result) return RowFilterState.In;
            if (parentRow.ChildRows == null) return RowFilterState.Out;

            return parentRow.ChildRows.Aggregate(RowFilterState.Unknown, (x, y) => x | getRowFilterState(y));
        }

        private void treeDataGridView_CellFormatting (object sender, TreeDataGridViewCellFormattingEventArgs e)
        {
            var column = treeDataGridView.Columns[e.ColumnIndex];
            if (_columnSettings.ContainsKey(column) && _columnSettings[column].BackColor.HasValue)
                e.CellStyle.BackColor = _columnSettings[column].BackColor.Value;
            else
                e.CellStyle.BackColor = e.CellStyle.BackColor;

            if (viewFilter.PeptideGroup == null && viewFilter.Peptide == null && viewFilter.DistinctMatchKey == null)
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

            if (column is DataGridViewLinkColumn)
            {
                var cell = treeDataGridView[e.ColumnIndex, e.RowIndexHierarchy] as DataGridViewLinkCell;
                cell.LinkColor = cell.ActiveLinkColor = e.CellStyle.ForeColor;
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

            /*if (e.ColumnIndex == clusterColumn.Index && ProteinViewFilter != null)
            {
                object value = treeDataGridView[e.ColumnIndex, e.RowIndexHierarchy].Value;
                if (value == null)
                    return;

                var newDataFilter = new DataFilter(dataFilter) { FilterSource = this };
                newDataFilter.Cluster = new List<int> { (int) value };

                PeptideViewFilter(this, newDataFilter);
            }
            else if (e.ColumnIndex == coverageColumn.Index && ProteinViewVisualize != null)
            {
                Row row = rows[e.RowIndexHierarchy.First()];
                for (int i = 1; i < e.RowIndexHierarchy.Count; ++i)
                    row = row.ChildRows[e.RowIndexHierarchy[i]];

                if (row is ProteinRow)
                    ProteinViewVisualize(this, new ProteinViewVisualizeEventArgs() { Protein = (row as ProteinRow).Protein });
            }*/
        }

        void treeDataGridView_CellDoubleClick (object sender, TreeDataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndexHierarchy.First() < 0)
                return;

            Row row = rows[e.RowIndexHierarchy.First()];
            for (int i = 1; i < e.RowIndexHierarchy.Count; ++i)
                row = row.ChildRows[e.RowIndexHierarchy[i]];

            var newDataFilter = new DataFilter() { FilterSource = this };

            if (row is PeptideGroupRow)
                newDataFilter.PeptideGroup = new List<int>() { (row as PeptideGroupRow).PeptideGroup };
            if (row is DistinctPeptideRow)
                newDataFilter.Peptide = new List<Peptide>() { (row as DistinctPeptideRow).Peptide };
            else if (row is DistinctMatchRow)
                newDataFilter.DistinctMatchKey = new List<DistinctMatchKey>() { (row as DistinctMatchRow).DistinctMatch };

            if (PeptideViewFilter != null)
                PeptideViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        void treeDataGridView_PreviewKeyDown (object sender, PreviewKeyDownEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                treeDataGridView.ClearSelection();

            if (e.KeyCode != Keys.Enter)
                return;

            var newDataFilter = new DataFilter { FilterSource = this };

            if (treeDataGridView.SelectedCells.Count == 0)
                return;

            var processedRows = new Set<int>();
            var selectedPeptideGroups = new List<int>();
            var selectedPeptides = new List<Peptide>();
            var selectedMatches = new List<DistinctMatchKey>();

            foreach (DataGridViewCell cell in treeDataGridView.SelectedCells)
            {
                if (!processedRows.Insert(cell.RowIndex).WasInserted)
                    continue;

                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(cell.RowIndex);

                Row row = rows[rowIndexHierarchy.First()];
                for (int i = 1; i < rowIndexHierarchy.Count; ++i)
                    row = row.ChildRows[rowIndexHierarchy[i]];

                if (row is PeptideGroupRow)
                    selectedPeptideGroups.Add((row as PeptideGroupRow).PeptideGroup);
                else if (row is DistinctPeptideRow)
                    selectedPeptides.Add((row as DistinctPeptideRow).Peptide);
                else if (row is DistinctMatchRow)
                    selectedMatches.Add((row as DistinctMatchRow).DistinctMatch);
            }

            if (selectedPeptideGroups.Count > 0) newDataFilter.PeptideGroup = selectedPeptideGroups;
            if (selectedPeptides.Count > 0) newDataFilter.Peptide = selectedPeptides;
            if (selectedMatches.Count > 0) newDataFilter.DistinctMatchKey = selectedMatches;

            if (PeptideViewFilter != null)
                PeptideViewFilter(this, new ViewFilterEventArgs(newDataFilter));
        }

        private void SetDefaults()
        {
            _columnSettings = new Dictionary<DataGridViewColumn, ColumnProperty>()
            {
                { keyColumn, new ColumnProperty() {Type = typeof(string)}},
                { distinctPeptidesColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctMatchesColumn, new ColumnProperty() {Type = typeof(int)}},
                { filteredSpectraColumn, new ColumnProperty() {Type = typeof(int)}},
                { monoisotopicMassColumn, new ColumnProperty() {Type = typeof(float)}},
                { molecularWeightColumn, new ColumnProperty() {Type = typeof(float)}},
                { peptideGroupColumn, new ColumnProperty() {Type = typeof(int), Visible = false}},
                { proteinsColumn, new ColumnProperty() {Type = typeof(int), Visible = false}},
                { proteinAccessionsColumn, new ColumnProperty() {Type = typeof(string), Visible = false}},
                { proteinGroupsColumn, new ColumnProperty() {Type = typeof(string), Visible = false}},
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
                {distinctPeptidesColumn.Index, SortOrder.Descending},
                {distinctMatchesColumn.Index, SortOrder.Descending},
                {filteredSpectraColumn.Index, SortOrder.Descending},
                {monoisotopicMassColumn.Index, SortOrder.Ascending},
                {molecularWeightColumn.Index, SortOrder.Ascending},
                {peptideGroupColumn.Index, SortOrder.Ascending},
                {proteinsColumn.Index, SortOrder.Ascending},
                {proteinAccessionsColumn.Index, SortOrder.Ascending},
                {proteinGroupsColumn.Index, SortOrder.Ascending},
            };

            sortColumns = new List<SortColumn>
            {
                new SortColumn { Index = filteredSpectraColumn.Index, Order = SortOrder.Descending }
            };
        }

        public override void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            base.SetData(session, dataFilter);

            if (session == null)
                return;

            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) {Peptide = null, DistinctMatchKey = null};

            // remember the first selected row
            saveSelectionPath();

            ClearData();

            // stored to avoid cross-thread calls on the control
            checkedPivots = pivotSetupControl.CheckedPivots;
            checkedGroupings = groupingSetupControl.CheckedGroupings;

            setColumnVisibility();

            Text = TabText = "Loading peptide view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        private void createPivotColumns ()
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

                    for (int i = 0; i < quantColumns.Count; ++i)
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

                    int sampleMapIndex = 0;
                    for (int i = 0; i < quantColumns.Count; ++i)
                    {
                        string sampleName = groupName;
                        if (quantColumns[i].Visible && sampleNames != null)
                        {
                            sampleName = sampleNames[sampleMapIndex];
                            ++sampleMapIndex;
                        }

                        DataGridViewColumn newColumn = quantColumns[i].Clone() as DataGridViewColumn;
                        newColumn.HeaderText = String.Format("{0} ({1})", sampleName, newColumn.HeaderText);
                        newColumn.Tag = new Pair<bool, Map<long, PivotData>>(true, statsBySpectrumSourceGroup[groupId]);
                        newColumn.DataPropertyName = i.ToString();
                        newColumn.Name = "pivotQuantColumn" + i.ToString();
                        newColumn.FillWeight = 1;
                        // preserve the visibility of the cloned column
                        pivotColumns.Add(newColumn);
                    }

                    continue;
                }

                // otherwise add a single column for each group
                var column = new DataGridViewTextBoxColumn() { HeaderText = groupById[groupId].Name.TrimEnd('/') + '/', FillWeight = 1 };
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

            if (checkedPivots.Count > 0)
                treeDataGridView.Columns.AddRange(pivotColumns.ToArray());

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

        protected override bool updateGroupings (FormProperty formProperty)
        {
            bool groupingChanged = false;
            if (groupingSetupControl != null && formProperty.GroupingModes != null)
                groupingChanged = base.updateGroupings(formProperty);
            else
                setGroupings(new Grouping<GroupBy>() { Mode = GroupBy.PeptideGroup, Text = "Peptide Group" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Peptide, Text = "Peptide" });

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;

            //if (groupingChanged)
            //    setColumnVisibility();

            return groupingChanged;
        }

        public override void ClearData ()
        {
            Text = TabText = "Peptide View";

            // remember the first selected row
            saveSelectionPath();

            Controls.OfType<Control>().ForEach(o => o.Enabled = false);

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
                        statsBySpectrumSource = getPivotData(rootGrouping, pivotBySource, dataFilter);

                    statsBySpectrumSourceGroup = null;
                    Pivot<PivotBy> pivotByGroup = checkedPivots.FirstOrDefault(o => o.Mode.ToString().Contains("Group"));
                    if (pivotByGroup != null)
                        statsBySpectrumSourceGroup = getPivotData(rootGrouping, pivotByGroup, dataFilter);
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
            Text = TabText = String.Format("Peptide View: {0} distinct peptides, {1} distinct matches",
                                           totalCounts.DistinctPeptides, totalCounts.DistinctMatches);

            addPivotColumns();

            // try to (re)set selected item
            restoreSelectionPath();

            treeDataGridView.Refresh();
        }

        private void groupingSetupControl_GroupingChanging (object sender, GroupingChangingEventArgs<GroupBy> e)
        {
            // GroupBy.Peptide cannot be before GroupBy.PeptideGroup

            if (e.Grouping.Mode != GroupBy.PeptideGroup && e.Grouping.Mode != GroupBy.Peptide)
                return;

            var newGroupings = new List<Grouping<GroupBy>>(groupingSetupControl.Groupings);
            newGroupings.Remove(e.Grouping);
            newGroupings.Insert(e.NewIndex, e.Grouping);

            e.Cancel = GroupingSetupControl<GroupBy>.HasParentGrouping(newGroupings, GroupBy.PeptideGroup, GroupBy.Peptide);
        }

        protected override void setColumnVisibility ()
        {
            var columnsIrrelevantForGrouping = new Set<DataGridViewColumn>(new Comparison<DataGridViewColumn>((x, y) => x.Name.CompareTo(y.Name)));
            if (checkedGroupings.IsNullOrEmpty())
            {
                columnsIrrelevantForGrouping.Add(distinctPeptidesColumn);
                columnsIrrelevantForGrouping.Add(distinctMatchesColumn);
            }
            else if (checkedGroupings.First().Mode == GroupBy.PeptideGroup)
                columnsIrrelevantForGrouping.Add(peptideGroupColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Peptide)
                columnsIrrelevantForGrouping.Add(distinctPeptidesColumn);

            if (session != null && session.IsOpen)
                lock (session)
                {
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

            // for each row, for each keyword, check each column of interest against the keyword
            // the trick is to create a generic enumerator class which sucks out arbitrary objects from the rows to be compared against
            if (rows.First() is DistinctMatchRow)
                rows = rows.OfType<DistinctMatchRow>()
                           .Where(o => o.Peptide.Sequence.ContainsOrIsContainedBy(filterString) ||
                                       o.DistinctMatch.ToString().ContainsOrIsContainedBy(filterString) ||
                                       (proteinAccessionsColumn.Visible && o.ProteinAccessions.ContainsOrIsContainedBy(filterString)))
                           .Select(o => o as Row).ToList();
            else if (rows.First() is DistinctPeptideRow)
                rows = rows.OfType<DistinctPeptideRow>()
                           .Where(o => o.Peptide.Sequence.ContainsOrIsContainedBy(filterString) ||
                                       (proteinAccessionsColumn.Visible && o.ProteinAccessions.ContainsOrIsContainedBy(filterString)))
                           .Select(o => o as Row).ToList();
            else if (rows.First() is PeptideGroupRow)
                rows = rows.OfType<PeptideGroupRow>()
                           .Where(o => o.PeptideGroup.ToString() == text ||
                                       (proteinAccessionsColumn.Visible && o.ProteinAccessions.ContainsOrIsContainedBy(filterString)))
                           .Select(o => o as Row).ToList();
            else
                return false;
            return true;
        }
    }
}
