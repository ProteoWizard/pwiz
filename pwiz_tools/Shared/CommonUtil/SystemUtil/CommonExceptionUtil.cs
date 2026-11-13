/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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

using System;
using System.IO;
using System.Linq;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// The line Skyline draws between an exception whose message is written for the user and one
    /// that is a programming defect, shared so that Osprey (which cannot reference Skyline) draws
    /// it in the same place. Skyline's <c>ExceptionUtil.IsProgrammingDefect</c> forwards here.
    /// </summary>
    public static class CommonExceptionUtil
    {
        /// <summary>
        /// Returns true if the exception is not something which could happen while trying to read
        /// from disk, and should therefore be reported as a bug (with its type and stack) rather
        /// than shown to the user as a message. User-actionable exceptions carry friendly
        /// messages: file and data problems, access denied, cancellation, and
        /// <see cref="UserMessageException"/> (which covers all custom user-facing exceptions).
        /// </summary>
        public static bool IsProgrammingDefect(Exception exception)
        {
            if (exception is InvalidDataException
                || exception is IOException
                || exception is OperationCanceledException
                || exception is UnauthorizedAccessException
                || exception is UserMessageException)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// The exception a parallel loop actually threw: an <see cref="AggregateException"/> whose
        /// inner exceptions are all user-actionable is reported through its first inner exception,
        /// so a file problem found on a worker thread reads the same as one found on the caller's.
        /// Anything else is returned unchanged.
        /// </summary>
        public static Exception UnwrapUserException(Exception exception)
        {
            if (exception is AggregateException aggregate)
            {
                var inner = aggregate.Flatten().InnerExceptions;
                if (inner.Count > 0 && inner.All(e => !IsProgrammingDefect(e)))
                    return inner[0];
            }
            return exception;
        }
    }
}
