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
    /// Provides a mechanism for showing developers a non-blocking message.
    ///
    /// Really just a wrapper for TraceInformation, which we don't want to call
    /// directly as it looks too much like leftover debug code.
    ///
    /// </summary>
    public static class DebugMessage
    {
        public static void AsyncWrite(string message, params object[] args)
        {
            Trace.TraceInformation(message, args);
        }
    }
}
