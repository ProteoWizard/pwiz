/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2011 University of Washington - Seattle, WA
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
using pwiz.Common.SystemUtil;
// ReSharper disable NonLocalizedString
//TODO: Disabled strings so build will pass, Nick will be localizing this file.
namespace pwiz.Common.DataBinding
{
    /// <summary>
    /// Class for exporting data to a file.
    /// </summary>
    public interface IDataFormat
    {
        string FileFilter { get; }
        DsvWriter GetDsvWriter();
    }

    public static class DataFormats
    {
        // ReSharper disable InconsistentNaming
        public static readonly IDataFormat TSV = new TextFormat('\t', "Tab Separated Values(*.tsv)|*.tsv");
        public static readonly IDataFormat CSV = new TextFormat(',', "Comma Separated Values(*.csv)|*.csv");
        // ReSharper restore InconsistentNaming
        class TextFormat : IDataFormat
        {
            public TextFormat(char separator, string fileFilter)
            {
                Separator = separator;
                FileFilter = fileFilter;
            }

            char Separator { get; set; }
            public string FileFilter { get; private set; }

            public DsvWriter GetDsvWriter()
            {
                return new DsvWriter(LocalizationHelper.CurrentCulture, Separator);
            }
        }
    }
}
