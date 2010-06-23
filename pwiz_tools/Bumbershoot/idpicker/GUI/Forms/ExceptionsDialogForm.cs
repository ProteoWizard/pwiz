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
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2009 Vanderbilt University
//
// Contributor(s): Surendra Dasaris
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.IO;

using IdPickerGui.BLL;

namespace IdPickerGui
{
    public partial class ExceptionsDialogForm : Form
    {
        private string msg;
        private string details;
        private bool showDetails = true;

        public enum ExceptionType { Unknown = 0, Warning = 1, Error = 2 };

        /// <summary>
        /// Text next to error icon on top
        /// </summary>
        public string Msg
        {
            get { return msg; }
            set { msg = value; }
        }
        /// <summary>
        /// Text in the details box 
        /// </summary>
        public string Details
        {
            get { return details; }
            set { details = value; }
        }

        /// <summary>
        /// Whether or not details are visible at startup
        /// </summary>
        public bool ShowDetails
        {
            get { return showDetails; }
            set { showDetails = value; }
        }

        /// <summary>
        /// Default constructor
        /// </summary>
        public ExceptionsDialogForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Set msg and details
        /// </summary>
        public void loadForm(ExceptionType excType)
        {
            try
            {
                switch (excType)
                {
                    case ExceptionType.Error:
                        pbError.Image = Properties.Resources.error_x;
                        lblMsg.Text = "An error has occurred in the application.\r\n\r\n";
                        break;
                    case ExceptionType.Warning:
                        pbError.Image = Properties.Resources.warning;
                        lblMsg.Text = "Warning! Problems generating report.\r\n\r\n";
                        break;
                    default:
                        pbError.Image = Properties.Resources.error_x;
                        break;
                }

                tbDetails.Text = details;

                // text box text always selected after init
                tbDetails.SelectionStart = 0;

                tbDetails.Visible = ShowDetails;

            }
            catch (Exception exc)
            {
                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }

        }

        /// <summary>
        /// Show Details section (inner exceptions incl)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnDetails_Click(object sender, EventArgs e)
        {
            try
            {
                if (tbDetails.Visible)
                {
                    this.AutoSize = true;
                    this.Size = new Size(379, 150);
                    this.MinimumSize = new Size(379, 140);
                    this.SizeGripStyle = SizeGripStyle.Hide;
                    tbDetails.Visible = false;
                }
                else
                {
                    this.SizeGripStyle = SizeGripStyle.Show;
                    this.Size = new Size(379,241);
                    this.MinimumSize = new Size(379, 241);
                    this.SizeGripStyle = SizeGripStyle.Show;
                    this.AutoSize = false;

                    tbDetails.Visible = true;
                }

                
            }
            catch (Exception exc)
            {
                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }

        }

        /// <summary>
        /// Close ExceptionsDialogForm
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOk_Click(object sender, EventArgs e)
        {
            Close();
        }

        /// <summary>
        /// Opens logfile, path is initialized in IDPickerForm startup
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnViewLog_Click(object sender, EventArgs e)
        {
            try
            {
                if (File.Exists(ExceptionManager.LogFilePath))
                {
                    System.Diagnostics.Process.Start(ExceptionManager.LogFilePath);
                }

            }
            catch (Exception exc)
            {
                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }

        }

        
    }
}