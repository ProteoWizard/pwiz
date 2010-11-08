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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2010 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Data;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;

namespace IDPicker.Controls
{
    public partial class BasicFilterControl : UserControl
    {
        /// <summary>
        /// Occurs when the user changes the value of a filter control.
        /// </summary>
        public event EventHandler BasicFilterChanged;

        /// <summary>
        /// Gets a basic DataFilter from the filter controls or sets the filter controls from a DataFilter.
        /// </summary>
        public DataFilter DataFilter
        {
            get
            {
                decimal maxQValue;
                if (!Decimal.TryParse(maxQValueComboBox.Text, out maxQValue))
                    maxQValue = 0M;

                return new DataFilter()
                {
                    MaximumQValue = maxQValue / 100M,
                    //MaxAmbiguousIds
                    //MinPeptideLength
                    MinimumDistinctPeptidesPerProtein = minDistinctPeptidesTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minDistinctPeptidesTextBox.Text),
                    //MinDistinctMatchesPerProtein
                    MinimumAdditionalPeptidesPerProtein = minAdditionalPeptidesTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minAdditionalPeptidesTextBox.Text),
                    MinimumSpectraPerProtein = minSpectraTextBox.Text.Length == 0 ? 0 : Convert.ToInt32(minSpectraTextBox.Text)
                };
            }

            set
            {
                settingDataFilter = true;
                maxQValueComboBox.Text = (value.MaximumQValue * 100M).ToString();
                //maxAmbiguousIds
                //minPeptideLength
                minDistinctPeptidesTextBox.Text = value.MinimumDistinctPeptidesPerProtein.ToString();
                //minDistinctMatches
                minAdditionalPeptidesTextBox.Text = value.MinimumAdditionalPeptidesPerProtein.ToString();
                minSpectraTextBox.Text = value.MinimumSpectraPerProtein.ToString();
                settingDataFilter = false;
            }
        }

        bool settingDataFilter = false;

        public BasicFilterControl ()
        {
            InitializeComponent();
        }

        void doubleTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Decimal || e.KeyCode == Keys.OemPeriod)
            {
                if ((sender as Control).Text.Length == 0 || (sender as Control).Text.Contains('.'))
                    e.SuppressKeyPress = true;
            }
            else if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                    e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                    e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                    e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        void integerTextBox_KeyDown (object sender, KeyEventArgs e)
        {
            if (!(e.KeyCode >= Keys.D0 && e.KeyCode <= Keys.D9 ||
                e.KeyCode >= Keys.NumPad0 && e.KeyCode <= Keys.NumPad9 ||
                e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back ||
                e.KeyCode == Keys.Left || e.KeyCode == Keys.Right))
                e.SuppressKeyPress = true;
        }

        void filterControl_TextChanged (object sender, EventArgs e)
        {
            if (!settingDataFilter && !String.IsNullOrEmpty((sender as Control).Text) && BasicFilterChanged != null)
                BasicFilterChanged(this, EventArgs.Empty);
        }
    }
}
