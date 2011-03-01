//
// $Id: AboutForm.cs 80 2009-11-03 20:14:43Z holmanjd $
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

using System.Windows.Forms;

namespace BumberDash.Forms
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();

            const string currentGuiVersion = "1.2.4";

            aboutTextBox.Text = aboutTextBox.Text.Replace("<<version>>", currentGuiVersion);

            componentListView.Items.Add("GUI").SubItems.Add(currentGuiVersion);

            componentListView.Items.Add("MyriMatch").SubItems.Add(Properties.Settings.Default.MyriMatchVersion);

            componentListView.Items.Add("DirecTag").SubItems.Add(Properties.Settings.Default.DirecTagVersion);

            componentListView.Items.Add("TagRecon").SubItems.Add(Properties.Settings.Default.TagReconVersion);

        }
    }
}