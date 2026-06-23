/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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

using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Base for the native common file dialogs -- Open (<see cref="OpenFileDialogAutomation"/>) and
    /// Save (<see cref="SaveFileDialogAutomation"/>). Both are "#32770" dialogs that take a file name
    /// and then accept or cancel, but they expose their file-name field differently, so the
    /// path-entry gesture is abstract. They share the single "FileDialog" type name the MCP layer
    /// uses to route SetFormValue / ClickFormButton to either dialog uniformly.
    /// </summary>
    public abstract class FileDialogAutomation : NativeDialogAutomation
    {
        protected FileDialogAutomation(IntPtr windowHandle) : base(windowHandle)
        {
        }

        public override string DialogTypeName => @"FileDialog";

        // A file dialog presents three connector elements: an OK and a Cancel button (which accept/cancel
        // it) and a file-name field (set_value enters the path). They dispatch to this automation.
        public override IEnumerable<UiElement> Children => new UiElement[]
        {
            new NativeDialogButtonElement(this, true, @"OK"),
            new NativeDialogButtonElement(this, false, @"Cancel"),
            new NativeFileNameElement(this),
        };

        /// <summary>
        /// Types the file name(s) into the dialog's file name field without accepting; call
        /// <see cref="NativeDialogAutomation.Accept"/> to open/save. The Open dialog accepts several
        /// space-quoted paths for a multiselect (see <see cref="OpenFileDialogAutomation.QuotePaths"/>);
        /// the Save dialog takes a single path.
        /// </summary>
        public abstract void EnterPath(string path);
    }

    /// <summary>The OK or Cancel button of a native dialog. A click dispatches to the dialog's Accept or
    /// Cancel (the gesture differs by dialog type), so the connector drives it like any form button.</summary>
    internal sealed class NativeDialogButtonElement : UiElement
    {
        private readonly NativeDialogAutomation _dialog;
        private readonly bool _accept;
        private readonly string _label;

        public NativeDialogButtonElement(NativeDialogAutomation dialog, bool accept, string label)
        {
            _dialog = dialog;
            _accept = accept;
            _label = label;
        }

        public override string Name => string.Empty;
        public override Type ElementType => typeof(Button);
        public override string Label => _label;
        public override bool IsEnabled => _dialog.IsEnabled;
        public override bool IsVisible => true;
        public override bool IsControl => true;
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.Click || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action != UiAction.Click)
                return base.PerformAction(action, value, cancellationToken);
            if (_accept)
                _dialog.Accept();
            else
                _dialog.Cancel();
            return null;
        }
    }

    /// <summary>The file-name field of a native file dialog. set_value types the path(s) into it (without
    /// accepting -- click OK to commit), dispatching to the dialog's EnterPath.</summary>
    internal sealed class NativeFileNameElement : UiElement
    {
        private readonly FileDialogAutomation _dialog;
        public NativeFileNameElement(FileDialogAutomation dialog) { _dialog = dialog; }

        public override string Name => string.Empty;
        public override Type ElementType => typeof(TextBox);
        public override string Label => @"File name";
        public override bool IsEnabled => _dialog.IsEnabled;
        public override bool IsVisible => true;
        public override bool IsControl => true;
        public override bool SupportsAction(UiAction action) =>
            action == UiAction.SetValue || base.SupportsAction(action);
        public override object PerformAction(UiAction action, object value, CancellationToken cancellationToken)
        {
            if (action != UiAction.SetValue)
                return base.PerformAction(action, value, cancellationToken);
            _dialog.EnterPath(value as string);
            return null;
        }
    }
}
