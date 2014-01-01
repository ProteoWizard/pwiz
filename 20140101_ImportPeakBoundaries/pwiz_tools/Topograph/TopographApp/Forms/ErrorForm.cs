/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using System.Collections;
using System.Diagnostics;
using System.Text;
using System.Windows.Forms;
using pwiz.Topograph.Util;
using pwiz.Topograph.ui.Properties;

namespace pwiz.Topograph.ui.Forms
{
    public partial class ErrorForm : Form
    {
        public ErrorForm()
        {
            InitializeComponent();
            Icon = Resources.TopographIcon;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            bindingSource1.DataSource = ErrorHandler.GetErrors();
            ErrorHandler.ErrorAdded += ErrorHandler_ErrorAdded;
        }
        protected override void OnHandleDestroyed(EventArgs e)
        {
            ErrorHandler.ErrorAdded -= ErrorHandler_ErrorAdded;
            base.OnHandleDestroyed(e);
        }


        void ErrorHandler_ErrorAdded(Error error)
        {
            try
            {
                BeginInvoke(new Action(() =>
                                           {
                                               bindingSource1.DataSource = ErrorHandler.GetErrors();
                                           }));
            }
            catch (Exception exception)
            {
                Trace.TraceError("Exception:{0}", exception);
            }
        }

        private void BtnClearOnClick(object sender, EventArgs e)
        {
            bindingSource1.DataSource = new Error[0];
            ErrorHandler.ClearErrors();
        }

        private void BtnCopyOnClick(object sender, EventArgs e)
        {
            var str = new StringBuilder();
            foreach (Error error in (IEnumerable)bindingSource1.DataSource)
            {
                str.Append(error.DateTime);
                str.Append("\t");
                str.Append(error.Message);
                str.Append("\t");
                str.Append(error.Detail);
                str.Append("\r\n");
            }
            Clipboard.SetText(str.ToString());
        }

        private void BindingSource1OnCurrentChanged(object sender, EventArgs e)
        {
            var error = bindingSource1.Current as Error;
            if (error == null)
            {
                tbxDetail.Text = "";
            }
            else
            {
                tbxDetail.Text = error.Detail;
            }
        }
    }
}
