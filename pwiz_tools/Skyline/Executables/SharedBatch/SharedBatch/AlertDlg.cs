/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using pwiz.Common.GUI;


namespace SharedBatch
{
    /// <summary>
    /// Use for a <see cref="MessageBox"/> substitute that can be
    /// detected and closed by automated functional tests.
    /// </summary>
    public static class AlertDlg
    {
        public static void ShowMessage(IWin32Window parent, string message)
        {
            using var dlg = new CommonAlertDlg(message, MessageBoxButtons.OK);
            dlg.ShowDialog(parent);
        }

        public static void ShowWarning(IWin32Window parent, string message)
        {
            ShowMessage(parent, message);
        }

        public static void ShowError(IWin32Window parent, string message)
        {
            ShowMessage(parent, message);
        }

        public static void ShowInfo(IWin32Window parent, string message)
        {
            ShowMessage(parent, message);
        }
        public static DialogResult ShowQuestion(IWin32Window parent, string message)
        {
            using var dlg = new CommonAlertDlg(message, MessageBoxButtons.YesNo);
            return dlg.ShowDialog(parent);
        }

        public static void ShowErrorWithException(IWin32Window parent, string message, Exception exception)
        {
            using var dlg = new CommonAlertDlg(message, MessageBoxButtons.OK);
            dlg.Exception = exception;
            dlg.ShowDialog(parent);
        }


        public static DialogResult ShowOkCancel(IWin32Window parent, string message)
        {
            using var dlg = new CommonAlertDlg(message, MessageBoxButtons.OKCancel);
            return dlg.ShowDialog(parent);
        }

        public static DialogResult ShowLargeOkCancel(IWin32Window parent, string message)
        {
            return ShowOkCancel(parent, message);
        }
    }
}
