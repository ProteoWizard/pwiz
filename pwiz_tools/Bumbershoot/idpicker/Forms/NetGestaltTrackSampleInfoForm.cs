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
// Copyright 2014 Vanderbilt University
//
// Contributor(s): 
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Common.Collections;

namespace IDPicker.Forms
{
    public partial class NetGestaltTrackSampleInfoForm : Form
    {
        string _defaultFilepath;

        public IList<string> PivotGroupNames { get; set; }

        public NetGestaltTrackSampleInfoForm(string defaultFilepath)
        {
            InitializeComponent();

            _defaultFilepath = defaultFilepath;
        }

        protected override void OnLoad(EventArgs e)
        {
            if (PivotGroupNames.IsNullOrEmpty())
                throw new Exception("empty or null PivotGroupNames property");

            foreach (string name in PivotGroupNames)
                dataGridView.Rows.Add(name);
            base.OnLoad(e);
        }

        private void addColumnButton_Click(object sender, EventArgs e)
        {
            using (var input = new TextInputPrompt("Attribute Name", false, ""))
            {
                if (input.ShowDialog(this) == DialogResult.Cancel)
                    return;

                var column = new DataGridViewTextBoxColumn
                {
                    HeaderText = input.GetText(),
                    Name = input.GetText().Replace(" ", "").Trim() + "Attribute"
                };
                dataGridView.Columns.Add(column);
                dataGridView.Refresh();
            }
        }

        private void dataGridView_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.ColumnIndex <= 0 || e.Button != MouseButtons.Right)
                return;

            columnContextMenu.Tag = e.ColumnIndex;
            columnContextMenu.Show(MousePosition);
        }

        private void renameToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var input = new TextInputPrompt("Rename Attribute", false, ""))
            {
                if (input.ShowDialog(this) == DialogResult.Cancel)
                    return;

                int columnIndex = (int)columnContextMenu.Tag;
                var column = dataGridView.Columns[columnIndex];
                column.HeaderText = input.GetText();
                column.Name = input.GetText().Replace(" ", "").Trim() + "Attribute";
                dataGridView.Refresh();
            }
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            int columnIndex = (int) columnContextMenu.Tag;
            dataGridView.Columns.RemoveAt(columnIndex);
        }

        private void fillByRegExToolStripMenuItem_Click(object sender, EventArgs e)
        {
            using (var input = new TextInputPrompt("Fill Column by Regular Expression on Group Name", false, "") { InputFormat = null })
            using (var replace = new TextInputPrompt("Enter Replacement Expression", false, "$1") { InputFormat = null })
            {
                Regex regex;
                while (true)
                {
                    if (input.ShowDialog(this) == DialogResult.Cancel)
                        return;

                    try
                    {
                        regex = new Regex(input.GetText());
                    }
                    catch (Exception ex)
                    {
                        Program.HandleUserError(ex);
                        continue;
                    }

                    if (replace.ShowDialog(this) == DialogResult.Cancel)
                        return;

                    break;
                }

                int columnIndex = (int)columnContextMenu.Tag;
                foreach (DataGridViewRow row in dataGridView.Rows)
                {
                    string inputString = (string) row.Cells[0].Value;
                    string value = regex.Replace(inputString, replace.GetText());
                    if (value != inputString)
                        row.Cells[columnIndex].Value = value;
                }
            }
        }

        private void createButton_Click(object sender, EventArgs e)
        {
            /* Barcode    Sample
               data_type    CAT
               Broad-20120603-iTRAQ-114    WHIM-16
               Broad-20120603-iTRAQ-115    WHIM-2
               Broad-20120603-iTRAQ-116    WHIM-16
               Broad-20120603-iTRAQ-117    WHIM-2
            */

            string outputFilepath = _defaultFilepath;

            while (true)
                using (var sfd = new SaveFileDialog()
                                    {
                                        OverwritePrompt = true,
                                        FileName = Path.GetFileNameWithoutExtension(outputFilepath),
                                        DefaultExt = ".tsi",
                                        Filter = "NetGestalt TSI|*.tsi|All files|*.*"
                                    })
                {
                    if (sfd.ShowDialog(Program.MainWindow) == DialogResult.Cancel)
                        return;

                    outputFilepath = sfd.FileName;
                    if (Path.GetFileName(outputFilepath).Contains(" "))
                    {
                        MessageBox.Show("NetGestalt does not support spaces in imported filenames.",
                                        "Invalid NetGestalt Filename",
                                        MessageBoxButtons.OK,
                                        MessageBoxIcon.Error);
                        continue;
                    }
                    break;
                }

            using (var outputStream = new StreamWriter(outputFilepath, false, Encoding.ASCII))
            {
                outputStream.Write("GroupName");
                for (int i = 1; i < dataGridView.Columns.Count; ++i)
                    if (dataGridView.Columns[i].Name.EndsWith("Attribute"))
                        outputStream.Write("\t{0}", dataGridView.Columns[i].HeaderText);
                outputStream.WriteLine();

                // write data types for each column, right now only categorical is supported
                outputStream.Write("data_type");
                for (int i = 1; i < dataGridView.Columns.Count; ++i)
                    outputStream.Write("\tCAT"); // categorical
                outputStream.WriteLine();

                // write each group and its columns
                foreach(DataGridViewRow row in dataGridView.Rows)
                    outputStream.WriteLine(String.Join("\t", row.Cells.Cast<DataGridViewCell>().Select(o => o.Value)));
            }

            Close();
        }
    }
}
