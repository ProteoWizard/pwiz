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

using System;
using System.Diagnostics;
using System.Reflection;
using System.Threading;

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
        // Testing overrides
        public static Action<string, object[]> WriteDebugMessage = (message, args) => Trace.TraceInformation(message, args);
        public static Action<string, object[]> WriteUserMessage = (message, args) => Trace.TraceWarning(message, args);

        public static void WriteAsyncDebugMessage(string message, params object[] args)
        {
            if (args?.Length > 0)
            {
                WriteDebugMessage(message, args);
            }
            else
            {
                // If there were no formatting arguments, then pass the message as an argument so that it is not interpreted as a formatting template
                WriteDebugMessage(@"{0}", new object[] { message });
            }
        }

        public static void WriteAsyncUserMessage(string message, params object[] args)
        {
            // For Skyline UI, the TraceWarningListener class causes these messages to appear in the
            // Immediate Window, for commandline they appear in the console.
            if (args?.Length > 0)
            {
                WriteUserMessage(message, args);
            }
            else
            {
                // If there were formatting no arguments, then pass the message as an argument so that it is not interpreted as a formatting template
                WriteUserMessage(@"{0}", new object[] { message });
            }
        }
    }

    /// <summary>
    /// Like Trace.WriteLine, but with considerable detail when running a test
    /// </summary>
    public class DetailedTrace
    {
        public static void WriteLine(string msg, bool showStackTrace = false)
        {
            if (!(Assembly.GetEntryAssembly() ?? Assembly.GetCallingAssembly()).Location.Contains(@"TestRunner"))
            {
                Trace.WriteLine(msg);
            }
            else
            {
                // Give more detail - useful in case of parallel test interactions
                Trace.WriteLine(
                    $@"{msg} [UTC: {DateTime.UtcNow:s} PID: {Process.GetCurrentProcess().Id} Thread: {Thread.CurrentThread.ManagedThreadId})]");
                if (showStackTrace)
                {
                    // per https://docs.microsoft.com/en-us/dotnet/api/system.diagnostics.stacktrace?view=net-6.0
                    // Create a StackTrace that captures filename, line number and column information.
                    var st = new StackTrace(true);
                    var stackIndent = string.Empty;
                    for (var i = 0; i < st.FrameCount; i++)
                    {
                        var sf = st.GetFrame(i);
                        Trace.WriteLine($@"{stackIndent}{sf.GetMethod()} at {sf.GetFileName()}({sf.GetFileLineNumber()}:{sf.GetFileColumnNumber()})");
                        stackIndent += @"  ";
                    }
                }
            }
        }
    }
}
