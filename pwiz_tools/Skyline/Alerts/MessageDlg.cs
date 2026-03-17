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
using System.Drawing;
using System.Windows.Forms;
using pwiz.Common.GUI;

namespace pwiz.Skyline.Alerts
{
    public class MessageDlg : AlertDlg
    {
        public static void Show(IWin32Window parent, string message, bool ignoreModeUI = false)
        {
            new MessageDlg(message, ignoreModeUI).ShowAndDispose(parent);
        }

        public static void Show(IWin32Window parent, string message, bool ignoreModeUI, Image messageIcon)
        {
            new MessageDlg(message, ignoreModeUI) {MessageIcon = messageIcon}.ShowAndDispose(parent);
        }

        // For displaying a MessageDlg with a specific set of buttons
        public static DialogResult Show(IWin32Window parent, string message, bool IgnoreModeUI, MessageBoxButtons buttons)
        {
            return new MessageDlg(message, IgnoreModeUI, buttons).ShowAndDispose(parent);
        }

        public static DialogResult Show(IWin32Window parent, string message, bool IgnoreModeUI, MessageBoxButtons buttons, Image messageIcon)
        {
            return new MessageDlg(message, IgnoreModeUI, buttons) {MessageIcon = messageIcon}.ShowAndDispose(parent);
        }

        public static void ShowException(IWin32Window parent, Exception exception, bool ignoreModeUI = false)
        {
            ShowWithException(parent, exception.Message, exception, ignoreModeUI);
        }

        public static void ShowWithException(IWin32Window parent, string message, Exception exception, bool ignoreModeUI = false)
        {
            new MessageDlg(message, ignoreModeUI) { Exception = exception }.ShowAndDispose(parent);
        }

        public static void ShowWithDetails(IWin32Window parent, string message, string detailMessage, Image messageIcon, bool ignoreModeUI = false)
        {
            new MessageDlg(message, ignoreModeUI) { MessageIcon = messageIcon, DetailMessage = detailMessage}.ShowAndDispose(parent);
        }

        // Display a MessageDlg and add additional, caller-provided information about a network failure to the More Info section
        public static void ShowWithExceptionAndNetworkDetail(IWin32Window parent, string message, string networkDetailMessage, Exception exception, bool ignoreModeUI = false)
        {
            var dlg = new MessageDlg(message, ignoreModeUI)
            {
                Exception = exception
            };

            dlg.DetailMessage = 
                networkDetailMessage +
                Environment.NewLine + Environment.NewLine +
                dlg.DetailMessage;
            
            dlg.ShowAndDispose(parent);
        }

        public static void ShowError(IWin32Window parent, string message, bool ignoreModeUI = false)
        {
            new MessageDlg(message, ignoreModeUI) { MessageIcon = MessageIcons.Error}.ShowAndDispose(parent);
        }

        private MessageDlg(string message, bool ignoreModeUI, MessageBoxButtons buttons = MessageBoxButtons.OK) : base(message, buttons)
        {
            GetModeUIHelper().IgnoreModeUI = ignoreModeUI; // May not want any "peptide"->"molecule" translation
        }
    }
}
