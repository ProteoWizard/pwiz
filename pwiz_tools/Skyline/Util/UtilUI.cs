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
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Alerts;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.Tools;
using ZedGraph;

namespace pwiz.Skyline.Util
{
    /// <summary>
    /// For controls that do not allow completely turning off updates
    /// that cause painting during large operations.
    /// <para>
    /// The TreeView control often updates its scrollbar during long
    /// operations.  BeginUpdate keeps the client area from painting,
    /// but apparently not the scrollbar.  Certainly would be better
    /// if the TreeView could be stopped from sending all the paint
    /// and scrollbar update messages, but at least covering it with
    /// this control keeps it from flashing during operations like
    /// clearing the tree, and collapsing many nodes at once.</para>
    /// </summary>
    public sealed class CoverControl : Control
    {
        public CoverControl(Control cover)
            : base(cover.Parent, null)
        {
            SetStyle(ControlStyles.UserPaint, true);
            SetStyle(ControlStyles.AllPaintingInWmPaint, true);
            SetStyle(ControlStyles.DoubleBuffer, true);

            Bounds = cover.Bounds;

            BringToFront();
            Show();
        }
    }

    /// <summary>
    /// Broker communication between a long operation and the user,
    /// usually used with <see cref="pwiz.Skyline.Controls.LongWaitDlg"/>.
    /// </summary>
    public interface ILongWaitBroker
    {
        /// <summary>
        /// True if the use has canceled the long operation
        /// </summary>
        bool IsCanceled { get; }

        /// <summary>
        /// Percent complete in the progress indicator shown to the user
        /// </summary>
        int ProgressValue { get; set; }

        /// <summary>
        /// Message shown to the user
        /// </summary>
        string Message { set; }

        /// <summary>
        /// Returns true if the given original document is no longer the active
        /// document for this operation.
        /// </summary>
        bool IsDocumentChanged(SrmDocument docOrig);

        /// <summary>
        /// Shows a dialog box on the right thread, parented to the progress form
        /// </summary>
        DialogResult ShowDialog(Func<IWin32Window, DialogResult> show);

        /// <summary>
        /// Convenience function which calculates progress percentage and throws
        /// an OperationCanceledOperation if the user has canceled.
        /// </summary>
        void SetProgressCheckCancel(int step, int totalSteps);

        CancellationToken CancellationToken { get; }
    }

    /// <summary>
    /// Exposes <see cref="IProgressMonitor"/> for an action that requires the interface,
    /// given a <see cref="ILongWaitBroker"/>.
    /// </summary>
    public sealed class ProgressWaitBroker : IProgressMonitor
    {
        private readonly Action<IProgressMonitor> _performWork;
        private ILongWaitBroker _broker;

        public ProgressWaitBroker(Action<IProgressMonitor> performWork)
        {
            _performWork = performWork;
            Status = new ProgressStatus(string.Empty);
        }

        public void PerformWork(ILongWaitBroker broker)
        {
            _broker = broker;
            _performWork(this);
        }

        public IProgressStatus Status { get; private set; }

        public bool IsCanceled
        {
            get { return _broker.IsCanceled; }
        }

        public UpdateProgressResponse UpdateProgress(IProgressStatus status)
        {
            _broker.ProgressValue = status.PercentComplete;
            _broker.Message = status.Message;
            Status = status;
            return UpdateProgressResponse.normal;
        }

        public bool HasUI { get { return false; } }
    }

    public class ProgressUpdateEventArgs : EventArgs
    {
        public ProgressUpdateEventArgs(IProgressStatus progress)
        {
            Progress = progress;
        }

        public IProgressStatus Progress { get; private set; }

        public UpdateProgressResponse Response { get; set; }
    }

    public interface IClipboardDataProvider
    {
        DataObject ProvideData();
    }

    public interface IUpdatable
    {
        /// <summary>
        /// Update this UI element.  Updates are performed on a time
        /// event to avoid blocking the UI thread.
        /// </summary>
        void UpdateUI(bool selectionChanged = true);
    }

