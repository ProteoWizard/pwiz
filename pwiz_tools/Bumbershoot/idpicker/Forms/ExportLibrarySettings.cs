//
// $Id$
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s):
//

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using IDPicker.DataModel;
using NHibernate;
using pwiz.CLI.analysis;

namespace IDPicker.Forms
{
    public partial class ExportLibrarySettings : Form
    {
        private ISession _session;
        public ExportLibrarySettings(ISession session, int minimumSpectraPerDistinctMatch, bool psmMode = false)
        {
            InitializeComponent();
            _session = session;
            SpectrumNumBox.Value = minimumSpectraPerDistinctMatch;
            ExportPSMsPanel.Visible = psmMode;
            ExportLibraryPanel.Visible = !psmMode;
        }

        private void ExportLibrarySettings_Load(object sender, EventArgs e)
        {
            PrecursorNumBox.Value = (decimal)GetDefaultPrecursor();
            FragmentNumBox.Value = (decimal)GetDefaultFragment();
            methodBox.Text = "Dot Product Compare";
            outputFormatBox.Text = ".sptxt";
        }

        private double GetDefaultFragment()
        {
            double result;
            var tolerance = _session.CreateSQLQuery("Select value from analysisParameter where name=\"FragmentMzTolerance\"").List<string>().FirstOrDefault();
            if (tolerance != null)
            {
                if (tolerance.EndsWith("mz") && double.TryParse(tolerance.Substring(0, tolerance.Length - 2), out result))
                    return result;
                if (tolerance.EndsWith("ppm") && double.TryParse(tolerance.Substring(0, tolerance.Length - 3), out result))
                    return result / 1000;
            }

            return 0.5;
        }

        private double GetDefaultPrecursor()
        {
            double result;
            var modeQuery =
                _session.CreateSQLQuery("Select value from analysisParameter where name=\"PrecursorMzToleranceRule\"").List<string>();
            if (!modeQuery.Any())
                return 0.5;
            var mode = modeQuery.FirstOrDefault();
            string tolerance;
            if (mode == "avg" || mode == "auto")
                tolerance = _session.CreateSQLQuery("Select value from analysisParameter where name=\"AvgPrecursorMzTolerance\"").List<string>().FirstOrDefault();
            else
                tolerance = _session.CreateSQLQuery("Select value from analysisParameter where name=\"MonoPrecursorMzTolerance\"").List<string>().FirstOrDefault();
            if (tolerance.EndsWith("mz") && double.TryParse(tolerance.Substring(0, tolerance.Length - 2), out result))
                return result;
            if (tolerance.EndsWith("ppm") &&
                double.TryParse(tolerance.Substring(0, tolerance.Length - 3), out result))
                return result/1000;
            return 0.5;
        }

        public ExportForm.LibraryExportOptions GetSettings()
        {
            return new ExportForm.LibraryExportOptions
                                     {
                                         precursorMzTolerance = (double) PrecursorNumBox.Value,
                                         fragmentMzTolerance = (double) FragmentNumBox.Value,
                                         minimumSpectra = (int) Math.Round(SpectrumNumBox.Value),
                                         method = methodBox.Text,
                                         outputFormat = outputFormatBox.Text,
                                         decoys = decoysBox.Checked,
                                         crossPeptide = crossBox.Checked
                                     };
        }

        private void crossBox_CheckedChanged(object sender, EventArgs e)
        {
            PrecursorLabel.Enabled = PrecursorNumBox.Enabled = crossBox.Checked;
        }
    }
}
