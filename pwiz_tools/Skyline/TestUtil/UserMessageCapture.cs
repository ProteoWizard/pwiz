/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
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

using System;
using System.Collections.Generic;
using pwiz.Common.SystemUtil;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Captures the user messages written through <see cref="Messages"/> for the life of the
    /// scope, restoring the previous writer on dispose.
    /// </summary>
    /// <example>
    /// using (var capture = new UserMessageCapture())
    /// {
    ///     DoSomethingThatWarns();
    ///     CollectionAssert.Contains(capture.CapturedMessages, expectedMessage);
    /// }
    /// </example>
    public class UserMessageCapture : IDisposable
    {
        private readonly Action<string, object[]> _originalWriteUserMessage;
        private readonly List<string> _capturedMessages = new List<string>();

        public UserMessageCapture()
        {
            _originalWriteUserMessage = Messages.WriteUserMessage; // Save the original delegate
            Messages.WriteUserMessage = (message, args) =>
            {
                // Messages can arrive from background worker threads, e.g. chromatogram import.
                lock (_capturedMessages)
                {
                    _capturedMessages.Add(string.Format(message, args));
                }
            };
        }

        /// <summary>
        /// A snapshot of the messages captured so far, safe to enumerate while writers are active.
        /// </summary>
        public List<string> CapturedMessages
        {
            get
            {
                lock (_capturedMessages)
                {
                    return new List<string>(_capturedMessages);
                }
            }
        }

        /// <summary>
        /// Discards the messages captured so far, to isolate what a subsequent step writes.
        /// Necessary because <see cref="CapturedMessages"/> hands back a copy.
        /// </summary>
        public void Clear()
        {
            lock (_capturedMessages)
            {
                _capturedMessages.Clear();
            }
        }

        public void Dispose()
        {
            Messages.WriteUserMessage = _originalWriteUserMessage;
        }
    }
}
