/*
 * Original author: Yuval Boss <yuval .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2013 University of Washington - Seattle, WA
 * 
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Globalization;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class QualityControlUI : Form
    {
        private enum Args {normalize_to, allow_missing_peaks}

        public string[] Arguments { get; private set; }
        public QualityControlUI(string[] oldArgs)
        {
            InitializeComponent();

            comboBoxNoramilzeTo.SelectedIndex = 1;

            if (oldArgs != null && oldArgs.Length == 1)
                Arguments = oldArgs;
        }

        private const string TRUESTRING = "TRUE"; // Not L10N
        private const string FALSESTRING = "FALSE"; // Not L10N
        private void GenerateArguments()
        {
            Arguments = Arguments ?? new string[2];
            Arguments[(int)Args.normalize_to] = (comboBoxNoramilzeTo.SelectedIndex).ToString(CultureInfo.InvariantCulture);
            Arguments[(int)Args.allow_missing_peaks] = (cboxAllowMissingPeaks.Checked) ? TRUESTRING : FALSESTRING;           
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
          DialogResult = DialogResult.Cancel;        
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            GenerateArguments();
            DialogResult = DialogResult.OK;
     
        }

    }
    public class MSstatsQualityControlCollector
    {

        public static string[] CollectArgs(IWin32Window parent, string report, string[] args)
        {
            using (var dlg = new QualityControlUI(args))
            {
                if (parent != null)
                {
                    return (dlg.ShowDialog(parent) == DialogResult.OK) ? dlg.Arguments : null;
                }
                return (dlg.ShowDialog() == DialogResult.OK) ? dlg.Arguments : null;
            }
        }
    }   
}
