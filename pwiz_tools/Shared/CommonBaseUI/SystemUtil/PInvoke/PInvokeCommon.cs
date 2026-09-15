/*
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

using System.Drawing;
using System.Runtime.InteropServices;

namespace pwiz.Common.SystemUtil.PInvoke
{
    /// <summary>
    /// A place for types and constants used by more than one Win32 DLL for P/Invoke.
    ///
    /// The PInvoke namespace isolates Skyline's use of Win32 APIs in one place to reduce duplication
    /// and increase visibility.
    ///
    /// A handful of classes are allowed to use P/Invoke locally (specifically, the [DllImport]
    /// attribute). This is the exception and not the rule.
    ///
    /// Over time, use of these APIs should feel more like idiomatic .NET and less like
    /// using Win32.
    ///
    /// Classes in this namespace represent Win32 DLLs. Rules:
    ///     (1) Any DLL referenced from PInvoke must have its own class - even if it's just a single function
    ///     (2) Class name and DLL name must match, ignoring case
    ///         Example: user32.dll is represented in User32.cs
    ///     (3) Functions using [DllImport] must be all lower case and end in ".dll"
    ///         Example: [DllImport("user32.dll")]
    ///     (4) Functions must be declared in a class's outer scope and not in nested or helper classes.
    ///         Functions can be public or private.
    ///     (5) DLLs with extension methods should have an extra Extensions class matching the DLL name
    ///         Example: User32Extensions.cs
    ///
    /// PInvoke naming rules are checked during code inspection.
    /// </summary>
    public static class PInvokeCommon
    {
        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once InconsistentNaming
        public struct SIZE
        {
            public int cx;
            public int cy;

            public Size Size => new Size(cx, cy);
        }

    }
}