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
using IDPicker.Controls;

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
            public TotalCounts(NHibernate.ISession session, DataFilter dataFilter)
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
                                             "COUNT(DISTINCT psm.DistinctMatchKey), " +
                                             "COUNT(DISTINCT psm.Peptide), " +
                                             "COUNT(DISTINCT psm.Analysis), " +
                                             "COUNT(DISTINCT psm.Charge)";

            #region Constructor
            public AggregateRow(object[] queryRow, DataFilter dataFilter)
            {
                Spectra = (long)queryRow[0];
                DistinctMatches = (int)(long)queryRow[1];
                DistinctPeptides = (int)(long)queryRow[2];
                DistinctAnalyses = (int)(long)queryRow[3];
                DistinctCharges = (int)(long)queryRow[4];
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
                SpectrumSourceGroup = (queryRow[5] as DataModel.SpectrumSourceGroupLink).Group;
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
                SpectrumSource = (DataModel.SpectrumSource)queryRow[5];
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
                Analysis = (DataModel.Analysis)queryRow[5];

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
                Charge = (int)queryRow[5];
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
                Peptide = (DataModel.Peptide)queryRow[5];
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
                Spectrum = (DataModel.Spectrum)queryRow[5];
                Source = (DataModel.SpectrumSource)queryRow[6];
                Group = (DataModel.SpectrumSourceGroup)queryRow[7];

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
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch)queryRow[0];
                Spectrum = (DataModel.Spectrum)queryRow[3];
                Source = (DataModel.SpectrumSource)queryRow[4];
                Group = (DataModel.SpectrumSourceGroup)queryRow[5];
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

        public SpectrumTableForm()
        {
            InitializeComponent();

            FormClosing += delegate(object sender, FormClosingEventArgs e)
            {
                e.Cancel = true;
                DockState = DockState.DockBottomAutoHide;
            };

            basicAggregateRowsByType = new Dictionary<GroupBy, IEnumerable<AggregateRow>>();

            _columnSettings = new Dictionary<OLVColumn, ColumnProperty>();
            SetDefaults();
        }

        Dictionary<OLVColumn, ColumnProperty> _columnSettings;

        protected override void OnLoad(EventArgs e)
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
            groupingSetupControl.GroupingChanging +=
                new EventHandler<GroupingChangingEventArgs<GroupBy>>(groupingSetupControl_GroupingChanging);
            groupingSetupPopup = new Popup(groupingSetupControl) { FocusOnOpen = true };
            groupingSetupPopup.Closed += new ToolStripDropDownClosedEventHandler(groupingSetupPopup_Closed);

            #region Column aspect getters

            var allLayouts =
                new List<string>(
                    Util.StringCollectionToStringArray(Properties.Settings.Default.SpectrumTableFormSettings));
            if (allLayouts.Count > 1)
            {
                //get User Defualt Layout
                var retrievedList = allLayouts[1].Split(Environment.NewLine.ToCharArray()).ToList();

                //Make sure layout has same number of columns as ObjectListView
                if (retrievedList.Count == treeListView.Columns.Count + 2)
                {
                    //Go through each column and assign properties
                    foreach (OLVColumn column in treeListView.Columns)
                    {
                        //Go through lines and find the one that matches current column
                        for (var x = 0; x < retrievedList.Count - 1; x++)
                        {
                            var splitSetting = retrievedList[x].Split('|');

                            if (splitSetting[0] == column.Text)
                            {
                                _columnSettings.Add(column,
                                    new ColumnProperty
                                    {
                                        Scope = "SpectrumTableForm",
                                        Name = column.Text,
                                        Type = splitSetting[1],
                                        DecimalPlaces = int.Parse(splitSetting[2]),
                                        ColorCode = int.Parse(splitSetting[3]),
                                        Visible = bool.Parse(splitSetting[4]),
                                        Locked = bool.Parse(splitSetting[5])
                                    });
                                break;
                            }
                        }
                    }

                    //set overall colors from end properties
                    treeListView.BackColor = Color.FromArgb(int.Parse(retrievedList[treeListView.Columns.Count - 2]));
                    treeListView.ForeColor = Color.FromArgb(int.Parse(retrievedList[treeListView.Columns.Count - 1]));

                    //foreach (var kvp in _columnSettings)
                    //    kvp.Key.IsVisible = kvp.Value.Visible;
                    //treeListView.RebuildColumns();
                }
            }


            SetColumnAspectGetters();

            #endregion

            treeListView.UseCellFormatEvents = true;
            treeListView.FormatCell +=
                delegate(object sender, FormatCellEventArgs currentCell)
                {
                    currentCell.SubItem.BackColor = Color.FromArgb(_columnSettings[currentCell.Column].ColorCode);
                };

            treeListView.CanExpandGetter += delegate(object x) { return !(x is PeptideSpectrumMatchScoreRow); };
            treeListView.ChildrenGetter += new TreeListView.ChildrenGetterDelegate(getChildren);
            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);
            treeListView.KeyPress += new KeyPressEventHandler(treeListView_KeyPress);
            treeListView.AfterExpanding += new EventHandler<AfterExpandingEventArgs>(treeListView_AfterExpanding);
            treeListView.AfterCollapsing += new EventHandler<AfterCollapsingEventArgs>(treeListView_AfterCollapsing);
        }

        private void SetDefaults()
        {
            _columnSettings = new Dictionary<OLVColumn, ColumnProperty>();
            var columnType = new Dictionary<OLVColumn, string>
                                 {
                                     {keyColumn, "Key"},
                                     {totalSpectraColumn, "Integer"},
                                     {spectraColumn, "Integer"},
                                     {distinctPeptidesColumn, "Integer"},
                                     {distinctMatchesColumn, "Integer"},
                                     {distinctAnalysesColumn, "Integer"},
                                     {distinctChargesColumn, "Integer"},
                                     {analysisColumn, "String"},
                                     {precursorMzColumn, "Float"},
                                     {chargeColumn, "Integer"},
                                     {observedMassColumn, "Float"},
                                     {exactMassColumn, "Float"},
                                     {massErrorColumn, "Float"},
                                     {qvalueColumn, "Float"},
                                     {sequenceColumn, "String"}
                                 };

            foreach (var kvp in columnType)
            {
                var tempColumnProperty = new ColumnProperty()
                {
                    Scope = "SpectrumTableForm",
                    Type = columnType[kvp.Key],
                    DecimalPlaces = -1,
                    Visible = true,
                    Locked = false,
                    ColorCode = treeListView.BackColor.ToArgb(),
                    Name = kvp.Key.Text
                };

                _columnSettings.Add(kvp.Key, tempColumnProperty);
            }
        }

        private void SetColumnAspectGetters()
        {
            keyColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return (x as SpectrumSourceGroupRow).SpectrumSourceGroup.Name == "/"
                               ? "/"
                               : Path.GetFileName((x as SpectrumSourceGroupRow).SpectrumSourceGroup.Name);
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
                    return String.Format("{0}: {1}", (x as PeptideSpectrumMatchScoreRow).Name,
                                         (x as PeptideSpectrumMatchScoreRow).Value);
                return null;
            };

            keyColumn.ImageGetter += delegate(object x)
                                         {
                                             if (x is SpectrumSourceGroupRow)
                                                 return Properties.Resources.XPfolder_closed;
                                             if (x is SpectrumSourceRow)
                                                 return Properties.Resources.file;
                                             if (x is SpectrumRow)
                                                 return Properties.Resources.SpecrumIcon;
                                             if (x is PeptideSpectrumMatchRow)
                                                 return Properties.Resources.PSMIcon;
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

            distinctPeptidesColumn.AspectGetter += delegate(object x)
            {
                if (x is AggregateRow)
                    return (x as AggregateRow).DistinctPeptides;
                return null;
            };

            distinctMatchesColumn.AspectGetter += delegate(object x)
            {
                if (x is AggregateRow)
                    return (x as AggregateRow).DistinctMatches;
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
                var decimalPlaces = _columnSettings[precursorMzColumn].DecimalPlaces;

                if (x is SpectrumRow)
                    return (decimalPlaces >= 0) ?
                        Math.Round((x as SpectrumRow).Spectrum.PrecursorMZ, decimalPlaces) :
                        (x as SpectrumRow).Spectrum.PrecursorMZ;
                else if (x is PeptideSpectrumMatchRow)
                    return (decimalPlaces >= 0) ?
                        Math.Round((x as PeptideSpectrumMatchRow).Spectrum.PrecursorMZ, decimalPlaces) :
                        (x as PeptideSpectrumMatchRow).Spectrum.PrecursorMZ;
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
                    double returnValue = psm.Spectrum.PrecursorMZ * psm.Charge - psm.Charge * pwiz.CLI.chemistry.Proton.Mass;
                    if (_columnSettings[observedMassColumn].DecimalPlaces >= 0)
                        returnValue = Math.Round(returnValue, (int)_columnSettings[observedMassColumn].DecimalPlaces);
                    return returnValue;
                }
                return null;
            };

            exactMassColumn.AspectGetter += delegate(object x)
            {
                var decimalPlaces = _columnSettings[exactMassColumn].DecimalPlaces;

                if (x is PeptideSpectrumMatchRow)
                    return (decimalPlaces >= 0) ?
                        Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass, decimalPlaces) :
                        (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass;
                return null;
            };

            massErrorColumn.AspectGetter += delegate(object x)
            {
                var decimalPlaces = _columnSettings[massErrorColumn].DecimalPlaces;

                if (x is PeptideSpectrumMatchRow)
                    return (decimalPlaces >= 0) ?
                        Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMassError, decimalPlaces) :
                        (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMassError;
                return null;
            };

            qvalueColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                {
                    var decimalPlaces = _columnSettings[massErrorColumn].DecimalPlaces;
                    var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                    if (decimalPlaces >= 0)
                        return psm.Rank > 1 ? "n/a" : Math.Round(psm.QValue, decimalPlaces).ToString();
                    else
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
        }

        internal List<ColumnProperty> GetCurrentProperties()
        {
            foreach (var kvp in _columnSettings)
                kvp.Value.Visible = false;
            foreach (var column in treeListView.ColumnsInDisplayOrder)
                _columnSettings[column].Visible = true;

            var currentList = _columnSettings.Select(kvp => kvp.Value).ToList();

            currentList.Add(new ColumnProperty
            {
                Scope = "SpectrumTableForm",
                Name = "BackColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.BackColor.ToArgb(),
                Visible = false,
                Locked = false
            });
            currentList.Add(new ColumnProperty
            {
                Scope = "SpectrumTableForm",
                Name = "TextColor",
                Type = "GlobalSetting",
                DecimalPlaces = -1,
                ColorCode = treeListView.ForeColor.ToArgb(),
                Visible = false,
                Locked = false
            });

            return currentList;
        }

        #region Set column visibility
        void treeListView_AfterExpanding(object sender, AfterExpandingEventArgs e)
        {
            treeListView_setColumnVisibility();
        }

        void treeListView_AfterCollapsing(object sender, AfterCollapsingEventArgs e)
        {
            treeListView_setColumnVisibility();
        }

        void treeListView_setColumnVisibility()
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
            distinctPeptidesColumn.IsVisible = showAggregateColumns && deepestGroupingMode != GroupBy.Off && !GroupingSetupControl<GroupBy>.HasParentGrouping(checkedGroupings, deepestGroupingMode, GroupBy.Peptide);
            distinctMatchesColumn.IsVisible = showAggregateColumns && deepestGroupingMode != GroupBy.Off && !GroupingSetupControl<GroupBy>.HasParentGrouping(checkedGroupings, deepestGroupingMode, GroupBy.Spectrum);
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

        IEnumerable getChildren(object x)
        {
            if (x is SpectrumSourceGroupRow)
            {
                var row = x as SpectrumSourceGroupRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = new List<SpectrumSourceGroup>() };
                childFilter.SpectrumSourceGroup.Add(row.SpectrumSourceGroup);
                return getSpectrumSourceRows(childFilter);
            }
            else if (x is SpectrumSourceRow)
            {
                var row = x as SpectrumSourceRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { SpectrumSource = new List<SpectrumSource>() };
                childFilter.SpectrumSource.Add(row.SpectrumSource);
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Source);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is AnalysisRow)
            {
                var row = x as AnalysisRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Analysis = new List<Analysis>() };
                childFilter.Analysis.Add(row.Analysis);
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Analysis);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is PeptideRow)
            {
                var row = x as PeptideRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Peptide = new List<Peptide>() };
                childFilter.Peptide.Add(row.Peptide);
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Peptide);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is ChargeRow)
            {
                var row = x as ChargeRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Charge = new List<int>() };
                childFilter.Charge.Add(row.Charge);
                var childGrouping = GroupingSetupControl<GroupBy>.GetChildGrouping(checkedGroupings, GroupBy.Charge);
                return getChildren(childGrouping, childFilter);
            }
            else if (x is SpectrumRow)
            {
                var row = x as SpectrumRow;
                var parentFilter = row.DataFilter ?? dataFilter;
                var childFilter = new DataFilter(parentFilter) { Spectrum = new List<Spectrum>() };
                childFilter.Spectrum.Add(row.Spectrum);
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

        void treeListView_CellClick(object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null ||
                e.HitTest.HitTestLocation == HitTestLocation.ExpandButton)
                return;

            var newDataFilter = new DataFilter() { FilterSource = this };

            if (e.Item.RowObject is SpectrumSourceGroupRow)
                newDataFilter.SpectrumSourceGroup = new List<SpectrumSourceGroup> { (e.Item.RowObject as SpectrumSourceGroupRow).SpectrumSourceGroup };
            else if (e.Item.RowObject is SpectrumSourceRow)
                newDataFilter.SpectrumSource = new List<SpectrumSource> { (e.Item.RowObject as SpectrumSourceRow).SpectrumSource };
            else if (e.Item.RowObject is SpectrumRow)
                newDataFilter.Spectrum = new List<Spectrum> { (e.Item.RowObject as SpectrumRow).Spectrum };
            else if (e.Item.RowObject is AnalysisRow)
                newDataFilter.Analysis = new List<Analysis> { (e.Item.RowObject as AnalysisRow).Analysis };
            else if (e.Item.RowObject is PeptideRow)
                newDataFilter.Peptide = new List<Peptide> { (e.Item.RowObject as PeptideRow).Peptide };
            else if (e.Item.RowObject is ChargeRow)
                newDataFilter.Charge = new List<int> { (e.Item.RowObject as ChargeRow).Charge };
            else if (e.Item.RowObject is PeptideSpectrumMatchRow)
            {
                if (SpectrumViewVisualize != null)
                    SpectrumViewVisualize(this, new SpectrumViewVisualizeEventArgs()
                                                    {
                                                        PeptideSpectrumMatch =
                                                            (e.Item.RowObject as PeptideSpectrumMatchRow).
                                                            PeptideSpectrumMatch
                                                    });
                return;
            }

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
        }
        void treeListView_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (e.KeyChar != (char)Keys.Enter)
                return;

            e.Handled = true;
            var newDataFilter = new DataFilter() { FilterSource = this };

            foreach (var item in treeListView.SelectedObjects)
            {
                if (item == null)
                    continue;

                if (item is SpectrumSourceGroupRow)
                {
                    if (newDataFilter.SpectrumSourceGroup == null)
                        newDataFilter.SpectrumSourceGroup = new List<SpectrumSourceGroup> { (item as SpectrumSourceGroupRow).SpectrumSourceGroup };
                    else
                        newDataFilter.SpectrumSourceGroup.Add((item as SpectrumSourceGroupRow).SpectrumSourceGroup);
                }
                else if (item is SpectrumSourceRow)
                {
                    if (newDataFilter.SpectrumSource == null)
                        newDataFilter.SpectrumSource = new List<SpectrumSource> { (item as SpectrumSourceRow).SpectrumSource };
                    else
                        newDataFilter.SpectrumSource.Add((item as SpectrumSourceRow).SpectrumSource);
                }
                else if (item is SpectrumRow)
                {
                    if (newDataFilter.Spectrum == null)
                        newDataFilter.Spectrum = new List<Spectrum> { (item as SpectrumRow).Spectrum };
                    else
                        newDataFilter.Spectrum.Add((item as SpectrumRow).Spectrum);
                }
                else if (item is AnalysisRow)
                {
                    if (newDataFilter.Analysis == null)
                        newDataFilter.Analysis = new List<Analysis> { (item as AnalysisRow).Analysis };
                    else
                        newDataFilter.Analysis.Add((item as AnalysisRow).Analysis);
                }
                else if (item is PeptideRow)
                {
                    if (newDataFilter.Peptide == null)
                        newDataFilter.Peptide = new List<Peptide> { (item as PeptideRow).Peptide };
                    else
                        newDataFilter.Peptide.Add((item as PeptideRow).Peptide);
                }
                else if (item is ChargeRow)
                {
                    if (newDataFilter.Charge == null)
                        newDataFilter.Charge = new List<int> { (item as ChargeRow).Charge };
                    else
                        newDataFilter.Charge.Add((item as ChargeRow).Charge);
                }

            }

            if (SpectrumViewFilter != null)
                SpectrumViewFilter(this, newDataFilter);
        }

        #region getChildren functions for each row type

        // returns both groups and sources
        IEnumerable<AggregateRow> getSpectrumSourceRows(DataFilter parentFilter)
        {
            var groupsFilter = new DataFilter(parentFilter) { SpectrumSourceGroup = null };
            var groups = session.CreateQuery(AggregateRow.Selection + ", ssgl " +
                                             groupsFilter.GetFilteredQueryString(
                                                 DataFilter.FromPeptideSpectrumMatch,
                                                 DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                             "GROUP BY ssgl.Group.id")
                .List<object[]>()
                .Select(o => new SpectrumSourceGroupRow(o, parentFilter));

            if (parentFilter != null)
            {
                if (parentFilter.SpectrumSourceGroup != null)
                {
                    var otherList = new List<SpectrumSourceGroupRow>();
                    foreach (var item in parentFilter.SpectrumSourceGroup)
                        otherList.AddRange(groups.Where(o => o.SpectrumSourceGroup.IsImmediateChildOf(item)));
                    groups = otherList;
                }
                else
                    groups = groups.Where(o => o.SpectrumSourceGroup.Name == "/");
            }

            var sources = session.CreateQuery(AggregateRow.Selection + ", psm.Spectrum.Source " +
                                              parentFilter.GetFilteredQueryString(
                                                  DataFilter.FromPeptideSpectrumMatch) +
                                              "GROUP BY psm.Spectrum.Source.id")
                .List<object[]>()
                .Select(o => new SpectrumSourceRow(o, parentFilter));

            if (parentFilter != null)
            {
                if (parentFilter.SpectrumSourceGroup != null)
                {
                    var otherList = new List<SpectrumSourceRow>();
                    foreach (var item in parentFilter.SpectrumSourceGroup)
                        otherList.AddRange(sources.Where(o => o.SpectrumSource.Group.Id == item.Id));
                    sources = otherList;
                }
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

        IEnumerable<AggregateRow> getAnalysisRows(DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", psm.Analysis " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                       "GROUP BY psm.Analysis.id")
                          .List<object[]>()
                          .Select(o => new AnalysisRow(o, parentFilter) as AggregateRow);
        }

        IEnumerable<AggregateRow> getPeptideRows(DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", psm.Peptide " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                       "GROUP BY psm.Peptide.id")
                          .List<object[]>()
                          .Select(o => new PeptideRow(o, parentFilter) as AggregateRow);
        }

        IEnumerable<AggregateRow> getChargeRows(DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", psm.Charge " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                       "GROUP BY psm.Charge")
                          .List<object[]>()
                          .Select(o => new ChargeRow(o, parentFilter) as AggregateRow);
        }

        IEnumerable<SpectrumRow> getSpectrumRows(DataFilter parentFilter)
        {
            return session.CreateQuery(AggregateRow.Selection + ", s, ss, ssg " +
                                       parentFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                           DataFilter.PeptideSpectrumMatchToSpectrumSourceGroup) +
                                       "GROUP BY s.id " +
                                       "ORDER BY s.Index")
                          .List<object[]>()
                          .Select(o => new SpectrumRow(o, parentFilter, checkedGroupings));
        }

        IEnumerable<PeptideSpectrumMatchRow> getPeptideSpectrumMatchRows(DataFilter parentFilter)
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

        IEnumerable getChildren(PeptideSpectrumMatchRow x, DataFilter parentFilter)
        {
            return x.PeptideSpectrumMatch.Scores.Select(o => new PeptideSpectrumMatchScoreRow(o));
        }

        IEnumerable getChildren(Grouping<GroupBy> grouping, DataFilter parentFilter)
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

        public void ClearData()
        {
            Text = TabText = "Spectrum View";

            treeListView.DiscardAllState();
            treeListView.Roots = null;
            treeListView.Refresh();
            Refresh();
        }

        public void ClearData(bool clearBasicFilter)
        {
            if (clearBasicFilter)
                basicDataFilter = null;
            ClearData();
        }

        public void SetData(NHibernate.ISession session, DataFilter userFilter)
        {
            if (session == null)
                return;

            this.session = session;
            this.userFilter = userFilter;
            dataFilter = new DataFilter(userFilter) { Spectrum = null, SpectrumSource = null, SpectrumSourceGroup = null };
            oldSelectionPath = new List<string>();
            for (int x = 0; x < treeListView.Items.Count; x++)
            {
                if (treeListView.IsExpanded(treeListView.GetModelObject(x)))
                    oldSelectionPath.Add(treeListView.Items[x].Text);
            }
            oldSelectionPath.Add(treeListView.SelectedItem == null
                                     ? "<<No Item Selected>>"
                                     : treeListView.SelectedItem.Text);


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

        internal void LoadLayout(IList<ColumnProperty> listOfSettings)
        {
            if (listOfSettings.Count > 0)
            {
                var columnlist = _columnSettings.Select(kvp => kvp.Key).ToList();

                foreach (var column in columnlist)
                {
                    var rowSettings = listOfSettings.Where(x => x.Name == column.Text).SingleOrDefault() ??
                        listOfSettings.Where(x => x.Type == "Key").SingleOrDefault();

                    if (rowSettings != null)
                    {
                        _columnSettings[column] = new ColumnProperty
                                                      {
                                                          Scope = "SpectrumTableForm",
                                                          Name = rowSettings.Name,
                                                          Type = rowSettings.Type,
                                                          DecimalPlaces = rowSettings.DecimalPlaces,
                                                          ColorCode = rowSettings.ColorCode,
                                                          Visible = rowSettings.Visible,
                                                          Locked = rowSettings.Visible
                                                      };
                    }
                }

                SetColumnAspectGetters();
                var backColor = listOfSettings.Where(x => x.Name == "BackColor").SingleOrDefault();
                var textColor = listOfSettings.Where(x => x.Name == "TextColor").SingleOrDefault();
                treeListView.BackColor = Color.FromArgb(backColor.ColorCode);
                treeListView.ForeColor = Color.FromArgb(textColor.ColorCode);

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = kvp.Value.Visible;
                treeListView.RebuildColumns();
            }
        }


        void setData(object sender, DoWorkEventArgs e)
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

        void renderData(object sender, RunWorkerCompletedEventArgs e)
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
                                                                                           (o as SpectrumSourceGroupRow).SpectrumSourceGroup.Id == rootGroup.Id).SingleOrDefault();

                if (rootGroupRow != null)
                {
                    treeListView.Roots = new object[] { rootGroupRow };

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
                    (this.ParentForm as IDPickerForm).ApplyBasicFilter();
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
                exportMenu.Items[2].Text = "Show Current in Excel";
            }

            exportMenu.Show(Cursor.Position);
        }

        internal List<List<string>> getFormTable()
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
            for (int i = 0; i < exportedRows.Count; ++i)
            {
                var tableRow = treeListView.Items[(int)exportedRows[i]];

                row = new List<string>();

                string indention = string.Empty;
                for (int tabs = 0; tabs < tableRow.IndentCount; tabs++)
                    indention += "     ";

                row.Add("'" + indention + tableRow.SubItems[0].Text);

                for (int x = 1; x < numColumns; ++x)
                    row.Add(tableRow.SubItems[x].Text);
                table.Add(row);
            }

            return table;
        }

        internal List<TreeNode> getSpectrumSourceGroupTree()
        {
            var groupNodes = new List<TreeNode>();
            var groups = session.CreateQuery(AggregateRow.Selection + ", ssgl " +
                                             userFilter.GetFilteredQueryString(
                                                 DataFilter.FromPeptideSpectrumMatch,
                                                 DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                             "GROUP BY ssgl.Group.id")
                .List<object[]>()
                .Select(o => new SpectrumSourceGroupRow(o, userFilter));
            foreach (var group in groups)
            {
                var newNode = new TreeNode
                                  {
                                      Text = group.SpectrumSourceGroup.Name,
                                      Tag =
                                          new[]
                                              {
                                                  "'" + group.SpectrumSourceGroup.Name + "'", group.Spectra.ToString(),
                                                  group.DistinctPeptides.ToString(), group.DistinctMatches.ToString(),
                                                  group.DistinctAnalyses.ToString(), group.DistinctCharges.ToString()
                                              }
                                  };
                var groupFilter = new DataFilter(userFilter)
                                      {
                                          SpectrumSourceGroup =
                                              new List<SpectrumSourceGroup> { group.SpectrumSourceGroup }
                                      };
                var sources = session.CreateQuery(AggregateRow.Selection + ", psm.Spectrum.Source " +
                                              groupFilter.GetFilteredQueryString(
                                                  DataFilter.FromPeptideSpectrumMatch) +
                                              "GROUP BY psm.Spectrum.Source.id")
                .List<object[]>().Select(o => new SpectrumSourceRow(o, groupFilter));
                SpectrumSourceGroupRow ssgr = group;
                sources = from SpectrumSourceRow s in sources
                          where s.SpectrumSource.Group == ssgr.SpectrumSourceGroup
                          select s;
                foreach (var source in sources)
                {
                    var newSubNode = new TreeNode
                                         {
                                             Text = "source" + source.SpectrumSource.Id ?? string.Empty + ".html",
                                             Tag = new[]
                                                       {
                                                           "'<a href =\"source" + (source.SpectrumSource.Id != null
                                                                                       ? source.SpectrumSource.Id.ToString()
                                                                                       : string.Empty) + ".html\">" +
                                                           source.SpectrumSource.Name + "</a>'",
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

        private void showCurrentInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            var table = getFormTable();
            var exportWrapper = new Dictionary<string, List<List<string>>> { { this.Name, table } };

            TableExporter.ShowInExcel(exportWrapper, false);
        }
        #endregion

        private void groupingSetupButton_Click(object sender, EventArgs e)
        {
            groupingSetupPopup.Show(sender as Button);
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

        private void groupingSetupControl_GroupingChanged(object sender, EventArgs e)
        {
            dirtyGroupings = true;
        }

        void groupingSetupPopup_Closed(object sender, ToolStripDropDownClosedEventArgs e)
        {
            if (dirtyGroupings)
            {
                dirtyGroupings = false;

                if (dataFilter != null && dataFilter.IsBasicFilter)
                    basicDataFilter = null; // force refresh of basic rows

                SetData(session, userFilter);
            }
        }

        private void displayOptionsButton_Click(object sender, EventArgs e)
        {
            Color[] currentColors = { treeListView.BackColor, treeListView.ForeColor };

            foreach (var kvp in _columnSettings)
                kvp.Value.Visible = kvp.Key.IsVisible;

            var ccf = new ColumnControlForm(_columnSettings, currentColors);

            if (ccf.ShowDialog() == DialogResult.OK)
            {
                _columnSettings = ccf.SavedSettings;

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = kvp.Value.Visible;

                treeListView.BackColor = ccf.WindowBackColorBox.BackColor;
                treeListView.ForeColor = ccf.WindowTextColorBox.BackColor;

                SetColumnAspectGetters();
                treeListView.RebuildColumns();
            }
        }

        private void showSourcesInExcelToolStripMenuItem_Click(object sender, EventArgs e)
        {
            TableExporter.ShowInExcel(getSourceContentsForExcel(), true);
        }

        internal Dictionary<string, List<List<string>>> getSourceContentsForExcel()
        {
            var allContents = new Dictionary<string, List<List<string>>>();
            var sources = session.QueryOver<SpectrumSource>().List();
            foreach (var source in sources)
            {
                var exportTable = new List<List<string>>();
                var scoreList = new HashSet<string>();
                exportTable.Add(new List<string>
                                    {
                                        "Spectrum/Rank",
                                        "Distinct Peptides",
                                        "Distinct Analysis",
                                        "Distinct Charges",
                                        "Precursor m/z",
                                        "Charge",
                                        "Observed Mass",
                                        "Exact Mass",
                                        "Mass Error",
                                        "Q Value",
                                        "Sequence"
                                    });

                var sourceFilter = new DataFilter(dataFilter) { SpectrumSource = new List<SpectrumSource> { source } };
                var spectraRows = getSpectrumRows(sourceFilter);
                foreach (var spectra in spectraRows)
                {
                    var key = spectra.Spectrum.NativeID;
                    try { key = pwiz.CLI.msdata.id.abbreviate(key); }
                    catch { }
                    exportTable.Add(new List<string>
                                        {
                                            key,
                                            spectra.DistinctPeptides.ToString(),
                                            spectra.DistinctAnalyses.ToString(),
                                            spectra.DistinctCharges.ToString(),
                                            spectra.Spectrum.PrecursorMZ.ToString()
                                        });
                    foreach (var match in spectra.Spectrum.Matches)
                    {
                        var observedMass = match.Spectrum.PrecursorMZ * match.Charge - match.Charge * pwiz.CLI.chemistry.Proton.Mass;
                        var newRow = new List<string>
                                         {
                                             "'          " + match.Rank,
                                             string.Empty,
                                             string.Empty,
                                             string.Empty,
                                             string.Empty,
                                             match.Charge.ToString(),
                                             observedMass.ToString(),
                                             match.MonoisotopicMass.ToString(),
                                             match.MonoisotopicMassError.ToString(),
                                             match.QValue.ToString(),
                                             match.Peptide.Sequence
                                         };
                        foreach (var score in match.Scores)
                            scoreList.Add(score.Key);
                        foreach (var score in scoreList)
                        {
                            newRow.Add(match.Scores.ContainsKey(score)
                                           ? match.Scores[score].ToString()
                                           : string.Empty);
                        }
                        exportTable.Add(newRow);
                    }
                }
                exportTable[0].AddRange(scoreList);
                allContents.Add(source.Name, exportTable);
            }
            return allContents;
        }

        internal Dictionary<string[], List<TreeNode>> getSourceContentsForHTML()
        {
            const int decimalPlaces = 4;
            var allContents = new Dictionary<string[], List<TreeNode>>();
            var sources = session.QueryOver<SpectrumSource>().List();
            foreach (var source in sources)
            {
                var exportTable = new List<TreeNode>();
                var scoreList = new HashSet<string>();
                var sourceFilter = new DataFilter(dataFilter) {SpectrumSource = new List<SpectrumSource> {source}};
                var spectraRows = getSpectrumRows(sourceFilter);

                foreach (var spectra in spectraRows)
                {
                    var key = spectra.Spectrum.NativeID;
                    try
                    {
                        key = pwiz.CLI.msdata.id.abbreviate(key);
                    }
                    catch
                    {
                    }
                    var newBranch = new TreeNode
                                        {
                                            Text = key,
                                            Tag = new[]
                                                      {
                                                          "'" + key + "'", spectra.DistinctPeptides.ToString(),
                                                          spectra.DistinctAnalyses.ToString(),
                                                          spectra.DistinctCharges.ToString(),
                                                          Math.Round(spectra.Spectrum.PrecursorMZ,decimalPlaces).ToString()
                                                      }
                                        };
                    foreach (var match in spectra.Spectrum.Matches)
                    {
                        var observedMass = match.Spectrum.PrecursorMZ*match.Charge -
                                           match.Charge*pwiz.CLI.chemistry.Proton.Mass;
                        var matchNode = new TreeNode {Text = match.Rank.ToString()};
                        var tag = new List<string>
                                      {
                                          "'" + match.Rank + "'",
                                          match.Charge.ToString(),
                                          Math.Round(observedMass,decimalPlaces).ToString(),
                                          Math.Round(match.MonoisotopicMass,decimalPlaces).ToString(),
                                          Math.Round(match.MonoisotopicMassError,decimalPlaces).ToString(),
                                          Math.Round(match.QValue,decimalPlaces).ToString(),
                                          "'" + match.Peptide.Sequence + "'"
                                      };
                        foreach (var score in match.Scores)
                            scoreList.Add("'" + score.Key + "'");
                        foreach (var score in scoreList)
                        {
                            tag.Add(match.Scores.ContainsKey(score.Trim("'".ToCharArray()))
                                        ? Math.Round(match.Scores[score.Trim("'".ToCharArray())],decimalPlaces).ToString()
                                        : string.Empty);
                        }
                        matchNode.Tag = tag.ToArray();
                        newBranch.Nodes.Add(matchNode);
                    }
                    exportTable.Add(newBranch);
                }
                var headers = new List<string>()
                                  {
                                      "'Rank'",
                                      "'Charge'",
                                      "'Observed Mass'",
                                      "'Monoisotopic Mass'",
                                      "'Mass Error'",
                                      "'Q Value'",
                                      "'Sequence'"
                                  };
                headers.AddRange(scoreList);
                allContents.Add(
                    new[]
                        {
                            source.Name, "source" + (source.Id == null
                                                         ? string.Empty
                                                         : source.Id.ToString())
                                         + ".html",
                            string.Join("|", headers.ToArray())
                        }, exportTable);
            }
            return allContents;
        }
    }

    public class SpectrumViewVisualizeEventArgs : EventArgs
    {
        public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; internal set; }
    }
}
