namespace pwiz.SkylineTestUtil
{
    partial class PauseAndContinueForm
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(PauseAndContinueForm));
            this.btnContinue = new System.Windows.Forms.Button();
            this.lblDescription = new System.Windows.Forms.Label();
            this.lblDescriptionLink = new System.Windows.Forms.LinkLabel();
            this.btnCopyToClipBoard = new System.Windows.Forms.Button();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.btnCopyMetafileToClipboard = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnContinue
            // 
            this.btnContinue.Location = new System.Drawing.Point(12, 32);
            this.btnContinue.Name = "btnContinue";
            this.btnContinue.Size = new System.Drawing.Size(75, 23);
            this.btnContinue.TabIndex = 0;
            this.btnContinue.Text = "Continue";
            this.btnContinue.UseVisualStyleBackColor = true;
            this.btnContinue.Click += new System.EventHandler(this.btnContinue_Click);
            // 
            // lblDescription
            // 
            this.lblDescription.AutoEllipsis = true;
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(13, 13);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(60, 13);
            this.lblDescription.TabIndex = 1;
            this.lblDescription.Text = "Description";
            // 
            // lblDescriptionLink
            // 
            this.lblDescriptionLink.AutoSize = true;
            this.lblDescriptionLink.Location = new System.Drawing.Point(32, 13);
            this.lblDescriptionLink.Name = "lblDescriptionLink";
            this.lblDescriptionLink.Size = new System.Drawing.Size(60, 13);
            this.lblDescriptionLink.TabIndex = 2;
            this.lblDescriptionLink.TabStop = true;
            this.lblDescriptionLink.Text = "Description";
            this.lblDescriptionLink.Visible = false;
            this.lblDescriptionLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lblDescriptionLink_LinkClicked);
            // 
            // btnCopyToClipBoard
            // 
            this.btnCopyToClipBoard.Enabled = false;
            this.btnCopyToClipBoard.Location = new System.Drawing.Point(12, 61);
            this.btnCopyToClipBoard.Name = "btnCopyToClipBoard";
            this.btnCopyToClipBoard.Size = new System.Drawing.Size(75, 23);
            this.btnCopyToClipBoard.TabIndex = 3;
            this.btnCopyToClipBoard.Text = "Copy Form";
            this.toolTip1.SetToolTip(this.btnCopyToClipBoard, resources.GetString("btnCopyToClipBoard.ToolTip"));
            this.btnCopyToClipBoard.UseVisualStyleBackColor = true;
            this.btnCopyToClipBoard.Visible = false;
            this.btnCopyToClipBoard.Click += new System.EventHandler(this.btnCopyToClipboard_Click);
            // 
            // btnCopyMetafileToClipboard
            // 
            this.btnCopyMetafileToClipboard.Enabled = false;
            this.btnCopyMetafileToClipboard.Location = new System.Drawing.Point(12, 90);
            this.btnCopyMetafileToClipboard.Name = "btnCopyMetafileToClipboard";
            this.btnCopyMetafileToClipboard.Size = new System.Drawing.Size(75, 23);
            this.btnCopyMetafileToClipboard.TabIndex = 4;
            this.btnCopyMetafileToClipboard.Text = "Copy Graph";
            this.toolTip1.SetToolTip(this.btnCopyMetafileToClipboard, "Copies metafile to clipboard");
            this.btnCopyMetafileToClipboard.UseVisualStyleBackColor = true;
            this.btnCopyMetafileToClipboard.Visible = false;
            this.btnCopyMetafileToClipboard.Click += new System.EventHandler(this.btnCopyMetaFileToClipboard_Click);
            // 
            // PauseAndContinueForm
            // 
            this.AcceptButton = this.btnContinue;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.AutoSize = true;
            this.ClientSize = new System.Drawing.Size(174, 128);
            this.ControlBox = false;
            this.Controls.Add(this.btnCopyMetafileToClipboard);
            this.Controls.Add(this.btnCopyToClipBoard);
            this.Controls.Add(this.lblDescriptionLink);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.btnContinue);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "PauseAndContinueForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.Manual;
            this.Text = "Pause Test";
            this.KeyDown += new System.Windows.Forms.KeyEventHandler(this.PauseAndContinueForm_KeyDown);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnContinue;
        private System.Windows.Forms.Label lblDescription;
        private System.Windows.Forms.LinkLabel lblDescriptionLink;
        private System.Windows.Forms.Button btnCopyToClipBoard;
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button btnCopyMetafileToClipboard;
    }
}