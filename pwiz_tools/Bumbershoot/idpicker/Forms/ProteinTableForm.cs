//
// $Id: $
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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using BrightIdeasSoftware;

namespace IDPicker.Forms
{
    public partial class ProteinTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public class ProteinGroupRow
        {
            public string Proteins { get; private set; }
            public long DistinctPeptides { get; private set; }
            public long DistinctMatches { get; private set; }
            public long Spectra { get; private set; }
            public string ProteinGroup { get; private set; }
            public long FirstProteinId { get; private set; }
            public string FirstProteinDescription { get; set; }
            public long ProteinCount { get; private set; }
            public long Cluster { get; private set; }

            #region Constructor
            public ProteinGroupRow (object[] queryRow)
            {
                Proteins = (string) queryRow[0];
                DistinctPeptides = (long) queryRow[1];
                DistinctMatches = (long) queryRow[2];
                Spectra = (long) queryRow[3];
                ProteinGroup = (string) queryRow[4];
                FirstProteinId = (long) queryRow[5];
                FirstProteinDescription = (string) queryRow[6];
                ProteinCount = (long) queryRow[7];
                Cluster = (long) queryRow[8];
            }
            #endregion
        }

        public class ProteinRow
        {
            public DataModel.Protein Protein { get; private set; }

            #region Constructor
            public ProteinRow (object x)
            {
                Protein = (DataModel.Protein) x;
            }
            #endregion
        }

        public class ProteinStats
        {
            public long DistinctPeptides { get; private set; }
            public long Spectra { get; private set; }

            #region Constructor
            public ProteinStats () { }
            public ProteinStats (object[] queryRow)
            {
                DistinctPeptides = (long) queryRow[2];
                Spectra = (long) queryRow[3];
            }
            #endregion
        }

