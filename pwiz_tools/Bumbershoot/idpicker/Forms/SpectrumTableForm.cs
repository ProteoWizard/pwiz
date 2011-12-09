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
using System.IO;
using System.Windows.Forms;
using System.Threading;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using BrightIdeasSoftware;
using PopupControl;
using IDPicker.DataModel;
using IDPicker.Controls;

namespace IDPicker.Forms
{
    public partial class SpectrumTableForm : BaseTableForm
    {
        #region Wrapper classes for encapsulating query results

        public class AggregateRow : Row
        {
            public int PeptideSpectrumMatches { get; private set; }
            public int Sources { get; private set; }
            public int Spectra { get; private set; }
            public int DistinctMatches { get; private set; }
            public int DistinctPeptides { get; private set; }
            public int DistinctAnalyses { get; private set; }
            public int DistinctCharges { get; private set; }
            public int DistinctProteins { get; private set; }

            public static int ColumnCount = 8;
            public static string Selection = "SELECT " +
                                             "COUNT(DISTINCT psm.id), " +
                                             "COUNT(DISTINCT psm.Spectrum.Source.id), " +
                                             "COUNT(DISTINCT psm.Spectrum.id), " +
                                             "COUNT(DISTINCT psm.DistinctMatchKey), " +
                                             "COUNT(DISTINCT psm.Peptide.id), " +
                                             "COUNT(DISTINCT psm.Analysis.id), " +
                                             "COUNT(DISTINCT psm.Charge), " +
                                             "COUNT(DISTINCT pi.Protein.id)";

            #region Constructor
            public AggregateRow(object[] queryRow, DataFilter dataFilter)
            {
                int column = -1;
                PeptideSpectrumMatches = Convert.ToInt32(queryRow[++column]);
                Sources = Convert.ToInt32(queryRow[++column]);
                Spectra = Convert.ToInt32(queryRow[++column]);
                DistinctMatches = Convert.ToInt32(queryRow[++column]);
                DistinctPeptides = Convert.ToInt32(queryRow[++column]);
                DistinctAnalyses = Convert.ToInt32(queryRow[++column]);
                DistinctCharges = Convert.ToInt32(queryRow[++column]);
                DistinctProteins = Convert.ToInt32(queryRow[++column]);
                DataFilter = dataFilter;
            }
            #endregion
        }

        public class SpectrumSourceGroupRow : AggregateRow
        {
            public DataModel.SpectrumSourceGroup SpectrumSourceGroup { get; private set; }

            #region Constructor
            public SpectrumSourceGroupRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                SpectrumSourceGroup = (queryRow[++column] as DataModel.SpectrumSourceGroupLink).Group;
            }
            #endregion
        }

        public class SpectrumSourceRow : AggregateRow
        {
            public DataModel.SpectrumSource SpectrumSource { get; private set; }

