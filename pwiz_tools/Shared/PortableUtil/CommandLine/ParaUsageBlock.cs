/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
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

namespace pwiz.Common.CommandLine
{
    /// <summary>
    /// A free-text paragraph in usage output (intro/section prose), rendered as a
    /// borderless wrapped block in plain text and a &lt;p&gt; element in HTML.
    /// </summary>
    public class ParaUsageBlock : IUsageBlock
    {
        public ParaUsageBlock(string text)
        {
            Text = text;
        }

        public string Text { get; private set; }

        public string ToString(int width, string formatType)
        {
            return ConsoleTable.ParaToString(width, Text, true);
        }

        public string ToHtmlString()
        {
            return @"<p>" + Text + @"</p>";
        }
    }
}
