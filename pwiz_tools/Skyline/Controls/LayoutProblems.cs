/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5) <noreply .at. anthropic.com>
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

using System.Collections.Generic;
using System.Linq;

namespace pwiz.Skyline.Controls
{
    public enum LayoutProblemType
    {
        /// <summary>
        /// A window Skyline recognizes, which does not apply to the current document. A
        /// layout saved for another document naturally has these; a layout saved for the
        /// document being opened should not.
        /// </summary>
        not_applicable,

        /// <summary>
        /// A window Skyline knows nothing about, which suggests the file is not a window
        /// layout file, or was written by a different version of Skyline.
        /// </summary>
        unrecognized,

        /// <summary>
        /// A window which threw while it was being restored.
        /// </summary>
        error
    }

    /// <summary>
    /// The windows that could not be restored while a window layout file was read.
    /// <para>
    /// Opening a document reports only the severe problems, because the layout beside a
    /// document describes that same document and anything unrecognized there means the file
    /// is wrong. Importing a layout reports everything, because the user has just pointed at
    /// a file which may have been saved for a completely different document, and every window
    /// left out is something they asked for and did not get.
    /// </para>
    /// </summary>
    public class LayoutProblems
    {
        private readonly List<LayoutProblem> _problems = new List<LayoutProblem>();

        public void Add(string windowName, LayoutProblemType problemType)
        {
            _problems.Add(new LayoutProblem { WindowName = windowName, ProblemType = problemType });
        }

        /// <summary>
        /// True when something was found that means the file itself is suspect, rather than
        /// merely mismatched with the current document.
        /// </summary>
        public bool AnySevere
        {
            get { return _problems.Any(problem => problem.ProblemType != LayoutProblemType.not_applicable); }
        }

        /// <summary>
        /// The windows to report, which is all of them when <paramref name="severeOnly"/> is
        /// false, and only the ones that indicate a bad file when it is true.
        /// </summary>
        public IList<string> GetWindowNames(bool severeOnly)
        {
            return _problems.Where(problem => !severeOnly || problem.ProblemType != LayoutProblemType.not_applicable)
                .Select(problem => problem.WindowName).ToArray();
        }
    }

    public class LayoutProblem
    {
        public string WindowName { get; set; }
        public LayoutProblemType ProblemType { get; set; }
    }
}
