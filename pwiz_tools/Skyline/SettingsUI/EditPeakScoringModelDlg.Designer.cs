namespace pwiz.Skyline.SettingsUI
{
    partial class EditPeakScoringModelDlg
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(EditPeakScoringModelDlg));
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle15 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle18 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle19 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle16 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle17 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle20 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle21 = new System.Windows.Forms.DataGridViewCellStyle();
            this.helpTip = new System.Windows.Forms.ToolTip(this.components);
            this.textName = new System.Windows.Forms.TextBox();
            this.btnTrainModel = new System.Windows.Forms.Button();
            this.gridPeakCalculators = new pwiz.Skyline.Controls.DataGridViewEx();
            this.IsEnabled = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.PeakCalculatorName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PeakCalculatorWeight = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.PeakCalculatorPercentContribution = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.bindingPeakCalculators = new System.Windows.Forms.BindingSource(this.components);
            this.btnOk = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.tabControl1 = new System.Windows.Forms.TabControl();
            this.tabPage1 = new System.Windows.Forms.TabPage();
            this.zedGraphMProphet = new ZedGraph.ZedGraphControl();
            this.tabPage2 = new System.Windows.Forms.TabPage();
            this.toolStripFind = new System.Windows.Forms.ToolStrip();
            this.findPeptidesButton = new System.Windows.Forms.ToolStripButton();
            this.zedGraphSelectedCalculator = new ZedGraph.ZedGraphControl();
            this.tabPage4 = new System.Windows.Forms.TabPage();
            this.zedGraphPValues = new ZedGraph.ZedGraphControl();
            this.tabPage3 = new System.Windows.Forms.TabPage();
            this.zedGraphQValues = new ZedGraph.ZedGraphControl();
            this.label3 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.decoyCheckBox = new System.Windows.Forms.CheckBox();
            this.secondBestCheckBox = new System.Windows.Forms.CheckBox();
            this.label6 = new System.Windows.Forms.Label();
            this.lblColinearWarning = new System.Windows.Forms.Label();
            this.comboModel = new System.Windows.Forms.ComboBox();
            this.label4 = new System.Windows.Forms.Label();
            this.dataGridViewTextBoxColumn1 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn2 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumn3 = new System.Windows.Forms.DataGridViewTextBoxColumn();
            ((System.ComponentModel.ISupportInitialize)(this.gridPeakCalculators)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeakCalculators)).BeginInit();
            this.tabControl1.SuspendLayout();
            this.tabPage1.SuspendLayout();
            this.tabPage2.SuspendLayout();
            this.toolStripFind.SuspendLayout();
            this.tabPage4.SuspendLayout();
            this.tabPage3.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.SuspendLayout();
            // 
            // helpTip
            // 
            this.helpTip.AutoPopDelay = 15000;
            this.helpTip.InitialDelay = 500;
            this.helpTip.ReshowDelay = 100;
            // 
            // textName
            // 
            resources.ApplyResources(this.textName, "textName");
            this.textName.Name = "textName";
            this.helpTip.SetToolTip(this.textName, resources.GetString("textName.ToolTip"));
            // 
            // btnTrainModel
            // 
            resources.ApplyResources(this.btnTrainModel, "btnTrainModel");
            this.btnTrainModel.Name = "btnTrainModel";
            this.helpTip.SetToolTip(this.btnTrainModel, resources.GetString("btnTrainModel.ToolTip"));
            this.btnTrainModel.UseVisualStyleBackColor = true;
            this.btnTrainModel.Click += new System.EventHandler(this.btnTrainModel_Click);
            // 
            // gridPeakCalculators
            // 
            this.gridPeakCalculators.AllowUserToAddRows = false;
            this.gridPeakCalculators.AllowUserToDeleteRows = false;
            resources.ApplyResources(this.gridPeakCalculators, "gridPeakCalculators");
            this.gridPeakCalculators.AutoGenerateColumns = false;
            this.gridPeakCalculators.ClipboardCopyMode = System.Windows.Forms.DataGridViewClipboardCopyMode.EnableAlwaysIncludeHeaderText;
            dataGridViewCellStyle15.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle15.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle15.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle15.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle15.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle15.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle15.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeakCalculators.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle15;
            this.gridPeakCalculators.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.gridPeakCalculators.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.IsEnabled,
            this.PeakCalculatorName,
            this.PeakCalculatorWeight,
            this.PeakCalculatorPercentContribution});
            this.gridPeakCalculators.DataSource = this.bindingPeakCalculators;
            dataGridViewCellStyle18.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle18.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle18.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle18.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle18.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle18.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle18.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.gridPeakCalculators.DefaultCellStyle = dataGridViewCellStyle18;
            this.gridPeakCalculators.Name = "gridPeakCalculators";
            dataGridViewCellStyle19.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle19.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle19.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle19.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle19.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle19.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle19.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.gridPeakCalculators.RowHeadersDefaultCellStyle = dataGridViewCellStyle19;
            this.gridPeakCalculators.RowHeadersVisible = false;
            this.gridPeakCalculators.ShowEditingIcon = false;
            this.helpTip.SetToolTip(this.gridPeakCalculators, resources.GetString("gridPeakCalculators.ToolTip"));
            this.gridPeakCalculators.SelectionChanged += new System.EventHandler(this.gridPeakCalculators_SelectionChanged);
            // 
            // IsEnabled
            // 
            this.IsEnabled.DataPropertyName = "IsEnabled";
            this.IsEnabled.FalseValue = "False";
            resources.ApplyResources(this.IsEnabled, "IsEnabled");
            this.IsEnabled.Name = "IsEnabled";
            this.IsEnabled.TrueValue = "True";
            // 
            // PeakCalculatorName
            // 
            this.PeakCalculatorName.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.PeakCalculatorName.DataPropertyName = "Name";
            this.PeakCalculatorName.FillWeight = 500F;
            resources.ApplyResources(this.PeakCalculatorName, "PeakCalculatorName");
            this.PeakCalculatorName.Name = "PeakCalculatorName";
            this.PeakCalculatorName.ReadOnly = true;
            // 
            // PeakCalculatorWeight
            // 
            this.PeakCalculatorWeight.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.PeakCalculatorWeight.DataPropertyName = "Weight";
            dataGridViewCellStyle16.Format = "N4";
            this.PeakCalculatorWeight.DefaultCellStyle = dataGridViewCellStyle16;
            this.PeakCalculatorWeight.FillWeight = 80F;
            resources.ApplyResources(this.PeakCalculatorWeight, "PeakCalculatorWeight");
            this.PeakCalculatorWeight.Name = "PeakCalculatorWeight";
            this.PeakCalculatorWeight.ReadOnly = true;
            // 
            // PeakCalculatorPercentContribution
            // 
            this.PeakCalculatorPercentContribution.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.PeakCalculatorPercentContribution.DataPropertyName = "PercentContribution";
            dataGridViewCellStyle17.Format = "0.0%";
            dataGridViewCellStyle17.NullValue = null;
            this.PeakCalculatorPercentContribution.DefaultCellStyle = dataGridViewCellStyle17;
            resources.ApplyResources(this.PeakCalculatorPercentContribution, "PeakCalculatorPercentContribution");
            this.PeakCalculatorPercentContribution.Name = "PeakCalculatorPercentContribution";
            this.PeakCalculatorPercentContribution.ReadOnly = true;
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
            // tabControl1
            // 
            resources.ApplyResources(this.tabControl1, "tabControl1");
            this.tabControl1.Controls.Add(this.tabPage1);
            this.tabControl1.Controls.Add(this.tabPage2);
            this.tabControl1.Controls.Add(this.tabPage4);
            this.tabControl1.Controls.Add(this.tabPage3);
            this.tabControl1.Name = "tabControl1";
            this.tabControl1.SelectedIndex = 0;
            this.tabControl1.SelectedIndexChanged += new System.EventHandler(this.tabControl1_SelectedIndexChanged);
            // 
            // tabPage1
            // 
            this.tabPage1.Controls.Add(this.zedGraphMProphet);
            resources.ApplyResources(this.tabPage1, "tabPage1");
            this.tabPage1.Name = "tabPage1";
            this.tabPage1.UseVisualStyleBackColor = true;
            // 
            // zedGraphMProphet
            // 
            resources.ApplyResources(this.zedGraphMProphet, "zedGraphMProphet");
            this.zedGraphMProphet.IsEnableHPan = false;
            this.zedGraphMProphet.IsEnableHZoom = false;
            this.zedGraphMProphet.IsEnableVPan = false;
            this.zedGraphMProphet.IsEnableVZoom = false;
            this.zedGraphMProphet.IsEnableWheelZoom = false;
            this.zedGraphMProphet.IsShowCopyMessage = false;
            this.zedGraphMProphet.Name = "zedGraphMProphet";
            this.zedGraphMProphet.ScrollGrace = 0D;
            this.zedGraphMProphet.ScrollMaxX = 0D;
            this.zedGraphMProphet.ScrollMaxY = 0D;
            this.zedGraphMProphet.ScrollMaxY2 = 0D;
            this.zedGraphMProphet.ScrollMinX = 0D;
            this.zedGraphMProphet.ScrollMinY = 0D;
            this.zedGraphMProphet.ScrollMinY2 = 0D;
            this.zedGraphMProphet.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraph_ContextMenuBuilder);
            // 
            // tabPage2
            // 
            this.tabPage2.Controls.Add(this.toolStripFind);
            this.tabPage2.Controls.Add(this.zedGraphSelectedCalculator);
            resources.ApplyResources(this.tabPage2, "tabPage2");
            this.tabPage2.Name = "tabPage2";
            this.tabPage2.UseVisualStyleBackColor = true;
            // 
            // toolStripFind
            // 
            resources.ApplyResources(this.toolStripFind, "toolStripFind");
            this.toolStripFind.GripStyle = System.Windows.Forms.ToolStripGripStyle.Hidden;
            this.toolStripFind.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.findPeptidesButton});
            this.toolStripFind.Name = "toolStripFind";
            // 
            // findPeptidesButton
            // 
            this.findPeptidesButton.DisplayStyle = System.Windows.Forms.ToolStripItemDisplayStyle.Image;
            this.findPeptidesButton.Image = global::pwiz.Skyline.Properties.Resources.Find;
            resources.ApplyResources(this.findPeptidesButton, "findPeptidesButton");
            this.findPeptidesButton.Name = "findPeptidesButton";
            this.findPeptidesButton.Click += new System.EventHandler(this.findPeptidesButton_Click);
            // 
            // zedGraphSelectedCalculator
            // 
            resources.ApplyResources(this.zedGraphSelectedCalculator, "zedGraphSelectedCalculator");
            this.zedGraphSelectedCalculator.IsEnableHPan = false;
            this.zedGraphSelectedCalculator.IsEnableHZoom = false;
            this.zedGraphSelectedCalculator.IsEnableVPan = false;
            this.zedGraphSelectedCalculator.IsEnableVZoom = false;
            this.zedGraphSelectedCalculator.IsEnableWheelZoom = false;
            this.zedGraphSelectedCalculator.IsShowCopyMessage = false;
            this.zedGraphSelectedCalculator.Name = "zedGraphSelectedCalculator";
            this.zedGraphSelectedCalculator.ScrollGrace = 0D;
            this.zedGraphSelectedCalculator.ScrollMaxX = 0D;
            this.zedGraphSelectedCalculator.ScrollMaxY = 0D;
            this.zedGraphSelectedCalculator.ScrollMaxY2 = 0D;
            this.zedGraphSelectedCalculator.ScrollMinX = 0D;
            this.zedGraphSelectedCalculator.ScrollMinY = 0D;
            this.zedGraphSelectedCalculator.ScrollMinY2 = 0D;
            this.zedGraphSelectedCalculator.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraph_ContextMenuBuilder);
            this.zedGraphSelectedCalculator.MouseMoveEvent += new ZedGraph.ZedGraphControl.ZedMouseEventHandler(this.zedGraphSelectedCalculator_MouseMoveEvent);
            // 
            // tabPage4
            // 
            this.tabPage4.Controls.Add(this.zedGraphPValues);
            resources.ApplyResources(this.tabPage4, "tabPage4");
            this.tabPage4.Name = "tabPage4";
            this.tabPage4.UseVisualStyleBackColor = true;
            // 
            // zedGraphPValues
            // 
            resources.ApplyResources(this.zedGraphPValues, "zedGraphPValues");
            this.zedGraphPValues.IsEnableHPan = false;
            this.zedGraphPValues.IsEnableHZoom = false;
            this.zedGraphPValues.IsEnableVPan = false;
            this.zedGraphPValues.IsEnableVZoom = false;
            this.zedGraphPValues.IsEnableWheelZoom = false;
            this.zedGraphPValues.IsShowCopyMessage = false;
            this.zedGraphPValues.Name = "zedGraphPValues";
            this.zedGraphPValues.ScrollGrace = 0D;
            this.zedGraphPValues.ScrollMaxX = 0D;
            this.zedGraphPValues.ScrollMaxY = 0D;
            this.zedGraphPValues.ScrollMaxY2 = 0D;
            this.zedGraphPValues.ScrollMinX = 0D;
            this.zedGraphPValues.ScrollMinY = 0D;
            this.zedGraphPValues.ScrollMinY2 = 0D;
            this.zedGraphPValues.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraph_ContextMenuBuilder);
            // 
            // tabPage3
            // 
            this.tabPage3.Controls.Add(this.zedGraphQValues);
            resources.ApplyResources(this.tabPage3, "tabPage3");
            this.tabPage3.Name = "tabPage3";
            this.tabPage3.UseVisualStyleBackColor = true;
            // 
            // zedGraphQValues
            // 
            resources.ApplyResources(this.zedGraphQValues, "zedGraphQValues");
            this.zedGraphQValues.IsEnableHPan = false;
            this.zedGraphQValues.IsEnableHZoom = false;
            this.zedGraphQValues.IsEnableVPan = false;
            this.zedGraphQValues.IsEnableVZoom = false;
            this.zedGraphQValues.IsEnableWheelZoom = false;
            this.zedGraphQValues.IsShowCopyMessage = false;
            this.zedGraphQValues.Name = "zedGraphQValues";
            this.zedGraphQValues.ScrollGrace = 0D;
            this.zedGraphQValues.ScrollMaxX = 0D;
            this.zedGraphQValues.ScrollMaxY = 0D;
            this.zedGraphQValues.ScrollMaxY2 = 0D;
            this.zedGraphQValues.ScrollMinX = 0D;
            this.zedGraphQValues.ScrollMinY = 0D;
            this.zedGraphQValues.ScrollMinY2 = 0D;
            this.zedGraphQValues.ContextMenuBuilder += new ZedGraph.ZedGraphControl.ContextMenuBuilderEventHandler(this.zedGraph_ContextMenuBuilder);
            // 
            // label3
            // 
            resources.ApplyResources(this.label3, "label3");
            this.label3.Name = "label3";
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.decoyCheckBox);
            this.groupBox2.Controls.Add(this.secondBestCheckBox);
            this.groupBox2.Controls.Add(this.btnTrainModel);
            resources.ApplyResources(this.groupBox2, "groupBox2");
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.TabStop = false;
            // 
            // decoyCheckBox
            // 
            resources.ApplyResources(this.decoyCheckBox, "decoyCheckBox");
            this.decoyCheckBox.Name = "decoyCheckBox";
            this.decoyCheckBox.UseVisualStyleBackColor = true;
            this.decoyCheckBox.CheckedChanged += new System.EventHandler(this.decoyCheckBox_CheckedChanged);
            // 
            // secondBestCheckBox
            // 
            resources.ApplyResources(this.secondBestCheckBox, "secondBestCheckBox");
            this.secondBestCheckBox.Name = "secondBestCheckBox";
            this.secondBestCheckBox.UseVisualStyleBackColor = true;
            this.secondBestCheckBox.CheckedChanged += new System.EventHandler(this.falseTargetCheckBox_CheckedChanged);
            // 
            // label6
            // 
            resources.ApplyResources(this.label6, "label6");
            this.label6.Name = "label6";
            // 
            // lblColinearWarning
            // 
            resources.ApplyResources(this.lblColinearWarning, "lblColinearWarning");
            this.lblColinearWarning.ForeColor = System.Drawing.Color.Red;
            this.lblColinearWarning.Name = "lblColinearWarning";
            // 
            // comboModel
            // 
            this.comboModel.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboModel.FormattingEnabled = true;
            resources.ApplyResources(this.comboModel, "comboModel");
            this.comboModel.Name = "comboModel";
            // 
            // label4
            // 
            resources.ApplyResources(this.label4, "label4");
            this.label4.Name = "label4";
            // 
            // dataGridViewTextBoxColumn1
            // 
            this.dataGridViewTextBoxColumn1.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.Fill;
            this.dataGridViewTextBoxColumn1.DataPropertyName = "Name";
            this.dataGridViewTextBoxColumn1.FillWeight = 500F;
            resources.ApplyResources(this.dataGridViewTextBoxColumn1, "dataGridViewTextBoxColumn1");
            this.dataGridViewTextBoxColumn1.Name = "dataGridViewTextBoxColumn1";
            this.dataGridViewTextBoxColumn1.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn2
            // 
            this.dataGridViewTextBoxColumn2.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.dataGridViewTextBoxColumn2.DataPropertyName = "Weight";
            dataGridViewCellStyle20.Format = "N4";
            this.dataGridViewTextBoxColumn2.DefaultCellStyle = dataGridViewCellStyle20;
            this.dataGridViewTextBoxColumn2.FillWeight = 80F;
            resources.ApplyResources(this.dataGridViewTextBoxColumn2, "dataGridViewTextBoxColumn2");
            this.dataGridViewTextBoxColumn2.Name = "dataGridViewTextBoxColumn2";
            this.dataGridViewTextBoxColumn2.ReadOnly = true;
            // 
            // dataGridViewTextBoxColumn3
            // 
            this.dataGridViewTextBoxColumn3.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.dataGridViewTextBoxColumn3.DataPropertyName = "PercentContribution";
            dataGridViewCellStyle21.Format = "0.0%";
            dataGridViewCellStyle21.NullValue = null;
            this.dataGridViewTextBoxColumn3.DefaultCellStyle = dataGridViewCellStyle21;
            resources.ApplyResources(this.dataGridViewTextBoxColumn3, "dataGridViewTextBoxColumn3");
            this.dataGridViewTextBoxColumn3.Name = "dataGridViewTextBoxColumn3";
            this.dataGridViewTextBoxColumn3.ReadOnly = true;
            // 
            // EditPeakScoringModelDlg
            // 
            this.AcceptButton = this.btnOk;
            resources.ApplyResources(this, "$this");
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.Controls.Add(this.gridPeakCalculators);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.label6);
            this.Controls.Add(this.lblColinearWarning);
            this.Controls.Add(this.comboModel);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.label4);
            this.Controls.Add(this.btnOk);
            this.Controls.Add(this.textName);
            this.Controls.Add(this.tabControl1);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "EditPeakScoringModelDlg";
            this.ShowInTaskbar = false;
            ((System.ComponentModel.ISupportInitialize)(this.gridPeakCalculators)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.bindingPeakCalculators)).EndInit();
            this.tabControl1.ResumeLayout(false);
            this.tabPage1.ResumeLayout(false);
            this.tabPage2.ResumeLayout(false);
            this.tabPage2.PerformLayout();
            this.toolStripFind.ResumeLayout(false);
            this.toolStripFind.PerformLayout();
            this.tabPage4.ResumeLayout(false);
            this.tabPage3.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox2.PerformLayout();
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox textName;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnOk;
        private pwiz.Skyline.Controls.DataGridViewEx gridPeakCalculators;
        private System.Windows.Forms.Button btnTrainModel;
        private System.Windows.Forms.ToolTip helpTip;
        private System.Windows.Forms.BindingSource bindingPeakCalculators;
        private System.Windows.Forms.Label label3;
        private ZedGraph.ZedGraphControl zedGraphMProphet;
        private ZedGraph.ZedGraphControl zedGraphSelectedCalculator;
        private System.Windows.Forms.Label lblColinearWarning;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.ComboBox comboModel;
        private System.Windows.Forms.CheckBox secondBestCheckBox;
        private System.Windows.Forms.CheckBox decoyCheckBox;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.TabControl tabControl1;
        private System.Windows.Forms.TabPage tabPage1;
        private System.Windows.Forms.TabPage tabPage2;
        private System.Windows.Forms.DataGridViewCheckBoxColumn IsEnabled;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeakCalculatorName;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeakCalculatorWeight;
        private System.Windows.Forms.DataGridViewTextBoxColumn PeakCalculatorPercentContribution;
        private System.Windows.Forms.ToolStrip toolStripFind;
        private System.Windows.Forms.ToolStripButton findPeptidesButton;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn1;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn2;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumn3;
        private System.Windows.Forms.TabPage tabPage3;
        private ZedGraph.ZedGraphControl zedGraphQValues;
        private System.Windows.Forms.TabPage tabPage4;
        private ZedGraph.ZedGraphControl zedGraphPValues;
    }
}