    /// <summary>
    /// Implement if you need a graph to persist some information
    /// into the .view file
    /// </summary>
    public interface ILayoutPersistable
    {
        string GetPersistentString();
    }

    public sealed class MoveThreshold
    {
        public MoveThreshold(int width, int height)
        {
            Threshold = new Size(width, height);
        }

        public Size Threshold { get; set; }
        public Point? Location { get; set; }

        public bool Moved(Point locationNew)
        {
            return !Location.HasValue ||
                Math.Abs(Location.Value.X - locationNew.X) > Threshold.Width ||
                Math.Abs(Location.Value.Y - locationNew.Y) > Threshold.Height;            
        }
    }

    /// <summary>
    /// Simple class for custom drawing of text ranges
    /// </summary>
    public sealed class TextSequence
    {
        public String Text { get; set; }
        public Font Font { get; set; }
        public Color Color { get; set; }
        public int Position { get; set; }
        public int Width { get; set; }
        public bool IsPlainText { get; set; }

        public static IEnumerable<TextSequence> Coalesce(IEnumerable<TextSequence> sequences)
        {
            TextSequence lastSequence = null;
            StringBuilder stringBuilder = new StringBuilder();

            foreach (var textSequence in sequences)
            {
                if (lastSequence == null)
                {
                    lastSequence = textSequence;
                    continue;
                }

                if (Equals(lastSequence.Color, textSequence.Color) &&
                    ReferenceEquals(lastSequence.Font, textSequence.Font))
                {
                    stringBuilder.Append(textSequence.Text);
                    continue;
                }

                yield return new TextSequence
                {
                    Text = lastSequence.Text + stringBuilder,
                    Font = lastSequence.Font,
                    Color = lastSequence.Color,
                    IsPlainText = lastSequence.IsPlainText
                };
                stringBuilder.Clear();
                lastSequence = textSequence;
            }

            if (lastSequence != null)
            {
                yield return new TextSequence
                {
                    Text = lastSequence.Text + stringBuilder,
                    Font = lastSequence.Font,
                    Color = lastSequence.Color,
                    IsPlainText = lastSequence.IsPlainText
                };
            }
        }
    }

    /// <summary>
    /// This interface was created to enable swapping for a fake WebHelpers in testing.  
    /// </summary>
    public interface IWebHelpers
    {
        void OpenLink(string link);
        void PostToLink(string link, string postData);
    }

    /// <summary>
    /// Helpers for doing web stuff.
    /// </summary>
    public sealed class WebHelpers : IWebHelpers
    {        
        /// <summary>
        /// Opens a URL in a web browser without exception handling
        /// Used in the Model.ToolDescription when error messages aren't allowd. 
        /// </summary>        
        /// <param name="link">Web URL to open in browser</param>
        public static void OpenLink(string link)
        {
            try
            {
                Process.Start(link);
            }
            catch (Exception)
            {
                throw new WebToolException(UtilResources.Could_not_open_web_Browser_to_show_link_, link);
            }
        }
                
        /// <summary>
        /// Opens a URL in a web browser, with exception handling
        /// </summary>
        /// <param name="parent">Parent form for error messages</param>
        /// <param name="link">Web URL to open in browser</param>
        public static void OpenLink(IWin32Window parent, string link)
        {
            try
            {
                OpenLink(link);
            }
            catch (Exception)
            {
                ShowLinkFailure(parent, link);
            }
        }

        private const string SKYLINE_SITE_ROOT = "https://skyline.ms";

        public static string GetSkylineLink(string linkRelative)
        {
            return SKYLINE_SITE_ROOT + linkRelative;
        }

        public static void OpenSkylineLink(IWin32Window parent, string linkRelative)
        {
            OpenLink(parent, GetSkylineLink(linkRelative));
        }

        public static string GetSkylineShortLink(string shortName)
        {
            return GetSkylineLink(@"/" + shortName + @".url");
        }

