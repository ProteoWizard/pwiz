using System.Windows.Forms;
using pwiz.Common.DataBinding;
using pwiz.Common.DataBinding.Controls;
using pwiz.Common.DataBinding.Internal;

namespace pwiz.Topograph.ui.Forms
{
    partial class HalfLivesForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.cbxByProtein = new System.Windows.Forms.CheckBox();
            this.btnRequery = new System.Windows.Forms.Button();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.cbxBySample = new System.Windows.Forms.CheckBox();
            this.label7 = new System.Windows.Forms.Label();
            this.checkedListBoxTimePoints = new System.Windows.Forms.CheckedListBox();
            this.bindingSource1 = new BindingListSource(this.components);
            this.dataGridView1 = new BoundDataGridView();
            this.halfLifeSettingsControl = new pwiz.Topograph.ui.Controls.HalfLifeSettingsControl();
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // cbxByProtein
            // 
            this.cbxByProtein.AutoSize = true;
            this.cbxByProtein.Location = new System.Drawing.Point(3, 3);
            this.cbxByProtein.Name = "cbxByProtein";
            this.cbxByProtein.Size = new System.Drawing.Size(74, 17);
            this.cbxByProtein.TabIndex = 0;
            this.cbxByProtein.Text = "By Protein";
            this.cbxByProtein.UseVisualStyleBackColor = true;
            // 
            // btnRequery
            // 
            this.btnRequery.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRequery.Location = new System.Drawing.Point(572, 3);
            this.btnRequery.Name = "btnRequery";
            this.btnRequery.Size = new System.Drawing.Size(185, 19);
            this.btnRequery.TabIndex = 4;
            this.btnRequery.Text = "Recalculate";
            this.btnRequery.UseVisualStyleBackColor = true;
            this.btnRequery.Click += new System.EventHandler(this.BtnRequeryOnClick);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 46.3964F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 122F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 53.6036F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 190F));
            this.tableLayoutPanel1.Controls.Add(this.btnRequery, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.cbxByProtein, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.cbxBySample, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label7, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.checkedListBoxTimePoints, 3, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 434);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 2;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(760, 53);
            this.tableLayoutPanel1.TabIndex = 12;
            // 
            // cbxBySample
            // 
            this.cbxBySample.AutoSize = true;
            this.cbxBySample.Location = new System.Drawing.Point(103, 3);
            this.cbxBySample.Name = "cbxBySample";
            this.cbxBySample.Size = new System.Drawing.Size(76, 17);
            this.cbxBySample.TabIndex = 32;
            this.cbxBySample.Text = "By Sample";
            this.cbxBySample.UseVisualStyleBackColor = true;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(264, 0);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(82, 13);
            this.label7.TabIndex = 28;
            this.label7.Text = "Included Times:";
            // 
            // checkedListBoxTimePoints
            // 
            this.checkedListBoxTimePoints.FormattingEnabled = true;
            this.checkedListBoxTimePoints.Location = new System.Drawing.Point(383, 0);
            this.checkedListBoxTimePoints.Margin = new System.Windows.Forms.Padding(0);
            this.checkedListBoxTimePoints.Name = "checkedListBoxTimePoints";
            this.tableLayoutPanel1.SetRowSpan(this.checkedListBoxTimePoints, 2);
            this.checkedListBoxTimePoints.Size = new System.Drawing.Size(186, 49);
            this.checkedListBoxTimePoints.TabIndex = 27;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.DataSource = this.bindingSource1;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle2;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 512);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridView1.Size = new System.Drawing.Size(760, 64);
            this.dataGridView1.TabIndex = 3;
            // 
            // halfLifeSettingsControl
            // 
            this.halfLifeSettingsControl.AutoSize = true;
            this.halfLifeSettingsControl.AutoSizeMode = System.Windows.Forms.AutoSizeMode.GrowAndShrink;
            this.halfLifeSettingsControl.Dock = System.Windows.Forms.DockStyle.Top;
            this.halfLifeSettingsControl.IsExpanded = true;
            this.halfLifeSettingsControl.Location = new System.Drawing.Point(0, 0);
            this.halfLifeSettingsControl.Name = "halfLifeSettingsControl";
            this.halfLifeSettingsControl.Size = new System.Drawing.Size(760, 434);
            this.halfLifeSettingsControl.TabIndex = 13;
            // 
            // navBar1
            // 
            this.navBar1.AutoSize = true;
            this.navBar1.BindingListSource = this.bindingSource1;
            this.navBar1.Dock = System.Windows.Forms.DockStyle.Top;
            this.navBar1.Location = new System.Drawing.Point(0, 487);
            this.navBar1.Name = "navBar1";
            this.navBar1.Size = new System.Drawing.Size(760, 25);
            this.navBar1.TabIndex = 31;
            this.navBar1.WaitingMessage = "Press \"Recalculate\" button to see data.";
            // 
            // HalfLivesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 576);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.navBar1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Controls.Add(this.halfLifeSettingsControl);
            this.Name = "HalfLivesForm";
            this.TabText = "HalfLivesForm";
            this.Text = "HalfLivesForm";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.CheckBox cbxByProtein;
        private System.Windows.Forms.Button btnRequery;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.CheckedListBox checkedListBoxTimePoints;
        private System.Windows.Forms.Label label7;
        private BoundDataGridView dataGridView1;
        private BindingListSource bindingSource1;
        private System.Windows.Forms.CheckBox cbxBySample;
        private pwiz.Topograph.ui.Controls.HalfLifeSettingsControl halfLifeSettingsControl;
        private pwiz.Common.DataBinding.Controls.NavBar navBar1;
    }
}