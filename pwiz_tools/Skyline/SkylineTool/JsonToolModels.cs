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
}
