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

namespace seems
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();

            aboutTextBox.Text = aboutTextBox.Text.Replace( "<<version>>",
                String.Format( "{0} ({1})", seemsForm.Version, seemsForm.LastModified ) );

            foreach( System.Reflection.Assembly a in AppDomain.CurrentDomain.GetAssemblies() )
            {
                if( a.FullName.Contains( "DigitalRune" ) )
                {
                    Match versionMatch = Regex.Match( a.ToString(), @"Version=([\d.]+)" );
                    string version = versionMatch.Groups[1].Success ? versionMatch.Groups[1].Value : "unknown";
                    componentListView.Items.Add( "DR Docking Windows" ).SubItems.Add(
                        String.Format( "{0} ({1})", version, File.GetLastWriteTime( a.Location ).ToShortDateString() ) );
                } else if( a.FullName.Contains( "ZedGraph" ) )
                {
                    Match versionMatch = Regex.Match( a.ToString(), @"Version=([\d.]+)" );
                    string version = versionMatch.Groups[1].Success ? versionMatch.Groups[1].Value : "unknown";
                    componentListView.Items.Add( "ZedGraph" ).SubItems.Add(
                     String.Format( "{0} ({1})", version, File.GetLastWriteTime( a.Location ).ToShortDateString() ) );
                }
            }

            componentListView.Items.Add( "ProteoWizard MSData" ).SubItems.Add(
                String.Format( "{0} ({1})", pwiz.CLI.msdata.Version.ToString(), pwiz.CLI.msdata.Version.LastModified() ) );
            componentListView.Items.Add( "ProteoWizard Analysis" ).SubItems.Add(
                String.Format( "{0} ({1})", pwiz.CLI.analysis.Version.ToString(), pwiz.CLI.analysis.Version.LastModified() ) );
            componentListView.Items.Add( "ProteoWizard Proteome" ).SubItems.Add(
                String.Format( "{0} ({1})", pwiz.CLI.proteome.Version.ToString(), pwiz.CLI.proteome.Version.LastModified() ) );
        }
    }
}