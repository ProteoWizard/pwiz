/*
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
using System.Runtime.InteropServices;
using Accessibility;

namespace pwiz.Common.PInvoke
{
    /// <summary>
    /// Microsoft Active Accessibility (oleacc.dll) -- the legacy IAccessible (MSAA) layer screen readers use
    /// to reach a surface whose UI Automation does not expose what is needed. Used to "press" the default
    /// button of a DirectUI surface (the common Open/Save file dialog), where UI Automation's InvokePattern
    /// silently no-ops.
    ///
    /// This lives in the pwiz.Common assembly (rather than alongside the other PInvoke classes in CommonUtil)
    /// because it depends on the Accessibility assembly, which CommonUtil deliberately does not reference.
    /// </summary>
    public static class Oleacc
    {
        /// <summary>
        /// Finds the default push button (a ROLE_SYSTEM_PUSHBUTTON with STATE_SYSTEM_DEFAULT) in the
        /// accessible tree of the window <paramref name="hwnd"/>, or returns null if none is found. Invoke it
        /// the way a screen reader does: <c>button.accDoDefaultAction(0)</c> (0 is CHILDID_SELF).
        /// </summary>
        public static IAccessible GetDefaultPushButton(IntPtr hwnd)
        {
            var iidAccessible = IID_IAccessible;
            if (AccessibleObjectFromWindow(hwnd, OBJID_CLIENT, ref iidAccessible, out var rootObj) != 0
                || !(rootObj is IAccessible root))
            {
                return null;
            }
            return FindDefaultPushButton(root, 0);
        }

        // Depth-first search of the accessible tree for the default push button. Only a "full" IAccessible
        // child can be returned (and later invoked on its own); a "simple" child -- an integer id queried on
        // its parent -- has no standalone IAccessible, so it is skipped.
        private static IAccessible FindDefaultPushButton(IAccessible container, int depth)
        {
            if (depth > MAX_MSAA_DEPTH)
                return null;
            int count;
            try
            {
                count = container.accChildCount;
            }
            catch (Exception)
            {
                return null;
            }
            if (count <= 0)
                return null;
            var children = new object[count];
            if (AccessibleChildren(container, 0, count, children, out var obtained) != 0)
                return null;
            for (int i = 0; i < obtained; i++)
            {
                if (!(children[i] is IAccessible child))
                    continue;
                if (IsDefaultPushButton(child))
                    return child;
                var found = FindDefaultPushButton(child, depth + 1);
                if (found != null)
                    return found;
            }
            return null;
        }

        private static bool IsDefaultPushButton(IAccessible acc)
        {
            try
            {
                return Convert.ToInt32(acc.get_accRole(CHILDID_SELF)) == ROLE_SYSTEM_PUSHBUTTON
                       && (Convert.ToInt32(acc.get_accState(CHILDID_SELF)) & STATE_SYSTEM_DEFAULT) != 0;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The IAccessible interface id, and the MSAA constants (oleacc).
        private static readonly Guid IID_IAccessible = new Guid(@"618736E0-3C3D-11CF-810C-00AA00389B71");
        private const uint OBJID_CLIENT = 0xFFFFFFFC;
        private const int CHILDID_SELF = 0;
        private const int ROLE_SYSTEM_PUSHBUTTON = 0x2B;
        private const int STATE_SYSTEM_DEFAULT = 0x100;
        private const int MAX_MSAA_DEPTH = 12;

        [DllImport(@"oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint id, ref Guid iid,
            [MarshalAs(UnmanagedType.Interface)] out object ppvObject);

        [DllImport(@"oleacc.dll")]
        private static extern int AccessibleChildren(IAccessible paccContainer, int iChildStart, int cChildren,
            [Out] object[] rgvarChildren, out int pcObtained);
    }
}
