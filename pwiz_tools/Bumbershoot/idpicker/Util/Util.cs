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
}
