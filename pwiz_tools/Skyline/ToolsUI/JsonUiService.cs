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
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
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

        // How much of a window's message GetOpenForms reports. Enough to see WHAT is in the way and why; a form list
        // is a summary, and the whole text is there for the asking (get_form_image, or the message an action throws).
        private const int MAX_DETAIL_CHARS = 200;

        /// <summary>What a window says, cut down to fit in a form listing (see <see cref="FormInfo.DetailedMessage"/>).
        /// Newlines collapse to spaces so one form stays one line.</summary>
        internal static string TruncateDetail(string message)
        {
            if (string.IsNullOrEmpty(message))
                return null;
            var oneLine = Regex.Replace(message, @"\s+", @" ").Trim();
            return oneLine.Length <= MAX_DETAIL_CHARS
                ? oneLine
                : oneLine.Substring(0, MAX_DETAIL_CHARS) + @"...";
        }

        // Level 1: Primitives - UI thread marshaling

        /// <summary>
        /// Executes an action on THE UI THREAD -- the main window's, or the StartPage's before it exists. Exceptions
        /// are re-thrown wrapped, so the original stack survives the hop.
        ///
        /// <para>It targets the one UI thread and nothing else, and it is not the way to drive a FORM: a verb that
        /// knows which window it is working with goes through a form-scoped helper (JsonToolServer's InvokeOnForm /
        /// InvokeOnMainWindow, or UiElement's PerformAction / CallFunction), which puts the work on THAT window's
        /// thread and brings the dialog-watch and the request's cancellation with it. What is left for this is the
        /// handful of reads and sets that are about Skyline itself rather than any window -- the selection, the UI
        /// mode, the document.</para>
        /// </summary>
        public static void InvokeOnUiThread(Action action)
        {
            Exception caught = null;
            RequireMainOrStart().Invoke(new Action(() =>
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

        /// <summary>Executes a function on the UI thread and returns its result (see
        /// <see cref="InvokeOnUiThread(System.Action)"/>).</summary>
        public static T InvokeOnUiThread<T>(Func<T> func)
        {
            T result = default(T);
            InvokeOnUiThread(() => { result = func(); });
            return result;
        }

        // Level 2: UI patterns

        /// <summary>
        /// Creates a TextWriter that tees output to both the given capture writer
        /// and Skyline's Immediate Window. Shows the Immediate Window and writes
        /// the header before returning.
        /// </summary>
        /// <summary>Returns the main Skyline window, or throws an LLM-facing error if it does not exist yet
        /// (only the start page is showing). For verbs that genuinely need the document / main window
        /// itself, so they fail with a clear message instead of a NullReferenceException.</summary>
        public static SkylineWindow RequireMainWindow()
        {
            return Program.MainWindow ?? throw new InvalidOperationException(LlmInstruction.Format(
                @"This requires the main Skyline window, which is not available while the start page is showing."));
        }

        public static FormEx RequireMainOrStart()
        {
            return (FormEx) Program.MainWindow ?? Program.StartWindow ?? throw new InvalidOperationException(LlmInstruction.Format(
                @"Neither the main Skyline window nor the Start Page are showing"));
        }

        // ---- Document / selection reads shared by both tool servers -------------------------------------
        // These live here, not on ToolService, so that JsonToolServer does not need a ToolService to answer them:
        // the JSON server can be started on its own (see Program.StartToolService), before the main window even
        // exists, while the legacy ToolService is started only when an interactive tool runs.

        /// <summary>The path of the open document, or null when none is open (or the main window does not exist).</summary>
        public static string GetDocumentPath()
        {
            return Program.MainWindow?.DocumentFilePath;
        }

        /// <summary>The text of the selected node in the Targets tree -- what the selection "reads as".</summary>
        public static string GetSelectionText()
        {
            string name = null;
            InvokeOnUiThread(() => name = RequireMainWindow().SequenceTree.SelectedNode.Text);
            return name;
        }

        /// <summary>The name of the replicate the chromatogram graphs are showing.</summary>
        public static string GetReplicateName()
        {
            string name = null;
            InvokeOnUiThread(() => name = RequireMainWindow().SelectedGraphChromName);
            return name;
        }

        /// <summary>The element locator of the current selection at the given level ("replicate", "molecule", ...),
        /// or null when nothing is selected at that level.</summary>
        public static string GetSelectedElementLocator(string elementType)
        {
            ElementRef result = null;
            Exception exception = null;
            InvokeOnUiThread(() =>
            {
                try
                {
                    result = GetSelectedElementRefNow(elementType);
                }
                catch (Exception e)
                {
                    exception = e;
                }
            });
            if (exception != null)
            {
                throw new TargetInvocationException(exception);
            }
            return result?.ToString();
        }

        private static ElementRef GetSelectedElementRefNow(string elementType)
        {
            var mainWindow = RequireMainWindow();
            var document = mainWindow.DocumentUI;

            SrmDocument.Level nodeLevel;
            if (elementType == ReplicateRef.PROTOTYPE.ElementType)
            {
                if (!document.Settings.HasResults)
                {
                    return null;
                }

                return ReplicateRef.FromChromatogramSet(document.Settings.MeasuredResults
                    .Chromatograms[mainWindow.ComboResults.SelectedIndex]);
            }

            if (elementType == TransitionRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.Transitions;
            }
            else if (elementType == PrecursorRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.TransitionGroups;
            }
            else if (elementType == MoleculeRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.Molecules;
            }
            else if (elementType == MoleculeGroupRef.PROTOTYPE.ElementType)
            {
                nodeLevel = SrmDocument.Level.MoleculeGroups;
            }
            else
            {
                throw new ArgumentException(string.Format(
                    ToolsUIResources.ToolService_GetSelectedElementRefNow_Unsupported_element_type___0__, elementType));
            }

            var selectedPath = mainWindow.SelectedPath;
            if (selectedPath.Length <= (int) nodeLevel)
            {
                return null;
            }
            var elementRefs = new ElementRefs(document);
            return elementRefs.GetNodeRef(selectedPath.GetPathTo((int) nodeLevel));
        }

        public static TextWriter CreateImmediateWindowTee(TextWriter capture, string header)
        {
            RequireMainWindow();
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

        public static ActionResult InvokeOnMainWindow(Action<SkylineStandaloneForm> action, CancellationToken cancellationToken)
        {
            var mainWindow = RequireMainWindow();
            return DialogWatcher.PerformAction(mainWindow, () =>
            {
                var skylineStandaloneWindow =
                    new SkylineStandaloneForm(mainWindow, mainWindow.Handle, cancellationToken);
                action(skylineStandaloneWindow);
            }, cancellationToken);
        }

        public static ActionResult InvokeOnForm(string formId, Action<StandaloneWindow> action,
            CancellationToken cancellationToken)
        {
            var window = ResolveForm(formId, cancellationToken);
            return DialogWatcher.PerformAction(window.Hwnd, () =>
            {
                action(window);
            }, cancellationToken);
        }

        // Level 3: Complete UI operations - Generic form interaction

        // Populates a ContextMenuStrip the way right-clicking the graph would: it invokes the graph's
        // ContextMenuBuilder handlers (which add the Skyline-specific items) with a point at the
        // control's center, so the correct graph pane is chosen for a multi-pane graph. The event can
        // only be raised from inside ZedGraphControl, so its backing delegate is fetched by reflection.
        internal static void PopulateGraphContextMenu(ZedGraphControl zedGraph, ContextMenuStrip menuStrip)
        {
            var field = typeof(ZedGraphControl).GetField(@"ContextMenuBuilder",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var builder = field?.GetValue(zedGraph) as ZedGraphControl.ContextMenuBuilderEventHandler;
            if (builder == null)
                throw new ArgumentException(new LlmInstruction(@"This graph has no context menu."));
            var centerPoint = new System.Drawing.Point(zedGraph.Width / 2, zedGraph.Height / 2);
            // menuStrip is the control's reused ContextMenuStrip, so it may still hold items from a previous
            // populate or a prior real right-click; empty it first (as ZedGraph's own contextMenuStrip1_Opening
            // does) before the builder repopulates it.
            menuStrip.Items.Clear();
            builder(zedGraph, menuStrip, centerPoint, default(ZedGraphControl.ContextMenuObjectState));
        }

        // Verifies the resolved element supports the action (it is the kind the action targets); the
        // interactable gate is applied later by UiAction.Invoke. Returns the element; throws a clear error
        // (listing the element's actions) if the action does not apply.
        internal static UiElement RequireAction(UiElement element, UiAction action)
        {
            if (!action.AppliesTo(element))
                throw new ArgumentException(LlmInstruction.Format(
                    @"The control '{0}' does not support the action '{1}'. It supports: {2}.",
                    element.Label ?? element.Name, action.SnakeCaseName,
                    string.Join(@", ", element.SupportedActions.Select(a => a.SnakeCaseName))));
            return element;
        }

        // Resolves a UiElementPath to the single element it refers to, given the already-resolved
        // <paramref name="root"/> form (the FormElement or NativeDialog the path's root segment names -- the
        // caller resolves it from that segment's Text, e.g. with path.GetRoot()). Each non-root segment names
        // a child of the element its Parent resolves to, by Index (its position in the parent's child list),
        // Text (its visible label), and/or Type -- every property that is set must match.
        internal static UiElement ResolvePath(UiElementPath path, UiElement root)
        {
            if (path == null)
                throw new ArgumentException(new LlmInstruction(@"A path is required."));

            UiElement element;
            if (path.Parent == null)
            {
                // The root segment names a form (the caller resolved it), so its Type must be "Form" or unset.
                if (!string.IsNullOrEmpty(path.Type) && !string.Equals(path.Type, @"Form", StringComparison.OrdinalIgnoreCase))
                    throw new ArgumentException(LlmInstruction.Format(
                        @"The root of a path must be a form (Type 'Form' or unset), not '{0}'.", path.Type));
                element = root;
            }
            else
            {
                element = ResolvePath(path.Parent, root).GetChild(path);
            }
            // Record the resolved element's own path, so its get_children parents the controls it lists.
            element.Path = path;
            return element;
        }

        // Level 3: Complete UI operations - Graphs

        /// <summary>Every window a connector caller can address, as the FormInfo the get_open_forms verb reports --
        /// the same set <see cref="GetOpenFormElements"/> enumerates and <see cref="FindFormById"/> matches an id
        /// against, so a form that is listed here can always be resolved.
        ///
        /// <para>ONE trip to the main window describes them all. Nearly every form lives on the main window's thread,
        /// so from there each can report itself in full (see StandaloneWindow.GetFormInfo, which fills in only what
        /// the calling thread may read). Describing each form through ITS OWN window instead would post a message to
        /// a window that may be closing -- and a destroyed window drops the message, so the read would never
        /// return.</para></summary>
        public static FormInfo[] GetOpenForms(CancellationToken cancellationToken = default)
        {
            if (Program.MainWindow == null)
            {
                return DescribeForms(StandaloneWindow.GetTopLevelWindows(cancellationToken)).ToArray();
            }

            FormInfo[] openForms = null;
            InvokeOnMainWindow(_ =>
            {
                openForms = DescribeForms(StandaloneWindow.GetTopLevelWindows(cancellationToken).Concat(GetDockedForms(cancellationToken))).ToArray();
            }, cancellationToken);
            return openForms;
        }

        private static IEnumerable<FormInfo> DescribeForms(IEnumerable<StandaloneWindow> forms)
        {
            foreach (var form in forms)
            {
                FormInfo formInfo = null;
                try
                {
                    formInfo = form.GetFormInfo();
                }
                catch (Exception exception) when (!(exception is OperationCanceledException))
                {
                    // A form can close between the enumeration and the read -- skip the vanishing one rather than
                    // failing the whole GetOpenForms for a caller polling while a dialog closes. (A cancellation is
                    // the client giving up on the request, so that one is not swallowed.)
                }

                if (formInfo != null)
                {
                    yield return formInfo;
                }
            }
        }

        /// <summary>Resolves a formId to the window it addresses -- a managed form (StandaloneForm) or a native
        /// dialog (NativeDialog), so no verb special-cases a native dialog. Throws if no open window has the id.
        ///
        /// <para>An id can name more than one open window: a file dialog's message box carries the file dialog's own
        /// caption, so both are "Dialog:Save As" (see <see cref="NativeDialog.DialogTypeName"/>). The TOPMOST wins --
        /// which is the box, the one actually in the way and the one a user would have to deal with first. That falls
        /// out of the enumeration: EnumWindows walks top-level windows in Z-ORDER, topmost first, so the first match
        /// is the topmost. Load-bearing, so do not reorder these walks.</para>
        ///
        /// <para>Built with the CANCELLATION OF THE REQUEST that asked for it, so every marshal and wait the
        /// returned element (and the tree under it) makes can be abandoned when that client disconnects. The token
        /// comes from the JsonToolServer verb serving the request; in-process callers pass None.</para></summary>
        public static StandaloneWindow ResolveForm(string formId, CancellationToken cancellationToken)
        {
            ValidateFormIdFormat(formId);
            // The form is the root of its path; record it so get_controls parents the controls onto it.
            var formPath = new UiElementPath(null, formId, null, @"Form");
            foreach (var dialog in NativeDialog.GetOpenDialogs(cancellationToken))   // topmost first
                if (dialog.FormId == formId)
                {
                    dialog.Path = formPath;
                    return dialog;
                }
            // The managed form, already built (with its handle) by GetOpenFormElements; recording its path is a
            // plain field set, so no UI-thread marshal is needed.
            var formElement = (StandaloneForm) FindFormById(formId, cancellationToken);
            formElement.Path = formPath;
            return formElement;
        }

        public static string GetGraphData(string graphId, string filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            var form = ((StandaloneForm) FindFormById(graphId, cancellationToken)).Form as DockableFormEx;
            return InvokeOnUiThread(() =>
            {
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

        public static string GetFormImage(string formId, string filePath, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateFormIdFormat(formId);
            // ResolveForm throws "form not found" before any permission prompt, so a bad id never prompts.
            var form = ResolveForm(formId, cancellationToken);
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
                return denial;
            // The form captures itself in its own thread context (a managed form on the UI thread with
            // redaction; a native dialog by window handle on this thread).
            using (var bitmap = form.CaptureImage())
            {
                filePath = filePath ?? GetMcpTmpFilePath(FORM_FILE_PREFIX, form.Title, EXT_PNG);
                DirectoryEx.CreateForFilePath(filePath);
                bitmap.Save(filePath, ImageFormat.Png);
            }
            return filePath.ToForwardSlashPath();
        }

        public static ImageBytesMetadata GetFormImageBytes(string formId, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateFormIdFormat(formId);
            var form = ResolveForm(formId, cancellationToken);
            string denial = CheckScreenCaptureAvailability();
            if (denial != null)
            {
                // Permission denial / desktop unavailable: return a structured Message instead of bytes. The
                // wrapper emits Message as plain text content (no error flag) so the response shape matches
                // what the legacy file-based path returned for the same condition.
                return new ImageBytesMetadata { Message = denial };
            }
            using (var bitmap = form.CaptureImage())
            {
                return new ImageBytesMetadata
                {
                    Data = BitmapToPngBytes(bitmap),
                    FilePath = GetMcpTmpFilePath(FORM_FILE_PREFIX, form.Title, EXT_PNG).ToForwardSlashPath(),
                    MimeType = MIME_TYPE_PNG
                };
            }
        }

        // Captures a screenshot of a native window (e.g. the Open/Save file dialog) by its handle, on the
        // calling (pipe) thread -- the capture is a screen copy and must not marshal to the UI thread, which
        // may be blocked in the dialog's modal loop. GetWindowRect returns logical coordinates, scaled to
        // physical pixels to match the screen copy (the same convention as ScreenCapture.GetForeignWindowRects).
        internal static System.Drawing.Bitmap CaptureNativeWindow(IntPtr windowHandle)
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
        private static void EnsureGraphForm(string graphId, out DockableFormEx form, out ZedGraphControl graph, CancellationToken cancellationToken = default(CancellationToken))
        {
            form = ((StandaloneForm) FindFormById(graphId, cancellationToken)).Form as DockableFormEx;
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

        // Shared pre-flight for the image-capture tools. Returns null when the caller may proceed, or an LLM-facing
        // message it must surface. Bad input (id format, form not found, wrong form type) throws ArgumentException
        // instead -- a caller-contract violation, which must reach the caller whatever the environment, so the
        // validation runs BEFORE the screen-capture availability check.
        private static string CheckImageToolPreflight(string id, Action ensureExistsOnUi, bool requiresScreenCapture)
        {
            ValidateFormIdFormat(id);
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
        internal static string GetFormTitle(Form form)
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
        internal static string GetFormId(Form form)
        {
            return form.GetType().Name + @":" + GetFormTitle(form);
        }

        internal static ZedGraphControl TryGetZedGraphControl(DockableFormEx form)
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

        // The forms docked in the main Skyline window, each as a FormElement, or empty when the main window is not
        // up. Callable from any thread: the docked forms are child windows read through the main window's DockPanel,
        // which must be on its UI thread, so this Invokes the read (and the FormElement construction, which captures
        // each handle) onto that thread. Hidden/unknown-state docked forms are skipped (not on screen).
        private static IList<StandaloneWindow> GetDockedForms(CancellationToken cancellationToken)
        {
            Assume.IsNotNull(Program.MainWindow);
            Assume.IsFalse(Program.MainWindow.InvokeRequired);
            var result = new List<StandaloneWindow>();
            foreach (var form in Program.MainWindow.DockPanel.Contents.OfType<DockableFormEx>())
            {
                var dockState = form.DockState;
                if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                    continue;
                result.Add(StandaloneWindow.NewStandaloneWindow(form.Handle, cancellationToken));
            }
            return result;
        }

        /// <summary>
        /// Finds a managed form by its TypeName:Title identifier -- the main window, a form docked in it, or an
        /// open dialog -- and returns it as the already-built <see cref="StandaloneForm"/> (its window handle already
        /// captured). Matches against <see cref="GetOpenFormElements"/>, which enumerates the top-level windows off
        /// any thread and reads the docked forms through the main window's own Invoke, so this may be called from
        /// any thread (the caller need not marshal it onto the UI thread).
        /// </summary>
        private static StandaloneWindow FindFormById(string formId, CancellationToken cancellationToken)
        {
            ValidateFormIdFormat(formId);
            StandaloneWindow window = null;
            if (Program.MainWindow == null)
            {
                window = GetFormWithId(StandaloneWindow.GetTopLevelWindows(cancellationToken), formId);
            }
            else
            {
                DialogWatcher.PerformAction(Program.MainWindow, () =>
                {
                    window = GetFormWithId(StandaloneWindow.GetTopLevelWindows(cancellationToken), formId) ?? GetFormWithId(GetDockedForms(cancellationToken), formId);
                }, cancellationToken);
            }

            if (window != null)
            {
                return window;
            }

            throw new ArgumentException(LlmInstruction.Format(
                @"Form not found: {0}. Use skyline_get_open_forms to see available forms.",
                formId));
        }

        private static StandaloneWindow GetFormWithId(IEnumerable<StandaloneWindow> windows, string formId)
        {
            return windows.FirstOrDefault(window => window.FormId == formId);
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