            #region Constructor
            public SpectrumSourceRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                SpectrumSource = (DataModel.SpectrumSource) queryRow[++column];
            }
            #endregion
        }

        public class AnalysisRow : AggregateRow
        {
            public DataModel.Analysis Analysis { get; private set; }
            public string Key { get; private set; }

            #region Constructor
            public AnalysisRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Analysis = (DataModel.Analysis) queryRow[++column];

                Key = String.Format("{0} {1} {2}", Analysis.Id, Analysis.Software.Name, Analysis.Software.Version);
            }
            #endregion
        }

        public class ChargeRow : AggregateRow
        {
            public int Charge { get; private set; }

            #region Constructor
            public ChargeRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Charge = Convert.ToInt32(queryRow[++column]);
            }
            #endregion
        }

        public class PeptideRow : AggregateRow
        {
            public DataModel.Peptide Peptide { get; private set; }

            #region Constructor
            public PeptideRow(object[] queryRow, DataFilter dataFilter)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Peptide = (DataModel.Peptide) queryRow[++column];
            }
            #endregion
        }

        public class SpectrumRow : AggregateRow
        {
            public DataModel.Spectrum Spectrum { get; private set; }
            public DataModel.SpectrumSource Source { get; private set; }
            public DataModel.SpectrumSourceGroup Group { get; private set; }
            public string Key { get; private set; }

            #region Constructor
            public SpectrumRow(object[] queryRow, DataFilter dataFilter, IList<Grouping<GroupBy>> checkedGroupings)
                : base(queryRow, dataFilter)
            {
                int column = AggregateRow.ColumnCount - 1;
                Spectrum = (DataModel.Spectrum) queryRow[++column];
                Source = (DataModel.SpectrumSource) queryRow[++column];
                Group = (DataModel.SpectrumSourceGroup) queryRow[++column];

                Key = Spectrum.NativeID;

                // try to abbreviate, e.g. "controllerType=0 controllerNumber=1 scan=123" -> "0.1.123"
                try { Key = pwiz.CLI.msdata.id.abbreviate(Key); }
                catch { }

                // if not grouping by Source, prepend Spectrum.Source to the NativeID
                if (checkedGroupings.Count(o => o.Mode == GroupBy.Source) == 0)
                    Key = (Group.Name + "/" + Source.Name + "/" + Key).Replace("//", "/");
            }
            #endregion
        }

        public class PeptideSpectrumMatchRow : Row
        {
            public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }
            public DataModel.Spectrum Spectrum { get; private set; }
            public DataModel.SpectrumSource Source { get; private set; }
            public DataModel.SpectrumSourceGroup Group { get; private set; }
            public string Key { get; private set; }

            #region Constructor
            public PeptideSpectrumMatchRow(object[] queryRow, DataFilter dataFilter, IList<Grouping<GroupBy>> checkedGroupings)
            {
                int column = -1;
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch) queryRow[++column];
                column += 2; // skip pm and mod columns
                Spectrum = (DataModel.Spectrum) queryRow[++column];
                Source = (DataModel.SpectrumSource) queryRow[++column];
                Group = (DataModel.SpectrumSourceGroup) queryRow[++column];
                DataFilter = dataFilter;

                // if not grouping by Spectrum, use Spectrum as the key column
                if (checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) == 0)
                {
                    Key = Spectrum.NativeID;

                    // try to abbreviate, e.g. "controllerType=0 controllerNumber=1 scan=123" -> "0.1.123"
                    try { Key = pwiz.CLI.msdata.id.abbreviate(Key); }
                    catch { }

                    // if not grouping by Source, prepend Spectrum.Source to the NativeID
                    if (checkedGroupings.Count(o => o.Mode == GroupBy.Source) == 0)
                        Key = (Group.Name + "/" + Source.Name + "/" + Key).Replace("//", "/");
                }
                else
                    Key = PeptideSpectrumMatch.Rank.ToString();
            }
            #endregion
        }

        public class PeptideSpectrumMatchScoreRow : Row
        {
            public string Name { get; private set; }
            public double Value { get; private set; }

            #region Constructor
            public PeptideSpectrumMatchScoreRow(KeyValuePair<string, double> score)
            {
                Name = score.Key;
                Value = score.Value;
            }
            #endregion
        }

        struct TotalCounts
        {
            public int Groups;
            public int Sources;
            public long Spectra;
            public int Charges;
            public int Analyses;

            #region Constructor
            public TotalCounts (NHibernate.ISession session, DataFilter dataFilter)
            {
                lock (session)
                {
                    var total = session.CreateQuery("SELECT " +
                                                    "COUNT(DISTINCT psm.Spectrum.Source.Group), " +
                                                    "COUNT(DISTINCT psm.Spectrum.Source), " +
                                                    "COUNT(DISTINCT psm.Spectrum), " +
                                                    "COUNT(DISTINCT psm.Charge), " +
                                                    "COUNT(DISTINCT psm.Analysis) " +
                                                    dataFilter.GetFilteredQueryString(
                                                        DataFilter.FromPeptideSpectrumMatch))
                        .List<object[]>()[0];

                    Groups = Convert.ToInt32(total[0]);
                    Sources = Convert.ToInt32(total[1]);
                    Spectra = Convert.ToInt64(total[2]);
                    Charges = Convert.ToInt32(total[3]);
                    Analyses = Convert.ToInt32(total[4]);
                }
            }
            #endregion
        }

        #endregion

        #region getChildren functions for each row type

        // returns both groups and sources
        IList<Row> getSpectrumSourceRows (DataFilter parentFilter)
        {
            var nonGroupParentFilterKey = new DataFilterKey(new DataFilter(parentFilter) { SpectrumSourceGroup = null });

            if (!rowsBySource.ContainsKey(nonGroupParentFilterKey))
                lock (session)
                {
                    var groupsFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = null };
                    var groups = session.CreateQuery(AggregateRow.Selection + ", ssgl " +
                                                     groupsFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                         DataFilter.PeptideSpectrumMatchToPeptideInstance,
                                                                                         DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                                     "GROUP BY ssgl.Group.id")
                        .List<object[]>()
                        .Select(o => new SpectrumSourceGroupRow(o, parentFilter))
                        .ToList();

                    var sources = session.CreateQuery(AggregateRow.Selection + ", s.Source " +
                                                      parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                          DataFilter.PeptideSpectrumMatchToPeptideInstance,
                                                                                          DataFilter.PeptideSpectrumMatchToSpectrum) +
                                                      "GROUP BY s.Source.id")
                        .List<object[]>()
                        .Select(o => new SpectrumSourceRow(o, parentFilter))
                        .ToList();

                    rowsBySource[nonGroupParentFilterKey] = groups.Cast<Row>().Concat(sources.Cast<Row>()).ToList();
                }

            var ssgRows = rowsBySource[nonGroupParentFilterKey].Where(o => o is SpectrumSourceGroupRow).Select(o => o as SpectrumSourceGroupRow);
            var ssRows = rowsBySource[nonGroupParentFilterKey].Where(o => o is SpectrumSourceRow).Select(o => o as SpectrumSourceRow);
            var result = Enumerable.Empty<Row>();

            if (parentFilter != null && parentFilter.SpectrumSourceGroup != null)
                foreach (var item in parentFilter.SpectrumSourceGroup)
                    result = result.Concat(ssgRows.Where(o => o.SpectrumSourceGroup.IsImmediateChildOf(item)).Cast<Row>());
            else
                result = ssgRows.Where(o => o.SpectrumSourceGroup.Name == "/").Cast<Row>();

            if (parentFilter != null && parentFilter.SpectrumSourceGroup != null)
            {
                foreach (var item in parentFilter.SpectrumSourceGroup)
                    result = result.Concat(ssRows.Where(o => o.SpectrumSource.Group.Id == item.Id).Cast<Row>());
            }

            return result.ToList();
        }

        IList<Row> getAnalysisRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", psm.Analysis " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToPeptideInstance) +
                                       "GROUP BY psm.Analysis.id")
                          .List<object[]>()
                          .Select(o => new AnalysisRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getPeptideRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", psm.Peptide " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToPeptideInstance) +
                                       "GROUP BY psm.Peptide.id")
                          .List<object[]>()
                          .Select(o => new PeptideRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getChargeRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", psm.Charge " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToPeptideInstance) +
                                       "GROUP BY psm.Charge")
                          .List<object[]>()
                          .Select(o => new ChargeRow(o, parentFilter) as Row)
                          .ToList();
        }

        IList<Row> getSpectrumRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery(AggregateRow.Selection + ", s, ss, ssg " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToPeptideInstance,
                                                                           DataFilter.PeptideSpectrumMatchToSpectrumSourceGroup) +
                                       "GROUP BY s.id " +
                                       "ORDER BY ss.Name, s.Index")
                          .List<object[]>()
                          .Select(o => new SpectrumRow(o, parentFilter, checkedGroupings) as Row)
                          .ToList();
        }

        IList<Row> getPeptideSpectrumMatchRows (DataFilter parentFilter)
        {
            lock (session)
            return session.CreateQuery("SELECT DISTINCT psm, pm, mod, s, ss, ssg " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToPeptideInstance,
                                                                           DataFilter.PeptideSpectrumMatchToModification,
                                                                           DataFilter.PeptideSpectrumMatchToSpectrumSourceGroup) +
                                       "GROUP BY psm.id " +
                                       "ORDER BY psm.Analysis, psm.Charge ")
                          .List<object[]>()
                          .Select(o => new PeptideSpectrumMatchRow(o, parentFilter, checkedGroupings) as Row)
                          .ToList();
        }

        IList<Row> getChildren (PeptideSpectrumMatchRow x, DataFilter parentFilter)
        {
            lock (session)
            return x.PeptideSpectrumMatch.Scores.Select(o => new PeptideSpectrumMatchScoreRow(o) as Row).ToList();
        }

        IList<Row> getChildren (Grouping<GroupBy> grouping, DataFilter parentFilter)
        {
            if (grouping == null)
                return getPeptideSpectrumMatchRows(parentFilter);

            switch (grouping.Mode)
            {
                case GroupBy.Source:
                    // if there is no parent grouping, show the root group, else skip it
                    if (parentFilter == dataFilter)
                        return getSpectrumSourceRows(parentFilter);
                    else
                        return getChildren(getSpectrumSourceRows(parentFilter)[0]);

                case GroupBy.Spectrum: return getSpectrumRows(parentFilter);
                case GroupBy.Analysis: return getAnalysisRows(parentFilter);
                case GroupBy.Peptide: return getPeptideRows(parentFilter);
                case GroupBy.Charge: return getChargeRows(parentFilter);
                default: throw new NotImplementedException();
            }
        }

        protected override IList<Row> getChildren (Row parentRow)
        {
            if (parentRow.ChildRows != null)
            {
                // cached rows might be re-sorted below
            }
            else if (parentRow is SpectrumSourceGroupRow)
            {
                var row = parentRow as SpectrumSourceGroupRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = new List<SpectrumSourceGroup>() { row.SpectrumSourceGroup } };
                parentRow.ChildRows = getSpectrumSourceRows(childFilter);
            }
            else if (parentRow is SpectrumSourceRow)
            {
                var row = parentRow as SpectrumSourceRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = null, SpectrumSource = new List<SpectrumSource>() { row.SpectrumSource } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Source);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is AnalysisRow)
            {
                var row = parentRow as AnalysisRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Analysis = new List<Analysis>() { row.Analysis } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Analysis);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is PeptideRow)
            {
                var row = parentRow as PeptideRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Peptide = new List<Peptide>() { row.Peptide } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Peptide);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is ChargeRow)
            {
                var row = parentRow as ChargeRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Charge = new List<int>() { row.Charge } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Charge);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is SpectrumRow)
            {
                var row = parentRow as SpectrumRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = null, SpectrumSource = null, Spectrum = new List<Spectrum>() { row.Spectrum } };
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Spectrum);
                parentRow.ChildRows = getChildren(childGrouping, childFilter);
            }
            else if (parentRow is AggregateRow)
                throw new NotImplementedException();
            else // PeptideSpectrumMatchRow
                parentRow.ChildRows = getChildren(parentRow as PeptideSpectrumMatchRow, dataFilter);

            if (!sortColumns.IsNullOrEmpty())
            {
                var sortColumn = sortColumns.Last();
                parentRow.ChildRows = parentRow.ChildRows.OrderBy(o => getCellValue(sortColumn.Index, o), sortColumn.Order).ToList();
            }

            return parentRow.ChildRows;
        }
        #endregion

        public event EventHandler<DataFilter> SpectrumViewFilter;
        public event EventHandler<SpectrumViewVisualizeEventArgs> SpectrumViewVisualize;

        private TotalCounts totalCounts, basicTotalCounts;
        private Dictionary<DataFilterKey, List<Row>> rowsBySource, basicRowsBySource;
        
        // TODO: support multiple selected objects
        List<string> oldSelectionPath = new List<string>();

        DataGridViewColumn[] aggregateColumns, psmColumns;

        public SpectrumTableForm()
        {
            InitializeComponent();

            Text = TabText = "Spectrum View";

            aggregateColumns = new DataGridViewColumn[]
            {
                distinctPeptidesColumn,
                distinctMatchesColumn,
                filteredSpectraColumn,
                distinctProteinsColumn,
                distinctAnalysesColumn,
                distinctChargesColumn
            };

            psmColumns = new DataGridViewColumn[]
            {
                analysisColumn,
                chargeColumn,
                observedMassColumn,
                exactMassColumn,
                massErrorColumn,
                qvalueColumn,
                sequenceColumn
            };

            var editSourceGroupsButton = new ToolStripButton()
            {
                Text = "Source Grouping",
                Alignment = ToolStripItemAlignment.Right
            };
            editSourceGroupsButton.Click += editGroupsButton_Click;
            toolStrip.Items.Add(editSourceGroupsButton);

            pivotSetupButton.Visible = false;

            SetDefaults();

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;

            treeDataGridView.CellValueNeeded += treeDataGridView_CellValueNeeded;
            treeDataGridView.CellFormatting += treeDataGridView_CellFormatting;
            treeDataGridView.CellMouseClick += treeDataGridView_CellMouseClick;
            //treeDataGridView.CellContentClick += treeDataGridView_CellContentClick;
            treeDataGridView.CellDoubleClick += treeDataGridView_CellDoubleClick;
            treeDataGridView.PreviewKeyDown += treeDataGridView_PreviewKeyDown;
            treeDataGridView.CellIconNeeded += treeDataGridView_CellIconNeeded;
        }

        private void treeDataGridView_CellIconNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
            if (e.RowIndexHierarchy.First() >= rows.Count)
            {
                e.Value = null;
                return;
            }

            Row baseRow = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            if (baseRow is SpectrumSourceGroupRow) e.Value = Properties.Resources.XPfolder_closed;
            else if (baseRow is SpectrumSourceRow) e.Value = Properties.Resources.file;
            else if (baseRow is SpectrumRow) e.Value = Properties.Resources.SpectrumIcon;
            else if (baseRow is PeptideSpectrumMatchRow) e.Value = Properties.Resources.PSMIcon;
            else if (baseRow is PeptideRow) e.Value = Properties.Resources.Peptide;
        }

        private int getChildRowCount (AggregateRow row, Grouping<GroupBy> childGrouping)
        {
            if (childGrouping == null)
                return row.PeptideSpectrumMatches;
            else if (childGrouping.Mode == GroupBy.Source)
            {
                var dataFilter = row.DataFilter;
                if (dataFilter.SpectrumSourceGroup == null)
                {
                    // create a filter from the cached root group for this data filter
                    var nonGroupParentFilterKey = new DataFilterKey(dataFilter);
                    var rootGroup = (getSpectrumSourceRows(dataFilter)[0] as SpectrumSourceGroupRow).SpectrumSourceGroup;
                    dataFilter = new DataFilter(row.DataFilter) { SpectrumSourceGroup = new List<SpectrumSourceGroup>() { rootGroup } };
                }
                return getSpectrumSourceRows(dataFilter).Count;
            }
            else if (childGrouping.Mode == GroupBy.Spectrum)
                return row.Spectra;
            else if (childGrouping.Mode == GroupBy.Analysis)
                return row.DistinctAnalyses;
            else if (childGrouping.Mode == GroupBy.Peptide)
                return row.DistinctPeptides;
            else if (childGrouping.Mode == GroupBy.Charge)
                return row.DistinctCharges;
            else
                throw new NotImplementedException();
        }

        private void treeDataGridView_CellValueNeeded (object sender, TreeDataGridViewCellValueEventArgs e)
        {
            if (e.RowIndexHierarchy.First() >= rows.Count)
            {
                e.Value = null;
                return;
            }

            Row baseRow = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            Grouping<GroupBy> childGrouping = null;

            if (baseRow is SpectrumSourceGroupRow)
            {
                var row = baseRow as SpectrumSourceGroupRow;
                var nonGroupParentFilterKey = new DataFilterKey(new DataFilter(row.DataFilter) { SpectrumSourceGroup = null });

                var cachedRowsBySource = rowsBySource[nonGroupParentFilterKey];
                e.ChildRowCount = cachedRowsBySource.Where(o => o is SpectrumSourceGroupRow)
                                                    .Select(o => o as SpectrumSourceGroupRow)
                                                    .Count(o => o.SpectrumSourceGroup.IsImmediateChildOf(row.SpectrumSourceGroup));
                e.ChildRowCount += cachedRowsBySource.Where(o => o is SpectrumSourceRow)
                                                     .Select(o => o as SpectrumSourceRow)
                                                     .Count(o => o.SpectrumSource.Group == row.SpectrumSourceGroup);

                if (e.ChildRowCount == 0)
                    throw new InvalidDataException("no child rows for source group");
            }
            else if (baseRow is SpectrumSourceRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Source);
            else if (baseRow is SpectrumRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Spectrum);
            else if (baseRow is AnalysisRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Analysis);
            else if (baseRow is PeptideRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Peptide);
            else if (baseRow is ChargeRow)
                childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Charge);

            if (e.ChildRowCount == 0 && baseRow is AggregateRow)
                e.ChildRowCount = getChildRowCount(baseRow as AggregateRow, childGrouping);

            e.Value = getCellValue(e.ColumnIndex, baseRow);
        }

        protected override object getCellValue (int columnIndex, Row baseRow)
        {
            if (baseRow is SpectrumSourceGroupRow)
            {
                var row = baseRow as SpectrumSourceGroupRow;
                if (columnIndex == keyColumn.Index) return Path.GetFileName(row.SpectrumSourceGroup.Name) ?? "/";
            }
            else if (baseRow is SpectrumSourceRow)
            {
                var row = baseRow as SpectrumSourceRow;
                if (columnIndex == keyColumn.Index) return row.SpectrumSource.Name;
            }
            else if (baseRow is SpectrumRow)
            {
                var row = baseRow as SpectrumRow;
                if (columnIndex == keyColumn.Index) return row.Key;
                else if (columnIndex == precursorMzColumn.Index) return row.Spectrum.PrecursorMZ;
            }
            else if (baseRow is AnalysisRow)
            {
                var row = baseRow as AnalysisRow;
                if (columnIndex == keyColumn.Index) return String.Format("{0} {1} {2}", row.Analysis.Id, row.Analysis.Software.Name, row.Analysis.Software.Version);
            }
            else if (baseRow is PeptideRow)
            {
                var row = baseRow as PeptideRow;
                if (columnIndex == keyColumn.Index) return row.Peptide.Sequence;
            }
            else if (baseRow is ChargeRow)
            {
                var row = baseRow as ChargeRow;
                if (columnIndex == keyColumn.Index) return row.Charge;
            }
            else if (baseRow is PeptideSpectrumMatchRow)
            {
                var row = baseRow as PeptideSpectrumMatchRow;
                var psm = row.PeptideSpectrumMatch;
                if (columnIndex == keyColumn.Index) return row.Key;
                else if (columnIndex == observedMassColumn.Index) return psm.Spectrum.PrecursorMZ * psm.Charge - psm.Charge * pwiz.CLI.chemistry.Proton.Mass;
                else if (columnIndex == exactMassColumn.Index) return psm.MonoisotopicMassError < psm.MolecularWeightError ? psm.MonoisotopicMass : psm.MolecularWeight;
                else if (columnIndex == massErrorColumn.Index) return psm.MonoisotopicMassError < psm.MolecularWeightError ? psm.MonoisotopicMassError : psm.MolecularWeightError;
                else if (columnIndex == analysisColumn.Index) return String.Format("{0} {1} {2}", psm.Analysis.Id, psm.Analysis.Software.Name, psm.Analysis.Software.Version);
                else if (columnIndex == chargeColumn.Index) return psm.Charge;
                else if (columnIndex == qvalueColumn.Index) return psm.QValue;
                else if (columnIndex == sequenceColumn.Index) return psm.ToModifiedString();
                else if (checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) == 0)
                {
                    if (columnIndex == precursorMzColumn.Index) return row.Spectrum.PrecursorMZ;
                }
            }
            else if (baseRow is PeptideSpectrumMatchScoreRow)
            {
                var row = baseRow as PeptideSpectrumMatchScoreRow;
                if (columnIndex == keyColumn.Index) return String.Format("{0} = {1}", row.Name, row.Value);
            }

            if (baseRow is AggregateRow)
            {
                var row = baseRow as AggregateRow;
                if (columnIndex == filteredSpectraColumn.Index) return row.Spectra;
                else if (columnIndex == distinctMatchesColumn.Index) return row.DistinctMatches;
                else if (columnIndex == distinctPeptidesColumn.Index) return row.DistinctPeptides;
                else if (columnIndex == distinctProteinsColumn.Index) return row.DistinctProteins;
                else if (columnIndex == distinctChargesColumn.Index) return row.DistinctCharges;
                else if (columnIndex == distinctAnalysesColumn.Index) return row.DistinctAnalyses;
            }

            return null;
        }

        protected override RowFilterState getRowFilterState (Row parentRow)
        {
            bool result = false;
            if (parentRow is SpectrumSourceGroupRow)
            {
                if (viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Contains((parentRow as SpectrumSourceGroupRow).SpectrumSourceGroup);
            }
            else if (parentRow is SpectrumSourceRow)
            {
                if (viewFilter.SpectrumSource != null) result = viewFilter.SpectrumSource.Contains((parentRow as SpectrumSourceRow).SpectrumSource);
                if (!result && viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Intersect((parentRow as SpectrumSourceRow).SpectrumSource.Groups.Select(o => o.Group)).Any();
            }
            else if (parentRow is SpectrumRow)
            {
                var row = parentRow as SpectrumRow;
                if (viewFilter.Spectrum != null) result = viewFilter.Spectrum.Contains(row.Spectrum);
                if (viewFilter.SpectrumSource != null) result = viewFilter.SpectrumSource.Contains(row.Spectrum.Source);
                if (!result && viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Intersect(row.Spectrum.Source.Groups.Select(o => o.Group)).Any();
                result = result || viewFilter.SpectrumSourceGroup == null && viewFilter.SpectrumSource == null && viewFilter.Spectrum == null;
            }
            else if (parentRow is PeptideSpectrumMatchRow)
            {
                var row = parentRow as PeptideSpectrumMatchRow;
                if (viewFilter.Spectrum != null) result = viewFilter.Spectrum.Contains(row.Spectrum);
                if (viewFilter.SpectrumSource != null) result = viewFilter.SpectrumSource.Contains(row.Spectrum.Source);
                if (!result && viewFilter.SpectrumSourceGroup != null) result = viewFilter.SpectrumSourceGroup.Intersect(row.Spectrum.Source.Groups.Select(o => o.Group)).Any();
                result = result || viewFilter.SpectrumSourceGroup == null && viewFilter.SpectrumSource == null && viewFilter.Spectrum == null;
            }
            else if (parentRow is AnalysisRow)
            {
                if (viewFilter.Analysis != null) result = viewFilter.Analysis.Contains((parentRow as AnalysisRow).Analysis);
            }
            else if (parentRow is ChargeRow)
            {
                if (viewFilter.Charge != null) result = viewFilter.Charge.Contains((parentRow as ChargeRow).Charge);
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

            if (viewFilter.SpectrumSourceGroup == null &&
                viewFilter.SpectrumSource == null &&
                viewFilter.Spectrum == null &&
                viewFilter.Analysis == null &&
                viewFilter.Charge == null)
                return;

            Row row = GetRowFromRowHierarchy(e.RowIndexHierarchy);

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
            {
                Sort(e.ColumnIndex);

                // expand the root group automatically
                var rootGrouping = checkedGroupings.FirstOrDefault();
                if (rootGrouping != null && rootGrouping.Mode == GroupBy.Source)
                    treeDataGridView.Expand(0);
            }
        }

        private void SetDefaults()
        {
            _columnSettings = new Dictionary<DataGridViewColumn, ColumnProperty>()
            {
                { keyColumn, new ColumnProperty() {Type = typeof(string)}},
                { distinctPeptidesColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctMatchesColumn, new ColumnProperty() {Type = typeof(int)}},
                { filteredSpectraColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctAnalysesColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctChargesColumn, new ColumnProperty() {Type = typeof(int)}},
                { distinctProteinsColumn, new ColumnProperty() {Type = typeof(int)}},
                { precursorMzColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { observedMassColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { exactMassColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { massErrorColumn, new ColumnProperty() {Type = typeof(float), Precision = 4}},
                { analysisColumn, new ColumnProperty() {Type = typeof(int)}},
                { chargeColumn, new ColumnProperty() {Type = typeof(int)}},
                { qvalueColumn, new ColumnProperty() {Type = typeof(float), Precision = 2}},
                { sequenceColumn, new ColumnProperty() {Type = typeof(string)}}
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
                {distinctPeptidesColumn.Index, SortOrder.Descending},
                {distinctMatchesColumn.Index, SortOrder.Descending},
                {filteredSpectraColumn.Index, SortOrder.Descending},
                {distinctAnalysesColumn.Index, SortOrder.Descending},
                {distinctChargesColumn.Index, SortOrder.Descending},
                {distinctProteinsColumn.Index, SortOrder.Descending},
                {precursorMzColumn.Index, SortOrder.Ascending},
                {observedMassColumn.Index, SortOrder.Ascending},
                {exactMassColumn.Index, SortOrder.Ascending},
                {massErrorColumn.Index, SortOrder.Ascending},
                {analysisColumn.Index, SortOrder.Ascending},
                {chargeColumn.Index, SortOrder.Ascending},
                {qvalueColumn.Index, SortOrder.Ascending},
                {sequenceColumn.Index, SortOrder.Ascending},
            };
        }

        void treeDataGridView_CellDoubleClick (object sender, TreeDataGridViewCellEventArgs e)
        {
            if (e.ColumnIndex < 0 || e.RowIndexHierarchy.First() < 0)
                return;

            Row row = GetRowFromRowHierarchy(e.RowIndexHierarchy);

            var newDataFilter = new DataFilter() { FilterSource = this };

            if (row is SpectrumSourceGroupRow)
                newDataFilter.SpectrumSourceGroup = new List<SpectrumSourceGroup>() { (row as SpectrumSourceGroupRow).SpectrumSourceGroup };
            else if (row is SpectrumSourceRow)
                newDataFilter.SpectrumSource = new List<SpectrumSource>() { (row as SpectrumSourceRow).SpectrumSource };
            else if (row is SpectrumRow)
                newDataFilter.Spectrum = new List<Spectrum>() { (row as SpectrumRow).Spectrum };
            else if (row is AnalysisRow)
                newDataFilter.Analysis = new List<Analysis>() { (row as AnalysisRow).Analysis };
            else if (row is PeptideRow)
                newDataFilter.Peptide = new List<Peptide>() { (row as PeptideRow).Peptide };
            else if (row is ChargeRow)
                newDataFilter.Charge = new List<int>() { (row as ChargeRow).Charge };
            else if (row is PeptideSpectrumMatchRow)
            {
                if (SpectrumViewVisualize != null)
                    SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs()
                    {
                        PeptideSpectrumMatch = (row as PeptideSpectrumMatchRow). PeptideSpectrumMatch
                    });
                return;
            }

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
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
            var selectedSourceGroups = new List<SpectrumSourceGroup>();
            var selectedSources = new List<SpectrumSource>();
            var selectedSpectra = new List<Spectrum>();
            var selectedAnalyses = new List<Analysis>();
            var selectedPeptides = new List<Peptide>();
            var selectedCharges = new List<int>();
            var selectedMatches = new List<PeptideSpectrumMatch>();

            foreach (DataGridViewCell cell in treeDataGridView.SelectedCells)
            {
                if (!processedRows.Insert(cell.RowIndex).WasInserted)
                    continue;

                var rowIndexHierarchy = treeDataGridView.GetRowHierarchyForRowIndex(cell.RowIndex);
                Row row = GetRowFromRowHierarchy(rowIndexHierarchy);

                if (row is SpectrumSourceGroupRow)
                    selectedSourceGroups.Add((row as SpectrumSourceGroupRow).SpectrumSourceGroup);
                else if (row is SpectrumSourceRow)
                    selectedSources.Add((row as SpectrumSourceRow).SpectrumSource);
                else if (row is SpectrumRow)
                    selectedSpectra.Add((row as SpectrumRow).Spectrum);
                else if (row is AnalysisRow)
                    selectedAnalyses.Add((row as AnalysisRow).Analysis);
                else if (row is PeptideRow)
                    selectedPeptides.Add((row as PeptideRow).Peptide);
                else if (row is ChargeRow)
                    selectedCharges.Add((row as ChargeRow).Charge);
                else if (row is PeptideSpectrumMatchRow)
                    selectedMatches.Add((row as PeptideSpectrumMatchRow).PeptideSpectrumMatch);
            }

            if (selectedSourceGroups.Count > 0) newDataFilter.SpectrumSourceGroup = selectedSourceGroups;
            if (selectedSources.Count > 0) newDataFilter.SpectrumSource = selectedSources;
            if (selectedSpectra.Count > 0) newDataFilter.Spectrum = selectedSpectra;
            if (selectedAnalyses.Count > 0) newDataFilter.Analysis = selectedAnalyses;
            if (selectedPeptides.Count > 0) newDataFilter.Peptide = selectedPeptides;
            if (selectedCharges.Count > 0) newDataFilter.Charge = selectedCharges;

            // TODO: visualize multiple PSMs?
            //if (selectedMatches.Count > 0)

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
        }

        protected override bool updatePivots (FormProperty formProperty)
        {
            checkedPivots = new List<Pivot<PivotBy>>();
            return false;
        }

        protected override bool updateGroupings (FormProperty formProperty)
        {
            bool groupingChanged = false;
            if (groupingSetupControl != null && formProperty.GroupingModes != null)
                groupingChanged = base.updateGroupings(formProperty);
            else
                setGroupings(new Grouping<GroupBy>(true) { Mode = GroupBy.Source, Text = "Group/Source" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Spectrum, Text = "Spectrum" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Analysis, Text = "Analysis" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Charge, Text = "Charge" },
                             new Grouping<GroupBy>() { Mode = GroupBy.Peptide, Text = "Peptide" });

            groupingSetupControl.GroupingChanging += groupingSetupControl_GroupingChanging;

            if (groupingChanged)
                setColumnVisibility();

            return groupingChanged;
        }

        public override void ClearData ()
        {
            Text = TabText = "Spectrum View";

            treeDataGridView.RootRowCount = 0;
            Refresh();
        }

        public override void ClearData (bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        public override void SetData(NHibernate.ISession session, DataFilter dataFilter)
        {
            if (session == null)
                return;

            this.session = session;
            viewFilter = dataFilter;
            this.dataFilter = new DataFilter(dataFilter) { Spectrum = null, SpectrumSource = null, SpectrumSourceGroup = null };

            /*oldSelectionPath = new List<string>();
            for (int x = 0; x < treeListView.Items.Count; x++)
            {
                if (treeListView.IsExpanded(treeListView.GetModelObject(x)))
                    oldSelectionPath.Add(treeListView.Items[x].Text);
            }
            oldSelectionPath.Add(treeListView.SelectedItem == null
                                     ? "<<No Item Selected>>"
                                     : treeListView.SelectedItem.Text);*/

            /*if (treeListView.SelectedObject is SpectrumSourceGroupRow)
            {
                oldSelectionPath = getGroupTreePath((treeListView.SelectedObject as SpectrumSourceGroupRow).SpectrumSourceGroup);
            }
            else if (treeListView.SelectedObject is SpectrumSourceRow)
            {
                var source = (treeListView.SelectedObject as SpectrumSourceRow).SpectrumSource;
                oldSelectionPath = getGroupTreePath(source.Group);
                oldSelectionPath.Add(source.Name);
            }
            else if (treeListView.SelectedObject is SpectrumRow)
            {
                var spectrum = (treeListView.SelectedObject as SpectrumRow).Spectrum;
                oldSelectionPath = getGroupTreePath(spectrum.Source.Group);
                oldSelectionPath.Add(spectrum.Source.Name);
                oldSelectionPath.Add(treeListView.SelectedItem.Text);
            }*/

            ClearData();

            Text = TabText = "Loading spectrum view...";

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        void setData(object sender, DoWorkEventArgs e)
        {
            try
            {
                var rootGrouping = checkedGroupings.FirstOrDefault();

                if (dataFilter.IsBasicFilter)
                {
                    if (basicDataFilter == null || (viewFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = dataFilter;
                        basicTotalCounts = new TotalCounts(session, dataFilter);

                        rowsBySource = new Dictionary<DataFilterKey, List<Row>>();
                        basicRows = getChildren(rootGrouping, dataFilter);
                        basicRowsBySource = rowsBySource;
                    }

                    totalCounts = basicTotalCounts;
                    rowsBySource = basicRowsBySource;
                    rows = basicRows;
                }
                else
                {
                    totalCounts = new TotalCounts(session, dataFilter);
                    rowsBySource = new Dictionary<DataFilterKey, List<Row>>();
                    rows = getChildren(rootGrouping, dataFilter);
                }

                applySort();
            }
            catch (Exception ex)
            {
                e.Result = ex;
            }
        }

        void renderData(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Result is Exception)
                Program.HandleException(e.Result as Exception);

            treeDataGridView.RootRowCount = rows.Count();

            // show total counts in the form title
            Text = TabText = String.Format("Spectrum View: {0} groups, {1} sources, {2} spectra",
                                           totalCounts.Groups,
                                           totalCounts.Sources,
                                           totalCounts.Spectra);

            // try to (re)set selected item
            expandSelectionPath(oldSelectionPath);

            // expand the root group automatically
            var rootGrouping = checkedGroupings.FirstOrDefault();
            if (rootGrouping != null && rootGrouping.Mode == GroupBy.Source && rows.Count > 0)
                treeDataGridView.Expand(0);
            else
                treeDataGridView.Refresh();
        }

        private List<string> getGroupTreePath(DataModel.SpectrumSourceGroup group)
        {
            var result = new List<string>();
            string groupPath = group.Name;
            while (!String.IsNullOrEmpty(Path.GetDirectoryName(groupPath)))
            {
                result.Add(Path.GetFileName(groupPath) + '/');
                groupPath = Path.GetDirectoryName(groupPath);
            }
            result.Add("/");
            result.Reverse();
            return result;
        }

        private void expandSelectionPath(IEnumerable<string> selectionPath)
        {
            /*OLVListItem selectedItem = null;
            foreach (string branch in selectionPath)
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
        }

        private void editGroupsButton_Click(object sender, EventArgs e)
        {
            if (session != null)
            {
                var gcf = new GroupingControlForm(session.SessionFactory);

                if (gcf.ShowDialog() == DialogResult.OK)
                    (this.ParentForm as IDPickerForm).ApplyBasicFilter();
                //TODO- Find a better way of doing this
            }

        }

        private void groupingSetupControl_GroupingChanging(object sender, GroupingChangingEventArgs<GroupBy> e)
        {
            // GroupBy.Spectrum cannot be before GroupBy.Source

            if (e.Grouping.Mode != GroupBy.Spectrum && e.Grouping.Mode != GroupBy.Source)
                return;

            var newGroupings = new List<Grouping<GroupBy>>(groupingSetupControl.Groupings);
            newGroupings[newGroupings.IndexOf(e.Grouping)] = newGroupings.First(o => o.Mode == GroupBy.Analysis);
            newGroupings.Insert(e.NewIndex, e.Grouping);

            e.Cancel = GroupingSetupControl<GroupBy>.HasParentGrouping(newGroupings, GroupBy.Source, GroupBy.Spectrum);
        }

        protected override void setColumnVisibility ()
        {
            var keys = new List<string>();
            foreach (var grouping in checkedGroupings)
                keys.Add(grouping.Text);

            if (checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) > 0)
                keys.Add("Rank");
            else
                keys.Add("Spectrum");

            keyColumn.HeaderText = String.Join("/", keys.ToArray());

            var columnsIrrelevantForGrouping = new Set<DataGridViewColumn>(new Comparison<DataGridViewColumn>((x, y) => x.Name.CompareTo(y.Name)));

            if (session != null && session.IsOpen)
                lock (session)
                    if (session.Query<Analysis>().Count() == 1)
                    {
                        columnsIrrelevantForGrouping.Add(analysisColumn);
                        columnsIrrelevantForGrouping.Add(distinctAnalysesColumn);
                    }

            if (checkedGroupings.IsNullOrEmpty())
                aggregateColumns.ForEach(o => columnsIrrelevantForGrouping.Add(o));
            else if (checkedGroupings.First().Mode == GroupBy.Spectrum)
                columnsIrrelevantForGrouping.Add(filteredSpectraColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Peptide)
                columnsIrrelevantForGrouping.Add(distinctPeptidesColumn);
            else if (checkedGroupings.First().Mode == GroupBy.Analysis)
            {
                columnsIrrelevantForGrouping.Add(analysisColumn);
                columnsIrrelevantForGrouping.Add(distinctAnalysesColumn);
            }
            else if (checkedGroupings.First().Mode == GroupBy.Charge)
            {
                columnsIrrelevantForGrouping.Add(chargeColumn);
                columnsIrrelevantForGrouping.Add(distinctChargesColumn);
            }

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
    }

    public class SpectrumViewVisualizeEventArgs : EventArgs
    {
        public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; internal set; }
    }
}
