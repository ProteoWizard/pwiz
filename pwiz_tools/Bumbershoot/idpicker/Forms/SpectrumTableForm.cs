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
using IDPicker.DataModel;

namespace IDPicker.Forms
{

    public partial class SpectrumTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        #region Wrapper classes for encapsulating query results
        public class SpectrumSourceGroupRow
        {
            public DataModel.SpectrumSourceGroup SpectrumSourceGroup { get; private set; }
            public long Spectra { get; private set; }
            public long DistinctPeptides { get; private set; }
            public long DistinctMatches { get; private set; }

            #region Constructor
            public SpectrumSourceGroupRow (object[] queryRow)
            {
                SpectrumSourceGroup = (queryRow[0] as DataModel.SpectrumSourceGroupLink).Group;
                Spectra = (long) queryRow[1];
                DistinctPeptides = (long) queryRow[2];
                DistinctMatches = (long) queryRow[3];
            }
            #endregion
        }

        public class SpectrumSourceRow
        {
            public DataModel.SpectrumSource SpectrumSource { get; private set; }
            public long Spectra { get; private set; }
            public long DistinctPeptides { get; private set; }
            public long DistinctMatches { get; private set; }

            #region Constructor
            public SpectrumSourceRow (object[] queryRow)
            {
                SpectrumSource = (DataModel.SpectrumSource) queryRow[0];
                Spectra = (long) queryRow[1];
                DistinctPeptides = (long) queryRow[2];
                DistinctMatches = (long) queryRow[3];
            }
            #endregion
        }

        public class SpectrumRow
        {
            public DataModel.Spectrum Spectrum { get; private set; }

            #region Constructor
            public SpectrumRow (object queryRow)
            {
                Spectrum = (DataModel.Spectrum) queryRow;
            }
            #endregion
        }

        public class PeptideSpectrumMatchRow
        {
            public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }
            public DataModel.Spectrum Spectrum { get; private set; }

