/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
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
using System.IO;

namespace pwiz.Skyline.Util
{
    public class TraceWarningListener : TraceListener
    {
        private TextWriter _textWriter;
        public TraceWarningListener(TextWriter textWriter)
        {
            // N.B. if you want to see DebugMessage.AsynchWrite messages too, add .Information to the flags here 
            Filter = new EventTypeFilter(SourceLevels.Warning); 
            _textWriter = textWriter;
        }

        public override void Write(string message)
        {
            // Ignore messages which have no TraceEventType
        }

        public override void WriteLine(string message)
        {
            // Ignore messages which have no TraceEventType
        }

        /// <summary>
        /// Override the TraceEvent method to prevent "WriteHeader" and "WriteFooter" from being called.
        /// </summary>
        public override void TraceEvent(
            TraceEventCache eventCache,
            string source,
            TraceEventType eventType,
            int id,
            string format,
            params object[] args)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, format, args, null, null))
                return;
            _textWriter.WriteLine(format, args);
        }

        public override void TraceEvent(TraceEventCache eventCache, string source, TraceEventType eventType, int id, string message)
        {
            if (Filter != null && !Filter.ShouldTrace(eventCache, source, eventType, id, message, null, null, null))
                return;
            _textWriter.WriteLine(message);
        }
    }
}
