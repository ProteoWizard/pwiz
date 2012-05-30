//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;

namespace seems
{
    public class SelectColumnsDialog : DockableForm
    {
        private DataGridView dataGridView;
        private Map<string, CheckBox> columnToCheckboxMap;

        public SelectColumnsDialog( DataGridView dgv )
        {
            dataGridView = dgv;
            columnToCheckboxMap = new Map<string, CheckBox>();

            this.Name = "SelectColumnsDialog";
            this.Text = "Select Columns";

            SplitContainer splitContainer = new SplitContainer();
            splitContainer.Orientation = Orientation.Horizontal;
            splitContainer.Dock = DockStyle.Fill;
            this.Controls.Add( splitContainer );

            FlowLayoutPanel panel1 = new FlowLayoutPanel();
            panel1.FlowDirection = FlowDirection.TopDown;
            panel1.Dock = DockStyle.Fill;
            splitContainer.Panel1.Controls.Add( panel1 );
            
            foreach( DataGridViewColumn column in dgv.Columns )
            {
                CheckBox checkbox = new CheckBox();
                checkbox.Text = column.HeaderText;
                checkbox.Checked = column.Visible;
                columnToCheckboxMap.Add( column.Name, checkbox );
                panel1.Controls.Add( checkbox );
            }

            TableLayoutPanel panel2 = new TableLayoutPanel();
            panel2.ColumnCount = 2;
            panel2.RowCount = 1;
            panel2.Dock = DockStyle.Fill;
            splitContainer.Panel2.Controls.Add( panel2 );

            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.Click += new EventHandler( okButton_Click );
            this.AcceptButton = okButton;
            panel2.Controls.Add( okButton );
            panel2.SetCellPosition( okButton, new TableLayoutPanelCellPosition(0,0) );

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Click += new EventHandler( cancelButton_Click );
            this.CancelButton = cancelButton;
            panel2.Controls.Add( cancelButton );
            panel2.SetCellPosition( cancelButton, new TableLayoutPanelCellPosition(1,0) );

            splitContainer.FixedPanel = FixedPanel.Panel2;
            splitContainer.SplitterDistance = this.Height - okButton.Height * 2;
            splitContainer.IsSplitterFixed = true;
        }

        void okButton_Click( object sender, EventArgs e )
        {
            foreach( Map<string, CheckBox>.MapPair itr in columnToCheckboxMap )
            {
                dataGridView.Columns[itr.Key].Visible = itr.Value.Checked;
            }
            dataGridView.Refresh();

            this.Close();
        }

        void cancelButton_Click( object sender, EventArgs e )
        {
            this.Close();
        }
    }
}
