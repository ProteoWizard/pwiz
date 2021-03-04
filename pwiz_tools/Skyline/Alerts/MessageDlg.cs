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
    public class MessageDlg : AlertDlg
    {
        public static void Show(IWin32Window parent, string message)
        {
            new MessageDlg(message).ShowAndDispose(parent);
        }

        public static void ShowException(IWin32Window parent, Exception exception)
        {
            ShowWithException(parent, exception.Message, exception);
        }

        public static void ShowWithException(IWin32Window parent, string message, Exception exception)
        {
            new MessageDlg(message) { Exception = exception }.ShowAndDispose(parent);
        }

        private MessageDlg(string message) : base(message, MessageBoxButtons.OK)
        {
        }
    }
}
