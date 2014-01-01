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

namespace seems
{
	partial class ChromatogramListForm
	{
		/// <summary>
		/// Required designer variable.
		/// </summary>
		private System.ComponentModel.IContainer components = null;

		/// <summary>
		/// Clean up any resources being used.
		/// </summary>
		/// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
		protected override void Dispose( bool disposing )
		{
			if( disposing && ( components != null ) )
			{
				components.Dispose();
			}
			base.Dispose( disposing );
		}

		#region Windows Form Designer generated code

		/// <summary>
		/// Required method for Designer support - do not modify
		/// the contents of this method with the code editor.
		/// </summary>
		private void InitializeComponent()
		{
            this.components = new System.ComponentModel.Container();
            this.gridView = new System.Windows.Forms.DataGridView();
            this.chromatogramBindingSource = new System.Windows.Forms.BindingSource( this.components );
            this.chromatogramDataSet = new seems.Misc.ChromatogramDataSet();
            this.selectColumnsMenuStrip = new System.Windows.Forms.ContextMenuStrip( this.components );
            this.selectColumnsToolStripMenuItem = new System.Windows.Forms.ToolStripMenuItem();
            this.idDataGridViewTextBoxColumn = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.indexDataGridViewTextBoxColumn = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.typeDataGridViewTextBoxColumn = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.dataPointsDataGridViewTextBoxColumn = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            this.dpIdDataGridViewTextBoxColumn = new DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn();
            ( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.chromatogramBindingSource ) ).BeginInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.chromatogramDataSet ) ).BeginInit();
            this.selectColumnsMenuStrip.SuspendLayout();
            this.SuspendLayout();
            // 
            // gridView
            // 
            this.gridView.AllowUserToAddRows = false;
            this.gridView.AllowUserToDeleteRows = false;
            this.gridView.AllowUserToOrderColumns = true;
            this.gridView.AllowUserToResizeRows = false;
            this.gridView.AutoGenerateColumns = false;
            this.gridView.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.gridView.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.gridView.ColumnHeadersBorderStyle = System.Windows.Forms.DataGridViewHeaderBorderStyle.Single;
            this.gridView.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridView.Columns.AddRange( new System.Windows.Forms.DataGridViewColumn[] {
            this.idDataGridViewTextBoxColumn,
            this.indexDataGridViewTextBoxColumn,
            this.typeDataGridViewTextBoxColumn,
            this.dataPointsDataGridViewTextBoxColumn,
            this.dpIdDataGridViewTextBoxColumn} );
            this.gridView.Cursor = System.Windows.Forms.Cursors.Hand;
            this.gridView.DataSource = this.chromatogramBindingSource;
            this.gridView.Dock = System.Windows.Forms.DockStyle.Fill;
            this.gridView.Location = new System.Drawing.Point( 0, 0 );
            this.gridView.Name = "gridView";
            this.gridView.ReadOnly = true;
            this.gridView.RowHeadersVisible = false;
            this.gridView.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.CellSelect;
            this.gridView.Size = new System.Drawing.Size( 976, 411 );
            this.gridView.TabIndex = 10;
            this.gridView.CellMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_CellMouseClick );
            this.gridView.ColumnHeaderMouseClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_ColumnHeaderMouseClick );
            this.gridView.CellMouseDoubleClick += new System.Windows.Forms.DataGridViewCellMouseEventHandler( this.gridView_CellMouseDoubleClick );
            this.gridView.DataBindingComplete += new System.Windows.Forms.DataGridViewBindingCompleteEventHandler( this.gridView_DataBindingComplete );
            // 
            // chromatogramBindingSource
            // 
            this.chromatogramBindingSource.DataMember = "ChromatogramTable";
            this.chromatogramBindingSource.DataSource = this.chromatogramDataSet;
            // 
            // chromatogramDataSet
            // 
            this.chromatogramDataSet.DataSetName = "ChromatogramDataSet";
            this.chromatogramDataSet.SchemaSerializationMode = System.Data.SchemaSerializationMode.IncludeSchema;
            // 
            // selectColumnsMenuStrip
            // 
            this.selectColumnsMenuStrip.Items.AddRange( new System.Windows.Forms.ToolStripItem[] {
            this.selectColumnsToolStripMenuItem} );
            this.selectColumnsMenuStrip.Name = "selectColumnsMenuStrip";
            this.selectColumnsMenuStrip.Size = new System.Drawing.Size( 160, 26 );
            // 
            // selectColumnsToolStripMenuItem
            // 
            this.selectColumnsToolStripMenuItem.Name = "selectColumnsToolStripMenuItem";
            this.selectColumnsToolStripMenuItem.Size = new System.Drawing.Size( 159, 22 );
            this.selectColumnsToolStripMenuItem.Text = "Select Columns...";
            // 
            // idDataGridViewTextBoxColumn
            // 
            this.idDataGridViewTextBoxColumn.DataPropertyName = "Id";
            this.idDataGridViewTextBoxColumn.HeaderText = "Id";
            this.idDataGridViewTextBoxColumn.Name = "idDataGridViewTextBoxColumn";
            this.idDataGridViewTextBoxColumn.ReadOnly = true;
            this.idDataGridViewTextBoxColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // indexDataGridViewTextBoxColumn
            // 
            this.indexDataGridViewTextBoxColumn.DataPropertyName = "Index";
            this.indexDataGridViewTextBoxColumn.HeaderText = "Index";
            this.indexDataGridViewTextBoxColumn.Name = "indexDataGridViewTextBoxColumn";
            this.indexDataGridViewTextBoxColumn.ReadOnly = true;
            this.indexDataGridViewTextBoxColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // typeDataGridViewTextBoxColumn
            // 
            this.typeDataGridViewTextBoxColumn.DataPropertyName = "Type";
            this.typeDataGridViewTextBoxColumn.HeaderText = "Type";
            this.typeDataGridViewTextBoxColumn.Name = "typeDataGridViewTextBoxColumn";
            this.typeDataGridViewTextBoxColumn.ReadOnly = true;
            this.typeDataGridViewTextBoxColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // dataPointsDataGridViewTextBoxColumn
            // 
            this.dataPointsDataGridViewTextBoxColumn.DataPropertyName = "DataPoints";
            this.dataPointsDataGridViewTextBoxColumn.HeaderText = "Data Points";
            this.dataPointsDataGridViewTextBoxColumn.Name = "dataPointsDataGridViewTextBoxColumn";
            this.dataPointsDataGridViewTextBoxColumn.ReadOnly = true;
            // 
            // dpIdDataGridViewTextBoxColumn
            // 
            this.dpIdDataGridViewTextBoxColumn.DataPropertyName = "DpId";
            this.dpIdDataGridViewTextBoxColumn.HeaderText = "DpId";
            this.dpIdDataGridViewTextBoxColumn.Name = "dpIdDataGridViewTextBoxColumn";
            this.dpIdDataGridViewTextBoxColumn.ReadOnly = true;
            this.dpIdDataGridViewTextBoxColumn.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            // 
            // ChromatogramListForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 976, 411 );
            this.Controls.Add( this.gridView );
            this.DoubleBuffered = true;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.SizableToolWindow;
            this.Name = "ChromatogramListForm";
            this.TabText = "Chromatogram List";
            this.Text = "Chromatogram List";
            ( (System.ComponentModel.ISupportInitialize) ( this.gridView ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.chromatogramBindingSource ) ).EndInit();
            ( (System.ComponentModel.ISupportInitialize) ( this.chromatogramDataSet ) ).EndInit();
            this.selectColumnsMenuStrip.ResumeLayout( false );
            this.ResumeLayout( false );

		}

		#endregion

        private System.Windows.Forms.DataGridView gridView;
        private seems.Misc.ChromatogramDataSet chromatogramDataSet;
        private System.Windows.Forms.BindingSource chromatogramBindingSource;
        private System.Windows.Forms.ContextMenuStrip selectColumnsMenuStrip;
        private System.Windows.Forms.ToolStripMenuItem selectColumnsToolStripMenuItem;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn idDataGridViewTextBoxColumn;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn indexDataGridViewTextBoxColumn;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn typeDataGridViewTextBoxColumn;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn dataPointsDataGridViewTextBoxColumn;
        private DataGridViewAutoFilter.DataGridViewAutoFilterTextBoxColumn dpIdDataGridViewTextBoxColumn;
	}
}