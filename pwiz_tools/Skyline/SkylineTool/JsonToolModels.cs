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

// ReSharper disable UnusedAutoPropertyAccessor.Global

using System;

namespace SkylineTool
{
    /// <summary>
    /// JSON-RPC 2.0 error surfaced by <see cref="SkylineJsonToolClient"/> for any
    /// server response carrying an <c>error</c> envelope. The numeric
    /// <see cref="Code"/> matches the JSON-RPC <c>error.code</c> field
    /// (see <see cref="JsonToolConstants"/> for the well-known values like
    /// <see cref="JsonToolConstants.ERROR_METHOD_NOT_FOUND"/>) so callers can
    /// branch on the structured code instead of grepping the message string.
    /// Derives from <see cref="InvalidOperationException"/> for back-compat with
    /// existing catch sites that match on the legacy base type.
    /// </summary>
    public class JsonRpcException : InvalidOperationException
    {
        public int Code { get; }

        public JsonRpcException(int code, string message) : base(message)
        {
            Code = code;
        }
    }

    // POCO models for typed IJsonToolService parameters and return values.
    // Serialized as JSON over the named pipe: PascalCase properties map to
    // snake_case JSON via naming policies (Newtonsoft SnakeCaseNamingStrategy
    // on the server, System.Text.Json SnakeCaseLower on the client).
    //
    // Link-compiled into both .NET Framework 4.7.2 (Skyline) and .NET 8.0
    // (SkylineMcpServer), so must not reference any JSON library.

    // --- Report models ---

    /// <summary>
    /// Metadata returned after exporting a report. Contains the file path,
    /// row count, column headers, and a preview of the first few rows.
    /// </summary>
    public class ReportMetadata
    {
        public string FilePath { get; set; }
        public string ReportName { get; set; }
        public string Format { get; set; }
        public int? RowCount { get; set; }
        public string Columns { get; set; }
        public string Preview { get; set; }
    }

    /// <summary>
    /// Inline windowed result returned by GetReportRows / GetReportFromDefinitionRows.
    /// Rows are formatted strings matching the CSV export; the column descriptors carry
    /// the type so a caller can parse cells correctly.
    /// </summary>
    public class ReportRowsResult
    {
        public string Report { get; set; }
        public int TotalRows { get; set; }
        public ReportRowsColumn[] Columns { get; set; }
        public string[][] Rows { get; set; }
        public ReportRowsWindow Window { get; set; }
        public int? TruncatedAt { get; set; }
    }

