/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Fable 5.1) <noreply .at. anthropic.com>
 *
 * Copyright 2026 University of Washington - Seattle, WA
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

using pwiz.Skyline.Controls;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// A tip Skyline draws itself (a <see cref="CustomTip"/>, e.g. the <see cref="NodeTip"/> a Targets tree node
    /// brings up) while it is showing. It is a bare <see cref="NativeWindow"/>, neither a form nor a dialog, so it
    /// is listed for what a caller can do with it: see that it has come up, and capture its image whole -- it
    /// usually runs past the edge of the window it belongs to. It has no controls and nothing to dismiss: it goes
    /// away by itself when the mouse moves off the item it is about.
    /// </summary>
    public class TipWindow : StandaloneWindow
    {
        private readonly CustomTip _tip;

        public TipWindow(CustomTip tip, IntPtr hwnd, CancellationToken cancellationToken) : base(cancellationToken, hwnd)
        {
            _tip = tip;
        }

        public override string Title => string.Empty;
        public override string FormId => _tip.GetType().Name + @":" + Title;
        public override string Name => string.Empty;
        public override Type ElementType => _tip.GetType();
        public override bool IsEnabled => true;

        public override bool IsModal => false;
        public override bool IsTransient => true;
        public override bool IsProgressing => false;

        /// <summary>What the tip says, for a tip that knows its own text or table. A tip a tree node draws for
        /// itself has neither, so its content is only in its image. Read on the tip's UI thread.</summary>
        public override string DetailedMessage
        {
            get
            {
                var nodeTip = _tip as NodeTip;
                return nodeTip?.TipText ?? nodeTip?.TipTable?.ToString();
            }
        }

        public override IEnumerable<UiElement> EnumerateChildren() => Enumerable.Empty<UiElement>();

        public override System.Drawing.Bitmap CaptureImage() => JsonUiService.CaptureWindowRect(Hwnd);

        public override ActionResult SetValue(string controlId, string value)
        {
            throw new ArgumentException(new LlmInstruction(@"A tip has no controls to set a value on."));
        }

        public override ActionResult DismissWithAcceptButton()
        {
            throw NothingToDismiss();
        }

        public override ActionResult DismissWithCancelButton()
        {
            throw NothingToDismiss();
        }

        private static Exception NothingToDismiss()
        {
            return new ArgumentException(new LlmInstruction(
                @"A tip cannot be dismissed. It goes away when the mouse moves off the item it is about -- for a Targets tree tip, when the selection changes."));
        }

        public override FormInfo GetFormInfo()
        {
            var formInfo = new FormInfo
            {
                Type = _tip.GetType().Name,
                Title = Title,
                HasGraph = false,
                DockState = GetDockState(),
                Id = FormId,
            };
            // The tip's content belongs to the UI thread that built it (see StandaloneForm.GetFormInfo).
            if (false == Program.MainWindow?.InvokeRequired)
                formInfo.DetailedMessage = JsonUiService.TruncateDetail(DetailedMessage);
            return formInfo;
        }
    }
}
