using System;
using System.Diagnostics;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;

namespace TestRunnerLib.PInvoke
{
    public static class User32TestExtensions
    {
        /// <summary>
        /// Adjust z-order without activating
        /// </summary>
        public static void BringWindowToSameLevelWithoutActivating(this Form form, Form formInsertAfter)
        {
            const User32.SetWindowPosFlags flags = User32.SetWindowPosFlags.NOMOVE |
                                                   User32.SetWindowPosFlags.NOSIZE |
                                                   User32.SetWindowPosFlags.NOACTIVATE |
                                                   User32.SetWindowPosFlags.SHOWWINDOW;

            User32.SetWindowPos(form.Handle, formInsertAfter.Handle, 0, 0, 0, 0, flags);
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
    }
}