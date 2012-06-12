namespace pwiz.Topograph.ui.Forms
{
    partial class AddSearchResultsForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AddSearchResultsForm));
            this.tbxStatus = new System.Windows.Forms.TextBox();
            this.btnCancel = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.cbxOnlyExistingPeptides = new System.Windows.Forms.CheckBox();
            this.tableLayoutPanel1 = new System.Windows.Forms.TableLayoutPanel();
            this.tbxMinXCorr3 = new System.Windows.Forms.TextBox();
            this.tbxMinXCorr2 = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.label3 = new System.Windows.Forms.Label();
            this.label4 = new System.Windows.Forms.Label();
            this.tbxMinXCorr1 = new System.Windows.Forms.TextBox();
            this.cbxMinimumXCorr = new System.Windows.Forms.CheckBox();
            this.btnChooseSqtFiles = new System.Windows.Forms.Button();
            this.label1 = new System.Windows.Forms.Label();
            this.groupBox1 = new System.Windows.Forms.GroupBox();
            this.btnChooseDTASelect = new System.Windows.Forms.Button();
            this.label5 = new System.Windows.Forms.Label();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.label6 = new System.Windows.Forms.Label();
            this.groupBox3 = new System.Windows.Forms.GroupBox();
            this.tbxMaxQValue = new System.Windows.Forms.TextBox();
            this.label7 = new System.Windows.Forms.Label();
            this.btnChoosePercolatorResults = new System.Windows.Forms.Button();
            this.groupBox4 = new System.Windows.Forms.GroupBox();
            this.btnChoosePeptideList = new System.Windows.Forms.Button();
            this.groupBox5 = new System.Windows.Forms.GroupBox();
            this.btnBiblioSpec = new System.Windows.Forms.Button();
            this.tableLayoutPanel1.SuspendLayout();
            this.groupBox1.SuspendLayout();
            this.groupBox2.SuspendLayout();
            this.groupBox3.SuspendLayout();
            this.groupBox4.SuspendLayout();
            this.groupBox5.SuspendLayout();
            this.SuspendLayout();
            // 
            // tbxStatus
            // 
            this.tbxStatus.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxStatus.Location = new System.Drawing.Point(1, 550);
            this.tbxStatus.Multiline = true;
            this.tbxStatus.Name = "tbxStatus";
            this.tbxStatus.ReadOnly = true;
            this.tbxStatus.Size = new System.Drawing.Size(887, 18);
            this.tbxStatus.TabIndex = 1;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Location = new System.Drawing.Point(805, 619);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(65, 27);
            this.btnCancel.TabIndex = 0;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(1, 574);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(887, 20);
            this.progressBar.TabIndex = 2;
            // 
            // cbxOnlyExistingPeptides
            // 
            this.cbxOnlyExistingPeptides.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.cbxOnlyExistingPeptides.Location = new System.Drawing.Point(10, 44);
            this.cbxOnlyExistingPeptides.Name = "cbxOnlyExistingPeptides";
            this.cbxOnlyExistingPeptides.Size = new System.Drawing.Size(849, 34);
            this.cbxOnlyExistingPeptides.TabIndex = 3;
            this.cbxOnlyExistingPeptides.Text = "Only add search results whose peptide sequence is already in this workspace";
            this.cbxOnlyExistingPeptides.UseVisualStyleBackColor = true;
            this.cbxOnlyExistingPeptides.CheckedChanged += new System.EventHandler(this.cbxOnlyExistingPeptides_CheckedChanged);
            // 
            // tableLayoutPanel1
            // 
            this.tableLayoutPanel1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tableLayoutPanel1.ColumnCount = 2;
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 36.17021F));
            this.tableLayoutPanel1.ColumnStyles.Add(new System.Windows.Forms.ColumnStyle(System.Windows.Forms.SizeType.Percent, 63.82979F));
            this.tableLayoutPanel1.Controls.Add(this.tbxMinXCorr3, 1, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinXCorr2, 1, 2);
            this.tableLayoutPanel1.Controls.Add(this.label2, 0, 1);
            this.tableLayoutPanel1.Controls.Add(this.label3, 0, 2);
            this.tableLayoutPanel1.Controls.Add(this.label4, 0, 3);
            this.tableLayoutPanel1.Controls.Add(this.tbxMinXCorr1, 1, 1);
            this.tableLayoutPanel1.Controls.Add(this.cbxMinimumXCorr, 0, 0);
            this.tableLayoutPanel1.Location = new System.Drawing.Point(9, 84);
            this.tableLayoutPanel1.Name = "tableLayoutPanel1";
            this.tableLayoutPanel1.RowCount = 4;
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Percent, 25F));
            this.tableLayoutPanel1.RowStyles.Add(new System.Windows.Forms.RowStyle(System.Windows.Forms.SizeType.Absolute, 20F));
            this.tableLayoutPanel1.Size = new System.Drawing.Size(840, 101);
            this.tableLayoutPanel1.TabIndex = 5;
            // 
            // tbxMinXCorr3
            // 
            this.tbxMinXCorr3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinXCorr3.Enabled = false;
            this.tbxMinXCorr3.Location = new System.Drawing.Point(306, 78);
            this.tbxMinXCorr3.Name = "tbxMinXCorr3";
            this.tbxMinXCorr3.Size = new System.Drawing.Size(531, 20);
            this.tbxMinXCorr3.TabIndex = 5;
            this.tbxMinXCorr3.Text = "2.4";
            // 
            // tbxMinXCorr2
            // 
            this.tbxMinXCorr2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinXCorr2.Enabled = false;
            this.tbxMinXCorr2.Location = new System.Drawing.Point(306, 53);
            this.tbxMinXCorr2.Name = "tbxMinXCorr2";
            this.tbxMinXCorr2.Size = new System.Drawing.Size(531, 20);
            this.tbxMinXCorr2.TabIndex = 4;
            this.tbxMinXCorr2.Text = "2.0";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label2.Location = new System.Drawing.Point(3, 25);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(297, 25);
            this.label2.TabIndex = 0;
            this.label2.Text = "Charge 1:";
            this.label2.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label3.Location = new System.Drawing.Point(3, 50);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(297, 25);
            this.label3.TabIndex = 1;
            this.label3.Text = "Charge 2:";
            this.label3.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // label4
            // 
            this.label4.AutoSize = true;
            this.label4.Dock = System.Windows.Forms.DockStyle.Fill;
            this.label4.Location = new System.Drawing.Point(3, 75);
            this.label4.Name = "label4";
            this.label4.Size = new System.Drawing.Size(297, 26);
            this.label4.TabIndex = 2;
            this.label4.Text = "Charge 3 or more:";
            this.label4.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // tbxMinXCorr1
            // 
            this.tbxMinXCorr1.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tbxMinXCorr1.Enabled = false;
            this.tbxMinXCorr1.Location = new System.Drawing.Point(306, 28);
            this.tbxMinXCorr1.Name = "tbxMinXCorr1";
            this.tbxMinXCorr1.Size = new System.Drawing.Size(531, 20);
            this.tbxMinXCorr1.TabIndex = 3;
            this.tbxMinXCorr1.Text = "1.8";
            // 
            // cbxMinimumXCorr
            // 
            this.cbxMinimumXCorr.AutoSize = true;
            this.tableLayoutPanel1.SetColumnSpan(this.cbxMinimumXCorr, 2);
            this.cbxMinimumXCorr.Location = new System.Drawing.Point(3, 3);
            this.cbxMinimumXCorr.Name = "cbxMinimumXCorr";
            this.cbxMinimumXCorr.Size = new System.Drawing.Size(131, 17);
            this.cbxMinimumXCorr.TabIndex = 6;
            this.cbxMinimumXCorr.Text = "Minimum XCorr Values";
            this.cbxMinimumXCorr.UseVisualStyleBackColor = true;
            this.cbxMinimumXCorr.CheckedChanged += new System.EventHandler(this.cbxMinimumXCorr_CheckedChanged);
            // 
            // btnChooseSqtFiles
            // 
            this.btnChooseSqtFiles.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChooseSqtFiles.Location = new System.Drawing.Point(745, 193);
            this.btnChooseSqtFiles.Name = "btnChooseSqtFiles";
            this.btnChooseSqtFiles.Size = new System.Drawing.Size(113, 27);
            this.btnChooseSqtFiles.TabIndex = 6;
            this.btnChooseSqtFiles.Text = "Choose SQT Files...";
            this.btnChooseSqtFiles.UseVisualStyleBackColor = true;
            this.btnChooseSqtFiles.Click += new System.EventHandler(this.btnChooseFiles_Click);
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(9, 9);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(268, 13);
            this.label1.TabIndex = 7;
            this.label1.Text = "Topograph understands a few search result file formats.";
            // 
            // groupBox1
            // 
            this.groupBox1.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox1.Controls.Add(this.btnChooseDTASelect);
            this.groupBox1.Controls.Add(this.label5);
            this.groupBox1.Location = new System.Drawing.Point(12, 143);
            this.groupBox1.Name = "groupBox1";
            this.groupBox1.Size = new System.Drawing.Size(865, 46);
            this.groupBox1.TabIndex = 8;
            this.groupBox1.TabStop = false;
            this.groupBox1.Text = "DTASelect";
            // 
            // btnChooseDTASelect
            // 
            this.btnChooseDTASelect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnChooseDTASelect.Location = new System.Drawing.Point(675, 16);
            this.btnChooseDTASelect.Name = "btnChooseDTASelect";
            this.btnChooseDTASelect.Size = new System.Drawing.Size(183, 23);
            this.btnChooseDTASelect.TabIndex = 1;
            this.btnChooseDTASelect.Text = "Choose DTASelect Filter Files...";
            this.btnChooseDTASelect.UseVisualStyleBackColor = true;
            this.btnChooseDTASelect.Click += new System.EventHandler(this.btnChooseDTASelect_Click);
            // 
            // label5
            // 
            this.label5.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label5.Location = new System.Drawing.Point(6, 21);
            this.label5.Name = "label5";
            this.label5.Size = new System.Drawing.Size(663, 18);
            this.label5.TabIndex = 0;
            this.label5.Text = "You can choose a DTASelect filter file, and Topograph will add all of the peptide" +
                "s in it.";
            // 
            // groupBox2
            // 
            this.groupBox2.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox2.Controls.Add(this.label6);
            this.groupBox2.Controls.Add(this.tableLayoutPanel1);
            this.groupBox2.Controls.Add(this.cbxOnlyExistingPeptides);
            this.groupBox2.Controls.Add(this.btnChooseSqtFiles);
            this.groupBox2.Location = new System.Drawing.Point(12, 318);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(865, 226);
            this.groupBox2.TabIndex = 9;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Sequest or .pep.xml";
            // 
            // label6
            // 
            this.label6.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.label6.Location = new System.Drawing.Point(7, 16);
            this.label6.Name = "label6";
            this.label6.Size = new System.Drawing.Size(842, 33);
            this.label6.TabIndex = 6;
            this.label6.Text = "You can specify restrictions on which peptides are added from a Sequest (.sqt) or" +
                " pep.xml search results file";
            // 
            // groupBox3
            // 
            this.groupBox3.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox3.Controls.Add(this.tbxMaxQValue);
            this.groupBox3.Controls.Add(this.label7);
            this.groupBox3.Controls.Add(this.btnChoosePercolatorResults);
            this.groupBox3.Location = new System.Drawing.Point(13, 195);
            this.groupBox3.Name = "groupBox3";
            this.groupBox3.Size = new System.Drawing.Size(858, 57);
            this.groupBox3.TabIndex = 10;
            this.groupBox3.TabStop = false;
            this.groupBox3.Text = "Percolator Combined Results";
            // 
            // tbxMaxQValue
            // 
            this.tbxMaxQValue.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.tbxMaxQValue.Location = new System.Drawing.Point(130, 29);
            this.tbxMaxQValue.Name = "tbxMaxQValue";
            this.tbxMaxQValue.Size = new System.Drawing.Size(533, 20);
            this.tbxMaxQValue.TabIndex = 2;
            this.tbxMaxQValue.Text = "0.01";
            this.tbxMaxQValue.TextAlign = System.Windows.Forms.HorizontalAlignment.Right;
            // 
            // label7
            // 
            this.label7.AutoSize = true;
            this.label7.Location = new System.Drawing.Point(14, 31);
            this.label7.Name = "label7";
            this.label7.Size = new System.Drawing.Size(95, 13);
            this.label7.TabIndex = 1;
            this.label7.Text = "Maximum Q Value:";
            // 
            // btnChoosePercolatorResults
            // 
            this.btnChoosePercolatorResults.Location = new System.Drawing.Point(669, 26);
            this.btnChoosePercolatorResults.Name = "btnChoosePercolatorResults";
            this.btnChoosePercolatorResults.Size = new System.Drawing.Size(183, 23);
            this.btnChoosePercolatorResults.TabIndex = 0;
            this.btnChoosePercolatorResults.Text = "Choose Percolator Results...";
            this.btnChoosePercolatorResults.UseVisualStyleBackColor = true;
            this.btnChoosePercolatorResults.Click += new System.EventHandler(this.btnChoosePercolatorResults_Click);
            // 
            // groupBox4
            // 
            this.groupBox4.Controls.Add(this.btnChoosePeptideList);
            this.groupBox4.Location = new System.Drawing.Point(14, 260);
            this.groupBox4.Name = "groupBox4";
            this.groupBox4.Size = new System.Drawing.Size(863, 52);
            this.groupBox4.TabIndex = 11;
            this.groupBox4.TabStop = false;
            this.groupBox4.Text = "Simple List of Peptides";
            // 
            // btnChoosePeptideList
            // 
            this.btnChoosePeptideList.Location = new System.Drawing.Point(673, 19);
            this.btnChoosePeptideList.Name = "btnChoosePeptideList";
            this.btnChoosePeptideList.Size = new System.Drawing.Size(178, 23);
            this.btnChoosePeptideList.TabIndex = 0;
            this.btnChoosePeptideList.Text = "Chose Peptide List...";
            this.btnChoosePeptideList.UseVisualStyleBackColor = true;
            this.btnChoosePeptideList.Click += new System.EventHandler(this.btnChoosePeptideList_Click);
            // 
            // groupBox5
            // 
            this.groupBox5.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.groupBox5.Controls.Add(this.btnBiblioSpec);
            this.groupBox5.Location = new System.Drawing.Point(14, 37);
            this.groupBox5.Name = "groupBox5";
            this.groupBox5.Size = new System.Drawing.Size(856, 100);
            this.groupBox5.TabIndex = 12;
            this.groupBox5.TabStop = false;
            this.groupBox5.Text = "Most File Formats";
            // 
            // btnBiblioSpec
            // 
            this.btnBiblioSpec.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.btnBiblioSpec.Location = new System.Drawing.Point(673, 62);
            this.btnBiblioSpec.Name = "btnBiblioSpec";
            this.btnBiblioSpec.Size = new System.Drawing.Size(177, 23);
            this.btnBiblioSpec.TabIndex = 0;
            this.btnBiblioSpec.Text = "Use BiblioSpec";
            this.btnBiblioSpec.UseVisualStyleBackColor = true;
            this.btnBiblioSpec.Click += new System.EventHandler(this.btnBiblioSpec_Click);
            // 
            // AddSearchResultsForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(889, 658);
            this.Controls.Add(this.groupBox5);
            this.Controls.Add(this.groupBox4);
            this.Controls.Add(this.groupBox3);
            this.Controls.Add(this.groupBox2);
            this.Controls.Add(this.groupBox1);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.tbxStatus);
            this.Name = "AddSearchResultsForm";
            this.TabText = "Add Search Results";
            this.Text = "Add Search Results";
            this.tableLayoutPanel1.ResumeLayout(false);
            this.tableLayoutPanel1.PerformLayout();
            this.groupBox1.ResumeLayout(false);
            this.groupBox2.ResumeLayout(false);
            this.groupBox3.ResumeLayout(false);
            this.groupBox3.PerformLayout();
            this.groupBox4.ResumeLayout(false);
            this.groupBox5.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.TextBox tbxStatus;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.CheckBox cbxOnlyExistingPeptides;
        private System.Windows.Forms.TableLayoutPanel tableLayoutPanel1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Label label4;
        private System.Windows.Forms.TextBox tbxMinXCorr1;
        private System.Windows.Forms.TextBox tbxMinXCorr3;
        private System.Windows.Forms.TextBox tbxMinXCorr2;
        private System.Windows.Forms.Button btnChooseSqtFiles;
        private System.Windows.Forms.CheckBox cbxMinimumXCorr;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.GroupBox groupBox1;
        private System.Windows.Forms.Button btnChooseDTASelect;
        private System.Windows.Forms.Label label5;
        private System.Windows.Forms.GroupBox groupBox2;
        private System.Windows.Forms.Label label6;
        private System.Windows.Forms.GroupBox groupBox3;
        private System.Windows.Forms.Button btnChoosePercolatorResults;
        private System.Windows.Forms.TextBox tbxMaxQValue;
        private System.Windows.Forms.Label label7;
        private System.Windows.Forms.GroupBox groupBox4;
        private System.Windows.Forms.Button btnChoosePeptideList;
        private System.Windows.Forms.GroupBox groupBox5;
        private System.Windows.Forms.Button btnBiblioSpec;
    }
}