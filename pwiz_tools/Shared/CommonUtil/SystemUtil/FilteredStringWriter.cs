/*
 * Author: David Shteynberg <dshteyn .at. proteinms.net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2025 University of Washington - Seattle, WA
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
using System.IO;
using System.Linq;
using pwiz.Common.Collections;

namespace pwiz.Common.SystemUtil
{
    public class FilteredStringWriter : StringWriter
    {
        private IList<string> _filterStrings;
        private bool _enableImmediateLog;
        
        public FilteredStringWriter(ImmutableList<string> filterStrings, bool enableImmediateLog = false)
        {
            _filterStrings = filterStrings;
            _enableImmediateLog = enableImmediateLog;
        }


        /// <summary>
        /// Function to help with filtering lines of output.
        /// </summary>
        /// <param name="line">Line of text considered for filtering.</param>
        /// <returns>Return true if the provided line of text contains any of the provided filters of text, false otherwise.</returns>
        private bool FilterOutputLine(string line)
        {
            return _filterStrings.Any(line.Contains);
        }
        public override void WriteLine(string line)
        {
            string adjustedLine = line;
            bool skip_line = false;

            if (line.Contains(@"DiaNN/Spectronaut"))
                adjustedLine = line.Replace(@"DiaNN/Spectronaut", @"Skyline");
            else
                skip_line = FilterOutputLine(line);

            if (!skip_line && _enableImmediateLog)
                Messages.WriteAsyncUserMessage(adjustedLine);

            base.WriteLine(adjustedLine);
        }
    }
}
