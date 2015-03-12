//
// $Id: ExtensionMethods.cs 393 2012-02-17 23:02:13Z chambm $
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
// The Original Code is the IDPicker project.
//
// The Initial Developer of the Original Code is Matt Chambers.
//
// Copyright 2012 Vanderbilt University
//
// Contributor(s): Brendan MacLean <brendanx .at. u.washington.edu>,
//

using System;
using System.Globalization;
using System.Linq;
using System.Data;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Threading;
using System.Runtime.InteropServices;
using System.ComponentModel;

namespace IDPicker
{
    public static partial class Util
    {
        /// <summary>
        /// Try an action that might throw an exception. If it does, sleep for a little while and
        /// try the action one more time. This oddity is necessary because certain file system
        /// operations (like moving a directory) can fail due to temporary file locks held by
        /// anti-virus software.
        /// </summary>
        /// <typeparam name="TEx">type of exception to catch</typeparam>
        /// <param name="action">action to try</param>
        /// <param name="repeatCount">how many times to retry the action</param>
        /// <param name="repeatDelay">how long (in milliseconds) to wait before the action is retried</param>
        public static void TryRepeatedly<TEx>(Action action, int repeatCount, int repeatDelay) where TEx : Exception
        {
            for (int i=0; i < repeatCount; ++i)
                try
                {
                    action();
                    return;
                }
                catch (TEx)
                {
                    Thread.Sleep(repeatDelay);
                    if (i + 1 == repeatCount)
                        throw;
                }
        }

        /// <summary>
        /// Try an action the might throw an Exception. If it fails, sleep for 500 milliseconds and try
        /// again. See the comments above for more detail about why this is necessary.
        /// </summary>
        /// <param name="action">action to try</param>
        public static void TryRepeatedly (Action action) { TryRepeatedly<Exception>(action, 2, 500); }

        public static void InitializeAccessibleNames(this Control control)
        {
            var toolstrip = control as ToolStrip;
            //var dgv = control as DataGridView;
            if (toolstrip != null)
            {
                foreach (var button in toolstrip.Items.OfType<ToolStripButton>())
                    button.AccessibleName = button.Name;

                var hostedControls = toolstrip.Items.OfType<ToolStripControlHost>().ToList();
                for (int i = 0; i < hostedControls.Count && i < control.Controls.Count; ++i)
                    control.Controls[i].Name = hostedControls[i].Name;
            }
            else
                foreach (var child in control.Controls.Cast<Control>())
                    InitializeAccessibleNames(child);
        }

        public static void PostFormToURL(string url, IDictionary<string, object> paramDictionary)
        {
            var paramString = String.Join("&", paramDictionary.Select(o => String.Format("{0}={1}", o.Key, o.Value)));
            const string postFormFileString = "<html><body><script>\n" +
                                              "function post(path, params, method) { method = method || 'post'; var form = document.createElement('form'); form.setAttribute('method', method); form.setAttribute('action', path); for(var key in params) { if(params.hasOwnProperty(key)) { var hiddenField = document.createElement('input'); hiddenField.setAttribute('type', 'hidden'); hiddenField.setAttribute('name', key); hiddenField.setAttribute('value', params[key]); form.appendChild(hiddenField); } } document.body.appendChild(form); form.submit(); }\n" +
                                              "function getQueryVariables() { var query = window.location.search.substring(1); var vars = query.split('&'); var result = {}; for (var i=0; i < vars.length; i++) { var pair = vars[i].split('='); result[pair[0]] = pair[1]; } return(result); } var query = getQueryVariables(); var site = query.site; if (site == undefined) {alert('No site specified in query string.');} else {delete query.site; post(site, query);}\n" +
                                              "</script></body></html>";
            string postFormFile = System.IO.Path.GetTempPath() + "idpickerPostForm.html";
            if (!System.IO.File.Exists(postFormFile))
                System.IO.File.WriteAllText(postFormFile, postFormFileString);
            string postFormUrl = String.Format("file:///{0}?site={1}&{2}", postFormFile, Uri.EscapeUriString(url), Uri.EscapeUriString(paramString));

            string browserCommand = Microsoft.Win32.Registry.GetValue(@"HKEY_CLASSES_ROOT\http\shell\open\command", "", null).ToString();
            var tokens = browserCommand.Split(new string[] { ".exe" }, StringSplitOptions.None);
            string browserExe = tokens[0].TrimStart('"') + ".exe";
            string args = tokens[1].TrimStart('"', ' ');
            if (args.Contains("%1")) args = args.Replace("%1", postFormUrl);
            else args += " " + postFormFile;

            System.Diagnostics.Process.Start(browserExe, args);
        }

