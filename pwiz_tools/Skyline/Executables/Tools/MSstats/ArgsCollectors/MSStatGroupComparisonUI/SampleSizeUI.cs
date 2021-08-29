/*
 * Original author: Trevor Killeen <killeent .at. uw.edu>,
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
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class SampleSizeUi : Form
    {
        public string[] Arguments { get; private set; }

        public SampleSizeUi(DataSetInfo dataSetInfo, Arguments arguments)
        {
            InitializeComponent();
            commonOptionsControl1.SetDataSetInfo(dataSetInfo);
            RestoreArguments(arguments);
        }

        
        private void RestoreArguments(Arguments arguments)
        {
            commonOptionsControl1.RestoreArguments(arguments);
            tbxSampleSize.Text = arguments.GetInt(Arg.numSample).ToString();
            tbxPower.Text = arguments.GetDouble(Arg.power).ToString();
            numberFDR.Text = (arguments.GetDouble(Arg.FDR) ?? 0.05).ToString(CultureInfo.CurrentCulture);
            numberLDFC.Text = (arguments.GetDouble(Arg.ldfc) ?? 1.25).ToString(CultureInfo.CurrentCulture);
            numberUDFC.Text = (arguments.GetDouble(Arg.udfc) ?? 1.75).ToString(CultureInfo.CurrentCulture);
            if (string.IsNullOrEmpty(tbxSampleSize.Text) && string.IsNullOrEmpty(tbxPower.Text))
            {
                tbxSampleSize.Text = 2.ToString();
            }
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            OkDialog();
        }

        private Arguments GetArguments()
        {
            var arguments = new Arguments();
            if (!commonOptionsControl1.GetArguments(arguments))
            {
                return null;
            }

            if (!Util.ValidateOptionalDouble(tbxPower, out double? power))
            {
                return null;
            }

            if (power.HasValue)
            {
                if (power < 0 || power > 1)
                {
                    Util.ShowControlMessage(tbxPower, "Power must be between 0 and 1.");
                    return null;
                }
                arguments.Set(Arg.power, power.Value);
            }

            if (!Util.ValidateOptionalInteger(tbxSampleSize, out int? sampleSize))
            {
                return null;
            }

            if (sampleSize.HasValue)
            {

                if (sampleSize<= 0)
                {
                    Util.ShowControlMessage(tbxSampleSize, "Sample size must be a positive integer or blank");
                    return null;
                }

                arguments.Set(Arg.numSample, sampleSize.Value);
            }

            if (power.HasValue && sampleSize.HasValue)
            {
                Util.ShowControlMessage(tbxSampleSize, "Only one of Power or Sample Size can be specified, and the other must be blank.");
                return null;
            }

            if (!power.HasValue && !sampleSize.HasValue)
            {
                Util.ShowControlMessage(tbxSampleSize, "You must specify either Power or Sample Size.");
            }

                
            if (!Util.ValidateDouble(numberFDR, out double fdr))
            {
                return null;
            }
            arguments.Set(Arg.FDR, fdr);
            if (!Util.ValidateDouble(numberLDFC, out double ldfc))
            {
                return null;
            }
            arguments.Set(Arg.ldfc, ldfc);
            if (!Util.ValidateDouble(numberUDFC, out double udfc))
            {
                return null;
            }
            arguments.Set(Arg.udfc, udfc);
            return arguments;
        }

        private void OkDialog()
        {
            var arguments = GetArguments();
            if (arguments == null)
            {
                return;
            }

            Arguments = arguments.ToArgumentList().ToArray();
            DialogResult = DialogResult.OK;
        }

        private void btnDefault_Click(object sender, EventArgs e)
        {
            RestoreArguments(new Arguments());
        }
    }

    public class MSstatsSampleSizeCollector
    {
        public static string[] CollectArgs(IWin32Window parent, TextReader report, string[] args)
        {
            var dataSetInfo = DataSetInfo.ReadDataSet(report);
            using (var dlg = new SampleSizeUi(dataSetInfo, Arguments.FromArgumentList(args)))
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
