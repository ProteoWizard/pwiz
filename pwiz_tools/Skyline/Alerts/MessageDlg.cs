/*
 * Original author: Nick Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Windows.Forms;


namespace pwiz.Skyline.Alerts
{
    /// <summary>
    /// More or less reproduces the MessageBox API, but in a way that integrates well with our automated tests and our peptide -> small molecule interface translations
    /// </summary>
    public class MessageDlg : AlertDlg
    {
        public static DialogResult Show(IWin32Window parent, string message)
        {
            return Show(parent, message, null, MessageBoxButtons.OK, DialogResult.OK);
        }

        public static DialogResult Show(IWin32Window parent, string message, string title)
        {
            return Show(parent, message, title, MessageBoxButtons.OK, DialogResult.OK);
        }

        public static DialogResult Show(IWin32Window parent, string message, MessageBoxButtons buttons, DialogResult defaultButton = DialogResult.None)
        {
            return Show(parent, message, null, buttons, DialogResult.OK);
        }

        public static DialogResult Show(IWin32Window parent, string message, string title, MessageBoxButtons buttons, DialogResult defaultButton)
        {
            var dlg = new MessageDlg(message, title, buttons, defaultButton);
            return dlg.ShowAndDispose(parent);
        }

        public static void ShowException(IWin32Window parent, Exception exception)
        {
            ShowWithException(parent, exception.Message, exception);
        }

        public static void ShowWithException(IWin32Window parent, string message, Exception exception)
        {
            new MessageDlg(message) { Exception = exception }.ShowAndDispose(parent);
        }

        private MessageDlg(string message, string title = null, MessageBoxButtons buttons = MessageBoxButtons.OK, 
            DialogResult defaultButton = DialogResult.None) : base(message, buttons, title, defaultButton)
        {
        }
    }
}