        /// <summary>
        /// Generate a CRC32 checksum from a byte array.
        /// </summary>
        /// <see cref="http://sanity-free.org/134/standard_crc_16_in_csharp.html"/>
        public class Crc32
        {
            private static uint[] table;

            public static int ComputeChecksum(byte[] bytes)
            {
                uint crc = 0xffffffff;
                for (int i = 0; i < bytes.Length; ++i)
                {
                    byte index = (byte) (((crc) & 0xff) ^ bytes[i]);
                    crc = (uint) ((crc >> 8) ^ table[index]);
                }
                return (int) ~crc;
            }

            public static byte[] ComputeChecksumBytes(byte[] bytes)
            {
                return BitConverter.GetBytes(ComputeChecksum(bytes));
            }

            static Crc32()
            {
                uint poly = 0xedb88320;
                table = new uint[256];
                uint temp = 0;
                for (uint i = 0; i < table.Length; ++i)
                {
                    temp = i;
                    for (int j = 8; j > 0; --j)
                    {
                        if ((temp & 1) == 1)
                        {
                            temp = (uint) ((temp >> 1) ^ poly);
                        }
                        else
                        {
                            temp >>= 1;
                        }
                    }
                    table[i] = temp;
                }
            }
        }

        /// <summary>
        /// Puts an HTML fragment on the Windows clipboard (in CF_HTML format).
        /// </summary>
        /// <param name="htmlFragment">a fragment of HTML to put on the clipboard</param>
        public static void SetClipboardHtml(string htmlFragment)
        {
            var sb = new System.Text.StringBuilder();

            // Builds the CF_HTML header. See format specification here:
            // http://msdn.microsoft.com/library/default.asp?url=/workshop/networking/clipboard/htmlclipboard.asp

            // Because the Start/End tags refer to other offsets in the string, we use placeholders
            // and replace them with the real values later.
            const string header = @"Version:0.9
StartHTML:<<<<<<<1
EndHTML:<<<<<<<2
StartFragment:<<<<<<<3
EndFragment:<<<<<<<4
";

            const string pre = "<!DOCTYPE HTML PUBLIC \"-//W3C//DTD HTML 4.0 Transitional//EN\">\r\n" +
                               "<html><body>\r\n<!--StartFragment-->";
            const string post = "<!--EndFragment-->\r\n</body>\r\n</html>";

            sb.Append(header);
            int start_html = sb.Length;

            sb.Append(pre);
            int fragment_start = sb.Length;

            sb.Append(htmlFragment);
            int fragment_end = sb.Length;

            sb.Append(post);
            int end_html = sb.Length;

            // Replace offset placeholders
            sb.Replace("<<<<<<<1", start_html.ToString("D8"), 0, start_html);
            sb.Replace("<<<<<<<2", end_html.ToString("D8"), 0, start_html);
            sb.Replace("<<<<<<<3", fragment_start.ToString("D8"), 0, start_html);
            sb.Replace("<<<<<<<4", fragment_end.ToString("D8"), 0, start_html);

            string cf_html = sb.ToString();
            Clipboard.SetText(cf_html, TextDataFormat.Html);
        }
    }

