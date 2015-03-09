/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Model.DocSettings;
using pwiz.Skyline.Model.Hibernate.Query;
using pwiz.Skyline.Properties;
using pwiz.Skyline.Util;

namespace pwiz.Skyline.FileUI
{
    /// <summary>
    /// Dialog which allows the user to choose columns from the hierarchy from
    /// DbProtein to DbTransition.
    /// </summary>
    public partial class PivotReportDlg : FormEx
    {
        private ReportSpec _reportSpec;
        private readonly IEnumerable<ReportSpec> _existing;

        private Database _database;
        private ColumnSet _columnSet;

        private readonly List<NodeData> _columns = new List<NodeData>();

        public PivotReportDlg(IEnumerable<ReportSpec> existing)
        {
            _existing = existing;

            InitializeComponent();

            Icon = Resources.Skyline;
        }

        public string ReportName
        {
            get { return textName.Text; }
            set { textName.Text = value; }
        }

        public bool PivotReplicate
        {
            get { return cbxPivotReplicate.Checked; }
            set { cbxPivotReplicate.Checked = value; }
        }

        public bool PivotIsotopeLabelType
        {
            get { return cbxPivotIsotopeLabel.Checked; }
            set { cbxPivotIsotopeLabel.Checked = value; }
        }

        public int ColumnCount { get { return _columns.Count; } }

        public IEnumerable<string> ColumnNames
        {
            get
            {
                foreach (var column in _columns)
                    yield return column.Caption;
            }
        }

        public bool TrySelect(Identifier id)
        {
            Select(id);
            return treeView.SelectedNode != null;
        }
        
        public void Select(Identifier id)
        {   
            treeView.SelectedNode = FindNode(treeView.Nodes, id);
        }

        private static TreeNode FindNode(TreeNodeCollection nodes, Identifier id)
        {
            if (id.Parts.Count > 0)
            {
                foreach (TreeNode node in nodes)
                {
                    if (Equals(node.Text, id.Parts[0]))
                    {
                        if (id.Parts.Count == 1)
                            return node;
                        if (node.Nodes.Count == 0)
                            return null;

                        return FindNode(node.Nodes, id.RemovePrefix(1));
                    }
                }
            }
            return null;
        }

        public void SetDatabase(Database database)
        {
            _database = database;
            _columnSet = ColumnSet.GetTransitionsColumnSet(_database.GetSchema());
            treeView.Nodes.Clear();
            treeView.Nodes.AddRange(_columnSet.GetTreeNodes().ToArray());
        }

        public Report GetReport()
        {
            bool replicate = cbxPivotReplicate.Enabled && cbxPivotReplicate.Checked;
            bool isotopeLabel = cbxPivotIsotopeLabel.Enabled && cbxPivotIsotopeLabel.Checked;

            PivotType pivotType = null;
            if (replicate && isotopeLabel)
            {
                pivotType = PivotType.REPLICATE_ISOTOPE_LABEL;
            }
            else if (replicate)
            {
                pivotType = PivotType.REPLICATE;
            }
            else if (isotopeLabel)
            {
                pivotType = PivotType.ISOTOPE_LABEL;
            }
            return _columnSet.GetReport(_columns, pivotType);
        }

        public ReportSpec GetReportSpec()
        {
            return _reportSpec;
        }

        public void SetReport(Report report)
        {
            _columns.Clear();
            lbxColumns.Items.Clear();
            List<NodeData> newColumns;
            _columnSet.GetColumnInfos(report, treeView, out newColumns);
            foreach (NodeData column in newColumns)
            {
                AddColumn(column);
            }
            cbxPivotReplicate.Checked = false;
            cbxPivotIsotopeLabel.Checked = false;
            var pivotReport = report as PivotReport;
            if (pivotReport != null)
            {
                var testColumns = pivotReport.Columns.Union(pivotReport.CrossTabValues).ToArray();
                foreach(var id in pivotReport.CrossTabHeaders)
                {
                    if (PivotType.REPLICATE.GetCrosstabHeaders(testColumns).Contains(id))
                    {
                        cbxPivotReplicate.Checked = true;
                    }
                    if (PivotType.ISOTOPE_LABEL.GetCrosstabHeaders(testColumns).Contains(id))
                    {
                        cbxPivotIsotopeLabel.Checked = true;
                    }
                }
            }
        }

        public void SetReportSpec(ReportSpec reportSpec)
        {
            if (reportSpec == null)
            {
                textName.Text = string.Empty;
                SetReport(new SimpleReport());
            }
            else
            {
                textName.Text = reportSpec.Name;
                SetReport(Report.Load(reportSpec));
            }
            _reportSpec = reportSpec;
        }


