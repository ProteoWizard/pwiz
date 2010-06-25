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
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using NHibernate.Linq;
using IDPicker.DataModel;
using BrightIdeasSoftware;

namespace IDPicker.Forms
{
    public partial class PeptideTableForm : DockableForm
    {
        public TreeListView TreeListView { get { return treeListView; } }

        public class PeptideRow
        {
            public DataModel.Peptide Peptide { get; private set; }
            public long DistinctMatchesWithRoundedMass { get; private set; }
            public long Spectra { get; private set; }

            #region Constructor
            public PeptideRow (object[] queryRow)
            {
                Peptide = (DataModel.Peptide) queryRow[0];
                DistinctMatchesWithRoundedMass = (long) queryRow[1];
                Spectra = (long) queryRow[2];
            }
            #endregion
        }

        public class PeptideSpectrumMatchRow
        {
            public DataModel.PeptideSpectrumMatch PeptideSpectrumMatch { get; private set; }
            public long Spectra { get; private set; }
            public DistinctPeptideFormat DistinctPeptide { get; private set; }

            #region Constructor
            public PeptideSpectrumMatchRow (object[] queryRow)
            {
                PeptideSpectrumMatch = (DataModel.PeptideSpectrumMatch) queryRow[0];
                DistinctPeptide = new DistinctPeptideFormat("(psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))",
                                                            PeptideSpectrumMatch.ToModifiedString(),
                                                            (string) queryRow[1]);
                Spectra = (long) queryRow[2];
            }
            #endregion
        }

        public class PeptideInstanceRow
        {
            public DataModel.PeptideInstance PeptideInstance { get; private set; }

            #region Constructor
            public PeptideInstanceRow (object queryRow)
            {
                PeptideInstance = (DataModel.PeptideInstance) queryRow;
            }
            #endregion
        }

        public PeptideTableForm ()
        {
            InitializeComponent();

            HideOnClose = true;

            #region Column aspect getters
            sequenceColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Peptide.Sequence;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).DistinctPeptide.Sequence;
                else if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.Protein.Accession;
                return null;
            };

            filteredVariantsColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).DistinctMatchesWithRoundedMass;
                return null;
            };

            filteredSpectraColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Spectra;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).Spectra;
                return null;
            };

            monoisotopicMassColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Peptide.MonoisotopicMass;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MonoisotopicMass;
                return null;
            };

            molecularWeightColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideRow)
                    return (x as PeptideRow).Peptide.MolecularWeight;
                else if (x is PeptideSpectrumMatchRow)
                    return (x as PeptideSpectrumMatchRow).PeptideSpectrumMatch.MolecularWeight;
                return null;
            };

            offsetColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.Offset;
                return null;
            };

            terminalSpecificityColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                {
                    var specificTermini = new List<string>();
                    if ((x as PeptideInstanceRow).PeptideInstance.NTerminusIsSpecific)
                        specificTermini.Add("N");
                    if ((x as PeptideInstanceRow).PeptideInstance.CTerminusIsSpecific)
                        specificTermini.Add("C");
                    if (specificTermini.Count == 0)
                        specificTermini.Add("None");
                    return String.Join(",", specificTermini.ToArray());
                }
                return null;
            };

            missedCleavagesColumn.AspectGetter += delegate(object x)
            {
                if (x is PeptideInstanceRow)
                    return (x as PeptideInstanceRow).PeptideInstance.MissedCleavages;
                return null;
            };

            #endregion

            treeListView.CanExpandGetter += delegate(object x) { return x is PeptideRow; };
            treeListView.ChildrenGetter += delegate(object x)
            {
                dataFilter.Peptide = (x as PeptideRow).Peptide;
                object result;
                if (radioButton1.Checked)
                    result = session.CreateQuery("SELECT psm, (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass)), COUNT(DISTINCT psm.Spectrum) " +
                                                 dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                 "GROUP BY (psm.Peptide || ' ' || ROUND(psm.MonoisotopicMass))")
                                    .List<object[]>().Select(o => new PeptideSpectrumMatchRow(o));
                else
                    return session.CreateQuery("SELECT DISTINCT psm.Peptide.Instances " +
                                               dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch)/* +
                                               " GROUP BY psm.Peptide"*/
                                                                        )
                                  .List<object>().Select(o => new PeptideInstanceRow(o));

                dataFilter.Peptide = null;
                return result as System.Collections.IEnumerable;
            };

            treeListView.CellClick += new EventHandler<CellClickEventArgs>(treeListView_CellClick);

            radioButton1.CheckedChanged += new EventHandler(radioButton1_CheckedChanged);
        }

        void radioButton1_CheckedChanged (object sender, EventArgs e)
        {
            filteredSpectraColumn.IsVisible = radioButton1.Checked;
            filteredVariantsColumn.IsVisible = radioButton1.Checked;
            monoisotopicMassColumn.IsVisible = radioButton1.Checked;
            molecularWeightColumn.IsVisible = radioButton1.Checked;
            offsetColumn.IsVisible = radioButton2.Checked;
            terminalSpecificityColumn.IsVisible = radioButton2.Checked;
            missedCleavagesColumn.IsVisible = radioButton2.Checked;
            treeListView.RebuildColumns();
            SetData(session, dataFilter);
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

            if (e.Item.RowObject is PeptideRow)
                newDataFilter.Peptide = (e.Item.RowObject as PeptideRow).Peptide;
            else if (e.Item.RowObject is PeptideSpectrumMatchRow)
                newDataFilter.DistinctPeptideKey = (e.Item.RowObject as PeptideSpectrumMatchRow).DistinctPeptide;

            if (PeptideViewFilter != null)
                PeptideViewFilter(this, newDataFilter);
        }

        public event PeptideViewFilterEventHandler PeptideViewFilter;

        private NHibernate.ISession session;
        private DataFilter dataFilter, basicDataFilter;
        private IList<PeptideRow> rowsByPeptide, basicRowsByPeptide;

        public void SetData (NHibernate.ISession session, DataFilter dataFilter)
        {
            this.session = session;
            this.dataFilter = new DataFilter(dataFilter) { Peptide = null };

            var peptideQuery = session.CreateQuery("SELECT psm.Peptide, " +
                                                   "       COUNT(DISTINCT psm.SequenceAndMassDistinctKey), " +
                                                   "       COUNT(DISTINCT psm.Spectrum) " +
                                                   this.dataFilter.GetFilteredQueryString(DataFilter.FromPeptideSpectrumMatch) +
                                                   "GROUP BY psm.Peptide " +
                                                   "ORDER BY COUNT(DISTINCT psm.SequenceAndMassDistinctKey) DESC, COUNT(DISTINCT psm.Spectrum) DESC");

            if (dataFilter.IsBasicFilter || dataFilter.Peptide != null || dataFilter.DistinctPeptideKey != null)
            {
                if (basicDataFilter == null || (dataFilter.IsBasicFilter && dataFilter != basicDataFilter))
                {
                    basicDataFilter = new DataFilter(this.dataFilter);
                    basicRowsByPeptide = peptideQuery.List<object[]>().Select(o => new PeptideRow(o)).ToList();
                }

                rowsByPeptide = basicRowsByPeptide;
            }
            else
                rowsByPeptide = peptideQuery.List<object[]>().Select(o => new PeptideRow(o)).ToList();

            long totalDistinctMatches = rowsByPeptide.Sum(o => o.DistinctMatchesWithRoundedMass);
            long totalSpectra = rowsByPeptide.Sum(o => o.Spectra);

            // show total counts in the form title
            Text = TabText = String.Format("Peptide View: {0} peptides, {1} distinct matches, {2} spectra", rowsByPeptide.Count, totalDistinctMatches, totalSpectra);

            // TODO: support multiple selected objects
            string[] oldSelectionPath = new string[] { };
            if (treeListView.SelectedObject is object[])
            {
                if (treeListView.SelectedObject is PeptideRow)
                    oldSelectionPath = new string[] { treeListView.SelectedItem.Text };
                else if (treeListView.SelectedObject is PeptideSpectrumMatchRow)
                    oldSelectionPath = new string[] { (treeListView.SelectedObject as PeptideSpectrumMatchRow).PeptideSpectrumMatch.Peptide.Sequence, treeListView.SelectedItem.Text };
            }

            treeListView.DiscardAllState();
            treeListView.Roots = rowsByPeptide;

            // try to (re)set selected item
            expandSelectionPath(oldSelectionPath);

            treeListView.Refresh();
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
    }

    public delegate void PeptideViewFilterEventHandler (PeptideTableForm sender, DataFilter peptideViewFilter);
}
