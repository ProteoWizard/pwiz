//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
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

namespace IDPicker.Forms
{
    public partial class SpectrumTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public enum GroupBy { Source, Spectrum, Analysis, Charge, Peptide, Off }
        public GroupingSetupControl<GroupBy> GroupingSetupControl { get { return groupingSetupControl; } }
        private GroupingSetupControl<GroupBy> groupingSetupControl;
        private Popup groupingSetupPopup;

        public event EventHandler<DataFilter> SpectrumViewFilter;
        public event EventHandler<SpectrumViewVisualizeEventArgs> SpectrumViewVisualize;

        private NHibernate.ISession session = null;

        private DataFilter userFilter; // what the user has filtered on
        private DataFilter dataFilter; // how this view is filtered (i.e. never on its own rows)
        private DataFilter basicDataFilter; // the basic filter without the user filtering on rows

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
                var total = session.CreateQuery("SELECT " +
                                                "COUNT(DISTINCT psm.Spectrum.Source.Group), " +
                                                "COUNT(DISTINCT psm.Spectrum.Source), " +
                                                "COUNT(DISTINCT psm.Spectrum), " +
                                                "COUNT(DISTINCT psm.Charge), " +
                                                "COUNT(DISTINCT psm.Analysis) " +
                                                dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch))
                                   .List<object[]>()[0];

                Groups = Convert.ToInt32(total[0]);
                Sources = Convert.ToInt32(total[1]);
                Spectra = Convert.ToInt64(total[2]);
                Charges = Convert.ToInt32(total[3]);
                Analyses = Convert.ToInt32(total[4]);
            }
            #endregion
        }

        private TotalCounts totalCounts;

        private IEnumerable<AggregateRow> aggregateRows;
        private Dictionary<GroupBy, IEnumerable<AggregateRow>> basicAggregateRowsByType;
        private IEnumerable<PeptideSpectrumMatchRow> rowsByPSM, basicRowsByPSM;

        private IList<Grouping<GroupBy>> checkedGroupings;
        private bool dirtyGroupings = false;

        // TODO: support multiple selected objects
        List<string> oldSelectionPath = new List<string>();

        #region Wrapper classes for encapsulating query results

        public class Row
        {
            public DataFilter DataFilter { get; protected set; }
        }

        public class AggregateRow : Row
        {
            public long Spectra { get; private set; }
            public int DistinctMatches { get; private set; }
            public int DistinctPeptides { get; private set; }
            public int DistinctAnalyses { get; private set; }
            public int DistinctCharges { get; private set; }

            public static string Selection = "SELECT " +
                                             "COUNT(DISTINCT psm.Spectrum), " +
                                             "COUNT(DISTINCT psm.SequenceAndMassDistinctKey), " +
                                             "COUNT(DISTINCT psm.Peptide), " +
                                             "COUNT(DISTINCT psm.Analysis), " +
                                             "COUNT(DISTINCT psm.Charge)";

            #region Constructor
            public AggregateRow (object[] queryRow, DataFilter dataFilter)
            {
                Spectra = (long) queryRow[0];
                DistinctMatches = (int) (long) queryRow[1];
                DistinctPeptides = (int) (long) queryRow[2];
                DistinctAnalyses = (int) (long) queryRow[3];
                DistinctCharges = (int) (long) queryRow[4];
                DataFilter = dataFilter;
            }
            #endregion
        }

        public class SpectrumSourceGroupRow : AggregateRow
        {
            public DataModel.SpectrumSourceGroup SpectrumSourceGroup { get; private set; }

            #region Constructor
            public SpectrumSourceGroupRow (object[] queryRow, DataFilter dataFilter) : base(queryRow, dataFilter)
            {
                SpectrumSourceGroup = (queryRow[5] as DataModel.SpectrumSourceGroupLink).Group;
            }
            #endregion
        }

        public class SpectrumSourceRow : AggregateRow
        {
            public DataModel.SpectrumSource SpectrumSource { get; private set; }

            #region Constructor
            public SpectrumSourceRow (object[] queryRow, DataFilter dataFilter) : base(queryRow, dataFilter)
            {
                SpectrumSource = (DataModel.SpectrumSource) queryRow[5];
            }
            #endregion
        }

        public class AnalysisRow : AggregateRow
        {
            public DataModel.Analysis Analysis { get; private set; }
            public string Key { get; private set; }

            #region Constructor
            public AnalysisRow (object[] queryRow, DataFilter dataFilter) : base(queryRow, dataFilter)
            {
                Analysis = (DataModel.Analysis) queryRow[5];

                Key = String.Format("{0} {1} {2}", Analysis.Id, Analysis.Software.Name, Analysis.Software.Version);
            }
            #endregion
        }

        public class ChargeRow : AggregateRow
        {
            public int Charge { get; private set; }

            #region Constructor
            public ChargeRow (object[] queryRow, DataFilter dataFilter) : base(queryRow, dataFilter)
            {
                Charge = (int) queryRow[5];
            }
            #endregion
        }

        public class PeptideRow : AggregateRow
        {
            public DataModel.Peptide Peptide { get; private set; }

            #region Constructor
            public PeptideRow (object[] queryRow, DataFilter dataFilter) : base(queryRow, dataFilter)
            {
                Peptide = (DataModel.Peptide) queryRow[5];
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
            public SpectrumRow (object[] queryRow, DataFilter dataFilter, IList<Grouping<GroupBy>> checkedGroupings)
                : base(queryRow, dataFilter)
            {
                Spectrum = (DataModel.Spectrum) queryRow[5];
                Source = (DataModel.SpectrumSource) queryRow[6];
                Group = (DataModel.SpectrumSourceGroup) queryRow[7];

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
            public PeptideSpectrumMatchRow (object[] queryRow, DataFilter dataFilter, IList<Grouping<GroupBy>> checkedGroupings)
            {
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch) queryRow[0];
                Spectrum = (DataModel.Spectrum) queryRow[3];
                Source = (DataModel.SpectrumSource) queryRow[4];
                Group = (DataModel.SpectrumSourceGroup) queryRow[5];
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
        #endregion

        public SpectrumTableForm ()
        {
            InitializeComponent();

            HideOnClose = true;

            basicAggregateRowsByType = new Dictionary<GroupBy, IEnumerable<AggregateRow>>();
        }

        protected override void OnLoad (EventArgs e)
        {
            Text = TabText = "Spectrum View";

            var groupings = new List<Grouping<GroupBy>>();
            //groupings.Add(new Grouping<GroupBy>() { Mode = (int) GroupBy.SourceGroup, Text = "Source Group" });
            groupings.Add(new Grouping<GroupBy>(true) { Mode = GroupBy.Source, Text = "Group/Source" });
            groupings.Add(new Grouping<GroupBy>(true) { Mode = GroupBy.Spectrum, Text = "Spectrum" });
            groupings.Add(new Grouping<GroupBy>() { Mode = GroupBy.Analysis, Text = "Analysis" });
            groupings.Add(new Grouping<GroupBy>() { Mode = GroupBy.Peptide, Text = "Peptide" });
            groupings.Add(new Grouping<GroupBy>() { Mode = GroupBy.Charge, Text = "Charge" });

            //groupMode = GroupBy.SourceGroup;

            groupingSetupControl = new GroupingSetupControl<GroupBy>(groupings);
            groupingSetupControl.GroupingChanged += new EventHandler(groupingSetupControl_GroupingChanged);
            groupingSetupControl.GroupingChanging += new EventHandler<GroupingChangingEventArgs<GroupBy>>(groupingSetupControl_GroupingChanging);
            groupingSetupPopup = new Popup(groupingSetupControl) { FocusOnOpen = true };
            groupingSetupPopup.Closed += new ToolStripDropDownClosedEventHandler(groupingSetupPopup_Closed);

            #region Column aspect getters
            keyColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return Path.GetFileName((x as SpectrumSourceGroupRow).SpectrumSourceGroup.Name) + '/';
                else if (x is SpectrumSourceRow)
                    return (x as SpectrumSourceRow).SpectrumSource.Name;
                else if (x is AnalysisRow)
                    return (x as AnalysisRow).Key;
                else if (x is PeptideRow)
                    return (x as PeptideRow).Peptide.Sequence;
                else if (x is ChargeRow)
                    return (x as ChargeRow).Charge;
                else if (x is SpectrumRow)
                    return (x as SpectrumRow).Key;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).Key;
                else if (x is PeptideSpectrumMatchScoreRow)
                    return String.Format("{0}: {1}", (x as PeptideSpectrumMatchScoreRow).Name, (x as PeptideSpectrumMatchScoreRow).Value);
                return null;
            };

            totalSpectraColumn.AspectGetter += delegate(object x)
            {
                return null;
            };

            spectraColumn.AspectGetter += delegate(object x)
            {
                if (x is AggregateRow)
                    return (x as AggregateRow).Spectra;
                return null;
            };

            distinctMatchesColumn.AspectGetter += delegate(object x)
            {
                if (x is AggregateRow)
                    return (x as AggregateRow).DistinctMatches;
                return null;
            };

            distinctPeptidesColumn.AspectGetter += delegate(object x)
            {
                if (x is AggregateRow)
                    return (x as AggregateRow).DistinctPeptides;
                return null;
            };

            distinctAnalysesColumn.AspectGetter += delegate(object x)
            {
                if (x is AggregateRow)
                    return (x as AggregateRow).DistinctAnalyses;
                return null;
            };

            distinctChargesColumn.AspectGetter += delegate(object x)
            {
                if (x is AggregateRow)
                    return (x as AggregateRow).DistinctCharges;
                return null;
            };

            analysisColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                {
                    var analysis = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Analysis;
                    return String.Format("{0} {1} {2}", analysis.Id, analysis.Software.Name, analysis.Software.Version);
                }
                return null;
            };

            precursorMzColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumRow)
                    return (x as SpectrumRow).Spectrum.PrecursorMZ;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).Spectrum.PrecursorMZ;
                return null;
            };

            chargeColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Charge;
                return null;
            };

            observedMassColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                {
                    var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                    return psm.Spectrum.PrecursorMZ * psm.Charge - psm.Charge * pwiz.CLI.chemistry.Proton.Mass;
                }
                return null;
            };

            exactMassColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass;
                return null;
            };

            massErrorColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMassError;
                return null;
            };

            qvalueColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                {
                    var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                    return psm.Rank > 1 ? "n/a" : psm.QValue.ToString();
                }
                return null;
            };

            sequenceColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.ToModifiedString();
                return null;
            };
            #endregion

            treeListView.CanExpandGetter += delegate(object x) { return !(x is PeptideSpectrumMatchScoreRow); };
            treeListView.ChildrenGetter += new TreeListView.ChildrenGetterDelegate(getChildren);
            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);
            treeListView.AfterExpanding += new EventHandler<AfterExpandingEventArgs>(treeListView_AfterExpanding);
            treeListView.AfterCollapsing += new EventHandler<AfterCollapsingEventArgs>(treeListView_AfterCollapsing);
        }

        #region Set column visibility
        void treeListView_AfterExpanding (object sender, AfterExpandingEventArgs e)
        {
            treeListView_setColumnVisibility();
        }

        void treeListView_AfterCollapsing (object sender, AfterCollapsingEventArgs e)
        {
            treeListView_setColumnVisibility();
        }

        void treeListView_setColumnVisibility ()
        {
            object deepestRowObject = null;
            int deepestIndentCount = -1;
            for (int i = 0; i < treeListView.Items.Count; ++i)
            {
                var item = treeListView.Items[i] as OLVListItem;

                if (item.IndentCount > deepestIndentCount)
                {
                    deepestIndentCount = item.IndentCount;
                    deepestRowObject = item.RowObject;
                }

                // break iteration once maximum depth is reached
                if (deepestRowObject is PeptideSpectrumMatchRow)
                    break;
            }

            bool showAggregateColumns = deepestRowObject is AggregateRow;
            bool showSpectrumColumns = deepestRowObject is SpectrumRow ||
                                       (deepestRowObject is PeptideSpectrumMatchRow &&
                                        checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) == 0);
            bool showPsmColumns = deepestRowObject is PeptideSpectrumMatchRow;

            var keys = new List<string>();
            foreach (var grouping in checkedGroupings)
                keys.Add(grouping.Text);

            if (showPsmColumns)
            {
                if (checkedGroupings.Count(o => o.Mode == GroupBy.Spectrum) > 0)
                    keys.Add("Rank");
                else
                    keys.Add("Spectrum");
            }

            keyColumn.Text = String.Join("/", keys.ToArray());

            GroupBy deepestGroupingMode = GroupBy.Off;
            if (deepestRowObject is SpectrumSourceGroupRow || deepestRowObject is SpectrumSourceRow)
                deepestGroupingMode = GroupBy.Source;
            else if (deepestRowObject is SpectrumRow)
                deepestGroupingMode = GroupBy.Spectrum;
            else if (deepestRowObject is PeptideRow)
                deepestGroupingMode = GroupBy.Peptide;
            else if (deepestRowObject is AnalysisRow)
                deepestGroupingMode = GroupBy.Analysis;
            else if (deepestRowObject is ChargeRow)
                deepestGroupingMode = GroupBy.Charge;

            totalSpectraColumn.IsVisible = false;// showAggregateColumns;
            spectraColumn.IsVisible = showAggregateColumns && deepestGroupingMode != GroupBy.Off && !GroupingSetupControl<GroupBy>.HasParentGrouping(checkedGroupings, deepestGroupingMode, GroupBy.Spectrum);
            distinctMatchesColumn.IsVisible = showAggregateColumns && deepestGroupingMode != GroupBy.Off && !GroupingSetupControl<GroupBy>.HasParentGrouping(checkedGroupings, deepestGroupingMode, GroupBy.Spectrum);
            distinctPeptidesColumn.IsVisible = showAggregateColumns && deepestGroupingMode != GroupBy.Off && !GroupingSetupControl<GroupBy>.HasParentGrouping(checkedGroupings, deepestGroupingMode, GroupBy.Peptide);
            distinctAnalysesColumn.IsVisible = showAggregateColumns && deepestGroupingMode != GroupBy.Off && !GroupingSetupControl<GroupBy>.HasParentGrouping(checkedGroupings, deepestGroupingMode, GroupBy.Analysis);
            distinctChargesColumn.IsVisible = showAggregateColumns && deepestGroupingMode != GroupBy.Off && !GroupingSetupControl<GroupBy>.HasParentGrouping(checkedGroupings, deepestGroupingMode, GroupBy.Charge);

            bool hasMultipleAnalyses = session.Query<Analysis>().Count() > 1;

            analysisColumn.IsVisible = hasMultipleAnalyses && showPsmColumns && checkedGroupings.Count(o => o.Mode == GroupBy.Analysis) == 0;
            precursorMzColumn.IsVisible = showSpectrumColumns;
            chargeColumn.IsVisible = showPsmColumns && checkedGroupings.Count(o => o.Mode == GroupBy.Charge) == 0;
            observedMassColumn.IsVisible = showPsmColumns;
            exactMassColumn.IsVisible = showPsmColumns;
            massErrorColumn.IsVisible = showPsmColumns;
            qvalueColumn.IsVisible = showPsmColumns;
            sequenceColumn.IsVisible = showPsmColumns;// && checkedGroupings.Count(o => o.Mode == GroupBy.Peptide) == 0;

            treeListView.RebuildColumns();

            // resize to show the entire key column
            /*System.Drawing.Extensions.SetRedraw(treeListView, false);
            keyColumn.Width = 0;
            keyColumn.AutoResize(ColumnHeaderAutoResizeStyle.HeaderSize);
            keyColumn.AutoResize(ColumnHeaderAutoResizeStyle.ColumnContent);
            keyColumn.Width += deepestIndentCount * TreeListView.TreeRenderer.PIXELS_PER_LEVEL;
            System.Drawing.Extensions.SetRedraw(treeListView, true);*/
        }
        #endregion

        IEnumerable getChildren (object x)
        {
            if (x is SpectrumSourceGroupRow)
            {
                var row = x as SpectrumSourceGroupRow;
                var parentFilter = row.DataFilter == null ? dataFilter : row.DataFilter;
                var childFilter = new DataFilter(parentFilter) {SpectrumSourceGroup = row.SpectrumSourceGroup};
                return getSpectrumSourceRows(childFilter);
            }
            else if (x is SpectrumSourceRow)
            {
                var row = x as SpectrumSourceRow;
                var parentFilter = row.DataFilter == null ? dataFilter : row.DataFilter;
                var childFilter = new DataFilter(parentFilter) {SpectrumSource = row.SpectrumSource};
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Source);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is AnalysisRow)
            {
                var row = x as AnalysisRow;
                var parentFilter = row.DataFilter == null ? dataFilter : row.DataFilter;
                var childFilter = new DataFilter(parentFilter) {Analysis = row.Analysis};
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Analysis);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is PeptideRow)
            {
                var row = x as PeptideRow;
                var parentFilter = row.DataFilter == null ? dataFilter : row.DataFilter;
                var childFilter = new DataFilter(parentFilter) {Peptide = row.Peptide};
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Peptide);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is ChargeRow)
            {
                var row = x as ChargeRow;
                var parentFilter = row.DataFilter == null ? dataFilter : row.DataFilter;
                var childFilter = new DataFilter(parentFilter) {Charge = row.Charge};
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Charge);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is SpectrumRow)
            {
                var row = x as SpectrumRow;
                var parentFilter = row.DataFilter == null ? dataFilter : row.DataFilter;
                var childFilter = new DataFilter(parentFilter) {Spectrum = row.Spectrum};
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Spectrum);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is AggregateRow)
                throw new NotImplementedException();
            else // PeptideSpectrumMatchRow
            {
                return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Scores.Select(o => new PeptideSpectrumMatchScoreRow(o));
            }
        }

        void treeListView_CellClick (object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null ||
                e.HitTest.HitTestLocation == HitTestLocation.ExpandButton)
                return;

            var newDataFilter = new DataFilter()
            {
                MaximumQValue = dataFilter.MaximumQValue,
                FilterSource = this
            };

            if (e.Item.RowObject is SpectrumSourceGroupRow)
                newDataFilter.SpectrumSourceGroup = (e.Item.RowObject as SpectrumSourceGroupRow).SpectrumSourceGroup;
            else if (e.Item.RowObject is SpectrumSourceRow)
                newDataFilter.SpectrumSource = (e.Item.RowObject as SpectrumSourceRow).SpectrumSource;
            else if (e.Item.RowObject is SpectrumRow)
                newDataFilter.Spectrum = (e.Item.RowObject as SpectrumRow).Spectrum;
            else if (e.Item.RowObject is AnalysisRow)
                newDataFilter.Analysis = (e.Item.RowObject as AnalysisRow).Analysis;
            else if (e.Item.RowObject is PeptideRow)
                newDataFilter.Peptide = (e.Item.RowObject as PeptideRow).Peptide;
            else if (e.Item.RowObject is ChargeRow)
                newDataFilter.Charge = (e.Item.RowObject as ChargeRow).Charge;
            else if (e.Item.RowObject is PeptideSpectrumMatchRow)
            {
                if (SpectrumViewVisualize != null)
                    SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs()
                                                {
                                                    PeptideSpectrumMatch = (e.Item.RowObject as PeptideSpectrumMatchRow).PeptideSpectrumMatch
                                                });
                return;
            }

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
        }

        #region getChildren functions for each row type

        // returns both groups and sources
        IEnumerable<AggregateRow> getSpectrumSourceRows (DataFilter parentFilter)
        {
            var groupsFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = null };
            var groups = session.CreateQuery(AggregateRow.Selection + ", ssgl " + 
                                             groupsFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                 DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                             "GROUP BY ssgl.Group.id")
                                .List<object[]>()
                                .Select(o => new SpectrumSourceGroupRow(o, parentFilter));

            if (parentFilter != null)
            {
                if (parentFilter.SpectrumSourceGroup != null)
                    groups = groups.Where(o => o.SpectrumSourceGroup.IsImmediateChildOf(parentFilter.SpectrumSourceGroup));
                else
                    groups = groups.Where(o => o.SpectrumSourceGroup.Name == "/");
            }

            var sources = session.CreateQuery(AggregateRow.Selection + ", psm.Spectrum.Source " + 
                                              parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                              "GROUP BY psm.Spectrum.Source.id")
                                 .List<object[]>()
                                 .Select(o => new SpectrumSourceRow(o, parentFilter));

            if (parentFilter != null)
            {
                if (parentFilter.SpectrumSourceGroup != null)
                    sources = sources.Where(o => o.SpectrumSource.Group.Id == parentFilter.SpectrumSourceGroup.Id);
                else
                    return groups.Cast<AggregateRow>();
            }

            return groups.Cast<AggregateRow>().Concat(sources.Cast<AggregateRow>());

            /*var parentGroup = x.SpectrumSourceGroup;

            var childGroups = from r in aggregateRows.Where(o => o is SpectrumSourceGroupRow).Select(o => o as SpectrumSourceGroupRow)
                              where r.SpectrumSourceGroup.IsImmediateChildOf(parentGroup)
                              select r as object;

            var childSources = from r in aggregateRows.Where(o => o is SpectrumSourceRow).Select(o => o as SpectrumSourceRow)
                               where r.SpectrumSource.Group.Id == parentGroup.Id
                               select r as object;

            return childGroups.Concat(childSources);*/
        }

        IEnumerable<AggregateRow> getAnalysisRows (DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", psm.Analysis " + 
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                       "GROUP BY psm.Analysis.id")
                          .List<object[]>()
                          .Select(o => new AnalysisRow(o, parentFilter) as AggregateRow);
        }

        IEnumerable<AggregateRow> getPeptideRows (DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", psm.Peptide " + 
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                       "GROUP BY psm.Peptide.id")
                          .List<object[]>()
                          .Select(o => new PeptideRow(o, parentFilter) as AggregateRow);
        }

        IEnumerable<AggregateRow> getChargeRows (DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", psm.Charge " + 
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                       "GROUP BY psm.Charge")
                          .List<object[]>()
                          .Select(o => new ChargeRow(o, parentFilter) as AggregateRow);
        }

        IEnumerable<SpectrumRow> getSpectrumRows (DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", s, ss, ssg " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToSpectrumSourceGroup) +
                                       "GROUP BY s.id " +
                                       "ORDER BY s.Index")
                          .List<object[]>()
                          .Select(o => new SpectrumRow(o, parentFilter, checkedGroupings));
        }

        IEnumerable<PeptideSpectrumMatchRow> getPeptideSpectrumMatchRows (DataFilter parentFilter)
        {
            return session.CreateQuery("SELECT DISTINCT psm, pm, mod, s, ss, ssg " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToModification,
                                                                           DataFilter.PeptideSpectrumMatchToSpectrumSourceGroup) +
                                       "GROUP BY psm.id " +
                                       "ORDER BY psm.Analysis, psm.Charge ")
                          .List<object[]>()
                          .Select(o => new PeptideSpectrumMatchRow(o, parentFilter, checkedGroupings));
        }

        IEnumerable getChildren (PeptideSpectrumMatchRow x, DataFilter parentFilter)
        {
            return x.PeptideSpectrumMatch.Scores.Select(o => new PeptideSpectrumMatchScoreRow(o));
        }

        IEnumerable getChildren (Grouping<GroupBy> grouping, DataFilter parentFilter)
        {
            if (grouping == null)
                return getPeptideSpectrumMatchRows(parentFilter);

            switch (grouping.Mode)
            {
                case GroupBy.Source: return getSpectrumSourceRows(parentFilter);
                case GroupBy.Spectrum: return getSpectrumRows(parentFilter);
                case GroupBy.Analysis: return getAnalysisRows(parentFilter);
                case GroupBy.Peptide: return getPeptideRows(parentFilter);
                case GroupBy.Charge: return getChargeRows(parentFilter);
                default: throw new NotImplementedException();
            }
        }
        #endregion

        public void ClearData ()
        {
            Text = TabText = "Spectrum View";

            treeListView.DiscardAllState();
            treeListView.Roots = null;
            treeListView.Refresh();
            Refresh();
        }

        public void SetData (NHibernate.ISession session, DataFilter userFilter)
        {
            if (session == null)
                return;

            this.session = session;
            this.userFilter = userFilter;
            dataFilter = new DataFilter(userFilter) { Spectrum = null, SpectrumSource = null, SpectrumSourceGroup = null };

            checkedGroupings = groupingSetupControl.CheckedGroupings;

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

        void setData (object sender, DoWorkEventArgs e)
        {
            var rootGrouping = checkedGroupings.Count > 0 ? checkedGroupings.First() : null;

            if (dataFilter.IsBasicFilter)
            {
                if (basicDataFilter == null || (userFilter.IsBasicFilter && dataFilter != basicDataFilter))
                {
                    basicDataFilter = dataFilter;

                    lock (session)
                    {
                        totalCounts = new TotalCounts(session, dataFilter);

                        if (rootGrouping != null)
                            basicAggregateRowsByType[rootGrouping.Mode] = getChildren(rootGrouping, dataFilter).Cast<AggregateRow>();
                        else
                            basicRowsByPSM = getPeptideSpectrumMatchRows(dataFilter);
                    }
                }

                if (rootGrouping != null)
                    aggregateRows = basicAggregateRowsByType[rootGrouping.Mode];
                rowsByPSM = basicRowsByPSM;
            }
            else
            {
                lock (session)
                {
                    totalCounts = new TotalCounts(session, dataFilter);

                    if (rootGrouping != null)
                        aggregateRows = getChildren(rootGrouping, dataFilter).Cast<AggregateRow>();
                    else
                        rowsByPSM = getPeptideSpectrumMatchRows(dataFilter);
                }
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            // show total counts in the form title
            Text = TabText = String.Format("Spectrum View: {0} groups, {1} sources, {2} spectra",
                                           totalCounts.Groups,
                                           totalCounts.Sources,
                                           totalCounts.Spectra);

            var rootGrouping = checkedGroupings.Count > 0 ? checkedGroupings.First() : null;

            if (rootGrouping == null)
            {
                treeListView.Roots = rowsByPSM;
            }
            else if (rootGrouping.Mode == GroupBy.Source)
            {
                SpectrumSourceGroup rootGroup;
                lock (session) rootGroup = session.Query<SpectrumSourceGroup>().Where(o => o.Name == "/").Single();
                var rootGroupRow = aggregateRows == null ? null : aggregateRows.Where(o => o is SpectrumSourceGroupRow &&
                                                                                           (o as SpectrumSourceGroupRow).SpectrumSourceGroup.Id == rootGroup.Id).Single();

                if (rootGroupRow != null)
                {
                    treeListView.Roots = new object[] {rootGroupRow};

                    // by default, expand all groups
                    foreach (var row in aggregateRows.Where(o => o is SpectrumSourceGroupRow))
                        treeListView.Expand(row);
                }
            }
            else
            {
                treeListView.Roots = aggregateRows;
            }

            // if the view is filtered, expand all sources
            // TODO: this isn't a good idea when the filter has hundreds/thousands of spectra!
            /*if (!ReferenceEquals(rowsByGroup, basicRowsByGroup))
                foreach (var row in rowsBySource)
                    treeListView.Expand(row);*/

            // try to (re)set selected item
            expandSelectionPath(oldSelectionPath);

            treeListView_setColumnVisibility();

            treeListView.Refresh();
        }

        private List<string> getGroupTreePath (DataModel.SpectrumSourceGroup group)
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

        private void expandSelectionPath (IEnumerable<string> selectionPath)
        {
            OLVListItem selectedItem = null;
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
            }
        }

        private void editGroupsButton_Click(object sender, EventArgs e)
        {
            if (session != null)
            {
                var gcf = new GroupingControlForm(session);

                if (gcf.ShowDialog() == DialogResult.OK)
                {
                    basicDataFilter = null;
                    (this.ParentForm as IDPickerForm).ReloadSession(session);
                }
            }

        }

        #region Export handling
        private void exportButton_Click(object sender, EventArgs e)
        {
            if (treeListView.SelectedIndices.Count > 1)
            {
                exportMenu.Items[0].Text = "Copy Selected to Clipboard";
                exportMenu.Items[1].Text = "Export Selected to File";
                exportMenu.Items[2].Text = "Show Selected in Excel";
            }
             else
            {
                exportMenu.Items[0].Text = "Copy to Clipboard";
                exportMenu.Items[1].Text = "Export to File";
                exportMenu.Items[2].Text = "Show in Excel";
            }

            exportMenu.Show(Cursor.Position);
        }

        private List<List<string>> getFormTable()
        {
            var table = new List<List<string>>();
            var row = new List<string>();
            int numColumns;

            //get column names
            foreach (var column in treeListView.ColumnsInDisplayOrder)
                row.Add(column.Text);

            table.Add(row);
            numColumns = row.Count;

            IList exportedRows;
            if (treeListView.SelectedIndices.Count > 1)
                exportedRows = treeListView.SelectedIndices;
            else
            {
                exportedRows = new List<int>();
                for (int i = 0; i < treeListView.Items.Count; ++i)
                    exportedRows.Add(i);
            }

            // ObjectListView's virtual mode doesn't support GetEnumerator()
            for(int i=0; i < exportedRows.Count; ++i)
            {
                var tableRow = treeListView.Items[(int) exportedRows[i]];

                row = new List<string>();

                string indention = string.Empty;
                for (int tabs = 0; tabs < tableRow.IndentCount; tabs++)
                    indention += "     ";

                row.Add(indention + tableRow.SubItems[0].Text);

                for (int x = 1; x < numColumns; ++x)
                    row.Add(tableRow.SubItems[x].Text);
                table.Add(row);
            }

            return table;
        }

        private void clipboardToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.CopyToClipboard(table);
        }

        private void fileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ExportToFile(table);
        }

        private void showInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();

            TableExporter.ShowInExcel(table);
        }
        #endregion

        private void groupingSetupButton_Click (object sender, EventArgs e)
        {
            groupingSetupPopup.Show(sender as Button);
        }

        private void groupingSetupControl_GroupingChanging (object sender, GroupingChangingEventArgs<GroupBy> e)
        {
            // GroupBy.Spectrum cannot be before GroupBy.Source

            if (e.Grouping.Mode != GroupBy.Spectrum && e.Grouping.Mode != GroupBy.Source)
                return;

            var newGroupings = new List<Grouping<GroupBy>>(groupingSetupControl.Groupings);
            newGroupings[newGroupings.IndexOf(e.Grouping)] = newGroupings.First(o => o.Mode == GroupBy.Analysis);
            newGroupings.Insert(e.NewIndex, e.Grouping);

            e.Cancel = GroupingSetupControl<GroupBy>.HasParentGrouping(newGroupings, GroupBy.Source, GroupBy.Spectrum);
        }

        private void groupingSetupControl_GroupingChanged (object sender, EventArgs e)
        {
            dirtyGroupings = true;
        }

        void groupingSetupPopup_Closed (object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (dirtyGroupings)
            {
                dirtyGroupings = false;

                if (dataFilter != null && dataFilter.IsBasicFilter)
                    basicDataFilter = null; // force refresh of basic rows

                SetData(session, userFilter);
            }
        }
    }

    public class SpectrumViewVisualizeEventArgs : EventArgs
    {
        public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; internal set; }
    }
}
