namespace pwiz.Skyline.SettingsUI.Irt
{
    partial class CalibrateIrtDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(CalibrateIrtDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle5 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.textMaxIrt = new System.Windows.Forms.TextBox();
            this.textMinIrt = new System.Windows.Forms.TextBox();
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnUseCurrent = new System.Windows.Forms.Button();
            this.bindingSourceStandard = new System.Windows.Forms.BindingSource(this.components);
            this.gridViewCalibrate = new pwiz.Skyline.Controls.DataGridViewEx();
            this.calibratePeptides = new pwiz.Skyline.Controls.TargetColumn();
            this.calibrateMeasuredRt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.colIrt = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.label3 = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.comboRegression = new System.Windows.Forms.ComboBox();
            this.btnGraph = new System.Windows.Forms.Button();
            this.grpLinearEquation = new System.Windows.Forms.GroupBox();
            this.comboMaxPeptide = new System.Windows.Forms.ComboBox();
            this.label6 = new System.Windows.Forms.Label();
            this.comboMinPeptide = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.label7 = new System.Windows.Forms.Label();
            this.btnGraphIrts = new System.Windows.Forms.Button();
            this.labelStandardCount = new System.Windows.Forms.Label();
            this.panelPeptides = new System.Windows.Forms.Panel();
            this.lblEquation = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewCalibrate)).BeginInit();
            this.grpLinearEquation.SuspendLayout();
            this.panelPeptides.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // label2
            // 
            resources.ApplyResources(this.label2, "label2");
            this.label2.Name = "label2";
            // 
            // textMaxIrt
            // 
            resources.ApplyResources(this.textMaxIrt, "textMaxIrt");
            this.textMaxIrt.Name = "textMaxIrt";
            this.textMaxIrt.TextChanged += new System.EventHandler(this.UpdateControls);
            // 
            // textMinIrt
            // 
            resources.ApplyResources(this.textMinIrt, "textMinIrt");
            this.textMinIrt.Name = "textMinIrt";
            this.textMinIrt.TextChanged += new System.EventHandler(this.UpdateControls);
            // 
            // btnOk
            // 
            resources.ApplyResources(this.btnOk, "btnOk");
            this.btnOk.Name = "btnOk";
            this.btnOk.UseVisualStyleBackColor = true;
            this.btnOk.Click += new System.EventHandler(this.btnOk_Click);
            // 
            // btnCancel
            // 
            resources.ApplyResources(this.btnCancel, "btnCancel");
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // btnUseCurrent
            // 
            resources.ApplyResources(this.btnUseCurrent, "btnUseCurrent");
            this.btnUseCurrent.Name = "btnUseCurrent";
            this.btnUseCurrent.UseVisualStyleBackColor = true;
            this.btnUseCurrent.Click += new System.EventHandler(this.btnUseCurrent_Click);
            // 
            // gridViewCalibrate
            // 
            resources.ApplyResources(this.gridViewCalibrate, "gridViewCalibrate");
            this.gridViewCalibrate.AutoGenerateColumns = false;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewCalibrate.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridViewCalibrate.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridViewCalibrate.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.calibratePeptides,
            this.calibrateMeasuredRt,
            this.colIrt});
            this.gridViewCalibrate.DataSource = this.bindingSourceStandard;
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridViewCalibrate.DefaultCellStyle = dataGridViewCellStyle4;
            this.gridViewCalibrate.Name = "gridViewCalibrate";
            dataGridViewCellStyle5.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle5.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle5.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle5.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle5.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle5.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle5.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridViewCalibrate.RowHeadersDefaultCellStyle = dataGridViewCellStyle5;
            // 
            // calibratePeptides
            // 
            this.calibratePeptides.DataPropertyName = "Target";
            this.calibratePeptides.FillWeight = 161.0009F;
            resources.ApplyResources(this.calibratePeptides, "calibratePeptides");
            this.calibratePeptides.Name = "calibratePeptides";
            // 
            // calibrateMeasuredRt
            // 
            this.calibrateMeasuredRt.DataPropertyName = "RetentionTime";
            dataGridViewCellStyle2.Format = "N2";
            dataGridViewCellStyle2.NullValue = null;
            this.calibrateMeasuredRt.DefaultCellStyle = dataGridViewCellStyle2;
            this.calibrateMeasuredRt.FillWeight = 78.08542F;
            resources.ApplyResources(this.calibrateMeasuredRt, "calibrateMeasuredRt");
            this.calibrateMeasuredRt.Name = "calibrateMeasuredRt";
            // 
            // colIrt
            // 
            this.colIrt.DataPropertyName = "Irt";
            dataGridViewCellStyle3.Format = "N2";
            this.colIrt.DefaultCellStyle = dataGridViewCellStyle3;
            this.colIrt.FillWeight = 78.08542F;
            resources.ApplyResources(this.colIrt, "colIrt");
            this.colIrt.Name = "colIrt";
            this.colIrt.ReadOnly = true;
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            // 
            // comboRegression
            // 
            this.comboRegression.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboRegression.FormattingEnabled = true;
            resources.ApplyResources(this.comboRegression, "comboRegression");
            this.comboRegression.Name = "comboRegression";
            this.comboRegression.SelectedIndexChanged += new System.EventHandler(this.comboRegression_SelectedIndexChanged);
            // 
            // btnGraph
            // 
            resources.ApplyResources(this.btnGraph, "btnGraph");
            this.btnGraph.Name = "btnGraph";
            this.btnGraph.UseVisualStyleBackColor = true;
            this.btnGraph.Click += new System.EventHandler(this.btnGraph_Click);
            // 
            // grpLinearEquation
            // 
            this.grpLinearEquation.Controls.Add(this.lblEquation);
            this.grpLinearEquation.Controls.Add(this.comboMaxPeptide);
            this.grpLinearEquation.Controls.Add(this.label6);
            this.grpLinearEquation.Controls.Add(this.comboMinPeptide);
            this.grpLinearEquation.Controls.Add(this.label5);
            this.grpLinearEquation.Controls.Add(this.label4);
            this.grpLinearEquation.Controls.Add(this.btnGraph);
            this.grpLinearEquation.Controls.Add(this.comboRegression);
            this.grpLinearEquation.Controls.Add(this.label1);
            this.grpLinearEquation.Controls.Add(this.textMinIrt);
            this.grpLinearEquation.Controls.Add(this.textMaxIrt);
            this.grpLinearEquation.Controls.Add(this.label2);
            resources.ApplyResources(this.grpLinearEquation, "grpLinearEquation");
            this.grpLinearEquation.Name = "grpLinearEquation";
            this.grpLinearEquation.TabStop = false;
            // 
            // comboMaxPeptide
            // 
            this.comboMaxPeptide.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMaxPeptide.FormattingEnabled = true;
            resources.ApplyResources(this.comboMaxPeptide, "comboMaxPeptide");
            this.comboMaxPeptide.Name = "comboMaxPeptide";
            this.comboMaxPeptide.SelectedIndexChanged += new System.EventHandler(this.UpdateControls);
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // comboMinPeptide
            // 
            this.comboMinPeptide.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboMinPeptide.FormattingEnabled = true;
            resources.ApplyResources(this.comboMinPeptide, "comboMinPeptide");
            this.comboMinPeptide.Name = "comboMinPeptide";
            this.comboMinPeptide.SelectedIndexChanged += new System.EventHandler(this.UpdateControls);
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // label7
            // 
            resources.ApplyResources(this.label7, "label7");
            this.label7.Name = "label7";
            // 
            // btnGraphIrts
            // 
            resources.ApplyResources(this.btnGraphIrts, "btnGraphIrts");
            this.btnGraphIrts.Name = "btnGraphIrts";
            this.btnGraphIrts.UseVisualStyleBackColor = true;
            this.btnGraphIrts.Click += new System.EventHandler(this.btnGraphIrts_Click);
            // 
            // labelStandardCount
            // 
            resources.ApplyResources(this.labelStandardCount, "labelStandardCount");
            this.labelStandardCount.Name = "labelStandardCount";
            // 
            // panelPeptides
            // 
            resources.ApplyResources(this.panelPeptides, "panelPeptides");
            this.panelPeptides.Controls.Add(this.label7);
            this.panelPeptides.Controls.Add(this.labelStandardCount);
            this.panelPeptides.Controls.Add(this.gridViewCalibrate);
            this.panelPeptides.Name = "panelPeptides";
            // 
            // lblEquation
            // 
            resources.ApplyResources(this.lblEquation, "lblEquation");
            this.lblEquation.Name = "lblEquation";
            // 
            // CalibrateIrtDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.panelPeptides);
            this.Controls.Add(this.btnGraphIrts);
            this.Controls.Add(this.grpLinearEquation);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.btnUseCurrent);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "CalibrateIrtDlg";
            this.ShowInTaskbar = false;
            this.Load += new System.EventHandler(this.OnLoad);
            ((System.ComponentModel.ISupportInitialize)(this.modeUIHandler)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingSourceStandard)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridViewCalibrate)).EndInit();
            this.grpLinearEquation.ResumeLayout(false);
            this.grpLinearEquation.PerformLayout();
            this.panelPeptides.ResumeLayout(false);
            this.panelPeptides.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.TextBox textMaxIrt;
        private System.Windows.Forms.TextBox textMinIrt;
        private pwiz.Skyline.Controls.DataGridViewEx gridViewCalibrate;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnUseCurrent;
        private System.Windows.Forms.BindingSource bindingSourceStandard;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.ComboBox comboRegression;
        private System.Windows.Forms.Button btnGraph;
        private System.Windows.Forms.GroupBox grpLinearEquation;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.ComboBox comboMaxPeptide;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox comboMinPeptide;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.Label label7;
        private Controls.TargetColumn calibratePeptides;
        private System.Windows.Forms.DataGridViewTextBoxColumn calibrateMeasuredRt;
        private System.Windows.Forms.DataGridViewTextBoxColumn colIrt;
        private System.Windows.Forms.Button btnGraphIrts;
        private System.Windows.Forms.Label labelStandardCount;
        private System.Windows.Forms.Panel panelPeptides;
        private System.Windows.Forms.Label lblEquation;
    }
}