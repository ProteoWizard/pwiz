/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.6) <noreply .at. anthropic.com>
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
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.GUI;
using pwiz.Common.SystemUtil;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Controls;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
using SkylineTool;
using ZedGraph;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Static service for Skyline UI interactions invoked from the JSON pipe server.
    /// Provides UI thread marshaling primitives, reusable UI patterns (e.g., ImmediateWindow
    /// output tee), and complete UI operations (graph enumeration, selection, etc.).
    /// </summary>
    public static class JsonUiService
    {
        private const string EXT_PNG = @".png";
        private const string GRAPH_FILE_PREFIX = @"skyline-graph";

        // Level 1: Primitives - UI thread marshaling

        /// <summary>
        /// Executes an action on the UI thread.
        /// Exceptions propagate to the caller via wrapping to preserve the original stack trace.
        /// </summary>
        public static void InvokeOnUiThread(Action action)
        {
            Exception caught = null;
            Program.MainWindow.Invoke(new Action(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    caught = ex;
                }
            }));
            if (caught is ArgumentException argEx)
                throw new ArgumentException(argEx.Message, argEx.ParamName, argEx);
            if (caught != null)
                ExceptionUtil.WrapAndThrowException(caught);
        }

        /// <summary>
        /// Executes a function on the UI thread and returns the result.
        /// Must be called from a background thread (pipe server thread).
        /// Exceptions propagate to the caller.
        /// </summary>
        public static T InvokeOnUiThread<T>(Func<T> func)
        {
            Assume.IsTrue(Program.MainWindow.InvokeRequired);
            T result = default(T);
            Program.MainWindow.Invoke(new Action(() =>
            {
                result = func();
            }));
            return result;
        }

        private const int DIALOG_POLL_INTERVAL_MILLIS = 100;

        /// <summary>
        /// Runs <paramref name="work"/> on a background thread and waits for it, but returns
        /// immediately if a dialog appears, so the call never blocks on a modal:
        ///   - a new <see cref="CommonAlertDlg"/> (error / confirmation) -> reads its text and
        ///     throws, surfacing that text to the caller;
        ///   - a new native dialog (Open/Save common dialog, window class #32770) -> returns, leaving
        ///     it open for the caller to drive with the form verbs (GetOpenForms / SetFormValue /
        ///     ClickFormButton).
        /// The dialog is left open in both cases. Progress dialogs (LongWaitDlg) are not
        /// CommonAlertDlg, so normal command progress does not trip this. Used by verbs that can pop
        /// a dialog (RunCommand, SetFormValue, ClickFormButton, ...). Must be called off the UI thread.
        /// </summary>
        public static T RunWithDialogWatch<T>(Func<T> work)
        {
            var preexistingAlerts = InvokeOnUiThread(() =>
                new HashSet<Form>(FormUtil.OpenForms.OfType<CommonAlertDlg>()));
            var preexistingNative = new HashSet<IntPtr>(
                NativeDialogAutomation.GetOpenDialogs().Select(dialog => dialog.WindowHandle));

            T result = default(T);
            Exception workError = null;
            // Capture the worker's exception here rather than letting it escape, so the RunAsync
            // reporter does not surface its own error dialog.
            var worker = ActionUtil.RunAsync(() =>
            {
                try { result = work(); }
                catch (Exception ex) { workError = ex; }
            }, @"JsonTool command");

            while (!worker.Join(DIALOG_POLL_INTERVAL_MILLIS))
            {
                string alertText = FindNewAlertText(preexistingAlerts);
                if (alertText != null)
                    throw new InvalidOperationException(alertText);
                if (NativeDialogAutomation.GetOpenDialogs().Any(d => !preexistingNative.Contains(d.WindowHandle)))
                    return result; // a native modal (e.g. Save/Open) is up; return rather than block on it
            }

            if (workError != null)
            {
                // Preserve ArgumentException (maps to an invalid-params error) like InvokeOnUiThread.
                if (workError is ArgumentException argEx)
                    throw new ArgumentException(argEx.Message, argEx.ParamName, argEx);
                ExceptionUtil.WrapAndThrowException(workError);
            }
            return result;
        }

        public static void RunWithDialogWatch(Action work)
        {
            RunWithDialogWatch(() => { work(); return true; });
        }

        // Returns the message of a CommonAlertDlg that has appeared since the watch started, or null.
        // Enumerated on the UI thread; while an alert is up the UI thread runs its modal message loop,
        // which pumps this marshaled call.
        private static string FindNewAlertText(HashSet<Form> preexistingAlerts)
        {
            return InvokeOnUiThread(() =>
            {
                var alert = FormUtil.OpenForms.OfType<CommonAlertDlg>()
                    .FirstOrDefault(form => form.Visible && !preexistingAlerts.Contains(form));
                if (alert == null)
                    return null;
                return string.IsNullOrEmpty(alert.DetailMessage)
                    ? alert.Message
                    : alert.Message + Environment.NewLine + alert.DetailMessage;
            });
        }

        // Level 2: UI patterns

        /// <summary>
        /// Creates a TextWriter that tees output to both the given capture writer
        /// and Skyline's Immediate Window. Shows the Immediate Window and writes
        /// the header before returning.
        /// </summary>
        public static TextWriter CreateImmediateWindowTee(TextWriter capture, string header)
        {
            TextWriter immediateWriter = null;
            Program.MainWindow.Invoke(new Action(() =>
            {
                Program.MainWindow.ShowImmediateWindow();
                Program.MainWindow.ImmediateWindow.WriteFresh(header);
                Program.MainWindow.ImmediateWindow.WriteLine(string.Empty);
                immediateWriter = Program.MainWindow.ImmediateWindow.Writer;
            }));
            return new TeeTextWriter(capture, immediateWriter);
        }

        // Level 3: Complete UI operations - Selection

        /// <summary>
        /// Special locator string representing the insertion point at the end of the document tree.
        /// Used by GetSelection/SetSelection to round-trip the insertion node selection.
        /// </summary>
        public const string INSERT_NODE_LOCATOR = @"/Insert";

        public static SelectionInfo GetSelection()
        {
            return InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var document = skylineWindow.DocumentUI;
                var sequenceTree = skylineWindow.SequenceTree;
                var selectedPaths = sequenceTree.SelectedPaths;
                if (selectedPaths.Count == 0)
                    return new SelectionInfo { Locators = Array.Empty<string>() };

                var elementRefs = new ElementRefs(document);
                var locators = new List<string>();
                foreach (var path in selectedPaths)
                {
                    if (path.IsRoot)
                        continue;
                    if (sequenceTree.IsInsertPath(path))
                    {
                        locators.Add(INSERT_NODE_LOCATOR);
                        continue;
                    }
                    var nodeRef = elementRefs.GetNodeRef(path);
                    if (nodeRef == null)
                        continue;
                    locators.Add(nodeRef.ToString());
                }
                return new SelectionInfo { Locators = locators.ToArray() };
            });
        }

        public static void SetSelection(string elementLocatorString, string additionalLocators)
        {
            InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;

                // Primary selection
                if (elementLocatorString == INSERT_NODE_LOCATOR)
                {
                    skylineWindow.SequenceTree.SelectedPath =
                        new IdentityPath(SequenceTree.NODE_INSERT_ID);
                }
                else
                {
                    // Full navigation (bookmark, replicate, scroll)
                    skylineWindow.SelectElement(
                        ElementRefs.FromObjectReference(ElementLocator.Parse(elementLocatorString)));
                }

                // Secondary selections
                if (!string.IsNullOrEmpty(additionalLocators))
                {
                    var document = skylineWindow.DocumentUI;
                    var allPaths = new List<IdentityPath> { skylineWindow.SequenceTree.SelectedPath };
                    foreach (var line in additionalLocators.Split('\n'))
                    {
                        var trimmed = line.Trim();
                        if (string.IsNullOrEmpty(trimmed))
                            continue;
                        if (trimmed == INSERT_NODE_LOCATOR)
                        {
                            allPaths.Add(new IdentityPath(SequenceTree.NODE_INSERT_ID));
                            continue;
                        }
                        var elementRef = ElementRefs.FromObjectReference(ElementLocator.Parse(trimmed));
                        if (elementRef is NodeRef nodeRef)
                        {
                            var path = nodeRef.ToIdentityPath(document);
                            if (path != null)
                                allPaths.Add(path);
                        }
                    }
                    skylineWindow.SequenceTree.SelectedPaths = allPaths;
                }
            });
        }

        public static void SetReplicate(string replicateName)
        {
            InvokeOnUiThread(() =>
            {
                var document = Program.MainWindow.DocumentUI;
                if (!document.Settings.HasResults)
                    throw new InvalidOperationException(@"Document has no results");
                var chromatograms = document.Settings.MeasuredResults.Chromatograms;
                int index = chromatograms.IndexOf(c => c.Name == replicateName);
                if (index < 0)
                    throw new ArgumentException(@"Replicate not found: " + replicateName);
                Program.MainWindow.SelectedResultsIndex = index;
            });
        }

        // Level 3: Complete UI operations - Generic form interaction

        /// <summary>
        /// Invokes a main-menu item by its visible path (see <see cref="IJsonToolService"/>). The
        /// item is located on the UI thread (throwing if absent), then its click is posted with
        /// BeginInvoke so a menu item that opens a modal dialog does not block the caller.
        /// </summary>
        public static void InvokeMenuItem(string menuPath)
        {
            var item = InvokeOnUiThread(() => FindMenuItem(menuPath));
            Program.MainWindow.BeginInvoke((Action)item.PerformClick);
        }

        /// <summary>
        /// Clicks a button on an open form, or accepts/cancels a native dialog
        /// (see <see cref="IJsonToolService"/>).
        /// </summary>
        public static void ClickFormButton(string formId, string button)
        {
            ValidateFormIdFormat(formId);
            if (TryGetNativeDialog(formId, out var dialog))
            {
                if (IsCancelAction(button))
                    dialog.Cancel();
                else
                    dialog.Accept();
                return;
            }
            var handle = InvokeOnUiThread(() => FindButton(formId, button).Handle);
            // Click synchronously (BM_CLICK) inside the dialog-watch so a dialog the click pops is
            // observed: a resulting alert is surfaced (throws its text) and a native dialog (e.g.
            // Save/Open) returns immediately rather than blocking on its modal. BM_CLICK clicks like a
            // mouse, bypassing PerformClick's CanSelect / validation gates (which can silently no-op).
            RunWithDialogWatch(() =>
            {
                User32.SendMessage(handle, User32.WinMessageType.BM_CLICK, IntPtr.Zero, IntPtr.Zero);
                return true;
            });
        }

        /// <summary>
        /// Sets a control's value on an open form, or the file name(s) on a native file dialog
        /// (see <see cref="IJsonToolService"/>).
        /// </summary>
        public static void SetFormValue(string formId, string controlId, string value)
        {
            // Watch for a dialog so a setter that triggers a validation/confirmation alert fails fast
            // with its text, or that opens a native dialog returns immediately, instead of blocking.
            RunWithDialogWatch(() =>
            {
                ValidateFormIdFormat(formId);
                if (TryGetNativeDialog(formId, out var dialog))
                {
                    if (!(dialog is OpenFileDialogAutomation fileDialog))
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Setting values is not supported for native dialog {0}.", formId));
                    fileDialog.EnterPath(value);
                    return;
                }
                InvokeOnUiThread(() =>
                {
                    var control = FindControl<Control>(FindFormById(formId), controlId);
                    if (control == null)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"Control not found on form {0}: {1}.", formId, controlId));
                    SetControlValue(control, value);
                });
            });
        }

        // Level 3: Complete UI operations - Graphs

        public static FormInfo[] GetOpenForms()
        {
            var results = InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var formInfos = new List<FormInfo>();
                var dockedForms = new HashSet<Form>();
                foreach (var form in skylineWindow.DockPanel.Contents.OfType<DockableFormEx>())
                {
                    dockedForms.Add(form);
                    var dockState = form.DockState;
                    if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                        continue;
                    var zedGraph = TryGetZedGraphControl(form);
                    formInfos.Add(new FormInfo
                    {
                        Type = form.GetType().Name,
                        Title = GetFormTitle(form),
                        HasGraph = zedGraph != null,
                        DockState = dockState.ToString(),
                        Id = GetFormId(form),
                    });
                }

                // Enumerate non-docked forms (dialogs, popups)
                foreach (var form in FormUtil.OpenForms)
                {
                    if (form == skylineWindow || dockedForms.Contains(form))
                        continue;
                    if (!form.Visible)
                        continue;
                    formInfos.Add(new FormInfo
                    {
                        Type = form.GetType().Name,
                        Title = GetFormTitle(form),
                        HasGraph = false,
                        DockState = @"Dialog",
                        Id = GetFormId(form),
                    });
                }
                return formInfos;
            });

            // Native common dialogs (e.g. the Open/Save file dialog) are not WinForms forms and
            // so never appear in FormUtil.OpenForms. Enumerate them via UI Automation. This runs
            // on the pipe thread, NOT inside InvokeOnUiThread: when such a dialog is modal the UI
            // thread is busy in the dialog's own message loop, and querying it from that thread
            // can deadlock.
            foreach (var dialog in NativeDialogAutomation.GetOpenDialogs())
            {
                results.Add(new FormInfo
                {
                    Type = dialog.DialogTypeName,
                    Title = dialog.Title,
                    HasGraph = false,
                    DockState = @"Dialog",
                    Id = GetNativeDialogId(dialog),
                    IsNative = true,
                });
            }
            return results.ToArray();
        }

        private static string GetNativeDialogId(NativeDialogAutomation dialog)
        {
            return dialog.DialogTypeName + @":" + dialog.Title;
        }

        private static bool TryGetNativeDialog(string formId, out NativeDialogAutomation dialog)
        {
            foreach (var openDialog in NativeDialogAutomation.GetOpenDialogs())
            {
                if (GetNativeDialogId(openDialog) == formId)
                {
                    dialog = openDialog;
                    return true;
                }
            }
            dialog = null;
            return false;
        }

        public static string GetGraphData(string graphId, string filePath)
        {
            return InvokeOnUiThread(() =>
            {
                var form = FindFormById(graphId) as DockableFormEx;
                var zedGraph = form != null ? TryGetZedGraphControl(form) : null;
                if (zedGraph == null)
                {
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Not a graph form: {0}. Use skyline_get_open_forms to find forms with HasGraph=True.",
                        graphId));
                }
                var graphData = CopyGraphDataToolStripMenuItem.GetGraphData(zedGraph.MasterPane);
                if (graphData.Panes.Count == 0)
                    return string.Empty;
                filePath = filePath ?? GetMcpTmpFilePath(
                    GRAPH_FILE_PREFIX, form.Text, TextUtil.EXT_TSV);
                DirectoryEx.CreateForFilePath(filePath);
                using (var saver = new FileSaver(filePath))
                {
                    File.WriteAllText(saver.SafeName, graphData.ToString());
                    saver.Commit();
                }
                return filePath.ToForwardSlashPath();
            });
        }

        public static string GetGraphImage(string graphId, string filePath)
        {
            string denial = CheckImageToolPreflight(graphId,
                () => EnsureGraphForm(graphId, out _, out _),
                requiresScreenCapture: false);
            if (denial != null)
                return denial;
            return InvokeOnUiThread(() =>
            {
                using (var bitmap = RenderGraphBitmap(graphId, out var form))
                {
                    filePath = filePath ?? GetMcpTmpFilePath(
                        GRAPH_FILE_PREFIX, form.Text, EXT_PNG);
                    DirectoryEx.CreateForFilePath(filePath);
                    using (var saver = new FileSaver(filePath))
                    {
                        bitmap.Save(saver.SafeName, ImageFormat.Png);
                        saver.Commit();
                    }
                }
                return filePath.ToForwardSlashPath();
            });
        }

        public static ImageBytesMetadata GetGraphImageBytes(string graphId)
        {
            string denial = CheckImageToolPreflight(graphId,
                () => EnsureGraphForm(graphId, out _, out _),
                requiresScreenCapture: false);
            if (denial != null)
                return new ImageBytesMetadata { Message = denial };
            return InvokeOnUiThread(() =>
            {
                using (var bitmap = RenderGraphBitmap(graphId, out var form))
                {
                    return new ImageBytesMetadata
                    {
                        Data = BitmapToPngBytes(bitmap),
                        FilePath = GetMcpTmpFilePath(GRAPH_FILE_PREFIX, form.Text, EXT_PNG)
                            .ToForwardSlashPath(),
                        MimeType = MIME_TYPE_PNG
                    };
                }
            });
        }

        private const string FORM_FILE_PREFIX = @"skyline-form";

        // LLM-facing instruction text for the form-image permission states.
        // Wrapped in LlmInstruction so the type makes the not-translated
        // contract explicit, and exposed as fields so tests can assert against
        // the canonical value instead of a brittle English substring
        // (see CRITICAL-RULES.md on translation-proof tests).
        public static readonly LlmInstruction LLM_MSG_SCREEN_CAPTURE_DENIED =
            new LlmInstruction(@"Screen capture denied by user.");
        public static readonly LlmInstruction LLM_MSG_SCREEN_CAPTURE_PERMISSION_REQUIRED =
            new LlmInstruction(@"Screen capture permission required. A confirmation dialog is now open in Skyline; ask the user to grant or deny it, then call this tool again. This is the documented two-phase handshake, not an error.");
        public static readonly LlmInstruction LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE =
            new LlmInstruction(@"Screen capture is not available. The desktop session may be disconnected (e.g. Docker container, disconnected Remote Desktop, or locked workstation). Reconnect the desktop session and try again.");

        public static string GetFormImage(string formId, string filePath)
        {
            ValidateFormIdFormat(formId);
            if (TryGetNativeDialog(formId, out var nativeDialog))
                return GetNativeDialogImage(nativeDialog, filePath);
            string denial = CheckImageToolPreflight(formId,
                () => FindFormById(formId),
                requiresScreenCapture: true);
            if (denial != null)
                return denial;
            return InvokeOnUiThread(() =>
            {
                // Re-resolve the form on the UI thread so a close during the
                // pipe-thread work in CheckScreenCaptureAvailability surfaces
                // as the same "form not found" ArgumentException as a bad
                // formId, not as ObjectDisposedException during capture.
                var form = FindFormById(formId);
                using (var bitmap = CaptureGrantedForm(form))
                {
                    filePath = filePath ?? GetMcpTmpFilePath(
                        FORM_FILE_PREFIX, GetFormTitle(form), EXT_PNG);
                    DirectoryEx.CreateForFilePath(filePath);
                    bitmap.Save(filePath, ImageFormat.Png);
                }
                return filePath.ToForwardSlashPath();
            });
        }

        public static ImageBytesMetadata GetFormImageBytes(string formId)
        {
            ValidateFormIdFormat(formId);
            if (TryGetNativeDialog(formId, out var nativeDialog))
                return GetNativeDialogImageBytes(nativeDialog);
            string denial = CheckImageToolPreflight(formId,
                () => FindFormById(formId),
                requiresScreenCapture: true);
            if (denial != null)
            {
                // Permission denial / desktop unavailable: return a structured
                // Message instead of bytes. The wrapper emits Message as plain
                // text content (no error flag) so the response shape matches
                // what the legacy file-based path returned for the same condition.
                return new ImageBytesMetadata { Message = denial };
            }
            return InvokeOnUiThread(() =>
            {
                var form = FindFormById(formId);
                using (var bitmap = CaptureGrantedForm(form))
                {
                    return new ImageBytesMetadata
                    {
                        Data = BitmapToPngBytes(bitmap),
                        FilePath = GetMcpTmpFilePath(FORM_FILE_PREFIX, GetFormTitle(form), EXT_PNG)
                            .ToForwardSlashPath(),
                        MimeType = MIME_TYPE_PNG
                    };
                }
            });
        }

        // Captures a native dialog (e.g. the Open/Save file dialog) to a PNG file. Unlike the
        // WinForms path this runs entirely on the calling (pipe) thread: the capture is a screen
        // copy by window handle and must not marshal to the UI thread, which may be blocked in a
        // modal dialog's message loop.
        private static string GetNativeDialogImage(NativeDialogAutomation dialog, string filePath)
        {
            if (Program.MainWindow == null)
                return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
                return denial;
            using (var bitmap = CaptureNativeWindow(dialog.WindowHandle))
            {
                filePath = filePath ?? GetMcpTmpFilePath(FORM_FILE_PREFIX, dialog.Title, EXT_PNG);
                DirectoryEx.CreateForFilePath(filePath);
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath.ToForwardSlashPath();
        }

        private static ImageBytesMetadata GetNativeDialogImageBytes(NativeDialogAutomation dialog)
        {
            if (Program.MainWindow == null)
                return new ImageBytesMetadata { Message = LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE };
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
                return new ImageBytesMetadata { Message = denial };
            using (var bitmap = CaptureNativeWindow(dialog.WindowHandle))
            {
                return new ImageBytesMetadata
                {
                    Data = BitmapToPngBytes(bitmap),
                    FilePath = GetMcpTmpFilePath(FORM_FILE_PREFIX, dialog.Title, EXT_PNG)
                        .ToForwardSlashPath(),
                    MimeType = MIME_TYPE_PNG
                };
            }
        }

        // Captures a screenshot of a native window by its handle. GetWindowRect returns logical
        // coordinates, scaled to physical pixels to match the screen copy (the same convention as
        // ScreenCapture.GetForeignWindowRects).
        private static System.Drawing.Bitmap CaptureNativeWindow(IntPtr windowHandle)
        {
            User32.SetForegroundWindow(windowHandle);
            var rect = new User32.RECT();
            User32.GetWindowRect(windowHandle, ref rect);
            var screenRect = rect.Rectangle * ScreenCapture.GetScalingFactor();
            return ScreenCapture.CaptureScreen(screenRect);
        }

        // Validates that the given id identifies a form bearing a ZedGraph
        // control. Throws ArgumentException if the form is missing or is not
        // a graph form. Used both as the existence-check step in
        // CheckImageToolPreflight and as the first step of actual rendering.
        private static void EnsureGraphForm(string graphId, out DockableFormEx form, out ZedGraphControl graph)
        {
            form = FindFormById(graphId) as DockableFormEx;
            graph = form != null ? TryGetZedGraphControl(form) : null;
            if (graph == null)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Not a graph form: {0}. Use skyline_get_open_forms to find forms with HasGraph=True.",
                    graphId));
            }
        }

        // Renders the bitmap for a ZedGraph form, returning the bitmap and the host form.
        // Caller owns the bitmap and must dispose it.
        private static System.Drawing.Bitmap RenderGraphBitmap(string graphId, out DockableFormEx form)
        {
            EnsureGraphForm(graphId, out form, out var graph);
            return graph.MasterPane.GetImage(graph.MasterPane.IsAntiAlias);
        }

        // Shared pre-flight for the image-capture tools (form and graph variants).
        // Runs format validation on the pipe thread, guards against a null
        // MainWindow, and runs the type-specific existence check on the UI thread
        // -- in that order so that bad input throws ArgumentException regardless
        // of environment. Optionally runs the screen-capture availability check
        // (form variants only). Returns null when the caller may proceed, or an
        // LLM-facing message the caller must surface. Throws ArgumentException
        // for bad input (id format wrong, referenced form not found, wrong form
        // type) -- those are caller-contract violations and must reach the
        // caller regardless of environment.
        //
        // Note: when MainWindow is null we use LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE
        // for all variants, including graphs. The wording is slightly off for
        // graphs (which do not capture from the screen), but the user-facing
        // intent ("try again momentarily") is correct.
        private static string CheckImageToolPreflight(string id, Action ensureExistsOnUi, bool requiresScreenCapture)
        {
            ValidateFormIdFormat(id);
            if (Program.MainWindow == null)
                return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            // Passing an Action binds InvokeOnUiThread to the void overload,
            // which preserves ArgumentException across the thread boundary.
            InvokeOnUiThread(ensureExistsOnUi);
            return requiresScreenCapture ? CheckScreenCaptureAvailability() : null;
        }

        // Returns null when screen capture can proceed, or the LLM-facing
        // denial / pending / desktop-unavailable message that the form-image
        // tools should return to the caller without attempting capture.
        // Called from the pipe thread (no Invoke marshal) so a Pending or
        // Denied response does not pay the UI-thread round trip.
        private static string CheckScreenCaptureAvailability()
        {
            switch (ScreenCapture.EnsurePermission())
            {
                case PermissionResult.denied:
                    return LLM_MSG_SCREEN_CAPTURE_DENIED;
                case PermissionResult.pending:
                    return LLM_MSG_SCREEN_CAPTURE_PERMISSION_REQUIRED;
                case PermissionResult.unavailable:
                    return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            }
            if (!ScreenCapture.IsDesktopAvailable())
            {
                return LLM_MSG_SCREEN_CAPTURE_UNAVAILABLE;
            }
            return null;
        }

        // Cheap formId well-formedness check that runs on the pipe thread,
        // before the screen-capture permission prompt fires. Catching obviously
        // bad input here avoids interrupting the user with a permission dialog
        // for a request that can never succeed.
        private static void ValidateFormIdFormat(string formId)
        {
            if (formId == null || formId.IndexOf(':') < 0)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Invalid form ID format: {0}. Expected 'TypeName:Title'. Use skyline_get_open_forms to get valid IDs.",
                    formId ?? string.Empty));
            }
        }

        // Captures a screenshot of an open form (with redaction). Caller owns
        // the bitmap. Must be called on the UI thread, and only after
        // CheckScreenCaptureAvailability has returned null.
        private static System.Drawing.Bitmap CaptureGrantedForm(Form form)
        {
            ScreenCapture.ActivateForm(form);
            var screenRect = ScreenCapture.GetWindowRectangle(form);
            return ScreenCapture.CaptureAndRedact(screenRect, form);
        }

        private const string MIME_TYPE_PNG = @"image/png";

        private static byte[] BitmapToPngBytes(System.Drawing.Bitmap bitmap)
        {
            using (var memory = new MemoryStream())
            {
                bitmap.Save(memory, ImageFormat.Png);
                return memory.ToArray();
            }
        }

        // Private helpers - Graph support

        /// <summary>
        /// Returns the display title for a form, used both in GetOpenForms output
        /// and in FindFormById matching.
        /// </summary>
        private static string GetFormTitle(Form form)
        {
            if (form is DockableFormEx dockable)
                return !string.IsNullOrEmpty(dockable.Text) ? dockable.Text
                    : !string.IsNullOrEmpty(dockable.TabText) ? dockable.TabText
                    : dockable.GetType().Name;
            return !string.IsNullOrEmpty(form.Text) ? form.Text : form.GetType().Name;
        }

        /// <summary>
        /// Builds a stable form identifier from type name and title.
        /// </summary>
        private static string GetFormId(Form form)
        {
            return form.GetType().Name + @":" + GetFormTitle(form);
        }

        private static ZedGraphControl TryGetZedGraphControl(DockableFormEx form)
        {
            // Use reflection to find any public property that returns ZedGraphControl,
            // so that new graph forms are automatically supported.
            foreach (var prop in form.GetType().GetProperties())
            {
                if (typeof(ZedGraphControl).IsAssignableFrom(prop.PropertyType) && prop.GetIndexParameters().Length == 0)
                    return prop.GetValue(form) as ZedGraphControl;
            }
            return null;
        }

        /// <summary>
        /// Finds a form by its TypeName:Title identifier from GetOpenForms.
        /// Searches docked forms first, then non-docked forms (dialogs).
        /// </summary>
        private static Form FindFormById(string formId)
        {
            ValidateFormIdFormat(formId);
            int colonIndex = formId.IndexOf(':');
            string typeName = formId.Substring(0, colonIndex);
            string title = formId.Substring(colonIndex + 1);

            var skylineWindow = Program.MainWindow;

            // Search docked forms
            foreach (var form in skylineWindow.DockPanel.Contents.OfType<DockableFormEx>())
            {
                var dockState = form.DockState;
                if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                    continue;
                if (form.GetType().Name == typeName && GetFormTitle(form) == title)
                    return form;
            }

            // Search non-docked forms (dialogs)
            var dockedForms = new HashSet<Form>(
                skylineWindow.DockPanel.Contents.OfType<DockableFormEx>());
            foreach (var form in FormUtil.OpenForms)
            {
                if (form == skylineWindow || dockedForms.Contains(form))
                    continue;
                if (!form.Visible)
                    continue;
                if (form.GetType().Name == typeName && GetFormTitle(form) == title)
                    return form;
            }

            throw new ArgumentException(LlmInstruction.Format(
                @"Form not found: {0}. Use skyline_get_open_forms to see available forms.",
                formId));
        }

        // --- Generic form interaction helpers ---

        // Walks the main menu by path (segments split on '>', '|', '/'), matching each segment by
        // normalized text or by control name. Must run on the UI thread. Throws if no item matches.
        private static ToolStripMenuItem FindMenuItem(string menuPath)
        {
            var segments = (menuPath ?? string.Empty)
                .Split(new[] { '>', '|', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim()).Where(s => s.Length > 0).ToArray();
            if (segments.Length == 0)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Empty menu path: {0}. Expected e.g. 'File > Import > Peptide Search'.",
                    menuPath ?? string.Empty));
            var items = Program.MainWindow.MainMenuStrip.Items;
            ToolStripMenuItem current = null;
            foreach (var segment in segments)
            {
                current = items.OfType<ToolStripMenuItem>().FirstOrDefault(i => MenuItemMatches(i, segment));
                if (current == null)
                    throw new ArgumentException(LlmInstruction.Format(
                        @"Menu item not found: {0} (no match for '{1}').", menuPath, segment));
                items = current.DropDownItems;
            }
            return current;
        }

        private static bool MenuItemMatches(ToolStripMenuItem item, string label)
        {
            return string.Equals(NormalizeLabel(item.Text), NormalizeLabel(label), StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(item.Name, label, StringComparison.OrdinalIgnoreCase);
        }

        // Finds a button by control name or visible text anywhere in the form. Must run on the UI
        // thread. Throws if none matches.
        private static Button FindButton(string formId, string button)
        {
            var found = FindControl<Button>(FindFormById(formId), button);
            if (found == null)
                throw new ArgumentException(LlmInstruction.Format(
                    @"Button not found on form {0}: {1}.", formId, button));
            return found;
        }

        // Depth-first search for a control of type T matching by control name or visible text.
        private static T FindControl<T>(Control parent, string key) where T : Control
        {
            foreach (Control child in parent.Controls)
            {
                if (child is T typed && ControlMatches(child, key))
                    return typed;
                var nested = FindControl<T>(child, key);
                if (nested != null)
                    return nested;
            }
            return null;
        }

        private static bool ControlMatches(Control control, string key)
        {
            return string.Equals(control.Name, key, StringComparison.OrdinalIgnoreCase)
                || string.Equals(NormalizeLabel(control.Text), NormalizeLabel(key), StringComparison.CurrentCultureIgnoreCase);
        }

        private static void SetControlValue(Control control, string value)
        {
            switch (control)
            {
                case CheckBox checkBox:
                    checkBox.Checked = bool.TryParse(value, out var parsed) ? parsed : value == @"1";
                    break;
                case ComboBox comboBox:
                    int index = comboBox.FindStringExact(value);
                    if (index < 0)
                        throw new ArgumentException(LlmInstruction.Format(
                            @"No item '{0}' in combo box {1}.", value, control.Name));
                    comboBox.SelectedIndex = index;
                    break;
                case TextBoxBase textBox:
                    textBox.Text = value;
                    break;
                default:
                    control.Text = value;
                    break;
            }
        }

        // True when the requested button names the cancel/close action of a native dialog. Locale
        // sensitive by nature; callers that key on the visible label inherit that.
        private static bool IsCancelAction(string button)
        {
            var normalized = NormalizeLabel(button);
            return string.Equals(normalized, @"Cancel", StringComparison.CurrentCultureIgnoreCase)
                || string.Equals(normalized, @"Close", StringComparison.OrdinalIgnoreCase);
        }

        // Strips the mnemonic '&' and a trailing ellipsis/period so menu and button captions compare
        // equal to the plain label a tutorial uses ("Peptide Search" == "&Peptide Search...").
        private static string NormalizeLabel(string text)
        {
            if (string.IsNullOrEmpty(text))
                return string.Empty;
            return text.Replace(@"&", string.Empty).Trim().TrimEnd('.', '…', ' ').Trim();
        }

        /// <summary>
        /// Returns the shared MCP temp directory, creating it if needed.
        /// Respects the SKYLINE_MCP_TMP_DIR environment variable.
        /// </summary>
        public static string GetMcpTmpDir()
        {
            string tmpDir = Environment.GetEnvironmentVariable(@"SKYLINE_MCP_TMP_DIR");
            if (string.IsNullOrEmpty(tmpDir))
            {
                tmpDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Skyline", @"mcp", @"tmp");
            }
            Directory.CreateDirectory(tmpDir);
            return tmpDir;
        }

        /// <summary>
        /// Generates a timestamped file path in the MCP temp directory.
        /// Format: {prefix}-{sanitized_title}-{yyyyMMdd-HHmmss}{extension}
        /// </summary>
        public static string GetMcpTmpFilePath(string prefix, string title, string extension)
        {
            string safe = Regex.Replace(title ?? string.Empty, @"[^\w\-. ]", @"_").Trim();
            if (safe.Length > 50) safe = safe.Substring(0, 50);
            string timestamp = DateTime.Now.ToString(@"yyyyMMdd-HHmmss");
            return Path.Combine(GetMcpTmpDir(),
                string.Format(@"{0}-{1}-{2}{3}", prefix, safe, timestamp, extension));
        }
    }
}
