using System;
using System.Diagnostics;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;

namespace TestRunnerLib.PInvoke
{
    public static class User32TestExtensions
    {
        /// <summary>
        /// Adjust z-order without activating.
        /// </summary>
        /// <param name="form">The form for which to change z-order</param>
        /// <param name="formInsertAfterHandle">The handle of the form to place the other form behind. It is important that this form is specified by its handle to avoid CrossThreadOperationException</param>
        public static void BringWindowToSameLevelWithoutActivating(this Form form, IntPtr formInsertAfterHandle)
        {
            const User32.SetWindowPosFlags flags = User32.SetWindowPosFlags.NOMOVE |
                                                   User32.SetWindowPosFlags.NOSIZE |
                                                   User32.SetWindowPosFlags.NOACTIVATE |
                                                   User32.SetWindowPosFlags.SHOWWINDOW;

            User32.SetWindowPos(form.Handle, formInsertAfterHandle, 0, 0, 0, 0, flags);
        }

        public static void HideCaret(this ComboBox comboBox)
        {
            var handleEdit = User32Test.FindWindowEx(comboBox.Handle, IntPtr.Zero, "Edit", null);
            if (handleEdit != IntPtr.Zero)
            {
                User32.HideCaret(handleEdit);
            }
        }

        public static int GetGuiResources(this Process process, User32Test.HandleType type)
        {
            return User32Test.GetGuiResources(process.Handle, (int)type);
        }

        public static void SetForegroundWindow(this Control control)
        {
            User32Test.SetForegroundWindow(control.Handle);
        }

        public static void AllowSetForegroundWindow(this Process process)
        {
            User32Test.AllowSetForegroundWindow((uint) process.Id);
        }
    }
}