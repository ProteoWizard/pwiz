namespace ProteomeDb.Forms
{
    partial class ProteomeDbForm
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
            this.btnCreate = new System.Windows.Forms.Button();
            this.btnOpen = new System.Windows.Forms.Button();
            this.lbxOrganisms = new System.Windows.Forms.ListBox();
            this.btnAddOrganism = new System.Windows.Forms.Button();
            this.lbxDigestion = new System.Windows.Forms.ListBox();
            this.btnDigest = new System.Windows.Forms.Button();
            this.tbxTask = new System.Windows.Forms.TextBox();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.btnCancel = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnCreate
            // 
            this.btnCreate.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCreate.Location = new System.Drawing.Point(17, 19);
            this.btnCreate.Name = "btnCreate";
            this.btnCreate.Size = new System.Drawing.Size(255, 23);
            this.btnCreate.TabIndex = 0;
            this.btnCreate.Text = "Create Proteome Database";
            this.btnCreate.UseVisualStyleBackColor = true;
            this.btnCreate.Click += new System.EventHandler(this.btnCreate_Click);
            // 
            // btnOpen
            // 
            this.btnOpen.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOpen.Location = new System.Drawing.Point(16, 55);
            this.btnOpen.Name = "btnOpen";
            this.btnOpen.Size = new System.Drawing.Size(256, 23);
            this.btnOpen.TabIndex = 1;
            this.btnOpen.Text = "Open Proteome Database";
            this.btnOpen.UseVisualStyleBackColor = true;
            this.btnOpen.Click += new System.EventHandler(this.btnOpen_Click);
            // 
            // lbxOrganisms
            // 
            this.lbxOrganisms.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.lbxOrganisms.FormattingEnabled = true;
            this.lbxOrganisms.Location = new System.Drawing.Point(17, 93);
            this.lbxOrganisms.Name = "lbxOrganisms";
            this.lbxOrganisms.Size = new System.Drawing.Size(255, 43);
            this.lbxOrganisms.TabIndex = 2;
            // 
            // btnAddOrganism
            // 
            this.btnAddOrganism.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.btnAddOrganism.Enabled = false;
            this.btnAddOrganism.Location = new System.Drawing.Point(16, 142);
            this.btnAddOrganism.Name = "btnAddOrganism";
            this.btnAddOrganism.Size = new System.Drawing.Size(254, 23);
            this.btnAddOrganism.TabIndex = 3;
            this.btnAddOrganism.Text = "Add Organism";
            this.btnAddOrganism.UseVisualStyleBackColor = true;
            this.btnAddOrganism.Click += new System.EventHandler(this.btnAddOrganism_Click);
            // 
            // lbxDigestion
            // 
            this.lbxDigestion.FormattingEnabled = true;
            this.lbxDigestion.Location = new System.Drawing.Point(18, 173);
            this.lbxDigestion.Name = "lbxDigestion";
            this.lbxDigestion.Size = new System.Drawing.Size(252, 43);
            this.lbxDigestion.TabIndex = 4;
            // 
            // btnDigest
            // 
            this.btnDigest.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.btnDigest.Enabled = false;
            this.btnDigest.Location = new System.Drawing.Point(15, 222);
            this.btnDigest.Name = "btnDigest";
            this.btnDigest.Size = new System.Drawing.Size(255, 23);
            this.btnDigest.TabIndex = 5;
            this.btnDigest.Text = "Digest";
            this.btnDigest.UseVisualStyleBackColor = true;
            this.btnDigest.Click += new System.EventHandler(this.btnDigest_Click);
            // 
            // tbxTask
            // 
            this.tbxTask.Location = new System.Drawing.Point(20, 251);
            this.tbxTask.Name = "tbxTask";
            this.tbxTask.ReadOnly = true;
            this.tbxTask.Size = new System.Drawing.Size(252, 20);
            this.tbxTask.TabIndex = 6;
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(19, 283);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(251, 23);
            this.progressBar.TabIndex = 7;
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left)
                        | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.Enabled = false;
            this.btnCancel.Location = new System.Drawing.Point(20, 312);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(252, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // ProteomeDbForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(284, 347);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.tbxTask);
            this.Controls.Add(this.btnDigest);
            this.Controls.Add(this.lbxDigestion);
            this.Controls.Add(this.btnAddOrganism);
            this.Controls.Add(this.lbxOrganisms);
            this.Controls.Add(this.btnOpen);
            this.Controls.Add(this.btnCreate);
            this.Name = "ProteomeDbForm";
            this.Text = "ProteinDbForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCreate;
        private System.Windows.Forms.Button btnOpen;
        private System.Windows.Forms.ListBox lbxOrganisms;
        private System.Windows.Forms.Button btnAddOrganism;
        private System.Windows.Forms.ListBox lbxDigestion;
        private System.Windows.Forms.Button btnDigest;
        private System.Windows.Forms.TextBox tbxTask;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Button btnCancel;
    }
}