        public void OkDialog()
        {
            var helper = new MessageBoxHelper(this);

            string name;
            if (!helper.ValidateNameTextBox(textName, out name))
                return;

            if (_columns.Count == 0)
            {
                MessageBox.Show(this, Resources.PivotReportDlg_OkDialog_A_report_must_have_at_least_one_column, Program.Name);
                return;
            }

            ReportSpec reportSpec = GetReport().GetReportSpec(name);

            if ((_reportSpec == null || !Equals(reportSpec.Name, _reportSpec.Name)) &&
                    _existing.Contains(reportSpec, new NameComparer<ReportSpec>()))
            {
                helper.ShowTextBoxError(textName, Resources.PivotReportDlg_OkDialog_The_report__0__already_exists, name);
                return;                    
            }

            _reportSpec = reportSpec;

            DialogResult = DialogResult.OK;
            Close();
        }

        private void btnExecute_Click(object sender, EventArgs e)
        {
            ShowPreview();
        }

        public void ShowPreview()
        {
            if (_columns.Count == 0)
            {
                MessageBox.Show(this, Resources.PivotReportDlg_ShowPreview_A_report_must_have_at_least_one_column_, Program.Name);
                return;
            }

            Report report = GetReport();
            ResultSet resultSet = report.Execute(_database);
            PreviewReportDlg previewReportDlg = new PreviewReportDlg();
            previewReportDlg.SetResults(resultSet);
            previewReportDlg.Show(Owner);
        }

        private void treeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            NodeData nodeData = GetNodeData(treeView.SelectedNode);
            bool allowAdd = nodeData != null && !_columns.Contains(nodeData);
            btnAdd.Enabled = allowAdd;
        }

        private void AddColumn(NodeData nodeData)
        {
            if (_columns.Contains(nodeData) || nodeData == null)
            {
                return;
            }
            _columns.Add(nodeData);
            lbxColumns.Items.Add(nodeData.Caption);
            UpdatePivotCheckboxEnabled();
        }

        private void btnAdd_Click(object sender, EventArgs e)
        {
            AddSelectedColumn();
        }

        public void AddSelectedColumn()
        {
            TreeNode node = treeView.SelectedNode;
            if (node == null)
                return;
            NodeData nodeData = GetNodeData(node);
            AddColumn(nodeData);
            treeView.SelectedNode = treeView.SelectedNode.NextNode;
            treeView.Focus();
        }

        private bool IsEnabled(PivotType pivotType)
        {
            foreach (NodeData nodeData in _columns)
            {
                Type table;
                String column;
                _columnSet.ResolveColumn(nodeData, out table, out column);
                if (pivotType.IsCrosstabValue(table, column))
                {
                    return true;
                }
            }
            return false;
        }

        private void UpdatePivotCheckboxEnabled()
        {
            cbxPivotReplicate.Enabled = IsEnabled(PivotType.REPLICATE);
            cbxPivotIsotopeLabel.Enabled = IsEnabled(PivotType.ISOTOPE_LABEL);
        }

        private static NodeData GetNodeData(TreeNode treeNode)
        {
            return treeNode.Tag as NodeData;
        }

        private void btnRemove_Click(object sender, EventArgs e)
        {
            int indexSel = lbxColumns.SelectedIndex;
            if (indexSel != -1)
            {
                RemoveIndexSel(indexSel);
            }
            UpdatePivotCheckboxEnabled();
        }

        private void RemoveIndexSel(int indexSel)
        {
            _columns.RemoveAt(indexSel);
            lbxColumns.Items.RemoveAt(indexSel);
            if (indexSel < lbxColumns.Items.Count)
                lbxColumns.SelectedIndex = indexSel;
            else if (indexSel > 0)
                lbxColumns.SelectedIndex = indexSel - 1;
        }

        public void RemoveColumn(string column)
        {
            int index = _columns.FindIndex(nodeData => nodeData.Caption == column);
            if(index != -1)
                RemoveIndexSel(index);
        }

        private void btnUp_Click(object sender, EventArgs e)
        {
            MoveColumn(lbxColumns.SelectedIndex, -1);
        }

        private void btnDown_Click(object sender, EventArgs e)
        {
            MoveColumn(lbxColumns.SelectedIndex, 1);
        }

        private void MoveColumn(int moveFrom, int offset)
        {
            int moveTo = moveFrom + offset;
            if (moveFrom != -1 && 0 <= moveTo && moveTo < lbxColumns.Items.Count)
            {
                var column = _columns[moveFrom];
                var item = lbxColumns.Items[moveFrom];
                _columns.RemoveAt(moveFrom);
                lbxColumns.Items.RemoveAt(moveFrom);
                _columns.Insert(moveTo, column);
                lbxColumns.Items.Insert(moveTo, item);
                lbxColumns.SelectedIndex = moveTo;
            }                        
        }

        private void btnOk_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private void lbxColumns_SelectedIndexChanged(object sender, EventArgs e)
        {
            int selIndex = lbxColumns.SelectedIndex;
            btnRemove.Enabled = (selIndex != -1);
            btnUp.Enabled = selIndex > 0;
            btnDown.Enabled = selIndex != -1 && selIndex < lbxColumns.Items.Count - 1;
        }
    }
}