    /// <summary>
    /// Column metadata for inline rows results.
    ///
    /// <para><see cref="Type"/> is one of a stable, JSON-friendly vocabulary:
    /// "string", "boolean", "integer", "number", "datetime", or "other" for
    /// types that don't map cleanly. The CLR type name is intentionally not
    /// exposed so the contract is stable across refactors.</para>
    ///
    /// <para><see cref="MaxObservedLength"/> and <see cref="MaxLengthSampled"/>
    /// are set only on text-valued columns (<see cref="Type"/> equal to "string"
    /// or "other" -- the latter covers entity wrappers like Peptide / Replicate
    /// that serialize to text) and only when the caller requested
    /// include_max_length. <see cref="MaxLengthSampled"/> is set to true only
    /// when the scan stopped at the sample cap (the value is a lower-bound
    /// estimate); it is null when the value is exact.</para>
    /// </summary>
    public class ReportRowsColumn
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public int? MaxObservedLength { get; set; }
        public bool? MaxLengthSampled { get; set; }
    }

    /// <summary>
    /// Window metadata for inline rows results: the actual offset/count returned and
    /// whether the response was truncated to respect the server-side token cap.
    /// </summary>
    public class ReportRowsWindow
    {
        public int Offset { get; set; }
        public int Count { get; set; }
        public bool Truncated { get; set; }
    }

    /// <summary>
    /// Definition of a custom report: columns to select, optional filters,
    /// sorting, and pivot settings.
    /// </summary>
    public class ReportDefinition
    {
        public string[] Select { get; set; }
        public string Name { get; set; }
        public ReportFilter[] Filter { get; set; }
        public ReportSort[] Sort { get; set; }
        public bool? PivotReplicate { get; set; }
        public bool? PivotIsotopeLabel { get; set; }
        public string Uimode { get; set; }
        public string DataSource { get; set; }
    }

    /// <summary>
    /// A single filter criterion for a report definition.
    /// </summary>
    public class ReportFilter
    {
        public string Column { get; set; }
        public string Op { get; set; }
        public string Value { get; set; }
    }

    /// <summary>
    /// A single sort specification for a report definition.
    /// </summary>
    public class ReportSort
    {
        public string Column { get; set; }
        public string Direction { get; set; }
    }

    // --- Tutorial models ---

    /// <summary>
    /// Metadata returned when fetching a tutorial: file path to the markdown
    /// content, title, table of contents, and line count.
    /// </summary>
    public class TutorialMetadata
    {
        public string FilePath { get; set; }
        public string Title { get; set; }
        public string Tutorial { get; set; }
        public string Language { get; set; }
        public int LineCount { get; set; }
        public TocEntry[] Toc { get; set; }
    }

    /// <summary>
    /// A single entry in a tutorial's table of contents.
    /// </summary>
    public class TocEntry
    {
        public string Heading { get; set; }
        public int Level { get; set; }
        public int Line { get; set; }
    }

    /// <summary>
    /// Metadata returned when fetching a tutorial image.
    /// </summary>
    public class TutorialImageMetadata
    {
        public string FilePath { get; set; }
        public string Image { get; set; }
    }

    /// <summary>
    /// Raw image bytes plus a server-suggested file path, returned by the inline
    /// image methods on <see cref="IJsonToolService"/>. The server does not
    /// write the file - the caller decides whether to emit the bytes inline
    /// (e.g. as an MCP <c>ImageContentBlock</c>) or to write them to
    /// <see cref="FilePath"/> when the inline payload would exceed a caller-side cap.
    ///
    /// <para><see cref="Data"/> carries the image bytes; over JSON-RPC the
    /// payload is base64-encoded by both Newtonsoft.Json (server) and
    /// System.Text.Json (client). Form and graph captures always produce PNG;
    /// tutorial images preserve whatever format the source file uses
    /// (typically PNG, but JPEG / GIF are also possible).</para>
    ///
    /// <para><see cref="FilePath"/> is the path the server would have written
    /// to if asked for the file form of the same image - it is suitable for
    /// fallback writes by the caller (timestamped, in the shared MCP temp
    /// directory) but does not exist on disk when the inline call returns.</para>
    ///
    /// <para><see cref="MimeType"/> describes the byte payload. Form / graph
    /// captures always set this to <c>"image/png"</c>; tutorial images use the
    /// MIME type implied by the source filename extension.</para>
    ///
    /// <para><see cref="Message"/> is set (with <see cref="Data"/> null) when
    /// the server has a structured non-image response to convey - for example,
    /// screen-capture permission denial or an unavailable desktop session.
    /// Callers should emit <see cref="Message"/> as text content (no error
    /// flag) so the response shape stays consistent with the file-based path.</para>
    /// </summary>
    public class ImageBytesMetadata
    {
        public byte[] Data { get; set; }
        public string FilePath { get; set; }
        public string MimeType { get; set; }
        public string Message { get; set; }
    }

    // --- Document status and selection models ---

    /// <summary>
    /// Document overview returned by GetDocumentStatus.
    /// </summary>
    public class DocumentStatus
    {
        public string DocumentPath { get; set; }
        public string DocumentType { get; set; }
        public int Groups { get; set; }
        public string GroupsLabel { get; set; }
        public int Molecules { get; set; }
        public string MoleculesLabel { get; set; }
        public int Precursors { get; set; }
        public int Transitions { get; set; }
        public int Replicates { get; set; }
        public bool HasUnsavedChanges { get; set; }
    }

    /// <summary>
    /// Current selection state returned by GetSelection.
    /// </summary>
    public class SelectionInfo
    {
        public string[] Locators { get; set; }
    }

    /// <summary>
    /// Detailed column documentation for a report topic returned by GetReportDocTopic.
    /// </summary>
    public class ReportDocTopicDetail
    {
        public string Name { get; set; }
        public ColumnDefinition[] Columns { get; set; }
    }

    /// <summary>
    /// A single column definition within a report documentation topic.
    /// </summary>
    public class ColumnDefinition
    {
        public string Name { get; set; }
        public string Description { get; set; }
        public string Type { get; set; }
    }

    // --- Catalog and enumeration models ---

    /// <summary>
    /// A tutorial entry from the catalog returned by GetAvailableTutorials.
    /// </summary>
    public class TutorialListItem
    {
        public string Category { get; set; }
        public string Name { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string WikiUrl { get; set; }
        public string ZipUrl { get; set; }
    }

    /// <summary>
    /// Summary of a report documentation topic returned by GetReportDocTopics.
    /// </summary>
    public class ReportDocTopicSummary
    {
        public string Name { get; set; }
        public int ColumnCount { get; set; }
    }

    /// <summary>
    /// Information about an open form/window returned by GetOpenForms.
    /// </summary>
    public class FormInfo
    {
        public string Type { get; set; }
        public string Title { get; set; }
        public bool HasGraph { get; set; }
        public string DockState { get; set; }
        public string Id { get; set; }
        /// <summary>
        /// True for native operating-system windows (e.g. the common Open/Save file dialog)
        /// that are driven through UI Automation rather than as WinForms forms.
        /// </summary>
        public bool IsNative { get; set; }
    }

    /// <summary>
    /// Information about one interactive control on a form, returned by GetControls. Lets a caller
    /// discover what is on a form -- and how to address it -- without reading the source: <see cref="Path"/>
    /// is the locator to pass back (to PerformAction), and the rest reports the control's current state and
    /// the actions it supports. <see cref="Name"/> is the internal control name -- informational only, the
    /// connector does not match on it.
    /// </summary>
    public class ControlInfo
    {
        public UiElementPath Path { get; set; }
        public string Name { get; set; }
        public string Value { get; set; }
        public bool Enabled { get; set; }
        public bool Visible { get; set; }
        /// <summary>The actions that can be performed on this control (e.g. "click", "set_value").</summary>
        public string[] Actions { get; set; }
    }

    /// <summary>
    /// A document tree element with name and locator returned by GetLocations.
    /// </summary>
    public class LocationEntry
    {
        public string Name { get; set; }
        public string Locator { get; set; }
    }

    /// <summary>
    /// A single entry in the undo/redo stack returned by GetUndoRedo.
    /// Negative index = undo step, positive = redo step.
    /// </summary>
    public class UndoRedoEntry
    {
        public int Index { get; set; }
        public string Description { get; set; }
    }

    /// <summary>
    /// A path that refers to a UI element -- a control, a menu/list item, or a tree node -- relative to its
    /// <see cref="Parent"/>. Within the parent's children an element is matched by any combination of:
    /// <see cref="Text"/> (its visible text), <see cref="Index"/> (its position in the parent's child list),
    /// and <see cref="Type"/> (its kind, e.g. "TreeView" for a caption-less control, or "ContextMenu" for a
    /// control's right-click menu); whichever are set must all match, else it is an element-not-found error.
    /// The chain bottoms out at a form: a path with a null <see cref="Parent"/> names the form, its
    /// <see cref="Text"/> set to the form id from GetOpenForms (and <see cref="Type"/> "Form").
    /// </summary>
    public class UiElementPath
    {
        public UiElementPath(UiElementPath parent, string text, int? index, string type = null)
        {
            Parent = parent;
            Text = text;
            Index = index;
            Type = type;
        }

        private UiElementPath()
        {
        }

        public UiElementPath Parent { get; private set; }
        public string Text { get; private set; }
        public int? Index { get; private set; }
        public string Type { get; private set; }

        protected bool Equals(UiElementPath other)
        {
            return Equals(Parent, other.Parent) && Text == other.Text && Index == other.Index && Type == other.Type;
        }

        public override bool Equals(object obj)
        {
            if (obj is null)
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((UiElementPath)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Parent != null ? Parent.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Text != null ? Text.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Index.GetHashCode();
                hashCode = (hashCode * 397) ^ (Type != null ? Type.GetHashCode() : 0);
                return hashCode;
            }
        }

        public override string ToString()
        {
            var result = Text ?? Index?.ToString() ?? Type ?? string.Empty;
            if (Parent == null)
            {
                return result;
            }
            return Parent + ">" + result;
        }
    }
}
