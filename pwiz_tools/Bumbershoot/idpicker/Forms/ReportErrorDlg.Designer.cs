//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the ProteoWizard project.
//
// The Initial Developer of the Original Code is Shannon Joyner.
//
// Copyright 2011 University of Washington - Seattle, WA
//
// Contributor(s): Matt Chambers
//

namespace IDPicker.Forms
{
    partial class ReportErrorDlg
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
            this.btnCancel = new System.Windows.Forms.Button();
            this.tbMessage = new System.Windows.Forms.RichTextBox();
            this.lblCommentBox = new System.Windows.Forms.Label();
            this.lblError = new System.Windows.Forms.Label();
            this.lblReportError = new System.Windows.Forms.Label();
            this.label1 = new System.Windows.Forms.Label();
            this.tbSourceCodeLocation = new System.Windows.Forms.TextBox();
            this.btnClipboard = new System.Windows.Forms.Button();
            this.lblEmail = new System.Windows.Forms.Label();
            this.tbEmail = new System.Windows.Forms.TextBox();
            this.tbErrorDescription = new System.Windows.Forms.TextBox();
            this.forceCloseCheckBox = new System.Windows.Forms.CheckBox();
            this.tbUsername = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.btnOK = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // btnCancel
            // 
            this.btnCancel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnCancel.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnCancel.Location = new System.Drawing.Point(694, 477);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(82, 24);
            this.btnCancel.TabIndex = 5;
            this.btnCancel.Text = "OK";
            this.btnCancel.UseVisualStyleBackColor = true;
            // 
            // tbMessage
            // 
            this.tbMessage.AcceptsTab = true;
            this.tbMessage.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbMessage.Location = new System.Drawing.Point(12, 322);
            this.tbMessage.Name = "tbMessage";
            this.tbMessage.Size = new System.Drawing.Size(760, 80);
            this.tbMessage.TabIndex = 1;
            this.tbMessage.Text = "";
            this.tbMessage.Visible = false;
            // 
            // lblCommentBox
            // 
            this.lblCommentBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblCommentBox.AutoSize = true;
            this.lblCommentBox.Location = new System.Drawing.Point(9, 306);
            this.lblCommentBox.Name = "lblCommentBox";
            this.lblCommentBox.Size = new System.Drawing.Size(259, 13);
            this.lblCommentBox.TabIndex = 0;
            this.lblCommentBox.Text = "Please describe how the problem occurred (Optional):";
            this.lblCommentBox.Visible = false;
            // 
            // lblError
            // 
            this.lblError.AutoSize = true;
            this.lblError.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblError.Location = new System.Drawing.Point(9, 56);
            this.lblError.Name = "lblError";
            this.lblError.Size = new System.Drawing.Size(61, 13);
            this.lblError.TabIndex = 8;
            this.lblError.Text = "Message:";
            // 
            // lblReportError
            // 
            this.lblReportError.AutoSize = true;
            this.lblReportError.Font = new System.Drawing.Font("Microsoft Sans Serif", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.lblReportError.Location = new System.Drawing.Point(9, 9);
            this.lblReportError.Name = "lblReportError";
            this.lblReportError.Size = new System.Drawing.Size(350, 36);
            this.lblReportError.TabIndex = 7;
            this.lblReportError.Text = "An unexpected error has occurred, as shown below.\r\nReport the error to help impro" +
    "ve IDPicker.";
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.label1.Location = new System.Drawing.Point(12, 135);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(132, 13);
            this.label1.TabIndex = 10;
            this.label1.Text = "Source code location:";
            // 
            // tbSourceCodeLocation
            // 
            this.tbSourceCodeLocation.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbSourceCodeLocation.Location = new System.Drawing.Point(12, 151);
            this.tbSourceCodeLocation.Multiline = true;
            this.tbSourceCodeLocation.Name = "tbSourceCodeLocation";
            this.tbSourceCodeLocation.ReadOnly = true;
            this.tbSourceCodeLocation.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbSourceCodeLocation.Size = new System.Drawing.Size(760, 152);
            this.tbSourceCodeLocation.TabIndex = 11;
            // 
            // btnClipboard
            // 
            this.btnClipboard.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.btnClipboard.Location = new System.Drawing.Point(12, 477);
            this.btnClipboard.Name = "btnClipboard";
            this.btnClipboard.Size = new System.Drawing.Size(135, 23);
            this.btnClipboard.TabIndex = 6;
            this.btnClipboard.Text = "Copy to Clipboard";
            this.btnClipboard.UseVisualStyleBackColor = true;
            this.btnClipboard.Click += new System.EventHandler(this.btnClipboard_Click);
            // 
            // lblEmail
            // 
            this.lblEmail.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.lblEmail.AutoSize = true;
            this.lblEmail.Location = new System.Drawing.Point(8, 444);
            this.lblEmail.Name = "lblEmail";
            this.lblEmail.Size = new System.Drawing.Size(123, 13);
            this.lblEmail.TabIndex = 2;
            this.lblEmail.Text = "Email address (Optional):";
            this.lblEmail.Visible = false;
            // 
            // tbEmail
            // 
            this.tbEmail.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.tbEmail.Location = new System.Drawing.Point(132, 441);
            this.tbEmail.Name = "tbEmail";
            this.tbEmail.Size = new System.Drawing.Size(299, 20);
            this.tbEmail.TabIndex = 3;
            this.tbEmail.Visible = false;
            // 
            // tbErrorDescription
            // 
            this.tbErrorDescription.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.tbErrorDescription.Location = new System.Drawing.Point(12, 72);
            this.tbErrorDescription.Multiline = true;
            this.tbErrorDescription.Name = "tbErrorDescription";
            this.tbErrorDescription.ReadOnly = true;
            this.tbErrorDescription.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.tbErrorDescription.Size = new System.Drawing.Size(760, 60);
            this.tbErrorDescription.TabIndex = 9;
            this.tbErrorDescription.Text = "1\r\n2\r\n";
            // 
            // forceCloseCheckBox
            // 
            this.forceCloseCheckBox.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.forceCloseCheckBox.AutoSize = true;
            this.forceCloseCheckBox.Location = new System.Drawing.Point(511, 482);
            this.forceCloseCheckBox.Name = "forceCloseCheckBox";
            this.forceCloseCheckBox.Size = new System.Drawing.Size(96, 17);
            this.forceCloseCheckBox.TabIndex = 12;
            this.forceCloseCheckBox.Text = "Close IDPicker";
            this.forceCloseCheckBox.UseVisualStyleBackColor = true;
            // 
            // tbUsername
            // 
            this.tbUsername.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.tbUsername.Location = new System.Drawing.Point(132, 414);
            this.tbUsername.Name = "tbUsername";
            this.tbUsername.Size = new System.Drawing.Size(299, 20);
            this.tbUsername.TabIndex = 14;
            this.tbUsername.Visible = false;
            // 
            // label2
            // 
            this.label2.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left)));
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(22, 417);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(109, 13);
            this.label2.TabIndex = 13;
            this.label2.Text = "User name (Optional):";
            this.label2.Visible = false;
            // 
            // btnOK
            // 
            this.btnOK.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right)));
            this.btnOK.DialogResult = System.Windows.Forms.DialogResult.OK;
            this.btnOK.Location = new System.Drawing.Point(613, 478);
            this.btnOK.Name = "btnOK";
            this.btnOK.Size = new System.Drawing.Size(75, 23);
            this.btnOK.TabIndex = 4;
            this.btnOK.Text = "Report";
            this.btnOK.UseVisualStyleBackColor = true;
            this.btnOK.Visible = false;
            // 
            // ReportErrorDlg
            // 
            this.AcceptButton = this.btnOK;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.CancelButton = this.btnCancel;
            this.ClientSize = new System.Drawing.Size(784, 509);
            this.ControlBox = false;
            this.Controls.Add(this.tbUsername);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.forceCloseCheckBox);
            this.Controls.Add(this.btnOK);
            this.Controls.Add(this.tbErrorDescription);
            this.Controls.Add(this.tbEmail);
            this.Controls.Add(this.lblEmail);
            this.Controls.Add(this.btnClipboard);
            this.Controls.Add(this.tbSourceCodeLocation);
            this.Controls.Add(this.label1);
            this.Controls.Add(this.lblReportError);
            this.Controls.Add(this.lblError);
            this.Controls.Add(this.lblCommentBox);
            this.Controls.Add(this.tbMessage);
            this.Controls.Add(this.btnCancel);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "ReportErrorDlg";
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Unexpected Error";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.RichTextBox tbMessage;
        private System.Windows.Forms.Label lblCommentBox;
        private System.Windows.Forms.Label lblError;
        private System.Windows.Forms.Label lblReportError;
        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox tbSourceCodeLocation;
        private System.Windows.Forms.Button btnClipboard;
        private System.Windows.Forms.Label lblEmail;
        private System.Windows.Forms.TextBox tbEmail;
        private System.Windows.Forms.TextBox tbErrorDescription;
        private System.Windows.Forms.CheckBox forceCloseCheckBox;
        private System.Windows.Forms.TextBox tbUsername;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.Button btnOK;
    }
}