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
using System.Text;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Text.RegularExpressions;
using System.Diagnostics;
using IdPickerGui.BLL;

namespace IdPickerGui
{
    class SpectrumGraph
    {
        public static void ParseQuery( Uri uri, out string source,
                                       out string id, out int charge, out string sequence )
        {
            source = null;
            id = null;
            charge = 0;
            sequence = null;

            string unescapedQuery = Uri.UnescapeDataString( uri.Query );
            string[] kvs = unescapedQuery.Substring( 1 ).Split( "&".ToCharArray() );
            foreach( string kv in kvs )
            {
                string[] kvPair = kv.Split( "=".ToCharArray() );
                string value = String.Join( "=", kvPair, 1, kvPair.Length - 1 );
                switch( kvPair[0] )
                {
                    case "source":
                        source = value;
                        break;
                    case "id":
                        id = value;
                        break;
                    case "charge":
                        charge = Convert.ToInt32( value );
                        break;
                    case "sequence":
                        sequence = value;
                        break;
                }
            }
        }

        public static void Show( IDPickerForm parent,
                                 IdPickerGui.MODEL.IDPickerInfo info,
                                 string source,
                                 string id,
                                 int charge,
                                 string sequence )
        {
            // find the source file in the search path
            string sourceFilepath = IDPicker.Util.FindSourceInSearchPath(source, info.SrcFilesDir);

            Show( parent, sourceFilepath, source, id, charge, sequence );
        }

        public static void Show( IDPickerForm parent,
                                 string sourceFilepath,
                                 string source,
                                 string id,
                                 int charge,
                                 string sequence )
        {
            sequence = sequence.Replace( '(', '[' );
            sequence = sequence.Replace( ')', ']' );

            Process seems = new Process();
            seems.StartInfo.FileName = Path.Combine( Path.GetDirectoryName( Application.ExecutablePath ), @"seems.exe" );
            seems.StartInfo.Arguments = String.Format( "\"{0}\" --id=\"{1}\" --annotation=\"pfr {2} 1 {3} a,b,c,x,y,z\"",
                                                      sourceFilepath,
                                                      id,
                                                      sequence,
                                                      Math.Min( 1, charge - 1 ) );
            seems.Start();
        }
    }
}
