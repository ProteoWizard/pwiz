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
    partial class TipOfTheDayForm
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
            this.pnlMain = new System.Windows.Forms.Panel();
            this.pnlTipHeading = new System.Windows.Forms.Panel();
            this.lblTipHeading = new System.Windows.Forms.Label();
            this.pnlLeft = new System.Windows.Forms.Panel();
            this.pbLightbulb = new System.Windows.Forms.PictureBox();
            this.pnlMsgBackground = new System.Windows.Forms.Panel();
            this.tbTipOfDayMsg = new System.Windows.Forms.TextBox();
            this.cbShowTips = new System.Windows.Forms.CheckBox();
            this.btnClose = new System.Windows.Forms.Button();
            this.btnNextTip = new System.Windows.Forms.Button();
            this.pnlMain.SuspendLayout();
            this.pnlTipHeading.SuspendLayout();
            this.pnlLeft.SuspendLayout();
            ( (System.ComponentModel.ISupportInitialize) ( this.pbLightbulb ) ).BeginInit();
            this.pnlMsgBackground.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlMain
            // 
            this.pnlMain.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlMain.BackColor = System.Drawing.SystemColors.ControlDark;
            this.pnlMain.Controls.Add( this.pnlTipHeading );
            this.pnlMain.Controls.Add( this.pnlLeft );
            this.pnlMain.Controls.Add( this.pnlMsgBackground );
            this.pnlMain.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.pnlMain.Location = new System.Drawing.Point( 12, 12 );
            this.pnlMain.Name = "pnlMain";
            this.pnlMain.Size = new System.Drawing.Size( 469, 260 );
            this.pnlMain.TabIndex = 0;
            // 
            // pnlTipHeading
            // 
            this.pnlTipHeading.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlTipHeading.BackColor = System.Drawing.Color.White;
            this.pnlTipHeading.Controls.Add( this.lblTipHeading );
            this.pnlTipHeading.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.pnlTipHeading.Location = new System.Drawing.Point( 63, 1 );
            this.pnlTipHeading.Name = "pnlTipHeading";
            this.pnlTipHeading.Size = new System.Drawing.Size( 405, 45 );
            this.pnlTipHeading.TabIndex = 4;
            // 
            // lblTipHeading
            // 
            this.lblTipHeading.AutoSize = true;
            this.lblTipHeading.BackColor = System.Drawing.Color.White;
            this.lblTipHeading.Font = new System.Drawing.Font( "Tahoma", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.lblTipHeading.Location = new System.Drawing.Point( 6, 13 );
            this.lblTipHeading.Name = "lblTipHeading";
            this.lblTipHeading.Size = new System.Drawing.Size( 134, 19 );
            this.lblTipHeading.TabIndex = 3;
            this.lblTipHeading.Text = "Did you know...";
            // 
            // pnlLeft
            // 
            this.pnlLeft.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left ) ) );
            this.pnlLeft.BackColor = System.Drawing.SystemColors.ControlDark;
            this.pnlLeft.Controls.Add( this.pbLightbulb );
            this.pnlLeft.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.pnlLeft.Location = new System.Drawing.Point( 0, 0 );
            this.pnlLeft.Name = "pnlLeft";
            this.pnlLeft.Size = new System.Drawing.Size( 60, 260 );
            this.pnlLeft.TabIndex = 0;
            // 
            // pbLightbulb
            // 
            this.pbLightbulb.Image = global::IdPickerGui.Properties.Resources.tips;
            this.pbLightbulb.InitialImage = global::IdPickerGui.Properties.Resources.tips;
            this.pbLightbulb.Location = new System.Drawing.Point( 16, 16 );
            this.pbLightbulb.Name = "pbLightbulb";
            this.pbLightbulb.Size = new System.Drawing.Size( 28, 35 );
            this.pbLightbulb.TabIndex = 0;
            this.pbLightbulb.TabStop = false;
            // 
            // pnlMsgBackground
            // 
            this.pnlMsgBackground.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( ( ( System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom )
                        | System.Windows.Forms.AnchorStyles.Left )
                        | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.pnlMsgBackground.BackColor = System.Drawing.Color.White;
            this.pnlMsgBackground.Controls.Add( this.tbTipOfDayMsg );
            this.pnlMsgBackground.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.pnlMsgBackground.Location = new System.Drawing.Point( 63, 47 );
            this.pnlMsgBackground.Name = "pnlMsgBackground";
            this.pnlMsgBackground.Size = new System.Drawing.Size( 405, 212 );
            this.pnlMsgBackground.TabIndex = 5;
            // 
            // tbTipOfDayMsg
            // 
            this.tbTipOfDayMsg.AcceptsReturn = true;
            this.tbTipOfDayMsg.AcceptsTab = true;
            this.tbTipOfDayMsg.BackColor = System.Drawing.Color.White;
            this.tbTipOfDayMsg.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.tbTipOfDayMsg.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.tbTipOfDayMsg.Font = new System.Drawing.Font( "Tahoma", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.tbTipOfDayMsg.ForeColor = System.Drawing.Color.Green;
            this.tbTipOfDayMsg.HideSelection = false;
            this.tbTipOfDayMsg.Location = new System.Drawing.Point( 9, 10 );
            this.tbTipOfDayMsg.Multiline = true;
            this.tbTipOfDayMsg.Name = "tbTipOfDayMsg";
            this.tbTipOfDayMsg.ReadOnly = true;
            this.tbTipOfDayMsg.Size = new System.Drawing.Size( 387, 192 );
            this.tbTipOfDayMsg.TabIndex = 0;
            // 
            // cbShowTips
            // 
            this.cbShowTips.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Left ) ) );
            this.cbShowTips.AutoSize = true;
            this.cbShowTips.Checked = true;
            this.cbShowTips.CheckState = System.Windows.Forms.CheckState.Checked;
            this.cbShowTips.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.cbShowTips.Location = new System.Drawing.Point( 12, 289 );
            this.cbShowTips.Name = "cbShowTips";
            this.cbShowTips.Size = new System.Drawing.Size( 123, 17 );
            this.cbShowTips.TabIndex = 1;
            this.cbShowTips.Text = "Show tips at startup";
            this.cbShowTips.UseVisualStyleBackColor = true;
            // 
            // btnClose
            // 
            this.btnClose.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnClose.DialogResult = System.Windows.Forms.DialogResult.Cancel;
            this.btnClose.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnClose.Location = new System.Drawing.Point( 406, 285 );
            this.btnClose.Name = "btnClose";
            this.btnClose.Size = new System.Drawing.Size( 75, 23 );
            this.btnClose.TabIndex = 2;
            this.btnClose.Text = "Close";
            this.btnClose.UseVisualStyleBackColor = true;
            // 
            // btnNextTip
            // 
            this.btnNextTip.Anchor = ( (System.Windows.Forms.AnchorStyles) ( ( System.Windows.Forms.AnchorStyles.Bottom | System.Windows.Forms.AnchorStyles.Right ) ) );
            this.btnNextTip.Font = new System.Drawing.Font( "Tahoma", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ( (byte) ( 0 ) ) );
            this.btnNextTip.Location = new System.Drawing.Point( 325, 285 );
            this.btnNextTip.Name = "btnNextTip";
            this.btnNextTip.Size = new System.Drawing.Size( 75, 23 );
            this.btnNextTip.TabIndex = 3;
            this.btnNextTip.Text = "Next";
            this.btnNextTip.UseVisualStyleBackColor = true;
            this.btnNextTip.Click += new System.EventHandler( this.btnNextTip_Click );
            // 
            // TipOfTheDayForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF( 6F, 13F );
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size( 493, 320 );
            this.Controls.Add( this.btnNextTip );
            this.Controls.Add( this.btnClose );
            this.Controls.Add( this.cbShowTips );
            this.Controls.Add( this.pnlMain );
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "TipOfTheDayForm";
            this.RightToLeftLayout = true;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterParent;
            this.Text = "Tip of the Day";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler( this.TipOfTheDayForm_FormClosing );
            this.pnlMain.ResumeLayout( false );
            this.pnlTipHeading.ResumeLayout( false );
            this.pnlTipHeading.PerformLayout();
            this.pnlLeft.ResumeLayout( false );
            ( (System.ComponentModel.ISupportInitialize) ( this.pbLightbulb ) ).EndInit();
            this.pnlMsgBackground.ResumeLayout( false );
            this.pnlMsgBackground.PerformLayout();
            this.ResumeLayout( false );
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Panel pnlMain;
        private System.Windows.Forms.Panel pnlLeft;
        private System.Windows.Forms.PictureBox pbLightbulb;
        private System.Windows.Forms.Label lblTipHeading;
        private System.Windows.Forms.Panel pnlTipHeading;
        private System.Windows.Forms.CheckBox cbShowTips;
        private System.Windows.Forms.Button btnClose;
        private System.Windows.Forms.Button btnNextTip;
        private System.Windows.Forms.TextBox tbTipOfDayMsg;
        private System.Windows.Forms.Panel pnlMsgBackground;
    }
}