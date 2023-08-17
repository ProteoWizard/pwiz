/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2015 University of Washington - Seattle, WA
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
using System.Runtime.InteropServices;

namespace pwiz.Common.SystemUtil
{
    /// <summary>
    /// This code was copied from a StackOverflow post:
    /// http://stackoverflow.com/questions/1295890/windows-7-progress-bar-in-taskbar-in-c
    /// </summary>
    public class TaskbarProgress
    {
        public enum TaskbarStates
        {
            NoProgress = 0,
            Indeterminate = 0x1,
            Normal = 0x2,
            Error = 0x4,
            Paused = 0x8
        }

        [ComImportAttribute]
        [GuidAttribute("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
        [InterfaceTypeAttribute(ComInterfaceType.InterfaceIsIUnknown)]
        private interface ITaskbarList3
        {
            // ITaskbarList
            [PreserveSig]
            void HrInit();
            [PreserveSig]
            void AddTab(IntPtr hwnd);
            [PreserveSig]
            void DeleteTab(IntPtr hwnd);
            [PreserveSig]
            void ActivateTab(IntPtr hwnd);
            [PreserveSig]
            void SetActiveAlt(IntPtr hwnd);

            // ITaskbarList2
            [PreserveSig]
            void MarkFullscreenWindow(IntPtr hwnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

            // ITaskbarList3
            [PreserveSig]
            void SetProgressValue(IntPtr hwnd, UInt64 ullCompleted, UInt64 ullTotal);
            [PreserveSig]
            void SetProgressState(IntPtr hwnd, TaskbarStates state);
        }

        [GuidAttribute("56FDF344-FD6D-11d0-958A-006097C9A090")]
        [ClassInterfaceAttribute(ClassInterfaceType.None)]
        [ComImportAttribute()]
        private class TaskbarInstance
        {
        }

        private ITaskbarList3 GetTaskbarList()
        {
            if (null == _taskbarList)
            {
                // ReSharper disable SuspiciousTypeConversion.Global
                _taskbarList = (ITaskbarList3) new TaskbarInstance();
                // ReSharper restore SuspiciousTypeConversion.Global
            }
            return _taskbarList;
        }

        private ITaskbarList3 _taskbarList;
        private static readonly bool _taskbarSupported = Environment.OSVersion.Version >= new Version(6, 1);

        public bool TaskBarSupported
        {
            get
            {
                return _taskbarSupported;
            }
        }

        public void SetState(IntPtr windowHandle, TaskbarStates taskbarState)
        {
            if (_taskbarSupported)
            {
                GetTaskbarList().SetProgressState(windowHandle, taskbarState);
            }
        }

        public void SetValue(IntPtr windowHandle, double progressValue, double progressMax)
        {
            if (_taskbarSupported)
            {
                GetTaskbarList().SetProgressValue(windowHandle, (ulong)progressValue, (ulong)progressMax);
            }
        }
    }
}
