/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2009 University of Washington - Seattle, WA
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
using System.Diagnostics;
using pwiz.Topograph.MsData;

namespace pwiz.Topograph.Util
{
    public static class ErrorHandler
    {
        public const int MaxErrorCount = 1000;
        static readonly List<Error> errors = new List<Error>();
        public static void AddError(Error error)
        {
            lock(errors)
            {
                while (errors.Count >= MaxErrorCount)
                {
                    errors.RemoveAt(0);
                }
                errors.Add(error);
            }
            var handler = ErrorAdded;
            if (handler != null)
            {
                handler.Invoke(error);
            }
        }

        public static void LogException(String component, String message, Exception exception)
        {
            if (exception is LockException)
            {
                return;
            }
            AddError(new Error(component, message, exception, new StackTrace(1)));
            Console.Out.WriteLine(exception);
        }

        public static void ClearErrors()
        {
            lock(errors)
            {
                errors.Clear();
            }
        }

        public static IList<Error> GetErrors()
        {
            lock(errors)
            {
                return errors.ToArray();
            }
        }

        public delegate void ErrorAddedHandler(Error error);

        public static event ErrorAddedHandler ErrorAdded;
    }
}
