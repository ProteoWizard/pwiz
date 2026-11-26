/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Runtime.InteropServices;
using System.Windows.Forms;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Shows a caught exception to the user, for the cases <see cref="ExceptionUtil"/> classifies.
    /// </summary>
    public static class ExceptionUtilUI
    {
        /// <summary>
        /// Calls either <see cref="MessageDlg.ShowWithException"/> or <see cref="ProgramContext.ReportException"/> depending
        /// on what <see cref="ExceptionUtil.IsProgrammingDefect"/> returns.
        /// <param name="parent">Parent window for message dialog</param>
        /// <param name="exception">The exception that was caught</param>
        /// <param name="message">Optional message which summarizes what Skyline was trying to do when the exception happened,
        /// to be inserted on a separate line before the exception's message.</param>
        /// </summary>
        public static void DisplayOrReportException(IWin32Window parent, Exception exception, string message = null)
        {
            if (ExceptionUtil.IsProgrammingDefect(exception))
            {
                ProgramContext.ReportException(exception);
            }
            else
            {
                string fullMessage = exception.Message;
                if (!string.IsNullOrEmpty(message))
                {
                    fullMessage = TextUtil.LineSeparate(message, fullMessage);
                }
                MessageDlg.ShowWithException(parent, fullMessage, exception);
            }
        }

        private const int CLIPBRD_E_CANT_OPEN = unchecked((int)0x800401D0);

        /// <summary>
        /// Displays either a friendly error message or an unhandled exception dialog
        /// depending on whether the error is expected. This is meant to be called from an exception handler
        /// inside ProcessKeyEvent.
        /// </summary>
        public static void HandleProcessKeyException(IWin32Window parent, Exception exception, Keys keys)
        {
            if ((exception as ExternalException)?.ErrorCode == CLIPBRD_E_CANT_OPEN)
            {
                MessageDlg.ShowWithException(parent, ClipboardHelper.GetClipboardErrorMessageForKeystroke(keys), exception, true);
                return;
            }

            string message = TextUtil.LineSeparate(string.Format(@"Error processing keystroke '{0}'.", keys),
                exception.Message);
            ProgramContext.ReportException(new ApplicationException(message, exception));
        }
    }
}
