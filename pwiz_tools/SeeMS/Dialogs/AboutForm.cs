//
// $Id$
//
//
// Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
//
// Copyright 2009 Vanderbilt University - Nashville, TN 37232
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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.Reflection.Metadata;
using System.Text.RegularExpressions;

namespace seems
{
    public partial class AboutForm : Form
    {
        public AboutForm()
        {
            InitializeComponent();

            aboutTextBox.Text = aboutTextBox.Text.Replace("<<appname>>",
                String.Format("SeeMS {0}", (Environment.Is64BitProcess ? "(64-bit) " : "") ) );

            aboutTextBox.Text = aboutTextBox.Text.Replace( "<<version>>",
                String.Format( "{0} ({1})", Version, LastModified.ToShortDateString() ) );
            aboutTextBox.Text = aboutTextBox.Text.Replace( "<<date>>", LastModified.Year.ToString() );

                componentListView.Items.Add( "DR Docking Windows" ).SubItems.Add(
                    String.Format( "{0} ({1})", GetAssemblyVersion(GetAssemblyByName("DigitalRune")),
                                                GetAssemblyLastModified(GetAssemblyByName("DigitalRune")).ToShortDateString() ) );

            componentListView.Items.Add( "ZedGraph" ).SubItems.Add(
                    String.Format( "{0} ({1})", GetAssemblyVersion(GetAssemblyByName("ZedGraph")),
                                                GetAssemblyLastModified(GetAssemblyByName("ZedGraph")).ToShortDateString() ) );
        }

        public static string Version { get { return GetAssemblyVersion( Assembly.GetExecutingAssembly().GetName() ); } }
        public static DateTime LastModified { get { return GetAssemblyLastModified( Assembly.GetExecutingAssembly().GetName() ); } }

        public static AssemblyName GetAssemblyByName( string assemblyName )
        {
            if( Assembly.GetCallingAssembly().GetName().FullName.Contains( assemblyName ) )
                return Assembly.GetCallingAssembly().GetName();

            foreach( AssemblyName a in Assembly.GetCallingAssembly().GetReferencedAssemblies() )
            {
                if( a.FullName.Contains( assemblyName ) )
                    return a;
            }
            return null;
        }

        public static string GetAssemblyVersion( AssemblyName assembly )
        {
            Match versionMatch = Regex.Match( assembly.ToString(), @"Version=([\d.]+)" );
            return versionMatch.Groups[1].Success ? versionMatch.Groups[1].Value : "unknown";
        }

        public static DateTime GetAssemblyLastModified( AssemblyName assembly )
        {
            return File.GetLastWriteTime( Assembly.Load(assembly).Location );
        }
    }
}