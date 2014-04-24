using System;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using pwiz.Skyline.Properties;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// Helper class to decode HTML from the clipboard.
    /// See http://blogs.msdn.com/jmstall/archive/2007/01/21/html-clipboard.aspx for details.
    /// </summary>
    public class HtmlFragment
    {
        #region Read and decode from clipboard

        /// <summary>
        /// Get a HTML fragment from the clipboard.
        /// </summary>    
        /// <example>
        ///    string html = "<b>Hello!</b>";
        ///    HtmlFragment.CopyToClipboard(html);
        ///    HtmlFragment html2 = HtmlFragment.FromClipboard();
        ///    Debug.Assert(html2.Fragment == html);
        /// </example>
        /// <exception cref="ExternalException" />
        public static HtmlFragment FromClipboard()
        {
            string rawClipboardText = ClipboardEx.GetText(TextDataFormat.Html);
            HtmlFragment h = new HtmlFragment(rawClipboardText);
            return h;
        }

        /// <summary>
        /// Create an HTML fragment decoder around raw HTML text from the clipboard. 
        /// This text should have the header.
        /// </summary>
        /// <param name="rawClipboardText">raw html text, with header.</param>
        public HtmlFragment(string rawClipboardText)
        {
            // This decodes CF_HTML, which is an entirely text format using UTF-8.
            // Format of this header is described at:
            // http://msdn.microsoft.com/library/default.asp?url=/workshop/networking/clipboard/htmlclipboard.asp

            // Note the counters are byte counts in the original string, which may be Ansi. So byte counts
            // may be the same as character counts (since sizeof(char) == 1).
            // But System.String is unicode, and so byte counts are no longer the same as character counts,
            // (since sizeof(wchar) == 2). 
            int startHmtl = 0;

            int startFragment = 0;

            Regex r = new Regex("([a-zA-Z]+):(.+?)[\r\n]", // Not L10N
                                RegexOptions.IgnoreCase | RegexOptions.Compiled);

            for (Match m = r.Match(rawClipboardText); m.Success; m = m.NextMatch())
            {
                string key = m.Groups[1].Value.ToLowerInvariant();
                string val = m.Groups[2].Value;

                switch (key)
                {
                    // Version number of the clipboard. Starting version is 0.9. 
                    case "version": // Not L10N
                        _version = val;
                        break;

                    // Byte count from the beginning of the clipboard to the start of the context, or -1 if no context
                    case "starthtml": // Not L10N
                        if (startHmtl != 0)
                            throw new FormatException(Resources.HtmlFragment_HtmlFragment_StartHtml_is_already_declared);
                        startHmtl = int.Parse(val);
                        break;

                    // Byte count from the beginning of the clipboard to the end of the context, or -1 if no context.
                    case "endhtml": // Not L10N
                        if (startHmtl == 0)
                            throw new FormatException(Resources.HtmlFragment_HtmlFragment_StartHTML_must_be_declared_before_endHTML);
                        int endHtml = int.Parse(val);

                        _fullText = rawClipboardText.Substring(startHmtl, endHtml - startHmtl);
                        break;

                    //  Byte count from the beginning of the clipboard to the start of the fragment.
                    case "startfragment": // Not L10N
                        if (startFragment != 0)
                            throw new FormatException(Resources.HtmlFragment_HtmlFragment_StartFragment_is_already_declared);
                        startFragment = int.Parse(val);
                        break;

                    // Byte count from the beginning of the clipboard to the end of the fragment.
                    case "endfragment": // Not L10N
                        if (startFragment == 0)
                            throw new FormatException(Resources.HtmlFragment_HtmlFragment_StartFragment_must_be_declared_before_EndFragment);
                        int endFragment = int.Parse(val);
                        _fragment = rawClipboardText.Substring(startFragment, endFragment - startFragment);
                        break;

                    // Optional Source URL, used for resolving relative links.
                    case "sourceurl": // Not L10N
                        _source = new Uri(val);
                        break;
                }
            } // end for

            if (_fullText == null && _fragment == null)
            {
                throw new FormatException(Resources.HtmlFragment_HtmlFragment_No_data_specified);
            }
        }


        // Data. See properties for descriptions.
        private readonly string _version;
        private readonly string _fullText;
        private readonly string _fragment;
        private readonly Uri _source;

        /// <summary>
        /// Get the Version of the html. Usually something like "1.0".
        /// </summary>
        public string Version
        {
            get { return _version; }
        }


        /// <summary>
        /// Get the full text (context) of the HTML fragment. This includes tags that the HTML is enclosed in.
        /// May be null if context is not specified.
        /// </summary>
        public string Context
        {
            get { return _fullText; }
        }


        /// <summary>
        /// Get just the fragment of HTML text.
        /// </summary>
        public string Fragment
        {
            get { return _fragment; }
        }


        /// <summary>
        /// Get the Source URL of the HTML. May be null if no SourceUrl is specified. This is useful for resolving relative urls.
        /// </summary>
        public Uri SourceUrl
        {
            get { return _source; }
        }

        #endregion // Read and decode from clipboard

        #region Write to Clipboard
        // Helper to convert an integer into an 8 digit string.
        // String must be 8 characters, because it will be used to replace an 8 character string within a larger string.    
        static string To8DigitString(int x)
        {
            return String.Format("{0,8}", x); // Not L10N
        }

        /// <summary>
        /// Clears clipboard and copy a HTML fragment to the clipboard. This generates the header.
        /// </summary>
        /// <param name="htmlFragment">A html fragment.</param>
        /// <example>
        ///    HtmlFragment.CopyToClipboard("<b>Hello!</b>");
        /// </example>
        /// <exception cref="ExternalException" />
        public static void CopyToClipboard(string htmlFragment)
        {
            CopyToClipboard(htmlFragment, null, null);
        }

        /// <summary>
        /// Clears clipboard and copy a HTML fragment to the clipboard, providing additional meta-information.
        /// </summary>
        /// <param name="htmlFragment">a html fragment</param>
        /// <param name="title">optional title of the HTML document (can be null)</param>
        /// <param name="sourceUrl">optional Source URL of the HTML document, for resolving relative links (can be null)</param>
        /// <exception cref="ExternalException" />
        public static void CopyToClipboard(string htmlFragment, string title, Uri sourceUrl)
        {
            ClipboardEx.SetText(ClipBoardText(htmlFragment, title, sourceUrl), TextDataFormat.Html);
        }

        public static string ClipBoardText(string htmlFragment)
        {
            return ClipBoardText(htmlFragment, null, null);
        }

        public static string ClipBoardText(string htmlFragment, string title, Uri sourceUrl)
        {
            if (title == null)
                title = "From Clipboard"; // Not L10N? This is a title for an HTML page. I think the encodings different.

            StringBuilder sb = new StringBuilder();

            // Builds the CF_HTML header. See format specification here:
            // http://msdn.microsoft.com/library/default.asp?url=/workshop/networking/clipboard/htmlclipboard.asp

            // The string contains index references to other spots in the string, so we need placeholders so we can compute the offsets. 
            // The <<<<<<<_ strings are just placeholders. We'll backpatch them actual values afterwards.
            // The string layout (<<<) also ensures that it can't appear in the body of the html because the <
            // character must be escaped.
            const string header = // Not L10N
@"Format:HTML Format
Version:1.0
StartHTML:<<<<<<<1
EndHTML:<<<<<<<2
StartFragment:<<<<<<<3
EndFragment:<<<<<<<4
StartSelection:<<<<<<<3
EndSelection:<<<<<<<3
"; // Not L10N

            string pre = // Not L10N
@"<!DOCTYPE HTML PUBLIC ""-//W3C//DTD HTML 4.0 Transitional//EN"">
<HTML><HEAD><TITLE>" + title + @"</TITLE></HEAD><BODY><!--StartFragment-->"; // Not L10N

            const string post = @"<!--EndFragment--></BODY></HTML>"; // Not L10N

            sb.Append(header);
            if (sourceUrl != null)
            {
                sb.AppendFormat("SourceURL:{0}", sourceUrl); // Not L10N
            }
            int startHtml = sb.Length;

            sb.Append(pre);
            int fragmentStart = sb.Length;

            sb.Append(htmlFragment);
            int fragmentEnd = sb.Length;

            sb.Append(post);
            int endHtml = sb.Length;

            // Backpatch offsets
            sb.Replace("<<<<<<<1", To8DigitString(startHtml)); // Not L10N
            sb.Replace("<<<<<<<2", To8DigitString(endHtml)); // Not L10N
            sb.Replace("<<<<<<<3", To8DigitString(fragmentStart)); // Not L10N
            sb.Replace("<<<<<<<4", To8DigitString(fragmentEnd)); // Not L10N

            return sb.ToString();
        }

        #endregion // Write to Clipboard
    }

    
    /// <summary>
    /// String formatting for file size.
    /// Downloaded from: http://flimflan.com/blog/FileSizeFormatProvider.aspx 
    /// </summary>
    public class FileSizeFormatProvider : IFormatProvider, ICustomFormatter
    {
        public object GetFormat(Type formatType)
        {
            if (formatType == typeof(ICustomFormatter)) return this;
            return null;
        }

        private const string FILE_SIZE_FORMAT = "fs"; // Not L10N
        private const Decimal ONE_KILO_BYTE = 1024M;
        private const Decimal ONE_MEGA_BYTE = ONE_KILO_BYTE * 1024M;
        private const Decimal ONE_GIGA_BYTE = ONE_MEGA_BYTE * 1024M;

        public string Format(string format, object arg, IFormatProvider formatProvider)
        {
            if (format == null || !format.StartsWith(FILE_SIZE_FORMAT))
            {
                return DefaultFormat(format, arg, formatProvider);
            }

            if (arg is string)
            {
                return DefaultFormat(format, arg, formatProvider);
            }

            Decimal size;

            try
            {
                size = Convert.ToDecimal(arg);
            }
            catch (InvalidCastException)
            {
                return DefaultFormat(format, arg, formatProvider);
            }

            string suffix;

            if (size > ONE_GIGA_BYTE)
            {
                size /= ONE_GIGA_BYTE;
                suffix = " GB"; // Not L10N
            }
            else if (size > ONE_MEGA_BYTE)
            {
                size /= ONE_MEGA_BYTE;
                suffix = " MB"; // Not L10N
            }
            else if (size > ONE_KILO_BYTE)
            {
                size /= ONE_KILO_BYTE;
                suffix = " KB"; // Not L10N
            }
            else
            {
                suffix = " B"; // Not L10N
            }

            string precision = format.Substring(2);
            if (String.IsNullOrEmpty(precision))
                precision = "2"; // Not L10N
            string formatString = "{0:N" + precision + "}{1}";  // Avoid ReSharper analysis // Not L10N
            return String.Format(formatString, size, suffix);
        }

        private static string DefaultFormat(string format, object arg, IFormatProvider formatProvider)
        {
            IFormattable formattableArg = arg as IFormattable;
            if (formattableArg != null)
            {
                return formattableArg.ToString(format, formatProvider);
            }
            return arg.ToString();
        }
    }

    public static class ComboHelper
    {
        public static void AutoSizeDropDown(ToolStripComboBox comboBox)
        {
            AutoSizeDropDown((ComboBox) comboBox.Control);
        }

        public static void AutoSizeDropDown(ComboBox comboBox)
        {
            // Make the dropdown at least as wide as the combo box itself
            int widestWidth = comboBox.Width;

            using (Graphics g = comboBox.CreateGraphics())
            {
                foreach (object item in comboBox.Items)
                {
                    string valueToMeasure = item.ToString();

                    int currentWidth = TextRenderer.MeasureText(g, valueToMeasure, comboBox.Font).Width;
                    if (currentWidth > widestWidth)
                        widestWidth = currentWidth;
                }
            }

            comboBox.DropDownWidth = widestWidth;
        }        
    }

    public static class ClipboardHelper
    {
        [DllImport("user32.dll")]
        private static extern IntPtr GetOpenClipboardWindow();
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        public static string GetOpenClipboardMessage(string prefix)
        {
            try
            {
                IntPtr hwnd = GetOpenClipboardWindow();
                if (hwnd != IntPtr.Zero)
                {
                    uint processId;
                    GetWindowThreadProcessId(hwnd, out processId);
                    var process = Process.GetProcessById((int)processId);
                    var message = prefix + Environment.NewLine;
                    message += string.Format(Resources.ClipboardHelper_GetOpenClipboardMessage_The_process__0__ID__1__has_the_clipboard_open,
                            process.ProcessName, processId);
                    return message;
                }
            }
            catch (Exception)
            {
                return prefix;
            }
            return prefix;
        }
    }
}
