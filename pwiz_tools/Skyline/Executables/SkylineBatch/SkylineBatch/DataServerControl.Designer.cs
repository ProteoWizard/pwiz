namespace SkylineBatch
{
    partial class DataServerControl
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

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.label1 = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.btnTryReconnect = new System.Windows.Forms.Button();
            this.btnEditServer = new System.Windows.Forms.Button();
            this.label3 = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            this.textStatus = new System.Windows.Forms.RichTextBox();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(-3, 0);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(249, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Skyline Batch could not connect to the FTP server.";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(-3, 13);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(226, 13);
            this.label2.TabIndex = 3;
            this.label2.Text = "Try reconnecting or edit the server information.";
            // 
            // btnTryReconnect
            // 
            this.btnTryReconnect.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnTryReconnect.Location = new System.Drawing.Point(0, 76);
            this.btnTryReconnect.Name = "btnTryReconnect";
            this.btnTryReconnect.Size = new System.Drawing.Size(174, 23);
            this.btnTryReconnect.TabIndex = 1;
            this.btnTryReconnect.Text = "Try Reconnecting to Server";
            this.btnTryReconnect.UseVisualStyleBackColor = true;
            this.btnTryReconnect.Click += new System.EventHandler(this.btnTryReconnect_Click);
            // 
            // btnEditServer
            // 
            this.btnEditServer.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnEditServer.Location = new System.Drawing.Point(180, 76);
            this.btnEditServer.Name = "btnEditServer";
            this.btnEditServer.Size = new System.Drawing.Size(89, 23);
            this.btnEditServer.TabIndex = 2;
            this.btnEditServer.Text = "Edit Server";
            this.btnEditServer.UseVisualStyleBackColor = true;
            this.btnEditServer.Click += new System.EventHandler(this.btnEditServer_Click);
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(0, 30);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(40, 13);
            this.label3.TabIndex = 4;
            this.label3.Text = "Status:";
            // 
            // panel1
            // 
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.Controls.Add(this.textStatus);
            this.panel1.Location = new System.Drawing.Point(3, 46);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(456, 24);
            this.panel1.TabIndex = 6;
            // 
            // textStatus
            // 
            this.textStatus.BackColor = System.Drawing.SystemColors.Control;
            this.textStatus.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.textStatus.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.textStatus.Dock = System.Windows.Forms.DockStyle.Fill;
            this.textStatus.Location = new System.Drawing.Point(0, 0);
            this.textStatus.Margin = new System.Windows.Forms.Padding(2);
            this.textStatus.Name = "textStatus";
            this.textStatus.ReadOnly = true;
            this.textStatus.ScrollBars = System.Windows.Forms.RichTextBoxScrollBars.None;
            this.textStatus.Size = new System.Drawing.Size(456, 24);
            this.textStatus.TabIndex = 6;
            this.textStatus.TabStop = false;
            this.textStatus.Text = "";
            // 
            // DataServerControl
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.btnEditServer);
            this.Controls.Add(this.btnTryReconnect);
            this.Controls.Add(this.label1);
            this.Name = "DataServerControl";
            this.Size = new System.Drawing.Size(462, 102);
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnTryReconnect;
        private System.Windows.Forms.Button btnEditServer;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.RichTextBox textStatus;
    }
}
