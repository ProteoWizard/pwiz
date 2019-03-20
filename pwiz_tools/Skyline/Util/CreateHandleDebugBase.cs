/*
 * Original author: Tobias Rohde <tobiasr .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 *
 * Copyright 2018 University of Washington - Seattle, WA
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
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace pwiz.Skyline.Util
{
    public class CreateHandleDebugBase : FormEx
    {
        private static void ReflectInvoke(object _this, Type T, string methodName, BindingFlags flags, params object[] args)
        {
            MethodInfo method;

            if (args.Any(a => a == null))
                method = T.GetMethod(methodName, flags); // Just hope that there is no overload
            else
                method = T.GetMethod(methodName, flags, null, args.Select(a => a.GetType()).ToArray(), null);


            // ReSharper disable once PossibleNullReferenceException
            method.Invoke(_this, args);
        }

        private static void ReflectInvoke(object _this, Type T, string methodName, params object[] args)
        {
            ReflectInvoke(_this, T, methodName, BindingFlags.Instance | BindingFlags.NonPublic, args);
        }

        private static void ReflectInvoke<T>(T _this, string methodName, params object[] args)
        {
            ReflectInvoke(_this, typeof(T), methodName, args);
        }

        private static R ReflectGetProperty<T, R>(T _this, string name, BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance)
        {
            var prop = typeof(T).GetProperty(name, flags);
            // ReSharper disable once PossibleNullReferenceException
            return (R)prop.GetValue(_this);
        }

        private static R ReflectGetField<T, R>(T _this, string name, BindingFlags flags = BindingFlags.NonPublic | BindingFlags.Instance)
        {
            var prop = typeof(T).GetField(name, flags);
            // ReSharper disable once PossibleNullReferenceException
            return (R)prop.GetValue(_this);
        }

        private static int GetStateValue(string name)
        {
            return ReflectGetField<Control, int>(null, name, BindingFlags.NonPublic | BindingFlags.Static);
        }

        private static BitVector32.Section GetFormStateSection(string name)
        {
            return ReflectGetField<Form, BitVector32.Section>(null, name, BindingFlags.NonPublic | BindingFlags.Static);
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        [Localizable(false)]
        private void ControlCreateHandle()
        {
            Program.Log?.Invoke(string.Format("\r\nStacktrace: {0}\r\n", Environment.StackTrace));
            Program.Log?.Invoke("\r\nBegin ControlCreateHandle\r\n");
            var formType = typeof(Form);
            var controlType = typeof(Control);

            // We don't use ReflectInvoke for these since we use them so often
            var getState = controlType.GetMethod("GetState", BindingFlags.NonPublic | BindingFlags.Instance);
            var setState = controlType.GetMethod("SetState", BindingFlags.NonPublic | BindingFlags.Instance);

            var userCookie = IntPtr.Zero;

            if ((bool) getState.Invoke(this, new object[] {GetStateValue("STATE_DISPOSED")}))
                throw new ObjectDisposedException(GetType().Name);

            var stateCreatingHandle = GetStateValue("STATE_CREATINGHANDLE");

            if ((bool) getState.Invoke(this, new object[] {stateCreatingHandle}))
                return;
            Rectangle bounds;

            var unsafeNativeMethods =
                formType.Assembly.GetType("System.Windows.Forms.UnsafeNativeMethods");

            var themingScope = unsafeNativeMethods.GetNestedType("ThemingScope", BindingFlags.NonPublic | BindingFlags.Instance);

            var parent = ReflectGetField<Control, Control>(this, "parent");

            try
            {
                Program.Log?.Invoke("Set STATE_CREATINGHANDLE=1\r\n");
                setState.Invoke(this, new object[] {stateCreatingHandle, true});
                
                bounds = Bounds;
                if (ReflectGetProperty<Application, bool>(null, "UseVisualStyles",
                    BindingFlags.NonPublic | BindingFlags.Static))
                {
                    Program.Log?.Invoke("Activating ThemingScope\r\n");
                    ReflectInvoke(null, themingScope, "Activate", BindingFlags.Public | BindingFlags.Static);
                }
                    
                var stateMirrored = GetStateValue("STATE_MIRRORED");
                var createParams = CreateParams;
                Program.Log?.Invoke("Changing STATE_MIRRORED\r\n");
                setState.Invoke(this,
                    new object[]
                    {
                        stateMirrored,
                        (uint) (createParams.ExStyle & GetStateValue("STATE_EXCEPTIONWHILEPAINTING")) > 0U
                    });

                if (parent != null)
                {
                    Program.Log?.Invoke("Parent != null\r\n");
                    var clientRect = parent.ClientRectangle;
                    if (!clientRect.IsEmpty)
                    {
                        if (createParams.X != int.MinValue)
                            createParams.X -= clientRect.X;
                        if (createParams.Y != int.MinValue)
                            createParams.Y -= clientRect.Y;
                    }
                }

                if (createParams.Parent == IntPtr.Zero && (createParams.Style & stateMirrored) != 0)
                {
                    Program.Log?.Invoke("Parking handle\r\n");
                    ReflectInvoke(null, typeof(Application), "ParkHandle", createParams);
                }

                var window = ReflectGetField<Control, object>(this, "window");

                Program.Log?.Invoke("Creating handle on ControlNativeWindow\r\n");
                ReflectInvoke(window, window.GetType(), "CreateHandle", BindingFlags.Public | BindingFlags.Instance, createParams);
                Program.Log?.Invoke("Calling UpdateReflectParent\r\n");
                ReflectInvoke<Control>(this, "UpdateReflectParent", true);
            }
            finally
            {
                Program.Log?.Invoke("finally block: Setting STATE_CREATINGHANDLE=0 \r\n");
                setState.Invoke(this, new object[] { stateCreatingHandle, false });
                Program.Log?.Invoke("finally block: Deactivating ThemingScope \r\n");
                ReflectInvoke(null, themingScope, "Deactivate", BindingFlags.Public | BindingFlags.Static, userCookie);
            }

            if (Bounds == bounds)
                return;

            Program.Log?.Invoke("Bounds != bounds. Calling DoLayout\r\n");

            var layoutTransaction = formType.Assembly.GetType("System.Windows.Forms.Layout.LayoutTransaction");

            // Actually uses ParentInternal and nameof(Bounds) but that doesn't really matter
            ReflectInvoke(null, layoutTransaction, "DoLayout", BindingFlags.Public | BindingFlags.Static, parent, this, "Bounds");

            Program.Log?.Invoke("End ControlCreateHandle\r\n\r\n");
        }

        [SuppressMessage("ReSharper", "PossibleNullReferenceException")]
        [Localizable(false)]
        private void FormCreateHandle()
        {
            var formType = typeof(Form);

            var mdiParentInternal = ReflectGetProperty<Form, Form>(this, "MdiParentInternal");

            if (mdiParentInternal != null)
                ReflectInvoke(mdiParentInternal, "SuspendUpdateMenuHandles");

            try
            {
                var formState = ReflectGetField<Form, BitVector32>(this, "formState");

                if (IsMdiChild && mdiParentInternal.IsHandleCreated)
                {
                    var mdiClient = ReflectGetProperty<Form, MdiClient>(this, "MdiClient");

                    if (mdiClient != null && !mdiClient.IsHandleCreated)
                        mdiClient.CreateControl();
                }

                var formStateWindowState = GetFormStateSection("FormStateWindowState");
                if (IsMdiChild && formState[formStateWindowState] == 2)
                {
                    var formStateMdiChildMax = GetFormStateSection("FormStateMdiChildMax");

                    formState[formStateWindowState] = 0;
                    formState[formStateMdiChildMax] = 1;
                    ControlCreateHandle();
                    formState[formStateWindowState] = 2;
                    formState[formStateMdiChildMax] = 0;
                }
                else
                {
                    ControlCreateHandle();
                }

                ReflectInvoke<Form>(this, "UpdateHandleWithOwner");
                ReflectInvoke<Form>(this, "UpdateWindowIcon", false);
                ReflectInvoke<Form>(this, "AdjustSystemMenu");

                if (formState[GetFormStateSection("FormStateStartPos")] != 3)
                    ReflectInvoke<Form>(this, "ApplyClientSize");
                if (formState[GetFormStateSection("FormStateShowWindowOnCreate")] == 1)
                    Visible = true;
                if (Menu != null || !TopLevel || IsMdiContainer)
                    ReflectInvoke<Form>(this, "UpdateMenuHandles");
                if (!ShowInTaskbar)
                {
                    var ownerInternal = ReflectGetProperty<Form, Form>(this, "OwnerInternal");

                    if (ownerInternal == null && TopLevel)
                    {
                        var taskbarOwner = ReflectGetProperty<Form, HandleRef>(this, "TaskbarOwner");

                        var unsafeNativeMethods = formType.Assembly.GetType("System.Windows.Forms.UnsafeNativeMethods");

                        var setWindowLong = unsafeNativeMethods.GetMethod("SetWindowLong", new [] {typeof(HandleRef), typeof(int), typeof(HandleRef)});
                        setWindowLong.Invoke(null, new object[] {new HandleRef(this, Handle), -8, taskbarOwner});

                        if (Icon != null && taskbarOwner.Handle != IntPtr.Zero)
                        {
                            var sendMessage = unsafeNativeMethods.GetMethod("SendMessage",
                                new[] {typeof(HandleRef), typeof(int), typeof(int), typeof(IntPtr)});
                            sendMessage.Invoke(null, new object[] {taskbarOwner, 128, 1, Icon.Handle});
                        }
                    }
                }

                if (formState[GetFormStateSection("FormStateTopMost")] == 0)
                    return;
                TopMost = true;
            }
            finally
            {
                if (mdiParentInternal != null)
                {
                    ReflectInvoke(mdiParentInternal, "ResumeUpdateMenuHandles");
                    UpdateStyles();
                }
            }
        }

        /* Uncomment this for debugging purposes
        protected override void CreateHandle()
        {
            if (Program.FunctionalTest)
                FormCreateHandle();
            else
                base.CreateHandle();
        }
        */
    }
}