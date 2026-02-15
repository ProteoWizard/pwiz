/*
 * Original author: Brendan MacLean <brendanx .at. u.washington.edu>,
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
using System.Diagnostics;
using System.IO;
using System.Threading;

namespace pwiz.Common.SystemUtil
{
    public class AssumptionException : Exception
    {
        public AssumptionException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// This is a replacement for Debug.Assert, having the advantage that it is not omitted in a retail build.
    /// </summary>
    public static class Assume
    {

        public static bool InvokeDebuggerOnFail { get; private set; } // When set, we will invoke the debugger rather than fail.
        public class DebugOnFail : IDisposable
        {
            private bool _pushPopInvokeDebuggerOnFail;

            public DebugOnFail(bool invokeDebuggerOnFail = true)
            {
                _pushPopInvokeDebuggerOnFail = InvokeDebuggerOnFail; // Push
                InvokeDebuggerOnFail = invokeDebuggerOnFail;
            }

            public void Dispose()
            {
                InvokeDebuggerOnFail = _pushPopInvokeDebuggerOnFail; // Pop
            }
        }

        public static void IsTrue(bool condition, string error = "")
        {
            if (!condition)
                Fail(error);
        }

        public static void IsFalse(bool condition, string error = "")
        {
            if (condition)
                Fail(error);
        }

        public static void IsNotNull(object o, string parameterName = "")
        {
            if (o == null)
                Fail(string.IsNullOrEmpty(parameterName) ? @"null object" : parameterName + @" is null");
        }

        public static void IsNull(object o, string parameterName = "")
        {
            if (o != null)
                Fail(string.IsNullOrEmpty(parameterName) ? @"non-null object" : parameterName + @" is not null");
        }

        public static void AreEqual(object left, object right, string error = "")
        {
            if (!Equals(left, right))
            {
                if (string.IsNullOrEmpty(error))
                    error = $@"Expected <{left}> to equal <{right}>";
                Fail(error);
            }
        }

        public static void AreNotEqual(object left, object right, string error = "")
        {
            if (Equals(left, right))
            {
                if (string.IsNullOrEmpty(error))
                    error = $@"Expected <{left}> not to equal <{right}>";
                Fail(error);
            }
        }

        public static void AreEqual(double expected, double actual, double delta, string error = "")
        {
            if (Math.Abs(expected - actual) > delta)
            {
                if (string.IsNullOrEmpty(error))
                    error = $@"Expected {expected} to be within {delta} of actual value {actual}.";
                Fail(error);
            }
        }

        public static void Fail(string error = "")
        {
            if (InvokeDebuggerOnFail)
            {
                // Try to launch devenv with our solution sln so it presents in the list of debugger options.
                // This makes for better code navigation and easier debugging.
                try
                {
                    var path = @"\pwiz_tools\Skyline";
                    var basedir = AppDomain.CurrentDomain.BaseDirectory;
                    if (!string.IsNullOrEmpty(basedir))
                    {
                        var index = basedir.IndexOf(path, StringComparison.Ordinal);
                        var solutionPath = basedir.Substring(0, index + path.Length);
                        var skylineSln = Path.Combine(solutionPath, "Skyline.sln");
                        // Try to give user a hint as to which debugger to pick
                        var skylineTesterSln = Path.Combine(solutionPath, "USE THIS FOR ASSUME FAIL DEBUGGING.sln");
                        if (File.Exists(skylineTesterSln))
                            File.Delete(skylineTesterSln);
                        File.Copy(skylineSln, skylineTesterSln);
                        Process.Start(skylineTesterSln);
                        Thread.Sleep(20000); // Wait for it to fire up sp it's offered in the list of debuggers
                    }
                }
                // ReSharper disable once EmptyGeneralCatchClause
                catch (Exception)
                {
                }

                Console.WriteLine();
                if (!string.IsNullOrEmpty(error))
                    Console.WriteLine(error);
                Console.WriteLine(@"error encountered, launching debugger as requested by Assume.DebugOnFail");
                Debugger.Launch();
            }
            throw new AssumptionException(error);
        }

        /// <summary>
        /// This function does two things: it returns the value of a nullable that we assume has a value (this
        /// avoids Resharper warnings), and it throws an exception if the nullable unexpectedly has no value.
        /// </summary>
        /// <param name="value">a nullable int that is expected to have a value</param>
        /// <returns>the value of the nullable int</returns>
        public static T Value<T>(T? value) where T : struct
        {
            if (!value.HasValue)
                Fail(@"Nullable_was_expected_to_have_a_value"); 
            return value.Value;
        }
    }
}