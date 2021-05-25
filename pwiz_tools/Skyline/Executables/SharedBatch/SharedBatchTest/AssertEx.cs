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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SharedBatch;

namespace SharedBatchTest
{
    /// <summary>
    /// AssertEx provides an extended set of Assert functions, such as collection equality, as well as
    /// new implementations of common Assert functions such as Assert.IsTrue.
    /// 
    /// These implementations give two advantages over standard Assert.* functions:
    ///    Easier to set breakpoints while debugging
    ///    Tests can optionally invoke a debugger instead of quitting, using the <see cref="Assume.DebugOnFail"/> mechanism.
    /// </summary>
    public static class AssertEx
    {
        public static void AreEqualDeep<TItem>(IList<TItem> l1, IList<TItem> l2)
        {
            AreEqual(l1.Count, l2.Count);
            for (int i = 0; i < l1.Count; i++)
            {
                if (!Equals(l1[i], l2[i]))
                {
                    AreEqual(l1[i], l2[i]);  // For setting breakpoint
                }
            }
        }

        public static void AreEqual<T>(T expected, T actual, string message = null)
        {
            if (!Equals(expected, actual))
            {
                if (Assume.InvokeDebuggerOnFail)
                {
                    Assume.Fail(message); // Handles the debugger launch
                }
                Assert.AreEqual(expected, actual, message);
            }
        }

        public static void AreNotEqual<T>(T expected, T actual, string message)
        {
            if (Equals(expected, actual))
            {
                if (Assume.InvokeDebuggerOnFail)
                {
                    Assume.Fail(message); // Handles the debugger launch
                }
                Assert.AreNotEqual(expected, actual, message);
            }
        }

        public static void AreNotSame(object expected, object actual)
        {
            if (!ReferenceEquals(expected, actual))
            {
                if (Assume.InvokeDebuggerOnFail)
                {
                    Assume.Fail(); // Handles the debugger launch
                }
                Assert.AreNotSame(expected, actual);
            }
        }

        public static void Fail(string message = null)
        {
            if (Assume.InvokeDebuggerOnFail)
            {
                Assume.Fail(message); // Handles the debugger launch
            }
            Assert.Fail(message);
        }

        public static void Fail(string message, params object[] parameters)
        {
            if (Assume.InvokeDebuggerOnFail)
            {
                Assume.Fail(string.Format(message, parameters)); // Handles the debugger launch
            }
            Assert.Fail(message, parameters);
        }

        public static void AreEqual(double expected, double actual, double tolerance, string message = null)
        {
            if (!Equals(expected, actual))
            {
                if (Math.Abs(expected - actual) > tolerance)
                {
                    AreEqual(expected, actual, message);
                }
            }
        }

        public static void AreEqual(double? expected, double? actual, double tolerance, string message = null)
        {
            if (!Equals(expected, actual))
            {
                if (expected.HasValue && actual.HasValue)
                {
                    AreEqual(expected.Value, actual.Value, tolerance, message);
                }
                else
                {
                    AreEqual(expected, actual, message);
                }
            }
        }

        public static void IsTrue(bool expected, string message = null)
        {
            AreEqual(expected, true, message);
        }

        public static void IsFalse(bool expected, string message = null)
        {
            AreEqual(expected, false, message);
        }

        public static void IsNull(object obj, string message = null)
        {
            AreEqual(null, obj, message);
        }

        public static void IsNotNull(object obj, string message = null)
        {
            AreNotEqual(null, obj, message);
        }


        public static void ThrowsException<TEx>(Action throwEx, string message = null)
            where TEx : Exception
        {
            ThrowsException<TEx>(() => { throwEx(); return null; }, message);
        }

        public static void ThrowsException<TEx>(Func<object> throwEx, string message = null)
            where TEx : Exception
        {
            bool exceptionThrown = false;
            try
            {
                throwEx();
            }
            catch (TEx x)
            {
                if (message != null)
                    AreComparableStrings(message, x.Message);
                exceptionThrown = true;
            }
            // Assert that an exception was thrown. We do this outside of the catch block
            // so that the AssertFailedException will not get caught if TEx is Exception.
            IsTrue(exceptionThrown, "Exception expected");
        }


        public static void NoExceptionThrown<TEx>(Action throwEx)
            where TEx : Exception
        {
            NoExceptionThrown<TEx>(() => { throwEx(); return null; });
        }

        public static void NoExceptionThrown<TEx>(Func<object> throwEx)
            where TEx : Exception
        {
            try
            {
                throwEx();
            }
            catch (TEx x)
            {
                Fail(TextUtil.LineSeparate(string.Format("Unexpected exception: {0}", x.Message), x.StackTrace));
            }
        }

        public static void AreNoExceptions(IList<Exception> exceptions)
        {
            if (exceptions.Count == 0)
                return;

            Fail(TextUtil.LineSeparate(exceptions.Count == 1 ? "Unexpected exception:" : "Unexpected exceptions:",
                TextUtil.LineSeparate(exceptions.Select(x => x.ToString()))));
        }

        public static void Contains(string value, params string[] parts)
        {
            IsNotNull(value, "No message found");
            AreNotEqual(0, parts.Length, "Must have at least one thing contained");
            foreach (string part in parts)
            {
                if (!string.IsNullOrEmpty(part) && !value.Contains(part))
                    Fail("The text '{0}' does not contain '{1}'", value, part);
            }
        }

        public static void FileExists(string filePath, string message = null)
        {
            if (!File.Exists(filePath))
                Fail(TextUtil.LineSeparate(string.Format("Missing file {0}", filePath), message ?? string.Empty));
        }

        public static void FileNotExists(string filePath, string message = null)
        {
            if (File.Exists(filePath))
                Fail(TextUtil.LineSeparate(string.Format("Unexpected file exists {0}", filePath), message ?? string.Empty));
        }

        public static void AreComparableStrings(string expected, string actual, int? replacements = null)
        {
            // Split strings on placeholders
            string[] expectedParts = Regex.Split(expected, @"{\d}");
            if (replacements.HasValue)
            {
                AreEqual(replacements, expectedParts.Length - 1,
                    string.Format("Expected {0} replacements in string resource '{1}'", replacements, expected));
            }

            int startIndex = 0;
            foreach (var expectedPart in expectedParts)
            {
                int partIndex = actual.IndexOf(expectedPart, startIndex, StringComparison.Ordinal);
                AreNotEqual(-1, partIndex,
                    string.Format("Expected part '{0}' not found in the string '{1}'", expectedPart, actual));
                startIndex = partIndex + expectedPart.Length;
            }
        }

    }
}
