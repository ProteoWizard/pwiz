//
// $Id$
//
// Licensed under the Apache License, Version 2.0 (the "License"); 
// you may not use this file except in compliance with the License. 
// You may obtain a copy of the License at 
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software 
// distributed under the License is distributed on an "AS IS" BASIS, 
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied. 
// See the License for the specific language governing permissions and 
// limitations under the License.
//
// The Original Code is the ProteoWizard project.
//
// The Initial Developer of the Original Code is Shannon Joyner.
//
// Copyright 2011 University of Washington - Seattle, WA
//
// Contributor(s): Matt Chambers
//

using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using System.Net;
using System.Runtime.InteropServices;
using System.DirectoryServices.ActiveDirectory;


namespace IDPicker.Forms
{
    public partial class ReportErrorDlg : Form
    {
        public enum ReportChoice { choice, always, never }

        public ReportErrorDlg (Exception e, ReportChoice reportChoice)
        {
            _exception = e ?? new Exception("Unknown exception");

            InitializeComponent();

            //Icon = Resources.Skyline;

            tbErrorDescription.Text = replaceNewlines(e.Message);

            tbSourceCodeLocation.Text = StackTraceText;

            tbUsername.Text = getUserDisplayName();

            if (reportChoice != ReportChoice.choice)
            {
                btnOK.Visible = false;
                btnOK.DialogResult = DialogResult.Cancel;

                btnCancel.Text = "Close";
                if (reportChoice == ReportChoice.always)
                    btnCancel.DialogResult = DialogResult.OK;
                AcceptButton = btnCancel;

                StringBuilder error = new StringBuilder("An unexpected error has occurred, as shown below.");
                if (reportChoice == ReportChoice.always)
                    error.AppendLine().Append("An error report will be posted.");

                lblReportError.Text = error.ToString();
            }
        }

        private Exception _exception;

        private string trimStackTrace (string stackTrace)
        {
            if (String.IsNullOrEmpty(stackTrace))
                return "<no stack trace>";

            // remove directory path and System scope
            return Regex.Replace(stackTrace, @".\:\\(?:[^\\]+\\)*?([^\\]+\.cs\:)", "$1 ")
                        .Replace("System.", "");
        }

        private string replaceNewlines (string message)
        {
            if (message.Contains("\n") && !message.Contains(Environment.NewLine))
                return message.Replace("\n", Environment.NewLine);
            return message;
        }

        public string StackTraceText
        {
            get
            {
                StringBuilder stackTrace = new StringBuilder();

                stackTrace.Append("Exception type: ").AppendLine(ExceptionType);
                stackTrace.AppendLine("Stack trace:").AppendLine(trimStackTrace(_exception.StackTrace)).AppendLine();

                for (var x = _exception.InnerException; x != null; x = x.InnerException)
                {
                    if (ReferenceEquals(x, _exception.InnerException))
                        stackTrace.AppendLine("Inner exceptions:");
                    else
                        stackTrace.AppendLine("---------------------------------------------------------------");
                    stackTrace.Append("Exception type: ").AppendLine(x.GetType().FullName);
                    stackTrace.Append("Error message: ").AppendLine(x.Message);
                    stackTrace.AppendLine(replaceNewlines(x.Message)).AppendLine(trimStackTrace(x.StackTrace));
                }
                return stackTrace.ToString();
            }
        }


        public string MessageBody
        {
            get
            {
                StringBuilder sb = new StringBuilder();

                if (!string.IsNullOrEmpty(tbUsername.Text))
                    sb.Append("User name: ").AppendLine(tbUsername.Text);

                if (!string.IsNullOrEmpty(tbEmail.Text))
                    sb.Append("User email address: ").AppendLine(tbEmail.Text);

                if (!string.IsNullOrEmpty(tbMessage.Text))
                    sb.Append("User comments:").AppendLine().AppendLine(tbMessage.Text).AppendLine();

                sb.AppendFormat("IDPicker version: {0} ({1})", Util.Version, Environment.Is64BitProcess ? "64-bit" : "32-bit").AppendLine();
                sb.Append("Exception type: ").AppendLine(ExceptionType);
                sb.Append("Error message: ").AppendLine(replaceNewlines(_exception.Message)).AppendLine();

                // Stack trace with any inner exceptions
                sb.AppendLine(tbSourceCodeLocation.Text);

                return sb.ToString();
            }
        }

        public string ExceptionType { get { return _exception.GetType().FullName; } }
        public string Username { get { return tbUsername.Text; } }
        public string Email { get { return tbEmail.Text; } }
        public bool ForceClose { get { return forceCloseCheckBox.Checked; } }

        public void btnClipboard_Click (object sender, EventArgs e)
        {
            Clipboard.SetDataObject(MessageBody, true);
        }

        private string getUserDisplayName()
        {
            return String.Empty;
        }

        /*[DllImport("secur32.dll", CharSet = CharSet.Auto)]
        private static extern int GetUserNameEx (int nameFormat, StringBuilder userName, ref uint userNameSize);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern int NetUserGetInfo ([MarshalAs(UnmanagedType.LPWStr)] string serverName,
                                                  [MarshalAs(UnmanagedType.LPWStr)] string userName,
                                                  int level, out IntPtr bufPtr);

        [DllImport("netapi32.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
        private static extern long NetApiBufferFree (out IntPtr bufPtr);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct USER_INFO_10
        {
            [MarshalAs(UnmanagedType.LPWStr)] public string usri10_name;
            [MarshalAs(UnmanagedType.LPWStr)] public string usri10_comment;
            [MarshalAs(UnmanagedType.LPWStr)] public string usri10_usr_comment;
            [MarshalAs(UnmanagedType.LPWStr)] public string usri10_full_name;
        }

        private string getUserDisplayName ()
        {
            var username = new StringBuilder(1024);
            uint userNameSize = (uint) username.Capacity;

            // try to get display name and convert from "Last, First" to "First Last" if necessary
            if (0 != GetUserNameEx(3, username, ref userNameSize))
                return Regex.Replace(username.ToString(), @"(\S+), (\S+)", "$2 $1");

            // get SAM compatible name <server/machine>\\<username>
            if (0 != GetUserNameEx(2, username, ref userNameSize))
            {
                IntPtr bufPtr;
                try
                {
                    string domain = Regex.Replace(username.ToString(), @"(.+)\\.+", @"$1");
                    DirectoryContext context = new DirectoryContext(DirectoryContextType.Domain, domain);
                    DomainController dc = DomainController.FindOne(context);

                    if (0 == NetUserGetInfo(dc.IPAddress,
                                            Regex.Replace(username.ToString(), @".+\\(.+)", "$1"),
                                            10, out bufPtr))
                    {
                        var userInfo = (USER_INFO_10) Marshal.PtrToStructure(bufPtr, typeof (USER_INFO_10));
                        return Regex.Replace(userInfo.usri10_full_name, @"(\S+), (\S+)", "$2 $1");
                    }
                }
                finally
                {
                    NetApiBufferFree(out bufPtr);
                }
            }

            return String.Empty;
        }*/
    }
}
