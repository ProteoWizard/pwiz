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
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Reflection;
using System.Windows.Forms;


using IdPickerGui.MODEL;
using IdPickerGui.BLL;

namespace IdPickerGui
{
    public partial class TipOfTheDayForm : Form
    {
        private string[] tipList;

        public string[] TipList
        {
            get { return tipList; }
            set { tipList = value; }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        public TipOfTheDayForm()
        {
            TipList = new string[0];

            InitializeComponent();

            cbShowTips.Checked = Convert.ToBoolean(Properties.Settings.Default.ShowTipsOnStartup);

            displayTip(Properties.Settings.Default.TipNumber);
            
            // text box text always selected after init
            tbTipOfDayMsg.SelectionStart = 0;
        }

        /// <summary>
        /// Display the first tip in order for this user
        /// </summary>
        /// <param name="startingTipNumber"></param>
        private void displayTip(int startingTipNumber)
        {
            try
            {
                // tips go in resources-tips_of_the_day.txt seperated by | and cr
                string[] stringSeperators = new string[] { "|\r\n" };
                
                string tips = Properties.Resources.tips_of_the_day.ToString();

                TipList = tips.Split(stringSeperators, StringSplitOptions.RemoveEmptyEntries);

                if (startingTipNumber >= 0)
                {
                    tbTipOfDayMsg.Text = TipList[startingTipNumber].Trim();
                }
            }
            catch (Exception exc)
            {
                // since tip num is user setting poss if number tips decreases then an index would be out of bounds
                tbTipOfDayMsg.Text = "This tip is no longer available."; 

                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }

        }

        /// <summary>
        /// Set tip index in properties to idx + 1 or 0 (starting over)
        /// </summary>
        private void increaseOrResetTipNumber()
        {
            try
            {
                // reset tip num to display to 0 when all tips been through
                if (Properties.Settings.Default.TipNumber >= (TipList.Length - 1))
                {
                    Properties.Settings.Default.TipNumber = 0;
                }

                // show next tip
                else if (Properties.Settings.Default.TipNumber >= 0)
                {
                    Properties.Settings.Default.TipNumber = Properties.Settings.Default.TipNumber + 1;
                }

                Properties.Settings.Default.Save();

            }
            catch (Exception exc)
            {
                throw exc;
            }
        }

        /// <summary>
        /// Show the next tip in the tips_of_the_day.txt resource file.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNextTip_Click(object sender, EventArgs e)
        {
            try
            {
                increaseOrResetTipNumber();

                tbTipOfDayMsg.Text = TipList[Properties.Settings.Default.TipNumber];

            }
            catch (Exception exc)
            {
                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }

        }

        /// <summary>
        /// Update the showtipsonstartup user setting.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TipOfTheDayForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            try
            {
                increaseOrResetTipNumber();

                if (cbShowTips.Checked)
                {
                    Properties.Settings.Default.ShowTipsOnStartup = 1;
                }
                else
                {
                    Properties.Settings.Default.ShowTipsOnStartup = 0;
                }

                Properties.Settings.Default.Save();

            }
            catch (Exception exc)
            {
                ExceptionManager.logExceptionMessageByFormToFile(this, exc.Message, DateTime.Now);
            }
        }
    }
}