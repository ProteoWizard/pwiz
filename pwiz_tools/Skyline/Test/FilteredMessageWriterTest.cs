/*
 * Author: Brendan MacLean <brendanx .at. uw.edu>,
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
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using pwiz.Common.SystemUtil;
using pwiz.SkylineTestUtil;

namespace pwiz.SkylineTest
{
    [TestClass]
    public class FilteredMessageWriterTest : AbstractUnitTest
    {
        [TestMethod]
        public void FilteredUserMessageWriterTest()
        {
            using var capture = new UserMessageCapture();

            // Mock the WriteUserMessage delegate
            var filterStrings = new List<string>
            {
                @"filter this", // Filter out lines containing this
                @"s/old/new/",  // Replace "old" with "new"
                @"s/DiaNN\/Spectronaut/Skyline/" // Replace "DiaNN/Spectronaut" with "Skyline"
            };
            var writer = new FilteredUserMessageWriter(filterStrings);
            var inputLines = new List<string>
            {
                "This line contains filter this and should be skipped.",
                "This line contains old and should replace old with new.",
                "DiaNN/Spectronaut is a tool and should be replaced with Skyline.",
                "This line is unaffected and should be written as is."
            };
            // Act
            foreach (var line in inputLines)
            {
                writer.WriteLine(line);
            }
            // Assert
            var expectedOutput = new List<string>
            {
                "This line contains new and should replace new with new.",
                "Skyline is a tool and should be replaced with Skyline.",
                "This line is unaffected and should be written as is."
            };
            CollectionAssert.AreEqual(expectedOutput, capture.CapturedMessages);
        }
    }

    public class UserMessageCapture : IDisposable
    {
        private Action<string, object[]> _originalWriteUserMessage;
        
        public UserMessageCapture()
        {
            _originalWriteUserMessage = Messages.WriteUserMessage; // Save the original delegate
            CapturedMessages = new List<string>();
            Messages.WriteUserMessage = (message, args) => CapturedMessages.Add(string.Format(message, args));
        }
        
        public List<string> CapturedMessages { get; private set; }

        public void Dispose()
        {
            Messages.WriteUserMessage = _originalWriteUserMessage;
        }
    }
}
