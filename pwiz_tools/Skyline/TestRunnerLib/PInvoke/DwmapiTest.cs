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

using System.Runtime.InteropServices;
using System;
using pwiz.Common.SystemUtil.PInvoke;

namespace TestRunnerLib.PInvoke
{
    // ReSharper disable IdentifierTypo
    public static class DwmapiTest
    {
        // ReSharper disable InconsistentNaming
        [Flags]
        public enum TNPFlags
        {
            VISIBLE = 0x8,
            OPACITY = 0x4,
            // ReSharper disable IdentifierTypo
            RECTDESTINATION = 0x1,
        }
        // ReSharper restore InconsistentNaming

        [StructLayout(LayoutKind.Sequential)]
        // ReSharper disable once InconsistentNaming
        public struct THUMBNAIL_PROPERTIES
        {
            public int dwFlags;
            public User32.RECT rcDestination;
            public User32.RECT rcSource;
            public byte opacity;
            public bool fVisible;
            public readonly bool fSourceClientAreaOnly;
        }

        // ReSharper disable once StringLiteralTypo
        [DllImport("dwmapi.dll")]
        public static extern int DwmRegisterThumbnail(IntPtr dest, IntPtr src, out IntPtr thumb);

        // ReSharper disable once StringLiteralTypo
        [DllImport("dwmapi.dll")]
        public static extern int DwmUnregisterThumbnail(IntPtr thumb);

        // ReSharper disable once StringLiteralTypo
        [DllImport("dwmapi.dll")]
        public static extern int DwmQueryThumbnailSourceSize(IntPtr thumb, out PInvokeCommon.SIZE size);

        // ReSharper disable once StringLiteralTypo
        [DllImport("dwmapi.dll")]
        public static extern int DwmUpdateThumbnailProperties(IntPtr hThumb, ref THUMBNAIL_PROPERTIES props);
    }
}