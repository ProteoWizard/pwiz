/*
 * Original author: Matt Chambers <matt.chambers .@. vanderbilt.edu>
 *
 * Copyright 2024 University of Washington - Seattle, WA 98195
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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// The open-data-source dialog's view, saved in the user's settings. Here rather than with
    /// the dialog because the settings that persist it must load without WinForms.
    /// </summary>
    [Serializable]
    public sealed class OpenDataSourceState
    {
        public string DocumentPath { get; set; }
        public string InitialDirectory { get; set; }
        public string SourceTypeName { get; set; }
        public FileListView ListView { get; set; }
        public int ListSortColumnIndex { get; set; }
        public FileListSortOrder ListSortOrder { get; set; }
        public FileDialogSize WindowSize { get; set; }
    }

    /// <summary>
    /// A window size, serialized as System.Drawing.Size was (Width and Height), which this
    /// assembly does not use.
    /// </summary>
    [Serializable]
    public struct FileDialogSize
    {
        public int Width { get; set; }
        public int Height { get; set; }
    }

    /// <summary>
    /// The same members and values as System.Windows.Forms.View, which this replaced: settings
    /// store an enum by member name, so saved states still read back.
    /// </summary>
    public enum FileListView
    {
        LargeIcon,
        Details,
        SmallIcon,
        List,
        Tile
    }

    /// <summary>
    /// The same members and values as System.Windows.Forms.SortOrder, which this replaced.
    /// </summary>
    public enum FileListSortOrder
    {
        None,
        Ascending,
        Descending
    }
}
