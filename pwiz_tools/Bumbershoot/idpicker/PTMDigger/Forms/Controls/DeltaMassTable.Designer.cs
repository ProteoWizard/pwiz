namespace Forms.Controls
{
    partial class DeltaMassTable
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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle2 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            this.splitContainer = new System.Windows.Forms.SplitContainer();
            this.deltaMassMatrix = new System.Windows.Forms.DataGridView();
            this.groupBox2 = new System.Windows.Forms.GroupBox();
            this.rawDataPath = new System.Windows.Forms.RichTextBox();
            this.unimodAnnotations = new System.Windows.Forms.GroupBox();
            this.unimodAnnotation = new System.Windows.Forms.RichTextBox();
            this.splitContainer.Panel1.SuspendLayout();
            this.splitContainer.Panel2.SuspendLayout();
            this.splitContainer.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.deltaMassMatrix)).BeginInit();
            this.groupBox2.SuspendLayout();
            this.unimodAnnotations.SuspendLayout();
            this.SuspendLayout();
            // 
            // splitContainer
            // 
            this.splitContainer.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.splitContainer.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitContainer.Location = new System.Drawing.Point(0, 0);
            this.splitContainer.Name = "splitContainer";
            this.splitContainer.Orientation = System.Windows.Forms.Orientation.Horizontal;
            // 
            // splitContainer.Panel1
            // 
            this.splitContainer.Panel1.Controls.Add(this.deltaMassMatrix);
            // 
            // splitContainer.Panel2
            // 
            this.splitContainer.Panel2.Controls.Add(this.groupBox2);
            this.splitContainer.Panel2.Controls.Add(this.unimodAnnotations);
            this.splitContainer.Size = new System.Drawing.Size(1152, 575);
            this.splitContainer.SplitterDistance = 440;
            this.splitContainer.TabIndex = 0;
            // 
            // deltaMassMatrix
            // 
            this.deltaMassMatrix.AllowUserToAddRows = false;
            this.deltaMassMatrix.AllowUserToDeleteRows = false;
            this.deltaMassMatrix.AllowUserToOrderColumns = true;
            this.deltaMassMatrix.BackgroundColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.deltaMassMatrix.ColumnHeadersDefaultCellStyle = dataGridViewCellStyle1;
            this.deltaMassMatrix.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            dataGridViewCellStyle2.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle2.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle2.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle2.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle2.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle2.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle2.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.deltaMassMatrix.DefaultCellStyle = dataGridViewCellStyle2;
            this.deltaMassMatrix.Dock = System.Windows.Forms.DockStyle.Fill;
            this.deltaMassMatrix.Location = new System.Drawing.Point(0, 0);
            this.deltaMassMatrix.MultiSelect = false;
            this.deltaMassMatrix.Name = "deltaMassMatrix";
            this.deltaMassMatrix.ReadOnly = true;
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Control;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.WindowText;
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.True;
            this.deltaMassMatrix.RowHeadersDefaultCellStyle = dataGridViewCellStyle3;
            this.deltaMassMatrix.RowTemplate.Height = 24;
            this.deltaMassMatrix.Size = new System.Drawing.Size(1150, 438);
            this.deltaMassMatrix.TabIndex = 0;
            // 
            // groupBox2
            // 
            this.groupBox2.Controls.Add(this.rawDataPath);
            this.groupBox2.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.groupBox2.Location = new System.Drawing.Point(6, 7);
            this.groupBox2.Name = "groupBox2";
            this.groupBox2.Size = new System.Drawing.Size(336, 115);
            this.groupBox2.TabIndex = 6;
            this.groupBox2.TabStop = false;
            this.groupBox2.Text = "Where is your raw data?";
            // 
            // rawDataPath
            // 
            this.rawDataPath.BackColor = System.Drawing.SystemColors.Control;
            this.rawDataPath.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.rawDataPath.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rawDataPath.Location = new System.Drawing.Point(3, 18);
            this.rawDataPath.Name = "rawDataPath";
            this.rawDataPath.Size = new System.Drawing.Size(330, 94);
            this.rawDataPath.TabIndex = 2;
            this.rawDataPath.Text = "";
            // 
            // unimodAnnotations
            // 
            this.unimodAnnotations.Controls.Add(this.unimodAnnotation);
            this.unimodAnnotations.Font = new System.Drawing.Font("Microsoft Sans Serif", 7.8F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.unimodAnnotations.Location = new System.Drawing.Point(363, 7);
            this.unimodAnnotations.Name = "unimodAnnotations";
            this.unimodAnnotations.Size = new System.Drawing.Size(330, 113);
            this.unimodAnnotations.TabIndex = 5;
            this.unimodAnnotations.TabStop = false;
            this.unimodAnnotations.Text = "Unimod Annotations";
            // 
            // unimodAnnotation
            // 
            this.unimodAnnotation.BackColor = System.Drawing.SystemColors.Control;
            this.unimodAnnotation.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.unimodAnnotation.Dock = System.Windows.Forms.DockStyle.Fill;
            this.unimodAnnotation.Location = new System.Drawing.Point(3, 18);
            this.unimodAnnotation.Name = "unimodAnnotation";
            this.unimodAnnotation.Size = new System.Drawing.Size(324, 92);
            this.unimodAnnotation.TabIndex = 1;
            this.unimodAnnotation.Text = "";
            // 
            // DeltaMassTable
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.splitContainer);
            this.Name = "DeltaMassTable";
            this.Size = new System.Drawing.Size(1152, 575);
            this.splitContainer.Panel1.ResumeLayout(false);
            this.splitContainer.Panel2.ResumeLayout(false);
            this.splitContainer.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.deltaMassMatrix)).EndInit();
            this.groupBox2.ResumeLayout(false);
            this.unimodAnnotations.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        public System.Windows.Forms.SplitContainer splitContainer;
        public System.Windows.Forms.DataGridView deltaMassMatrix;
        private System.Windows.Forms.GroupBox unimodAnnotations;
        private System.Windows.Forms.RichTextBox unimodAnnotation;
        private System.Windows.Forms.GroupBox groupBox2;
        public System.Windows.Forms.RichTextBox rawDataPath;

    }
}