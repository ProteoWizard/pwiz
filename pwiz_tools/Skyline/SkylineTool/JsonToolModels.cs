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

namespace SkylineTool
{
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
    /// PNG bytes plus a server-suggested file path, returned by the inline
    /// image methods on <see cref="IJsonToolService"/>. The server does not
    /// write the file - the caller decides whether to emit the bytes inline
    /// (e.g. as an MCP <c>ImageContentBlock</c>) or to write them to
    /// <see cref="FilePath"/> when the inline payload would exceed a caller-side cap.
    ///
    /// <para><see cref="Data"/> is the raw PNG byte array; over JSON-RPC the
    /// payload is base64-encoded by both Newtonsoft.Json (server) and
    /// System.Text.Json (client).</para>
    ///
    /// <para><see cref="FilePath"/> is the path the server would have written
    /// to if asked for the file form of the same image - it is suitable for
    /// fallback writes by the caller (timestamped, in the shared MCP temp
    /// directory) but does not exist on disk when the inline call returns.</para>
    ///
    /// <para><see cref="MimeType"/> is always <c>"image/png"</c> in v1.</para>
    /// </summary>
    public class ImageBytesMetadata
    {
        public byte[] Data { get; set; }
        public string FilePath { get; set; }
        public string MimeType { get; set; }
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
}
