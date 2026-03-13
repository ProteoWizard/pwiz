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
}
