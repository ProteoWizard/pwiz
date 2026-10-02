/*
 * Original author: Nicholas Shulman <nicksh .at. u.washington.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 5.5) <noreply .at. anthropic.com>
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
using System.Linq;
using System.Text;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Presses one key on a control the way the keyboard does, whether or not the control has the focus, so
    /// every step a real key press goes through runs: the control's pre-processing (PreviewKeyDown, ProcessCmdKey up the parent
    /// chain for menu shortcuts, then ProcessDialogKey for Enter, Esc and Tab), then the key-down message, which
    /// WinForms shows first to any form that previews keys, then to KeyDown handlers, and then to the control's
    /// own window procedure (an arrow moving a list's selection, Home moving a tree's); then the character the
    /// key types, unless a KeyDown handler suppressed it (Backspace editing a text box); then the key-up.
    ///
    /// <para>A key message carries no modifiers: WinForms and the native controls read Ctrl, Shift and Alt from
    /// the thread's keyboard state. So while the key is pressed, that state says the key and exactly the
    /// modifiers named are down, and it is put back afterwards. Must run on the control's UI thread, whose
    /// keyboard state it is.</para>
    /// </summary>
    internal static class KeyStroke
    {
        private const byte KEY_DOWN = 0x80;

        // Keys whose key messages carry the extended-key flag, as the keyboard sends them.
        private static readonly Keys[] EXTENDED_KEYS =
        {
            Keys.Up, Keys.Down, Keys.Left, Keys.Right, Keys.Home, Keys.End, Keys.PageUp, Keys.PageDown,
            Keys.Insert, Keys.Delete
        };

        public static void Press(Control control, Keys keyData)
        {
            var keyCode = keyData & Keys.KeyCode;
            bool alt = (keyData & Keys.Alt) != 0;
            var savedState = new byte[256];
            User32.GetKeyboardState(savedState);
            var state = (byte[]) savedState.Clone();
            SetDown(state, (keyData & Keys.Shift) != 0, Keys.ShiftKey, Keys.LShiftKey, Keys.RShiftKey);
            SetDown(state, (keyData & Keys.Control) != 0, Keys.ControlKey, Keys.LControlKey, Keys.RControlKey);
            SetDown(state, alt, Keys.Menu, Keys.LMenu, Keys.RMenu);
            state[(int) keyCode] |= KEY_DOWN;
            User32.SetKeyboardState(state);
            try
            {
                var hwnd = control.Handle;
                uint scanCode = User32.MapVirtualKey((uint) keyCode, User32.MAPVK_VK_TO_VSC);
                long lParam = 1 | ((long) scanCode << 16) |
                              (EXTENDED_KEYS.Contains(keyCode) ? 1L << 24 : 0) |
                              (alt ? 1L << 29 : 0);
                // With Alt down the keyboard sends the system key messages (menu mnemonics).
                var keyDown = alt ? User32.WinMessageType.WM_SYSKEYDOWN : User32.WinMessageType.WM_KEYDOWN;
                var keyUp = alt ? User32.WinMessageType.WM_SYSKEYUP : User32.WinMessageType.WM_KEYUP;
                var charMessage = alt ? User32.WinMessageType.WM_SYSCHAR : User32.WinMessageType.WM_CHAR;

                if (!PreProcess(control, keyDown, (IntPtr) (int) keyCode, lParam))
                {
                    // The message loop translates the key-down, queuing its character, before dispatching it; a
                    // KeyDown handler that sets SuppressKeyPress removes the queued character. So the character
                    // is posted first, and only what is still queued after the key-down is delivered.
                    var character = GetCharacter(keyCode, scanCode, state);
                    if (character.HasValue)
                        User32.PostMessageA(hwnd, charMessage, character.Value, (int) lParam);
                    User32.SendMessage(hwnd, keyDown, (IntPtr) (int) keyCode, (IntPtr) lParam);
                    if (character.HasValue &&
                        User32.PeekMessage(out var queued, hwnd, (uint) charMessage, (uint) charMessage, User32.PM_REMOVE) &&
                        !control.IsDisposed &&
                        !PreProcess(control, charMessage, queued.wParam, (long) queued.lParam))
                    {
                        User32.SendMessage(hwnd, charMessage, queued.wParam, queued.lParam);
                    }
                }
                // The key-up has the previous-state and transition bits set.
                if (!control.IsDisposed)
                    User32.SendMessage(hwnd, keyUp, (IntPtr) (int) keyCode, (IntPtr) (lParam | (3L << 30)));
            }
            finally
            {
                User32.SetKeyboardState(savedState);
            }
        }

        // Control.PreProcessControlMessage is the message loop's step before a key message is dispatched: the
        // PreviewKeyDown event, then shortcuts (ProcessCmdKey) and dialog keys (ProcessDialogKey) for a key-down,
        // mnemonics for a character. Returns true when it used the message, which then goes no further.
        private static bool PreProcess(Control control, User32.WinMessageType messageType, IntPtr wParam, long lParam)
        {
            var message = Message.Create(control.Handle, (int) messageType, wParam, (IntPtr) lParam);
            return control.PreProcessControlMessage(ref message) == PreProcessControlState.MessageProcessed;
        }

        // The character the key types with this keyboard state, if it types exactly one.
        private static int? GetCharacter(Keys keyCode, uint scanCode, byte[] state)
        {
            var buffer = new StringBuilder(8);
            int count = User32.ToUnicode((uint) keyCode, scanCode, state, buffer, buffer.Capacity,
                User32.TOUNICODE_NO_STATE_CHANGE);
            return count == 1 ? buffer[0] : (int?) null;
        }

        // A modifier named in the key stroke is down as a left-hand press sets it (the generic and left keys);
        // one not named is up, whatever the real keyboard is doing, so only the modifiers asked for apply.
        private static void SetDown(byte[] state, bool down, Keys generic, Keys left, Keys right)
        {
            SetKey(state, generic, down);
            SetKey(state, left, down);
            SetKey(state, right, false);
        }

        private static void SetKey(byte[] state, Keys key, bool down)
        {
            if (down)
                state[(int) key] |= KEY_DOWN;
            else
                state[(int) key] &= unchecked((byte) ~KEY_DOWN);
        }
    }
}
