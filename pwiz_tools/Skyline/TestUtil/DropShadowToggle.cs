/*
 * Original author: Matt Chambers <matt.chambers42 .at. gmail.com>
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
using System.Threading;
using TestRunnerLib.PInvoke;

namespace pwiz.SkylineTestUtil
{
    /// <summary>
    /// Utility for toggling window drop shadows via SystemParametersInfo.
    /// Used during screenshot recording to capture both shadow and no-shadow
    /// versions for cross-platform comparison compatibility.
    /// </summary>
    public static class DropShadowToggle
    {
        /// <summary>
        /// Time to wait after toggling shadows for the DWM compositor to update.
        /// </summary>
        private const int DWM_SETTLE_MS = 300;

        public static bool GetDropShadowEnabled()
        {
            bool enabled = false;
            User32Test.SystemParametersInfo(User32Test.SPI_GETDROPSHADOW, 0, ref enabled, 0);
            return enabled;
        }

        public static void SetDropShadowEnabled(bool enabled)
        {
            // SPI_SETDROPSHADOW expects pvParam as IntPtr (0=false, non-zero=true)
            User32Test.SystemParametersInfo(User32Test.SPI_SETDROPSHADOW, 0,
                enabled ? new IntPtr(1) : IntPtr.Zero, User32Test.SPIF_SENDCHANGE);
        }

        /// <summary>
        /// Temporarily disables drop shadows and returns a disposable that restores the original state.
        /// Waits for the DWM compositor to settle after toggling.
        /// Returns null if shadows are already disabled.
        /// </summary>
        public static IDisposable TemporarilyDisable()
        {
            if (!GetDropShadowEnabled())
                return null;

            SetDropShadowEnabled(false);
            Thread.Sleep(DWM_SETTLE_MS);
            return new ShadowRestorer();
        }

        private class ShadowRestorer : IDisposable
        {
            public void Dispose()
            {
                SetDropShadowEnabled(true);
            }
        }
    }
}
