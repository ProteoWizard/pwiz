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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using IDPicker;

namespace IdPickerGui
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();

            aboutTextBox.Text = aboutTextBox.Text.Replace( "<<version>>",
                String.Format( "{0} ({1})",
                Util.Version,
                Util.LastModified.ToShortDateString() ) );

            string qonvertVersion = "not found";
            try
            {
                Process qonvert = new Process();
                qonvert.StartInfo.FileName = Path.Combine( Path.GetDirectoryName( Application.ExecutablePath ), "idpQonvert.exe" );
                qonvert.StartInfo.CreateNoWindow = true;
                qonvert.StartInfo.UseShellExecute = false;
                qonvert.StartInfo.RedirectStandardOutput = true;
                qonvert.Start();
                qonvert.WaitForExit();
                string introLine = qonvert.StandardOutput.ReadLine();
                Match introMatch = Regex.Match( introLine, "IDPickerQonvert (.*)" );
                if( introMatch.Success && introMatch.Groups.Count == 2 )
                    qonvertVersion = introMatch.Groups[1].Value;
            } catch { }

            componentListView.Items.Add( "GUI" ).SubItems.Add(
                String.Format( "{0} ({1})",
                IDPickerForm.Version,
                IDPickerForm.LastModified.ToShortDateString() ) );

            componentListView.Items.Add( "Qonvert" ).SubItems.Add( qonvertVersion );

            componentListView.Items.Add( "Workspace" ).SubItems.Add(
                String.Format( "{0} ({1})",
                Workspace.Version,
                Workspace.LastModified.ToShortDateString() ) );

            componentListView.Items.Add( "Presentation" ).SubItems.Add(
                String.Format( "{0} ({1})",
                Presentation.Version,
                Presentation.LastModified.ToShortDateString() ) );

            componentListView.Items.Add( "ProteoWizard MSData" ).SubItems.Add(
                String.Format( "{0} ({1})", pwiz.CLI.msdata.Version.ToString(), pwiz.CLI.msdata.Version.LastModified() ) );
            //componentListView.Items.Add( "ProteoWizard Proteome" ).SubItems.Add(
            //String.Format( "{0} ({1})", pwiz.CLI.proteome.Version.ToString(), pwiz.CLI.msdata.Version.LastModified() ) );
        }
    }
}