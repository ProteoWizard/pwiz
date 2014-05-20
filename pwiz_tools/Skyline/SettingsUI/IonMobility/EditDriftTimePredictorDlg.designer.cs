namespace pwiz.Skyline.SettingsUI.IonMobility
{
    partial class EditDriftTimePredictorDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditDriftTimePredictorDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.bindingChargeRegressionLines = new System.Windows.Forms.BindingSource(this.components);
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.gridRegression = new System.Windows.Forms.DataGridView();
            this.Charge = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Slope = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.Intercept = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.comboLibrary = new System.Windows.Forms.ComboBox();
            this.label5 = new System.Windows.Forms.Label();
            this.textResolvingPower = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.textName = new System.Windows.Forms.TextBox();
            this.label4 = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.bindingChargeRegressionLines)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridRegression)).BeginInit();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
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
            // label1
            // 
            resources.ApplyResources(this.label1, "label1");
            this.label1.Name = "label1";
            // 
            // gridRegression
            // 
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridRegression.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.gridRegression.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridRegression.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.Charge,
            this.Slope,
            this.Intercept});
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridRegression.DefaultCellStyle = dataGridViewCellStyle2;
            resources.ApplyResources(this.gridRegression, "gridRegression");
            this.gridRegression.Name = "gridRegression";
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridRegression.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.helpTip.SetToolTip(this.gridRegression, resources.GetString("gridRegression.ToolTip"));
            this.gridRegression.KeyDown += new System.Windows.Forms.KeyEventHandler(this.gridRegression_KeyDown);
            // 
            // Charge
            // 
            resources.ApplyResources(this.Charge, "Charge");
            this.Charge.Name = "Charge";
            // 
            // Slope
            // 
            resources.ApplyResources(this.Slope, "Slope");
            this.Slope.Name = "Slope";
            // 
            // Intercept
            // 
            resources.ApplyResources(this.Intercept, "Intercept");
            this.Intercept.Name = "Intercept";
            // 
            // comboLibrary
            // 
            resources.ApplyResources(this.comboLibrary, "comboLibrary");
            this.comboLibrary.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboLibrary.FormattingEnabled = true;
            this.comboLibrary.Name = "comboLibrary";
            this.helpTip.SetToolTip(this.comboLibrary, resources.GetString("comboLibrary.ToolTip"));
            this.comboLibrary.SelectedIndexChanged += new System.EventHandler(this.comboIonMobilityLibrary_SelectedIndexChanged);
            // 
            // label5
            // 
            resources.ApplyResources(this.label5, "label5");
            this.label5.Name = "label5";
            // 
            // textResolvingPower
            // 
            resources.ApplyResources(this.textResolvingPower, "textResolvingPower");
            this.textResolvingPower.Name = "textResolvingPower";
            this.helpTip.SetToolTip(this.textResolvingPower, resources.GetString("textResolvingPower.ToolTip"));
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
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // EditDriftTimePredictorDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.label1);
            this.Controls.Add(this.gridRegression);
            this.Controls.Add(this.comboLibrary);
            this.Controls.Add(this.label5);
            this.Controls.Add(this.textResolvingPower);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.btnOk);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditDriftTimePredictorDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.bindingChargeRegressionLines)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.gridRegression)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.TextBox textResolvingPower;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.ComboBox comboLibrary;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.BindingSource bindingChargeRegressionLines;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.DataGridView gridRegression;
        private System.Windows.Forms.DataGridViewTextBoxColumn Charge;
        private System.Windows.Forms.DataGridViewTextBoxColumn Slope;
        private System.Windows.Forms.DataGridViewTextBoxColumn Intercept;
    }
}