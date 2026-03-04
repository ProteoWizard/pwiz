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
using DigitalRune.Windows.Docking;
using pwiz.Common.SystemUtil;
using pwiz.Skyline.Controls.Graphs;
using pwiz.Skyline.Controls.Graphs.Calibration;
using pwiz.Skyline.EditUI;
using pwiz.Skyline.Model;
using pwiz.Skyline.Model.ElementLocators;
using pwiz.Skyline.Util;
using pwiz.Skyline.Util.Extensions;
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
        /// Executes an action on the UI thread. Returns "OK" on success
        /// or the exception message on failure.
        /// </summary>
        public static string InvokeOnUiThread(Action action)
        {
            string error = null;
            Program.MainWindow.Invoke(new Action(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    error = ex.Message;
                }
            }));
            return error ?? @"OK";
        }

        /// <summary>
        /// Executes a function on the UI thread and returns the result.
        /// Exceptions propagate to the caller.
        /// </summary>
        public static string InvokeOnUiThread(Func<string> func)
        {
            string result = null;
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

        public static string GetSelection()
        {
            return InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var document = skylineWindow.DocumentUI;
                var selectedPaths = skylineWindow.SequenceTree.SelectedPaths;
                if (selectedPaths.Count == 0)
                    return string.Empty;

                var elementRefs = new ElementRefs(document);
                var sb = new StringBuilder();
                foreach (var path in selectedPaths)
                {
                    if (path.IsRoot)
                        continue;
                    var nodeRef = elementRefs.GetNodeRef(path);
                    if (nodeRef == null)
                        continue;
                    if (sb.Length > 0)
                        sb.AppendLine();
                    sb.Append(nodeRef);
                }
                return sb.ToString();
            });
        }

        public static string SetSelection(string elementLocatorString, string additionalLocators)
        {
            return InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;

                // Primary selection - full navigation (bookmark, replicate, scroll)
                skylineWindow.SelectElement(
                    ElementRefs.FromObjectReference(ElementLocator.Parse(elementLocatorString)));

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

        public static string SetReplicate(string replicateName)
        {
            return InvokeOnUiThread(() =>
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

        public static string GetOpenForms()
        {
            return InvokeOnUiThread(() =>
            {
                var skylineWindow = Program.MainWindow;
                var sb = new StringBuilder();
                sb.Append(TextUtil.ToEscapedTSV(new[] {@"Type", @"Title", @"HasGraph", @"DockState", @"ID"}));
                int graphIndex = 0, formIndex = 0;
                foreach (var form in skylineWindow.DockPanel.Contents.OfType<DockableFormEx>())
                {
                    var dockState = form.DockState;
                    if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                        continue;
                    var zedGraph = TryGetZedGraphControl(form);
                    bool hasGraph = zedGraph != null;
                    string id = hasGraph ? @"graph:" + graphIndex++ : @"form:" + formIndex++;
                    string type = form.GetType().Name;
                    string title = !string.IsNullOrEmpty(form.Text) ? form.Text
                        : !string.IsNullOrEmpty(form.TabText) ? form.TabText
                        : type;
                    sb.AppendLine();
                    sb.Append(TextUtil.ToEscapedTSV(new[] {type, title, hasGraph.ToString(), dockState.ToString(), id}));
                }
                return sb.ToString();
            });
        }

        public static string GetGraphData(string graphId, string filePath)
        {
            return InvokeOnUiThread(() =>
            {
                var form = FindGraphForm(graphId);
                var zedGraph = TryGetZedGraphControl(form);
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
                var form = FindGraphForm(graphId);
                var zedGraph = TryGetZedGraphControl(form);
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

        // Private helpers - Graph support

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

        private static DockableFormEx FindGraphForm(string graphId)
        {
            if (!graphId.StartsWith(@"graph:") ||
                !int.TryParse(graphId.Substring(6), out int targetIndex))
            {
                throw new ArgumentException(new LlmInstruction(
                    @"Invalid graph ID: " + graphId +
                    @". Use skyline_get_open_forms to get valid IDs."));
            }

            int graphIndex = 0;
            foreach (var form in Program.MainWindow.DockPanel.Contents.OfType<DockableFormEx>())
            {
                var dockState = form.DockState;
                if (dockState == DockState.Hidden || dockState == DockState.Unknown)
                    continue;
                if (TryGetZedGraphControl(form) == null)
                    continue;
                if (graphIndex == targetIndex)
                    return form;
                graphIndex++;
            }
            throw new ArgumentException(new LlmInstruction(
                @"Graph not found: " + graphId +
                @". Use skyline_get_open_forms to see current graphs."));
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
