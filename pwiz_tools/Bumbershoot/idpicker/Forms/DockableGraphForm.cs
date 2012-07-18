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
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
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
using ZedGraph;

namespace IDPicker.Forms
{
    /// <summary>
    /// A simple wrapper for DockableForm filled with a ZedGraphControl and providing other convenient features.
    /// </summary>
    public partial class DockableGraphForm : DockableForm
    {
        private ZedGraphControl zedGraphControl;
        public ZedGraphControl ZedGraphControl { get { return zedGraphControl; } }

        public event EventHandler<ShowDataTableEventArgs> ShowDataTable;

        public DockableGraphForm ()
        {
            zedGraphControl = new ZedGraphControl
            {
                Dock = DockStyle.Fill
            };
            zedGraphControl.GraphPane.Title.IsVisible = false;
            Controls.Add(zedGraphControl);

            zedGraphControl.ContextMenuBuilder += zedGraphControl_ContextMenuBuilder;

            // graphs can't be closed
            FormClosing += (sender, e) => { e.Cancel = true; };
        }

        void zedGraphControl_ContextMenuBuilder (ZedGraphControl sender, ContextMenuStrip menuStrip, Point mousePt, ZedGraphControl.ContextMenuObjectState objState)
        {
            if (ShowDataTable == null)
                return;

            var e = new ShowDataTableEventArgs();
            ShowDataTable(this, e);

            int index = menuStrip.Items.Cast<ToolStripMenuItem>().TakeWhile(o => o.Text != "Show Point Values").Count();
            menuStrip.Items.Insert(index,
                new ToolStripMenuItem("Show Data Table", null,
                (x, y) =>
                {
                    var tableForm = new DockableForm
                    {
                        Text = this.Text + " Data Table",
                        Size = new Size(480, 600)
                    };

                    var dgv = new DataGridView
                    {
                        Dock = DockStyle.Fill,
                        DataSource = e.DataTable,
                        RowHeadersVisible = false,
                        AllowUserToDeleteRows = false,
                        AllowUserToAddRows = false,
                        ReadOnly = true,
                        ClipboardCopyMode = DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText
                    };
                    tableForm.Controls.Add(dgv);
                    tableForm.Show(this);
                }));
        }
    }

    public class ShowDataTableEventArgs : EventArgs
    {
        public DataTable DataTable { get; set; }
    }
}
