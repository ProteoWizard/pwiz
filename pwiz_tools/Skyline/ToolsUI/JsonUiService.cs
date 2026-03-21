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
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
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
            if (caught is ArgumentException)
                throw new ArgumentException(caught.Message, caught);
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

        // Level 3: Complete UI operations - Graphs

        public static FormInfo[] GetOpenForms()
        {
            return InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var results = new List<FormInfo>();
                var dockedForms = new HashSet<Form>();
                foreach (var form in skylineWindow.DockPanel.Contents.OfType<DockableFormEx>())
                {
                    dockedForms.Add(form);
                    var dockState = form.DockState;
                    if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                        continue;
                    var zedGraph = TryGetZedGraphControl(form);
                    results.Add(new FormInfo
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
                    results.Add(new FormInfo
                    {
                        Type = form.GetType().Name,
                        Title = GetFormTitle(form),
                        HasGraph = false,
                        DockState = @"Dialog",
                        Id = GetFormId(form),
                    });
                }
                return results.ToArray();
            });
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
                using (var bitmap = zedGraph.MasterPane.GetImage(zedGraph.MasterPane.IsAntiAlias))
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

        private const string FORM_FILE_PREFIX = @"skyline-form";

        public static string GetFormImage(string formId, string filePath)
        {
            return InvokeOnUiThread(() =>
            {
                var form = FindFormById(formId);

                // Check permission (dialog may appear over the target form)
                bool dialogShown = ScreenCapture.EnsurePermission(out bool wasFirstPrompt);
                if (!dialogShown)
                    return new LlmInstruction(@"Screen capture denied by user.");

                // Check desktop availability before attempting capture
                if (!ScreenCapture.IsDesktopAvailable())
                    return new LlmInstruction(@"Screen capture is not available. The desktop session may be disconnected (e.g. Docker container, disconnected Remote Desktop, or locked workstation). Reconnect the desktop session and try again.");

                // Activate the form
                ScreenCapture.ActivateForm(form);

                // If the permission dialog was just shown, allow time for it to
                // fully dismiss and for Windows to repaint the target form.
                if (wasFirstPrompt)
                    Thread.Sleep(1000);

                // Get screen rectangle
                var screenRect = ScreenCapture.GetWindowRectangle(form);

                // Resolve output path before capture so we can derive log path
                filePath = filePath ?? GetMcpTmpFilePath(
                    FORM_FILE_PREFIX, GetFormTitle(form), EXT_PNG);

                // Capture with redaction
                using (var bitmap = ScreenCapture.CaptureAndRedact(screenRect, form))
                {
                    DirectoryEx.CreateForFilePath(filePath);
                    bitmap.Save(filePath, ImageFormat.Png);
                }
                return filePath.ToForwardSlashPath();
            });
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
            switch (form)
            {
                case GraphSummary gs: return gs.GraphControl;
                case GraphChromatogram gc: return gc.GraphControl;
                case GraphSpectrum gsp: return gsp.ZedGraphControl;
                case GraphFullScan gfs: return gfs.ZedGraphControl;
                case CalibrationForm cf: return cf.ZedGraphControl;
                default: return null;
            }
        }

        /// <summary>
        /// Finds a form by its TypeName:Title identifier from GetOpenForms.
        /// Searches docked forms first, then non-docked forms (dialogs).
        /// </summary>
        private static Form FindFormById(string formId)
        {
            int colonIndex = formId.IndexOf(':');
            if (colonIndex < 0)
            {
                throw new ArgumentException(LlmInstruction.Format(
                    @"Invalid form ID format: {0}. Expected 'TypeName:Title'. Use skyline_get_open_forms to get valid IDs.",
                    formId));
            }

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

        // Private helpers - TeeTextWriter

        /// <summary>
        /// A TextWriter that writes to two underlying writers simultaneously.
        /// Used to capture command output while also echoing to the Immediate Window.
        /// </summary>
        private class TeeTextWriter : TextWriter
        {
            private readonly TextWriter _writer1;
            private readonly TextWriter _writer2;

            public TeeTextWriter(TextWriter writer1, TextWriter writer2)
            {
                _writer1 = writer1;
                _writer2 = writer2;
            }

            public override Encoding Encoding => _writer1.Encoding;

            public override void Write(char value)
            {
                _writer1.Write(value);
                _writer2.Write(value);
            }

            public override void Write(string value)
            {
                _writer1.Write(value);
                _writer2.Write(value);
            }

            public override void WriteLine(string value)
            {
                _writer1.WriteLine(value);
                _writer2.WriteLine(value);
            }

            public override void WriteLine()
            {
                _writer1.WriteLine();
                _writer2.WriteLine();
            }

            public override void Flush()
            {
                _writer1.Flush();
                _writer2.Flush();
            }
        }
    }
}
