//
// $Id$
//
// The contents of this file are subject to the Mozilla Public License
// Version 1.1 (the "License"); you may not use this file except in
// compliance with the License. You may obtain a copy of the License at
// http://www.mozilla.org/MPL/
//
// Software distributed under the License is distributed on an "AS IS"
// basis, WITHOUT WARRANTY OF ANY KIND, either express or implied. See the
// License for the specific language governing rights and limitations
// under the License.
//
// The Original Code is the IDPicker suite.
//
// The Initial Developer of the Original Code is Mike Litton.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s): Matt Chambers
//

namespace IdPickerGui
{
    partial class DeleteReportForm
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
            this.btnRemove = new System.Windows.Forms.Button();
            this.btnDelete = new System.Windows.Forms.Button();
            this.btnCancel = new System.Windows.Forms.Button();
            this.lblDialogQuestion = new System.Windows.Forms.Label();
            this.label2 = new System.Windows.Forms.Label();
            this.pbDeleteReportIcon = new System.Windows.Forms.PictureBox();
            ( (System.ComponentModel.ISupportInitialize) ( this.pbDeleteReportIcon ) ).BeginInit();
            this.SuspendLayout();
            // 
            // btnRemove
            // 
            this.btnRemove.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnRemove.Location = new System.Drawing.Point( 48, 84 );
            this.btnRemove.Name = "btnRemove";
            this.btnRemove.Size = new System.Drawing.Size( 79, 23 );
            this.btnRemove.TabIndex = 0;
            this.btnRemove.Text = "Remove";
            this.btnRemove.UseVisualStyleBackColor = true;
            this.btnRemove.Click += new System.EventHandler( this.btnRemove_Click );
            // 
            // btnDelete
            // 
            this.btnDelete.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnDelete.Location = new System.Drawing.Point( 133, 84 );
            this.btnDelete.Name = "btnDelete";
            this.btnDelete.Size = new System.Drawing.Size( 65, 23 );
            this.btnDelete.TabIndex = 1;
            this.btnDelete.Text = "Delete";
            this.btnDelete.UseVisualStyleBackColor = true;
            this.btnDelete.Click += new System.EventHandler( this.btnDelete_Click );
            // 
            // btnCancel
            // 
            this.btnCancel.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnCancel.Location = new System.Drawing.Point( 204, 84 );
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size( 65, 23 );
            this.btnCancel.TabIndex = 2;
            this.btnCancel.Text = "Cancel";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Click += new System.EventHandler( this.btnCancel_Click );
            // 
            // lblDialogQuestion
            // 
            this.lblDialogQuestion.AutoSize = true;
            this.lblDialogQuestion.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblDialogQuestion.Location = new System.Drawing.Point( 24, 40 );
            this.lblDialogQuestion.Name = "lblDialogQuestion";
            this.lblDialogQuestion.Size = new System.Drawing.Size( 273, 26 );
            this.lblDialogQuestion.TabIndex = 4;
            this.lblDialogQuestion.Text = "Do you wish to REMOVE this report from your history\r\nonly or DELETE the report an" +
                "d its destination directory?";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Font = new System.Drawing.Font( "Tahoma", 9.75F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.label2.Location = new System.Drawing.Point( 41, 9 );
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size( 99, 16 );
            this.label2.TabIndex = 5;
            this.label2.Text = "Delete Report";
            // 
            // pbDeleteReportIcon
            // 
            this.pbDeleteReportIcon.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Stretch;
            this.pbDeleteReportIcon.Image = global::IdPickerGui.Properties.Resources.DeleteFolderHS;
            this.pbDeleteReportIcon.InitialImage = global::IdPickerGui.Properties.Resources.DeleteFolderHS;
            this.pbDeleteReportIcon.Location = new System.Drawing.Point( 11, 8 );
            this.pbDeleteReportIcon.Name = "pbDeleteReportIcon";
            this.pbDeleteReportIcon.Size = new System.Drawing.Size( 24, 19 );
            this.pbDeleteReportIcon.TabIndex = 6;
            this.pbDeleteReportIcon.TabStop = false;
            // 
            // DeleteReportForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 316, 126 );
            this.Controls.Add( this.pbDeleteReportIcon );
            this.Controls.Add( this.label2 );
            this.Controls.Add( this.lblDialogQuestion );
            this.Controls.Add( this.btnCancel );
            this.Controls.Add( this.btnDelete );
            this.Controls.Add( this.btnRemove );
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "DeleteReportForm";
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "IDPicker";
            ( (System.ComponentModel.ISupportInitialize) ( this.pbDeleteReportIcon ) ).EndInit();
            this.ResumeLayout( false );
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button btnRemove;
        private System.Windows.Forms.Button btnDelete;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Label lblDialogQuestion;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.PictureBox pbDeleteReportIcon;
    }
}