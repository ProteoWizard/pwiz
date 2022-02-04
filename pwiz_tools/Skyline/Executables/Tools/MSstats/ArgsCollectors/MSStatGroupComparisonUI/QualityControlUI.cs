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
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace MSStatArgsCollector
{
    public partial class QualityControlUI : Form
    {
        public string[] Arguments { get; private set; }

        public QualityControlUI(DataSetInfo dataSetInfo, Arguments arguments)
        {
            InitializeComponent();
            commonOptionsControl1.InitializeOptions(dataSetInfo, arguments);
            tbxWidth.Text = (arguments.GetInt(Arg.width) ?? 10).ToString();
            tbxHeight.Text = (arguments.GetInt(Arg.height) ?? 10).ToString();
        }
        private Arguments GenerateArguments()
        {
            var arguments = new Arguments();
            if (!commonOptionsControl1.GetArguments(arguments))
            {
                return null;
            }

            if (!string.IsNullOrEmpty(tbxWidth.Text))
            {
                if (!Util.ValidateInteger(tbxWidth, out int width))
                {
                    return null;
                }

                arguments.Set(Arg.width, width);
            }

            if (!string.IsNullOrEmpty(tbxHeight.Text))
            {
                if (!Util.ValidateInteger(tbxHeight, out int height))
                {
                    return null;
                }
                arguments.Set(Arg.height, height);
            }

            return arguments;
        }

        private void btnOK_Click(object sender, EventArgs e)
        {
            var arguments = GenerateArguments();
            if (arguments == null)
            {
                return;
            }

            Arguments = arguments.ToArgumentList().ToArray();
            DialogResult = DialogResult.OK;
        }
    }
    public class MSstatsQualityControlCollector
    {
        public static string[] CollectArgs(IWin32Window parent, TextReader report, string[] args)
        {
            var dataSetInfo = DataSetInfo.ReadDataSet(report);
            using (var dlg = new QualityControlUI(dataSetInfo, Arguments.FromArgumentList(args)))
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