    public static class WinAPI
    {
        [DllImport("user32.dll", SetLastError = true)]
        public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, SetWindowPosFlags uFlags);

        [Flags()]
        public enum SetWindowPosFlags : uint
        {
            /// <summary>If the calling thread and the thread that owns the window are attached to different input queues, 
            /// the system posts the request to the thread that owns the window. This prevents the calling thread from 
            /// blocking its execution while other threads process the request.</summary>
            /// <remarks>SWP_ASYNCWINDOWPOS</remarks>
            AsynchronousWindowPosition = 0x4000,

            /// <summary>Prevents generation of the WM_SYNCPAINT message.</summary>
            /// <remarks>SWP_DEFERERASE</remarks>
            DeferErase = 0x2000,

            /// <summary>Draws a frame (defined in the window's class description) around the window.</summary>
            /// <remarks>SWP_DRAWFRAME</remarks>
            DrawFrame = 0x0020,

            /// <summary>Applies new frame styles set using the SetWindowLong function. Sends a WM_NCCALCSIZE message to 
            /// the window, even if the window's size is not being changed. If this flag is not specified, WM_NCCALCSIZE 
            /// is sent only when the window's size is being changed.</summary>
            /// <remarks>SWP_FRAMECHANGED</remarks>
            FrameChanged = 0x0020,

            /// <summary>Hides the window.</summary>
            /// <remarks>SWP_HIDEWINDOW</remarks>
            HideWindow = 0x0080,

            /// <summary>Does not activate the window. If this flag is not set, the window is activated and moved to the 
            /// top of either the topmost or non-topmost group (depending on the setting of the hWndInsertAfter 
            /// parameter).</summary>
            /// <remarks>SWP_NOACTIVATE</remarks>
            DoNotActivate = 0x0010,

            /// <summary>Discards the entire contents of the client area. If this flag is not specified, the valid 
            /// contents of the client area are saved and copied back into the client area after the window is sized or 
            /// repositioned.</summary>
            /// <remarks>SWP_NOCOPYBITS</remarks>
            DoNotCopyBits = 0x0100,

            /// <summary>Retains the current position (ignores X and Y parameters).</summary>
            /// <remarks>SWP_NOMOVE</remarks>
            IgnoreMove = 0x0002,

            /// <summary>Does not change the owner window's position in the Z order.</summary>
            /// <remarks>SWP_NOOWNERZORDER</remarks>
            DoNotChangeOwnerZOrder = 0x0200,

            /// <summary>Does not redraw changes. If this flag is set, no repainting of any kind occurs. This applies to 
            /// the client area, the nonclient area (including the title bar and scroll bars), and any part of the parent 
            /// window uncovered as a result of the window being moved. When this flag is set, the application must 
            /// explicitly invalidate or redraw any parts of the window and parent window that need redrawing.</summary>
            /// <remarks>SWP_NOREDRAW</remarks>
            DoNotRedraw = 0x0008,

            /// <summary>Same as the SWP_NOOWNERZORDER flag.</summary>
            /// <remarks>SWP_NOREPOSITION</remarks>
            DoNotReposition = 0x0200,

            /// <summary>Prevents the window from receiving the WM_WINDOWPOSCHANGING message.</summary>
            /// <remarks>SWP_NOSENDCHANGING</remarks>
            DoNotSendChangingEvent = 0x0400,

            /// <summary>Retains the current size (ignores the cx and cy parameters).</summary>
            /// <remarks>SWP_NOSIZE</remarks>
            IgnoreResize = 0x0001,

            /// <summary>Retains the current Z order (ignores the hWndInsertAfter parameter).</summary>
            /// <remarks>SWP_NOZORDER</remarks>
            IgnoreZOrder = 0x0004,

            /// <summary>Displays the window.</summary>
            /// <remarks>SWP_SHOWWINDOW</remarks>
            ShowWindow = 0x0040,
        }
    }
}
