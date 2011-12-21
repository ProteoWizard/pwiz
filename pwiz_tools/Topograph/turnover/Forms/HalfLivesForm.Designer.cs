using System.Windows.Forms;
using pwiz.Common.DataBinding;

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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle43 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle44 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle45 = new System.Windows.Forms.DataGridViewCellStyle();
            this.cbxByProtein = new System.Windows.Forms.CheckBox();
            this.label1 = new System.Windows.Forms.Label();
            this.tbxMinScore = new System.Windows.Forms.TextBox();
            this.btnRequery = new System.Windows.Forms.Button();
            this.tbxInitialTracerPercent = new System.Windows.Forms.TextBox();
            this.tbxFinalTracerPercent = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.label2 = new System.Windows.Forms.Label();
            this.cbxFixYIntercept = new System.Windows.Forms.CheckBox();
            this.comboCalculationType = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.checkedListBoxTimePoints = new System.Windows.Forms.CheckedListBox();
            this.label7 = new System.Windows.Forms.Label();
            this.navBar1 = new pwiz.Common.DataBinding.Controls.NavBar();
            this.bindingSource1 = new BindingSource(this.components);
            this.comboEvviesFilter = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.cbxBySample = new System.Windows.Forms.CheckBox();
            this.label5 = new System.Windows.Forms.Label();
            this.tbxMinAuc = new System.Windows.Forms.TextBox();
            this.dataGridView1 = new pwiz.Common.DataBinding.BoundDataGridView();
            this.tableLayoutPanel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).BeginInit();
            this.SuspendLayout();
            // 
            // cbxByProtein
            // 
            this.cbxByProtein.AutoSize = true;
            this.cbxByProtein.Location = new System.Drawing.Point(583, 28);
            this.cbxByProtein.Name = "cbxByProtein";
            this.cbxByProtein.Size = new System.Drawing.Size(74, 17);
            this.cbxByProtein.TabIndex = 0;
            this.cbxByProtein.Text = "By Protein";
            this.cbxByProtein.UseVisualStyleBackColor = true;
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label1.Location = new System.Drawing.Point(279, 25);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(94, 25);
            this.label1.TabIndex = 1;
            this.label1.Text = "Minimum Score:";
            this.label1.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMinScore
            // 
            this.tbxMinScore.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinScore.Location = new System.Drawing.Point(379, 28);
            this.tbxMinScore.Name = "tbxMinScore";
            this.tbxMinScore.Size = new System.Drawing.Size(198, 20);
            this.tbxMinScore.TabIndex = 2;
            // 
            // btnRequery
            // 
            this.btnRequery.Dock = System.Windows.Forms.DockStyle.Fill;
            this.btnRequery.Location = new System.Drawing.Point(583, 3);
            this.btnRequery.Name = "btnRequery";
            this.btnRequery.Size = new System.Drawing.Size(174, 19);
            this.btnRequery.TabIndex = 4;
            this.btnRequery.Text = "Recalculate";
            this.btnRequery.UseVisualStyleBackColor = true;
            this.btnRequery.Click += new System.EventHandler(this.btnRequery_Click);
            // 
            // tbxInitialTracerPercent
            // 
            this.tbxInitialTracerPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxInitialTracerPercent.Location = new System.Drawing.Point(103, 3);
            this.tbxInitialTracerPercent.Name = "tbxInitialTracerPercent";
            this.tbxInitialTracerPercent.Size = new System.Drawing.Size(170, 20);
            this.tbxInitialTracerPercent.TabIndex = 5;
            // 
            // tbxFinalTracerPercent
            // 
            this.tbxFinalTracerPercent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxFinalTracerPercent.Location = new System.Drawing.Point(103, 28);
            this.tbxFinalTracerPercent.Name = "tbxFinalTracerPercent";
            this.tbxFinalTracerPercent.Size = new System.Drawing.Size(170, 20);
            this.tbxFinalTracerPercent.TabIndex = 7;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 25);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(94, 25);
            this.label3.TabIndex = 8;
            this.label3.Text = "Final Tracer %";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.ColumnCount = 5;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 46.3964F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 100F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 53.6036F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Absolute, 179F));
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxInitialTracerPercent, 1, 0);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxFinalTracerPercent, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.btnRequery, 4, 0);
            this.tableLayoutPanel1.Controls.Add(this.label1, 2, 1);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinScore, 3, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxFixYIntercept, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.comboCalculationType, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.label6, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.checkedListBoxTimePoints, 3, 2);
            this.tableLayoutPanel1.Controls.Add(this.label7, 2, 2);
            this.tableLayoutPanel1.Controls.Add(this.navBar1, 0, 5);
            this.tableLayoutPanel1.Controls.Add(this.comboEvviesFilter, 1, 4);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 4);
            this.tableLayoutPanel1.Controls.Add(this.cbxByProtein, 4, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxBySample, 4, 2);
            this.tableLayoutPanel1.Controls.Add(this.label5, 2, 0);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinAuc, 3, 0);
            this.tableLayoutPanel1.Dock = System.Windows.Forms.DockStyle.Top;
            this.tableLayoutPanel1.Location = new System.Drawing.Point(0, 0);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 6;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(760, 157);
            this.tableLayoutPanel1.TabIndex = 12;
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 0);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(94, 25);
            this.label2.TabIndex = 7;
            this.label2.Text = "Initial Tracer %";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cbxFixYIntercept
            // 
            this.cbxFixYIntercept.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.cbxFixYIntercept, 2);
            this.cbxFixYIntercept.Dock = System.Windows.Forms.DockStyle.Fill;
            this.cbxFixYIntercept.Location = new System.Drawing.Point(3, 53);
            this.cbxFixYIntercept.Name = "cbxFixYIntercept";
            this.cbxFixYIntercept.Size = new System.Drawing.Size(270, 19);
            this.cbxFixYIntercept.TabIndex = 12;
            this.cbxFixYIntercept.Text = "Hold Initial Tracer % Constant";
            this.cbxFixYIntercept.UseVisualStyleBackColor = true;
            // 
            // comboCalculationType
            // 
            this.comboCalculationType.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboCalculationType.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboCalculationType.FormattingEnabled = true;
            this.comboCalculationType.Items.AddRange(new object[] {
            "Tracer %",
            "Individual Precursor Pool",
            "Avg Precursor Pool",
            "Avg Precursor Pool (Old Way)"});
            this.comboCalculationType.Location = new System.Drawing.Point(103, 78);
            this.comboCalculationType.Name = "comboCalculationType";
            this.comboCalculationType.Size = new System.Drawing.Size(170, 21);
            this.comboCalculationType.TabIndex = 21;
            this.comboCalculationType.SelectedIndexChanged += new System.EventHandler(this.comboCalculationType_SelectedIndexChanged);
            // 
            // label6
            // 
            this.label6.AutoSize = true;
            this.label6.Location = new System.Drawing.Point(3, 75);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(86, 13);
            this.label6.TabIndex = 22;
            this.label6.Text = "Calculation Type";
            // 
            // checkedListBoxTimePoints
            // 
            this.checkedListBoxTimePoints.Dock = System.Windows.Forms.DockStyle.Fill;
            this.checkedListBoxTimePoints.FormattingEnabled = true;
            this.checkedListBoxTimePoints.Location = new System.Drawing.Point(379, 53);
            this.checkedListBoxTimePoints.Name = "checkedListBoxTimePoints";
            this.tableLayoutPanel1.SetRowSpan(this.checkedListBoxTimePoints, 3);
            this.checkedListBoxTimePoints.Size = new System.Drawing.Size(198, 64);
            this.checkedListBoxTimePoints.TabIndex = 27;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(279, 50);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(82, 13);
            this.label7.TabIndex = 28;
            this.label7.Text = "Included Times:";
            // 
            // navBar1
            // 
            this.navBar1.AutoSize = true;
            this.navBar1.BindingSource = this.bindingSource1;
            this.tableLayoutPanel1.SetColumnSpan(this.navBar1, 5);
            this.navBar1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.navBar1.Location = new System.Drawing.Point(3, 128);
            this.navBar1.Name = "navBar1";
            this.navBar1.Size = new System.Drawing.Size(754, 26);
            this.navBar1.TabIndex = 31;
            // 
            // comboEvviesFilter
            // 
            this.comboEvviesFilter.Dock = System.Windows.Forms.DockStyle.Fill;
            this.comboEvviesFilter.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboEvviesFilter.FormattingEnabled = true;
            this.comboEvviesFilter.Location = new System.Drawing.Point(103, 103);
            this.comboEvviesFilter.Name = "comboEvviesFilter";
            this.comboEvviesFilter.Size = new System.Drawing.Size(170, 21);
            this.comboEvviesFilter.TabIndex = 33;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 100);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(94, 25);
            this.label4.TabIndex = 34;
            this.label4.Text = "Evvie\'s Filter";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // cbxBySample
            // 
            this.cbxBySample.AutoSize = true;
            this.cbxBySample.Location = new System.Drawing.Point(583, 53);
            this.cbxBySample.Name = "cbxBySample";
            this.cbxBySample.Size = new System.Drawing.Size(76, 17);
            this.cbxBySample.TabIndex = 32;
            this.cbxBySample.Text = "By Sample";
            this.cbxBySample.UseVisualStyleBackColor = true;
            // 
            // label5
            // 
            this.label5.AutoSize = true;
            this.label5.Location = new System.Drawing.Point(279, 0);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(73, 13);
            this.label5.TabIndex = 35;
            this.label5.Text = "Minimum AUC";
            // 
            // tbxMinAuc
            // 
            this.tbxMinAuc.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinAuc.Location = new System.Drawing.Point(379, 3);
            this.tbxMinAuc.Name = "tbxMinAuc";
            this.tbxMinAuc.Size = new System.Drawing.Size(198, 20);
            this.tbxMinAuc.TabIndex = 36;
            // 
            // dataGridView1
            // 
            this.dataGridView1.AllowUserToAddRows = false;
            this.dataGridView1.AllowUserToDeleteRows = false;
            this.dataGridView1.AllowUserToOrderColumns = true;
            dataGridViewCellStyle43.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle43.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle43.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle43.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle43.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle43.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle43.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle43;
            this.dataGridView1.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridView1.DataSource = this.bindingSource1;
            dataGridViewCellStyle44.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle44.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle44.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle44.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle44.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle44.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle44.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridView1.DefaultCellStyle = dataGridViewCellStyle44;
            this.dataGridView1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dataGridView1.Location = new System.Drawing.Point(0, 157);
            this.dataGridView1.Name = "dataGridView1";
            this.dataGridView1.ReadOnly = true;
            dataGridViewCellStyle45.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle45.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle45.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle45.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle45.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle45.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle45.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridView1.RowHeadersDefaultCellStyle = dataGridViewCellStyle45;
            this.dataGridView1.Size = new System.Drawing.Size(760, 254);
            this.dataGridView1.TabIndex = 3;
            // 
            // HalfLivesForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(760, 411);
            this.Controls.Add(this.dataGridView1);
            this.Controls.Add(this.tableLayoutPanel1);
            this.Name = "HalfLivesForm";
            this.TabText = "HalfLivesForm";
            this.Text = "HalfLivesForm";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSource1)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridView1)).EndInit();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.CheckBox cbxByProtein;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbxMinScore;
        private System.Windows.Forms.Button btnRequery;
        private System.Windows.Forms.TextBox tbxInitialTracerPercent;
        private System.Windows.Forms.TextBox tbxFinalTracerPercent;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.CheckBox cbxFixYIntercept;
        private System.Windows.Forms.ComboBox comboCalculationType;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.CheckedListBox checkedListBoxTimePoints;
        private System.Windows.Forms.Label label7;
        private pwiz.Common.DataBinding.Controls.NavBar navBar1;
        private BoundDataGridView dataGridView1;
        private BindingSource bindingSource1;
        private System.Windows.Forms.CheckBox cbxBySample;
        private System.Windows.Forms.ComboBox comboEvviesFilter;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.TextBox tbxMinAuc;
    }
}