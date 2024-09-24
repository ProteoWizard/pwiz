/*
 * Original author: Brian Pratt <bspratt .at. proteinms . net>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2024 University of Washington - Seattle, WA
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

using System.Diagnostics;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Provides a mechanism for showing non-blocking messages.
    ///
    /// Really just a wrapper for Trace, which we don't want to call
    /// directly as it looks too much like leftover debug code.
    ///
    /// </summary>
    public static class Messages
    {
        public static void WriteAsyncDebugMessage(string message, params object[] args)
        {
            Trace.TraceInformation(message, args);
        }

        public static void WriteAsyncUserMessage(string message, params object[] args)
        {
            // For Skyline UI, the TraceWarningListener class causes these messages to appear in the
            // Immediate Window, for commandline they appear in the console.
            Trace.TraceWarning(message, args);  
        }
    }
}