        public static void OpenSkylineShortLink(IWin32Window parent, string shortName)
        {
            OpenLink(parent, GetSkylineShortLink(shortName));
        }

        public static void OpenRegexDocLink(IWin32Window parent)
        {
            OpenLink(parent, @"http://www.regular-expressions.info/reference.html");
        }

        /// <summary>
        /// Shows a form with a link in it that allows a user to continue attempting failed link navigation
        /// </summary>
        public static void ShowLinkFailure(IWin32Window parent, string link)
        {
            AlertLinkDlg.Show(parent, UtilResources.Could_not_open_web_Browser_to_show_link_, link, link, false);
        }

        
        /// <summary>
        /// Post a report to a website, without exception handling.  
        /// </summary>        
        /// <param name="link">Web URL to post report to</param>
        /// <param name="postData">Report text</param>
        public static void PostToLink(string link, string postData)
        {
            string filePath = Path.GetTempFileName() + @".html";

            string javaScript = string.Format(

@"<script type=""text/javascript"">
function submitForm()
{{
    document.getElementById(""my_form"").submit();
}}
window.onload = submitForm;
</script>
<form id=""my_form"" action=""{0}"" method=""post"" style=""visibility: hidden;"">
<textarea name=""SkylineReport"">{1}</textarea>
</form>", 

                link, WebUtility.HtmlEncode(postData));

            try
            {
                using (var saver = new FileSaver(filePath))
                {
                    using (var writer = new StreamWriter(saver.SafeName))
                    {
                        writer.Write(javaScript);
                        writer.Flush();
                        writer.Close();
                    }

                    saver.Commit();
                }
            }
            catch (Exception)
            {                                
                throw new IOException(UtilResources.WebHelpers_PostToLink_Failure_saving_temporary_post_data_to_disk_);                
            }

            try
            {
                // CONSIDER: User could have a configuration that opens html documents
                //           with a text editor. This would defeat the redirection and post.
                Process.Start(filePath);
            }
            catch(Exception)
            {
                throw new WebToolException(UtilResources.Could_not_open_web_Browser_to_show_link_, link);
            }

            DeleteTempHelper d = new DeleteTempHelper(filePath);
            Thread t = new Thread(d.DeletePath);
            t.Start();
        }

        #region Implementation of IWebHelpers
        void IWebHelpers.PostToLink(string link, string postData)
        {
            PostToLink(link, postData);
        }

        void IWebHelpers.OpenLink(string link)
        {
            OpenLink(link);
        }
        #endregion // Implementation of IWebHelpers
    }

    public class DeleteTempHelper
    {
        private readonly string _path;

        public DeleteTempHelper(string path)
        {
            _path = path;
        }

        /// <summary>
        /// Wait 30 seconds then delete file located at _path.
        /// </summary>
        public void DeletePath()
        {
            Thread.Sleep(30*1000); //30 seconds.
            FileEx.SafeDelete(_path, true);
        }
    }

    public static class ZedGraphHelper
    {
        public static void BuildContextMenu(ZedGraphControl graphControl, ContextMenuStrip menuStrip, bool keepZoom = false)
        {
            var items = new ToolStripItem[menuStrip.Items.Count];
            for (int i = 0; i < items.Length; i++)
                items[i] = menuStrip.Items[i];

            // Remove some ZedGraph menu items not of interest
            bool seenZoom = false;
            foreach (var item in items)
            {
                var tag = (string)item.Tag;
                if (tag == @"set_default" || tag == @"show_val" || item is ToolStripSeparator)
                    menuStrip.Items.Remove(item);
                else if (tag == @"unzoom" || tag == @"undo_all")
                {
                    if (!keepZoom)
                        menuStrip.Items.Remove(item);
                    else if (!seenZoom)
                        menuStrip.Items.Insert(menuStrip.Items.IndexOf(item), new ToolStripSeparator());
                    seenZoom = true;
                }
            }
            ZedGraphClipboard.AddToContextMenu(graphControl, menuStrip);            
        }
    }
}

