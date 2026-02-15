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
            this.btnContinue = new System.Windows.Forms.Button();
            this.lblDescription = new System.Windows.Forms.Label();
            this.lblDescriptionLink = new System.Windows.Forms.LinkLabel();
            this.toolTip1 = new System.Windows.Forms.ToolTip(this.components);
            this.btnPreview = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnContinue
            // 
            this.btnContinue.Location = new System.Drawing.Point(18, 54);
            this.btnContinue.Name = "btnContinue";
            this.btnContinue.Size = new System.Drawing.Size(168, 35);
            this.btnContinue.TabIndex = 2;
            this.btnContinue.Text = "&Continue";
            this.btnContinue.UseVisualStyleBackColor = true;
            this.btnContinue.Click += new System.EventHandler(this.btnContinue_Click);
            // 
            // lblDescription
            // 
            this.lblDescription.AutoEllipsis = true;
            this.lblDescription.AutoSize = true;
            this.lblDescription.Location = new System.Drawing.Point(20, 20);
            this.lblDescription.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDescription.Name = "lblDescription";
            this.lblDescription.Size = new System.Drawing.Size(89, 20);
            this.lblDescription.TabIndex = 0;
            this.lblDescription.Text = "Description";
            // 
            // lblDescriptionLink
            // 
            this.lblDescriptionLink.AutoEllipsis = true;
            this.lblDescriptionLink.AutoSize = true;
            this.lblDescriptionLink.Location = new System.Drawing.Point(48, 20);
            this.lblDescriptionLink.Margin = new System.Windows.Forms.Padding(4, 0, 4, 0);
            this.lblDescriptionLink.Name = "lblDescriptionLink";
            this.lblDescriptionLink.Size = new System.Drawing.Size(89, 20);
            this.lblDescriptionLink.TabIndex = 1;
            this.lblDescriptionLink.TabStop = true;
            this.lblDescriptionLink.Text = "Description";
            this.lblDescriptionLink.Visible = false;
            this.lblDescriptionLink.LinkClicked += new System.Windows.Forms.LinkLabelLinkClickedEventHandler(this.lblDescriptionLink_LinkClicked);
            // 
            // btnPreview
            // 
            this.btnPreview.Location = new System.Drawing.Point(18, 95);
            this.btnPreview.Name = "btnPreview";
            this.btnPreview.Size = new System.Drawing.Size(168, 35);
            this.btnPreview.TabIndex = 3;
            this.btnPreview.Text = "&Preview";
            this.btnPreview.UseVisualStyleBackColor = true;
            this.btnPreview.Click += new System.EventHandler(this.btnPreview_Click);
            // 
            // PauseAndContinueForm
            // 
            this.AcceptButton = this.btnContinue;
            this.AutoScaleDimensions = new System.Drawing.SizeF(9F, 20F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(205, 143);
            this.Controls.Add(this.btnPreview);
            this.Controls.Add(this.lblDescriptionLink);
            this.Controls.Add(this.lblDescription);
            this.Controls.Add(this.btnContinue);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedToolWindow;
            this.KeyPreview = true;
            this.Margin = new System.Windows.Forms.Padding(4, 5, 4, 5);
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
        private System.Windows.Forms.ToolTip toolTip1;
        private System.Windows.Forms.Button btnPreview;
    }
}