        public ProteinTableForm ()
        {
            InitializeComponent();

            HideOnClose = true;

            #region Column aspect getters
            clusterColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).Cluster;
                return null;
            };

            accessionColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).Proteins;
                else if (x is ProteinRow)
                    return (x as ProteinRow).Protein.Accession;
                return null;
            };

            proteinCountColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).ProteinCount;
                return null;
            };

            filteredPeptidesColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).DistinctPeptides;
                return null;
            };
            
            filteredVariantsColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).DistinctMatches;
                return null;
            };

            filteredSpectraColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow)
                    return (x as ProteinGroupRow).Spectra;
                return null;
            };

            descriptionColumn.AspectGetter += delegate(object x)
            {
                if (x is ProteinGroupRow && (x as ProteinGroupRow).ProteinCount == 1)
                    return (x as ProteinGroupRow).FirstProteinDescription;
                else if (x is ProteinRow)
                    return (x as ProteinRow).Protein.Description;
                return null;
            };
            #endregion

            treeListView.CanExpandGetter += delegate(object x) { return x is ProteinGroupRow && (x as ProteinGroupRow).ProteinCount > 1; };
            treeListView.ChildrenGetter += delegate(object x)
            {
                return session.CreateQuery(
                    String.Format("SELECT pro FROM Protein pro WHERE pro.Accession IN ('{0}')",
                                  (x as ProteinGroupRow).Proteins.Replace(",", "','")))
                              .List<object>().Select(o => new ProteinRow(o));
            };
            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            treeListView.HyperlinkClicked += new EventHandler<HyperlinkClickedEventArgs>(treeListView_HyperlinkClicked);
            treeListView.HyperlinkStyle.Normal.ForeColor = SystemColors.WindowText;
            treeListView.HyperlinkStyle.Visited.ForeColor = SystemColors.WindowText;
        }

        void treeListView_HyperlinkClicked (object sender, HyperlinkClickedEventArgs e)
        {
            if (ReferenceEquals(e.Column, clusterColumn))
            {
                e.Handled = true; // do not treat as a URL

                var newDataFilter = new DataFilter()
                {
                    MaximumQValue = dataFilter.MaximumQValue,
                    FilterSource = this
                };

                newDataFilter.Cluster = (e.Item.RowObject as ProteinGroupRow).Cluster;
                if (ProteinViewFilter != null)
                    ProteinViewFilter(this, newDataFilter);
            }
        }

        void treeListView_CellClick (object sender, CellClickEventArgs e)
        {
            if (e.ClickCount < 2 || e.Item == null || e.Item.RowObject == null)
                return;

            var newDataFilter = new DataFilter()
            {
                MaximumQValue = dataFilter.MaximumQValue,
                FilterSource = this
            };

            if (e.Item.RowObject is ProteinGroupRow)
                newDataFilter.Protein = session.Get<DataModel.Protein>((e.Item.RowObject as ProteinGroupRow).FirstProteinId);
            else if (e.Item.RowObject is ProteinRow)
                newDataFilter.Protein = (e.Item.RowObject as ProteinRow).Protein;

            if (ProteinViewFilter != null)
                ProteinViewFilter(this, newDataFilter);
        }

        public event ProteinViewFilterEventHandler ProteinViewFilter;

        private NHibernate.ISession session;
        private DataFilter dataFilter, basicDataFilter;
        private IList<ProteinGroupRow> rowsByProteinGroup, basicRowsByProteinGroup;

        private List<OLVColumn> pivotColumns = new List<OLVColumn>();
        private Map<long, Map<long, ProteinStats>> statsPerProteinGroupBySpectrumSource;
        private Map<long, Map<long, ProteinStats>> statsPerProteinGroupBySpectrumSourceGroup;

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            this.dataFilter = new DataFilter(dataFilter) { Protein = null };

            var proteinGroupQuery = session.CreateQuery(
                "SELECT DISTINCT_GROUP_CONCAT(pro.Accession), " +
                "       COUNT(DISTINCT psm.Peptide.id), " +
                "       COUNT(DISTINCT psm.id), " +
                "       COUNT(DISTINCT psm.Spectrum.id), " +
                "       pro.ProteinGroup, " +
                "       MIN(pro.Id), " +
                "       MIN(pro.Description), " +
                "       COUNT(DISTINCT pro.Id), " +
                "       pro.Cluster " +
                this.dataFilter.GetFilteredQueryString(DataFilter.FromProtein,
                                                       DataFilter.ProteinToPeptideSpectrumMatch) +
                "GROUP BY pro.ProteinGroup " +
                "ORDER BY COUNT(DISTINCT psm.Peptide.id) DESC, COUNT(DISTINCT psm.id) DESC, COUNT(DISTINCT psm.Spectrum.id) DESC");

            var statsPerProteinGroupBySpectrumSourceQuery = session.CreateQuery(
                "SELECT DISTINCT_GROUP_CONCAT(pro.Accession), " +
                "       s.Source.id, " +
                "       COUNT(DISTINCT psm.Peptide), " +
                "       COUNT(DISTINCT psm.Spectrum), " +
                "       MIN(pro.id) " +
                this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                       DataFilter.PeptideSpectrumMatchToSpectrumSource,
                                                       DataFilter.PeptideSpectrumMatchToProtein) +
                "GROUP BY pro.ProteinGroup, s.Source.id");

            var statsPerProteinGroupBySpectrumSourceGroupQuery = session.CreateQuery(
                "SELECT DISTINCT_GROUP_CONCAT(pro.Accession), " +
                "       ssgl.Group.id, " +
                "       COUNT(DISTINCT psm.Peptide.id), " +
                "       COUNT(DISTINCT psm.Spectrum.id), " +
                "       MIN(pro.id) " +
                this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch,
                                                       DataFilter.PeptideSpectrumMatchToSpectrumSourceGroupLink,
                                                       DataFilter.PeptideSpectrumMatchToProtein) +
                "GROUP BY pro.ProteinGroup, ssgl.Group");

            var stats = statsPerProteinGroupBySpectrumSource = new Map<long, Map<long,ProteinStats>>();
            foreach (var queryRow in statsPerProteinGroupBySpectrumSourceQuery.List<object[]>())
                stats[(long) queryRow[1]][(long) queryRow[4]] = new ProteinStats(queryRow);

            var stats2 = statsPerProteinGroupBySpectrumSourceGroup = new Map<long, Map<long, ProteinStats>>();
            foreach (var queryRow in statsPerProteinGroupBySpectrumSourceGroupQuery.List<object[]>())
                stats2[(long) queryRow[1]][(long) queryRow[4]] = new ProteinStats(queryRow);

            treeListView.Freeze();
            foreach (var pivotColumn in pivotColumns)
                treeListView.Columns.Remove(pivotColumn);

            IList<string> sourceNames = session.QueryOver<DataModel.SpectrumSource>().Select(o => o.Name).List<string>();
            pivotColumns = new List<OLVColumn>();

            foreach (long sourceId in stats.Keys)
            {
                string uniqueSubstring;
                Util.UniqueSubstring(session.Get<DataModel.SpectrumSource>(sourceId).Name, sourceNames, out uniqueSubstring);
                var column = new OLVColumn() { Text = uniqueSubstring, Tag = sourceId };
                column.AspectGetter += delegate(object x)
                                       {
                                           if (x is ProteinGroupRow &&
                                               stats[(long) column.Tag].Contains((x as ProteinGroupRow).FirstProteinId))
                                                return stats[(long) column.Tag][(x as ProteinGroupRow).FirstProteinId].DistinctPeptides;
                                           return null;
                                       };
                pivotColumns.Add(column);
                treeListView.Columns.Insert(descriptionColumn.Index, column);
            }

            foreach (long groupId in stats2.Keys)
            {
                var column = new OLVColumn() { Text = session.Get<DataModel.SpectrumSourceGroup>(groupId).Name, Tag = groupId };
                column.AspectGetter += delegate(object x)
                {
                    if (x is ProteinGroupRow &&
                        stats2[(long) column.Tag].Contains((x as ProteinGroupRow).FirstProteinId))
                        return stats2[(long) column.Tag][(x as ProteinGroupRow).FirstProteinId].DistinctPeptides;
                    return null;
                };
                pivotColumns.Add(column);
                treeListView.Columns.Insert(descriptionColumn.Index, column);
            }
            treeListView.Unfreeze();

            if (dataFilter.IsBasicFilter || dataFilter.Protein != null)
            {
                if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                {
                    basicDataFilter = new DataFilter(this.dataFilter);
                    basicRowsByProteinGroup = proteinGroupQuery.List<object[]>().Select(o => new ProteinGroupRow(o)).ToList();
                }

                rowsByProteinGroup = basicRowsByProteinGroup;
            }
            else
            {
                rowsByProteinGroup = proteinGroupQuery.List<object[]>().Select(o => new ProteinGroupRow(o)).ToList();
            }

            long totalProteins = rowsByProteinGroup.Sum(o => o.ProteinCount);

            // show total counts in the form title
            Text = TabText = String.Format("Protein View: {0} protein groups, {1} proteins", rowsByProteinGroup.Count, totalProteins);

            // TODO: support multiple selected objects
            string[] oldSelectionPath = new string[] { };

            if(treeListView.SelectedObject is ProteinGroupRow)
            {
                oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
            }
            else if(treeListView.SelectedObject is ProteinRow)
            {
                var proteinGroup = (treeListView.GetParent(treeListView.SelectedObject) as ProteinGroupRow).Proteins;
                oldSelectionPath = new string[] { proteinGroup, treeListView.SelectedItem.Text };
            }

            treeListView.DiscardAllState();
            treeListView.Roots = rowsByProteinGroup;

            // try to (re)set selected item
            OLVListItem selectedItem = null;
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
            }

            treeListView.Refresh();
        }
    }

    public delegate void ProteinViewFilterEventHandler (ProteinTableForm sender, DataFilter proteinViewFilter);
}