            #region Constructor
            public PeptideSpectrumMatchRow (object[] queryRow)
            {
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch) queryRow[0];
                Spectrum = (DataModel.Spectrum) queryRow[1];
            }
            #endregion
        }
        #endregion

        Dictionary<OLVColumn, object[]> _columnSettings;

        public SpectrumTableForm ()
        {
            InitializeComponent();

            HideOnClose = true;
        }

        protected override void OnLoad (EventArgs e)
        {
            Text = TabText = "Spectrum View";

            topRankOnlyCheckBox.Checked = Properties.Settings.Default.TopRankOnly;

            #region Column aspect getters
            _columnSettings = new Dictionary<OLVColumn, object[]>();
            var retrievedList = new List<string>(Util.StringCollectionToStringArray(Properties.Settings.Default.SpectrumTableFormSettings));

            //count should = 6 x (max columns) + 2
            //6 values per column, with 2 general settings at end
            if (retrievedList.Count == 68)
            {
                SetPropertyFromUserSettings(ref _columnSettings, sourceOrScanColumn, ref  retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, totalSpectraColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, confidentSpectraColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, confidentPeptidesColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, precursorMzColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, chargeColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, observedMassColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, exactMassColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, massErrorColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, qvalueColumn, ref retrievedList);
                SetPropertyFromUserSettings(ref _columnSettings, sequenceColumn, ref  retrievedList);

                treeListView.BackColor = Color.FromArgb(int.Parse(retrievedList[0].ToString()));
                treeListView.ForeColor = Color.FromArgb(int.Parse(retrievedList[1].ToString()));

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];
                treeListView.RebuildColumns();
            }
            else
            {
                //Old Column Setting
                _columnSettings.Add(sourceOrScanColumn, new object[5] { "Key", -99, treeListView.BackColor, false, false });
                _columnSettings.Add(totalSpectraColumn, new object[5] { "String", -99, treeListView.BackColor, false, false });
                _columnSettings.Add(confidentSpectraColumn, new object[5] { "Integer", -99, treeListView.BackColor, false, false });
                _columnSettings.Add(confidentPeptidesColumn, new object[5] { "Integer", -99, treeListView.BackColor, false, false });
                _columnSettings.Add(precursorMzColumn, new object[5] { "Float", -1, treeListView.BackColor, false, false });
                _columnSettings.Add(chargeColumn, new object[5] { "Integer", -99, treeListView.BackColor, false, false });
                _columnSettings.Add(observedMassColumn, new object[5] { "Float", -1, treeListView.BackColor, false, false });
                _columnSettings.Add(exactMassColumn, new object[5] { "Float", -1, treeListView.BackColor, false, false });
                _columnSettings.Add(massErrorColumn, new object[5] { "Float", -1, treeListView.BackColor, false, false });
                _columnSettings.Add(qvalueColumn, new object[5] { "Float", -1, treeListView.BackColor, false, false });
                _columnSettings.Add(sequenceColumn, new object[5] { "String", -99, treeListView.BackColor, false, false });
            }

            SetColumnAspectGetters();

            #endregion

            treeListView.UseCellFormatEvents = true;
            treeListView.FormatCell += delegate(object sender, FormatCellEventArgs currentCell)
            {
                currentCell.SubItem.BackColor = (Color)_columnSettings[currentCell.Column][2];
            };

            treeListView.CanExpandGetter += delegate(object x) { return !(x is PeptideSpectrumMatchRow); };
            treeListView.ChildrenGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                {
                    var parentGroup = (x as SpectrumSourceGroupRow).SpectrumSourceGroup;

                    var childGroups = from r in rowsByGroup
                                      where r.SpectrumSourceGroup.IsImmediateChildOf(parentGroup)
                                      select r as object;

                    var childSources = from r in rowsBySource
                                       where r.SpectrumSource.Group.Id == parentGroup.Id
                                       select r as object;

                    return childGroups.Concat(childSources);
                }
                else if (x is SpectrumSourceRow && !topRankOnlyCheckBox.Checked)
                {
                    string whereClause = dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch);
                    whereClause += (whereClause.Contains("WHERE") ? "AND" : "WHERE") + " psm.Spectrum.Source.id = ";
                    return session.CreateQuery("SELECT DISTINCT psm.Spectrum " +
                                               whereClause +
                                               (x as SpectrumSourceRow).SpectrumSource.Id.ToString())
                        .List<DataModel.Spectrum>()
                        .Select(o => new SpectrumRow(o));
                }
                else if (x is SpectrumRow || (x is SpectrumSourceRow && topRankOnlyCheckBox.Checked))
                {
                    string whereClause = dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToModification);
                    whereClause += whereClause.Contains("WHERE") ? "AND" : "WHERE";
                    if (x is SpectrumRow)
                        whereClause += " psm.Spectrum.id = " + (x as SpectrumRow).Spectrum.Id;
                    else
                        whereClause += " psm.Spectrum.Source.id = " + (x as SpectrumSourceRow).SpectrumSource.Id;

                    return session.CreateQuery("SELECT DISTINCT psm, psm.Spectrum " + whereClause)
                                  .List<object[]>()
                                  .Select(o => new PeptideSpectrumMatchRow(o));
                }
                return null;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            treeListView.AfterExpanding += new EventHandler<AfterExpandingEventArgs>(treeListView_AfterExpanding);
            treeListView.AfterCollapsing += new EventHandler<AfterCollapsingEventArgs>(treeListView_AfterCollapsing);
        }

        private void SetColumnAspectGetters()
        {
            sourceOrScanColumn.AspectGetter = null;
            totalSpectraColumn.AspectGetter = null;
            confidentSpectraColumn.AspectGetter = null;
            confidentPeptidesColumn.AspectGetter = null;
            precursorMzColumn.AspectGetter = null;
            chargeColumn.AspectGetter = null;
            observedMassColumn.AspectGetter = null;
            exactMassColumn.AspectGetter = null;
            massErrorColumn.AspectGetter = null;
            qvalueColumn.AspectGetter = null;
            sequenceColumn.AspectGetter = null;

            sourceOrScanColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return Path.GetFileName((x as SpectrumSourceGroupRow).SpectrumSourceGroup.Name) + '/';
                else if (x is SpectrumSourceRow)
                    return (x as SpectrumSourceRow).SpectrumSource.Name;
                else if (x is SpectrumRow)
                    try { return pwiz.CLI.msdata.id.abbreviate((x as SpectrumRow).Spectrum.NativeID); }
                    catch { return (x as SpectrumRow).Spectrum.NativeID; }
                else if (x is PeptideSpectrumMatchRow && topRankOnlyCheckBox.Checked)
                    try { return pwiz.CLI.msdata.id.abbreviate((x as PeptideSpectrumMatchRow).Spectrum.NativeID); }
                    catch { return (x as PeptideSpectrumMatchRow).Spectrum.NativeID; }
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Rank;
                return null;
            };

            totalSpectraColumn.AspectGetter += delegate(object x)
            {
                return null;
            };

            confidentSpectraColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return (x as SpectrumSourceGroupRow).Spectra;
                else if (x is SpectrumSourceRow)
                    return (x as SpectrumSourceRow).Spectra;
                return null;
            };

            confidentPeptidesColumn.AspectGetter += delegate(object x)
            {
                if (x is SpectrumSourceGroupRow)
                    return (x as SpectrumSourceGroupRow).DistinctPeptides;
                else if (x is SpectrumSourceRow)
                    return (x as SpectrumSourceRow).DistinctPeptides;
                return null;
            };

            if ((int)_columnSettings[precursorMzColumn][1] == -1)
            {
                precursorMzColumn.AspectGetter += delegate(object x)
                {
                    if (x is SpectrumRow)
                        return (x as SpectrumRow).Spectrum.PrecursorMZ;
                    else if (x is PeptideSpectrumMatchRow)
                        return (x as PeptideSpectrumMatchRow).Spectrum.PrecursorMZ;
                    return null;
                };
            }
            else
            {
                precursorMzColumn.AspectGetter += delegate(object x)
                {
                    if (x is SpectrumRow)
                        return Math.Round((x as SpectrumRow).Spectrum.PrecursorMZ, (int)_columnSettings[precursorMzColumn][1]);
                    else if (x is PeptideSpectrumMatchRow)
                        return Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Spectrum.PrecursorMZ, (int)_columnSettings[precursorMzColumn][1]);
                    return null;
                };
            }

            chargeColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Charge;
                return null;
            };

            if ((int)_columnSettings[observedMassColumn][1] == -1)
            {
                observedMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideSpectrumMatchRow)
                    {
                        var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                        return psm.Spectrum.PrecursorMZ * psm.Charge - psm.Charge * pwiz.CLI.chemistry.Proton.Mass;
                    }
                    return null;
                };
            }
            else
            {
                observedMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideSpectrumMatchRow)
                    {
                        var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                        return Math.Round(psm.Spectrum.PrecursorMZ * psm.Charge - psm.Charge * pwiz.CLI.chemistry.Proton.Mass, (int)_columnSettings[observedMassColumn][1]);
                    }
                    return null;
                };
            }

            if ((int)_columnSettings[exactMassColumn][1] == -1)
            {
                exactMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideSpectrumMatchRow)
                        return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass;
                    return null;
                };
            }
            else
            {
                exactMassColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideSpectrumMatchRow)
                        return Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass, (int)_columnSettings[exactMassColumn][1]);
                    return null;
                };
            }

            if ((int)_columnSettings[massErrorColumn][1] == -1)
            {
                massErrorColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideSpectrumMatchRow)
                        return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMassError;
                    return null;
                };
            }
            else
            {
                massErrorColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideSpectrumMatchRow)
                        return Math.Round((x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMassError, (int)_columnSettings[massErrorColumn][1]);
                    return null;
                };
            }

            if ((int)_columnSettings[qvalueColumn][1] == -1)
            {
                qvalueColumn.AspectGetter += delegate(object x)
                {
                    if (x is PeptideSpectrumMatchRow)
                    {
                        var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                        return psm.Rank > 1 ? "n/a" : psm.QValue.ToString();
                    }
                    return null;
                };
            }
            else
            {
                qvalueColumn.AspectGetter += delegate(object x)
                {
                    string whereClause = dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch, DataFilter.PeptideSpectrumMatchToModification);
                    whereClause += whereClause.Contains("WHERE") ? "AND" : "WHERE";
                    if (x is SpectrumRow)
                        whereClause += " psm.Spectrum.id = " + (x as SpectrumRow).Spectrum.Id;
                    else
                        whereClause += " psm.Spectrum.Source.id = " + (x as SpectrumSourceRow).SpectrumSource.Id;
                    if (x is PeptideSpectrumMatchRow)
                    {
                        var psm = (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch;
                        return psm.Rank > 1 ? "n/a" : Math.Round(psm.QValue, (int)_columnSettings[qvalueColumn][1]).ToString();
                    }
                    return null;
                };
            }

                //    return session.CreateQuery("SELECT DISTINCT psm, psm.Spectrum " + whereClause)
                //                  .List<object[]>()
                //                  .Select(o => new PeptideSpectrumMatchRow(o));
                //}

            sequenceColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.ToModifiedString();
                return null;
            };

            treeListView.Refresh();
        }

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
            object deepestExpandedItem = null;
            for (int i = 0; i < treeListView.Items.Count; ++i)
            {
                var item = treeListView.Items[i] as OLVListItem;

                if (treeListView.IsExpanded(item.RowObject) &&
                    !(item.RowObject is SpectrumSourceGroupRow &&
                    deepestExpandedItem is SpectrumSourceRow))
                    deepestExpandedItem = item.RowObject;

                // break iteration once maximum depth is reached
                if (deepestExpandedItem is SpectrumRow ||
                    (deepestExpandedItem is SpectrumSourceRow && topRankOnlyCheckBox.Checked))
                    break;
            }

            bool showAggregateColumns = deepestExpandedItem == null || deepestExpandedItem is SpectrumSourceGroupRow;
            bool showSpectrumColumns = deepestExpandedItem is SpectrumSourceRow;
            bool showPsmColumns = deepestExpandedItem is SpectrumRow ||
                                  (deepestExpandedItem is SpectrumSourceRow && topRankOnlyCheckBox.Checked);

            totalSpectraColumn.IsVisible = false;// showAggregateColumns;
            if (!(bool)_columnSettings[confidentPeptidesColumn][4])
                confidentPeptidesColumn.IsVisible = showAggregateColumns;
            if (!(bool)_columnSettings[confidentSpectraColumn][4])
                confidentSpectraColumn.IsVisible = showAggregateColumns;
            if (!(bool)_columnSettings[precursorMzColumn][4])
                precursorMzColumn.IsVisible = showSpectrumColumns;
            if (!(bool)_columnSettings[chargeColumn][4])
                chargeColumn.IsVisible = showPsmColumns;
            if (!(bool)_columnSettings[observedMassColumn][4])
                observedMassColumn.IsVisible = showPsmColumns;
            if (!(bool)_columnSettings[exactMassColumn][4])
                exactMassColumn.IsVisible = showPsmColumns;
            if (!(bool)_columnSettings[massErrorColumn][4])
                massErrorColumn.IsVisible = showPsmColumns;
            if (!(bool)_columnSettings[qvalueColumn][4])
                qvalueColumn.IsVisible = showPsmColumns;
            if (!(bool)_columnSettings[sequenceColumn][4])
                sequenceColumn.IsVisible = showPsmColumns;

            treeListView.RebuildColumns();
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

        public event EventHandler<DataFilter> SpectrumViewFilter;
        public event EventHandler<SpectrumViewVisualizeEventArgs> SpectrumViewVisualize;

        private NHibernate.ISession session = null;
        private DataFilter dataFilter, basicDataFilter;
        private IList<SpectrumSourceGroupRow> rowsByGroup, basicRowsByGroup;
        private IList<SpectrumSourceRow> rowsBySource, basicRowsBySource;

        // TODO: support multiple selected objects
        List<string> oldSelectionPath = new List<string>();

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            if (session == null)
                return;

            this.session = session;
            this.dataFilter = new DataFilter(dataFilter) {Spectrum = null, SpectrumSource = null, SpectrumSourceGroup = null};

            if (treeListView.SelectedObject is SpectrumSourceGroupRow)
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
            }

            ClearData();

            Text = TabText = "Loading spectrum view...";

            //set column settings if they are set for the database
            IList<idpDBSettings> listOfSettings = new List<idpDBSettings>();
            try
            {
                listOfSettings = session.QueryOver<idpDBSettings>().Where(x => x.FormName == "SpectrumTableForm").List<idpDBSettings>();
            }
            catch
            {
                //sometimes throws "There was a problem converting an IDataReader to NDataReader" on filter attempt
                //does not appear to have any effect. Swallow error for now unless it turns out to be part of bigger problem;
            }

            if (listOfSettings.Count > 0)
            {
                _columnSettings = new Dictionary<OLVColumn, object[]>();

                SetPropertyFromDatabase(ref _columnSettings, sourceOrScanColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, totalSpectraColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, confidentSpectraColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, confidentPeptidesColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, precursorMzColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, chargeColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, observedMassColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, exactMassColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, massErrorColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, qvalueColumn, listOfSettings);
                SetPropertyFromDatabase(ref _columnSettings, sequenceColumn, listOfSettings);

                var backColor = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__BackColor").SingleOrDefault();
                var textColor = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__TextColor").SingleOrDefault();
                treeListView.BackColor = Color.FromArgb(backColor.ColorCode);
                treeListView.ForeColor = Color.FromArgb(textColor.ColorCode);

                foreach (var kvp in _columnSettings)
                    kvp.Key.IsVisible = (bool)kvp.Value[3];
                treeListView.RebuildColumns();
            }

            var workerThread = new BackgroundWorker()
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };

            workerThread.DoWork += new DoWorkEventHandler(setData);
            workerThread.RunWorkerCompleted += new RunWorkerCompletedEventHandler(renderData);
            workerThread.RunWorkerAsync();
        }

        public void ClearData ()
        {
            Text = TabText = "Spectrum View";

            treeListView.DiscardAllState();
            treeListView.Roots = null;
            treeListView.Refresh();
            Refresh();
        }
            

        void setData(object sender, DoWorkEventArgs e)
        {
            lock(session)
            try
            {
                var groupQuery = session.CreateQuery("SELECT ssgl, COUNT(DISTINCT psm.Spectrum.id), COUNT(DISTINCT psm.Peptide.id), COUNT(DISTINCT psm.FullDistinctKey) " +
                                                     this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                                                            DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink) +
                                                     "GROUP BY ssgl.Group.id");

                var sourceQuery = session.CreateQuery("SELECT psm.Spectrum.Source, COUNT(DISTINCT psm.Spectrum.id), COUNT(DISTINCT psm.Peptide.id), COUNT(DISTINCT psm.FullDistinctKey) " +
                                                      this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                      "GROUP BY psm.Spectrum.Source.id");

                groupQuery.SetReadOnly(true);
                sourceQuery.SetReadOnly(true);

                if (dataFilter.IsBasicFilter ||
                    dataFilter.SpectrumSourceGroup != null ||
                    dataFilter.SpectrumSource != null ||
                    dataFilter.Spectrum != null)
                {
                    if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                    {
                        basicDataFilter = new DataFilter(this.dataFilter);
                        basicRowsByGroup = groupQuery.List<object[]>().Select(o => new SpectrumSourceGroupRow(o)).ToList();
                        basicRowsBySource = sourceQuery.List<object[]>().Select(o => new SpectrumSourceRow(o)).ToList();
                    }

                    rowsByGroup = basicRowsByGroup;
                    rowsBySource = basicRowsBySource;
                }
                else
                {
                    rowsByGroup = groupQuery.List<object[]>().Select(o => new SpectrumSourceGroupRow(o)).ToList();
                    rowsBySource = sourceQuery.List<object[]>().Select(o => new SpectrumSourceRow(o)).ToList();
                }
            }
            catch (Exception)
            {
                throw;
            }
        }

        void renderData (object sender, RunWorkerCompletedEventArgs e)
        {
            SpectrumSourceGroup rootGroup;
            lock (session) rootGroup = session.Query<SpectrumSourceGroup>().Where(o => o.Name == "/").Single();
            var rootGroupRow = rowsByGroup.Where(o => o.SpectrumSourceGroup.Id == rootGroup.Id).Single();

            long totalSpectrumCount = rootGroupRow.Spectra;
            long totalPeptideCount = rootGroupRow.DistinctPeptides;

            // show total counts in the form title
            Text = TabText = String.Format("Spectrum View: {0} groups, {1} sources, {2} spectra, {3} peptides", rowsByGroup.Count, rowsBySource.Count, totalSpectrumCount, totalPeptideCount);

            treeListView.Roots = new object[] {rootGroupRow};

            // by default, expand all groups
            foreach (var row in rowsByGroup)
                treeListView.Expand(row);

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

        private void topRankOnlyCheckBox_CheckedChanged (object sender, EventArgs e)
        {
            SetData(session, dataFilter);
            Properties.Settings.Default.TopRankOnly = topRankOnlyCheckBox.Checked;
            Properties.Settings.Default.Save();
        }

        private void displayOptionsButton_Click(object sender, EventArgs e)
        {
            Color[] currentColors = { treeListView.BackColor, treeListView.ForeColor };

            foreach (var kvp in _columnSettings)
                kvp.Value[3] = kvp.Key.IsVisible;

            var ccf = new ColumnControlForm(_columnSettings,currentColors);

            if (ccf.ShowDialog() == DialogResult.OK)
            {
                _columnSettings = ccf._savedSettings;

                foreach (var kvp in _columnSettings)
                    if ((bool)kvp.Value[4] == true)
                        kvp.Key.IsVisible = (bool)kvp.Value[3];

                treeListView.BackColor = ccf.WindowBackColorBox.BackColor;
                treeListView.ForeColor = ccf.WindowTextColorBox.BackColor;

                SetColumnAspectGetters();
                treeListView.RebuildColumns();
            }
        }

        private void SetPropertyFromUserSettings(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, ref List<string> columnProperties)
        {
            for (int x = 0; x < columnProperties.Count - 6; x += 6)
            {
                if (columnProperties[x] == "Q Key")
                    columnProperties[x] = "Q Value";

                if (columnProperties[x] == targetColumn.Text)
                {
                    testDictionary.Add(targetColumn,
                        new object[5]{columnProperties[x+1],int.Parse(columnProperties[x+2]),
                            Color.FromArgb(int.Parse(columnProperties[x+3])),
                            bool.Parse(columnProperties[x+4]), bool.Parse(columnProperties[x+5])});

                    columnProperties.RemoveRange(x, 6);
                    break;
                }
            }
        }

        private void SetPropertyFromDatabase(ref Dictionary<OLVColumn, object[]> testDictionary, OLVColumn targetColumn, IList<idpDBSettings> FormSettings)
        {
            idpDBSettings rowSettings = FormSettings.Where<idpDBSettings>(x => x.ColumnName == targetColumn.Text).SingleOrDefault();

            testDictionary.Add(targetColumn,
                new object[5]{rowSettings.Type, rowSettings.DecimalPlaces,
                    Color.FromArgb(rowSettings.ColorCode),
                    rowSettings.Visible, rowSettings.Locked});

        }

        public void SaveSettings()
        {
            //save current column settings (In case user has changed columns using right-click menu
            foreach (var kvp in _columnSettings)
                kvp.Value[3] = false;
            foreach (OLVColumn column in treeListView.ColumnsInDisplayOrder)
                _columnSettings[column][3] = true;

            if (session == null)
            {
                //Store settings dictionary as string list
                //{Text of key, (string)Value[0], (string?)Value[1], Name of Color Value[2], (string)Value[3]}
                //Therefore the list will be translated back in sets of 5

                var storedList = new System.Collections.Specialized.StringCollection();

                foreach (var kvp in _columnSettings)
                {
                    storedList.Add(kvp.Key.Text);
                    storedList.Add(kvp.Value[0].ToString());
                    storedList.Add(kvp.Value[1].ToString());
                    storedList.Add(((Color)kvp.Value[2]).ToArgb().ToString());
                    storedList.Add(kvp.Value[3].ToString());
                    storedList.Add(kvp.Value[4].ToString());
                }

                storedList.Add(treeListView.BackColor.ToArgb().ToString());
                storedList.Add(treeListView.ForeColor.ToArgb().ToString());

                Properties.Settings.Default.SpectrumTableFormSettings = storedList;
                Properties.Settings.Default.Save();


            }
            else
            {
                var listOfSettings = session.QueryOver<idpDBSettings>().Where(x => x.FormName == "SpectrumTableForm").List();

                if (listOfSettings.Count > 0)
                {
                    foreach (var kvp in _columnSettings)
                    {
                        var settingRow = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == kvp.Key.Text).SingleOrDefault<idpDBSettings>();

                        settingRow.DecimalPlaces = (int)kvp.Value[1];
                        settingRow.ColorCode = ((Color)kvp.Value[2]).ToArgb();
                        settingRow.Visible = (bool)kvp.Value[3];
                        settingRow.Locked = (bool)kvp.Value[4];
                        session.Save(settingRow);
                    }

                    var formBackColorRow = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__BackColor").SingleOrDefault<idpDBSettings>();
                    var formTextColorRow = listOfSettings.Where<idpDBSettings>(x => x.ColumnName == "__TextColor").SingleOrDefault<idpDBSettings>();

                    formBackColorRow.ColorCode = treeListView.BackColor.ToArgb();
                    formTextColorRow.ColorCode = treeListView.ForeColor.ToArgb();
                    session.Save(formBackColorRow);
                    session.Save(formTextColorRow);
                }
                else
                {
                    foreach (var kvp in _columnSettings)
                    {
                        session.Save(new idpDBSettings()
                        {
                            FormName = "SpectrumTableForm",
                            ColumnName = kvp.Key.Text,
                            Type = kvp.Value[0].ToString(),
                            DecimalPlaces = (int)kvp.Value[1],
                            ColorCode = ((Color)kvp.Value[2]).ToArgb(),
                            Visible = (bool)kvp.Value[3],
                            Locked = (bool)kvp.Value[4]
                        });
                    }

                    session.Save(new idpDBSettings()
                    {
                        FormName = "SpectrumTableForm",
                        ColumnName = "__BackColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = treeListView.BackColor.ToArgb(),
                        Visible = false,
                        Locked = false
                    });
                    session.Save(new idpDBSettings()
                    {
                        FormName = "SpectrumTableForm",
                        ColumnName = "__TextColor",
                        Type = "GlobalSetting",
                        DecimalPlaces = -1,
                        ColorCode = treeListView.ForeColor.ToArgb(),
                        Visible = false,
                        Locked = false
                    });
                }

                session.Flush();
            }
        }

    }

    public class SpectrumViewVisualizeEventArgs : EventArgs
    {
        public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; internal set; }
    }
}
