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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class QualityControlUI : ArgsCollectorForm
    {
        private enum Args {Normalization, FeatureSelection, Width, Height, Max}

        public string[] Arguments { get; private set; }

        public QualityControlUI(string[] oldArgs)
        {
            InitializeComponent();
            comboBoxNormalizeTo.Items.AddRange(GetNormalizationOptionLabels().Cast<object>().ToArray());
            comboBoxNormalizeTo.SelectedIndex = 0;
            RestoreSettings(oldArgs);
        }
        private bool RestoreSettings(IList<string> arguments)
        {
            if (arguments == null || arguments.Count != (int)Args.Max)
            {
                return false;
            }

            SelectComboBoxValue(comboBoxNormalizeTo, arguments[(int)Args.Normalization], _normalizationOptionValues);
            cbxSelectHighQualityFeatures.Checked = arguments[(int) Args.FeatureSelection] == FeatureSubsetHighQuality;
            tbxWidth.Text = arguments[(int)Args.Width];
            tbxHeight.Text = arguments[(int)Args.Height];
            return true;
        }

        private void GenerateArguments()
        {
            var commandLineArguments = new List<string>();
            commandLineArguments.Add(_normalizationOptionValues[comboBoxNormalizeTo.SelectedIndex]);
            commandLineArguments.Add(cbxSelectHighQualityFeatures.Checked ? "highQuality" : "all");
            commandLineArguments.Add(tbxWidth.Text);
            commandLineArguments.Add(tbxHeight.Text);
            Arguments = commandLineArguments.ToArray();
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            GenerateArguments();
            DialogResult = DialogResult.OK;
        }
    }
    public class MSstatsQualityControlCollector
    {

        public static string[] CollectArgs(IWin32Window parent, TextReader report, string[] args)
        {
            using (var dlg = new QualityControlUI(args))
            {
                if (dlg.ShowDialog(parent) == DialogResult.OK)
                {
                    return dlg.Arguments;
                }

                return null;
            }
        }
    }   
}
