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

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// Base class for exceptions that contain messages intended to be displayed to users.
    /// These are user-actionable errors (network failures, file access issues, configuration problems,
    /// external tool failures) rather than programming defects (null references, index out of bounds, etc.).
    /// 
    /// Use this base class when:
    /// - The error is caused by external factors (network, filesystem, user input, external tools)
    /// - The exception message is written for end-users, not developers
    /// - The user can take action to fix the problem (reconnect network, fix permissions, etc.)
    /// 
    /// Do NOT use this for:
    /// - Programming errors (ArgumentException, NullReferenceException, InvalidOperationException)
    /// - Contract violations in APIs (use ArgumentException for parameter validation)
    /// - Internal state corruption (these should be reported as bugs)
    /// </summary>
    public class UserMessageException : Exception
    {
        public UserMessageException(string message) : base(message)
        {
        }

        public UserMessageException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}


