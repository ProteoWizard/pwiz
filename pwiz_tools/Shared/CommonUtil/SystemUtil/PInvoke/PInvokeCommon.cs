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
    ///     (1) Every DLL must have its own class
    ///     (2) Every class must represent exactly 1 Win32 DLL
    ///     (3) Class names and file names must match the DLL name (minus ".dll").
    ///         Example: "user32.dll" and User32 and User32.cs
    ///     (4) DLLs with more than 3 extension methods may have an extra "Extensions" class
    ///         Example: User32Extensions.cs
    ///
    /// Some of these rules are enforced with static analysis implemented in tests.
    /// </summary>
    public static class PInvokeCommon
    {
        // Note, using an all caps struct name to indicate this is a Win32 type
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