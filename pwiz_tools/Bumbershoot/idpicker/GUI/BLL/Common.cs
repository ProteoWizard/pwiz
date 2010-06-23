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
using System.Text;
using System.Windows.Forms;
using System.IO;

namespace IdPickerGui.BLL
{
    public static class Common
    {
        public static string docFilePath = String.Format( "file:///{0}/include/{1}",
                                                          Application.StartupPath.Replace( "\\", "/" ),
                                                          "idpicker-2-1-gui.html" );

        /// <summary>
        /// Get the anchor name that the given control name links to in the
        /// html help documentation
        /// </summary>
        /// <param name="cntrlName"></param>
        /// <returns></returns>
        public static string getAnchorNameByControlName(string cntrlName)
        {
            try
            {
                switch (cntrlName)
                {
                    case "lblDecoyPrefix":                      // RunReportForm step 1 and ToolsOptionsForm
                        return "DecoyPrefix";
                    case "lblDbInSelFiles":                     // RunReportForm step 1
                        return "ProteinDatabase";
                    case "lblParsimonyVariable":                // RunReportForm step 2
                        return "MinAdditionalPeptides";
                    case "lblMinDistinctPeptides":
                        return "MinDistinctPeptides";           // RunReportForm step 2
                    case "lblMaxAmbigIds":
                        return "MaxAmbiguousIds";
                    case "lblMinPeptideLength":                 // RunReportForm step 2
                        return "MinPeptideLength";
                    case "lblMaxFdr":                           // RunReportForm step 2
                        return "MaxFDR";
                    case "gbGroups":                            // RunReportForm step 2
                        return "AssembleGroups";
                    case "gbFiles":                             // RunReportForm step 2
                        return "AssembleGroups";
                    case "gbDefaultSettings":                    // Tools/Options
                        return "DefaultOptions";
                    case "gbSearchPaths":                       // Tools/Options
                        return "DefaultOptions";
                    default:
                        return "unknown";
                }

            }
            catch (Exception exc)
            {
                throw new Exception("Unable to find anchor for corresponding control name" + cntrlName, exc);
            }
        }
     
    }
}
