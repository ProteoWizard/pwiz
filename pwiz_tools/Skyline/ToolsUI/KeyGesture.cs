/*
 * Original author: Brendan MacLean <brendanx .at. uw.edu>,
 *                  MacCoss Lab, Department of Genome Sciences, UW
 * AI assistance: Claude Code (Claude Opus 4.8) <noreply .at. anthropic.com>
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
using System.Collections.Generic;
using System.Windows.Forms;
using pwiz.Common.SystemUtil.PInvoke;
using pwiz.Skyline.Util.Extensions;

namespace pwiz.Skyline.ToolsUI
{
    /// <summary>
    /// Sends keystrokes to ONE control, whether or not it has the focus.
    ///
    /// <para>The keys go straight to the control's window as real key messages, so the control processes them
    /// exactly as it would if the user had typed with it focused -- which is the whole point: typing into the
    /// document tree raises Skyline's auto-completion popup, and nothing short of real key input does that.
    /// Nothing is routed through the focused control or the foreground window, so a caller never has to arrange
    /// focus first, and driving a background Skyline cannot steal the user's keyboard.</para>
    ///
    /// <para>There is deliberately NO modifier (Ctrl/Alt/Shift) support. The only modified keystroke the
    /// tutorials call for is Ctrl+V, and a keystroke is the wrong way to do it: it would paste whatever happens
    /// to be on the user's clipboard. The connector pastes with the text it is given instead (the "paste"
    /// action, <see cref="IClipboardElement"/>), which needs no clipboard and no keystroke. Holding a modifier
    /// for a delivered message would also mean mutating the UI thread's key state, where a stuck Ctrl would
    /// corrupt every later gesture on that thread -- cost with no demand behind it.</para>
    /// </summary>
    internal static class KeyGesture
    {
        // A named key: the virtual key, and the character Windows' TranslateMessage would produce from it, or
        // '\0' for a key that produces none. That second part is load-bearing. A control gets a keystroke as up
        // to three messages -- WM_KEYDOWN, then (only for a key that maps to a character) WM_CHAR, then
        // WM_KEYUP -- and an edit control acts on the WM_CHAR: send Backspace as WM_KEYDOWN/WM_KEYUP alone and
        // the text box simply ignores it. Keys with no character (the arrows, Home/End, Delete) are handled
        // from WM_KEYDOWN, and must NOT get a WM_CHAR.
        private struct NamedKey
        {
            public NamedKey(Keys key, char character = '\0') { Key = key; Character = character; }
            public Keys Key { get; }
            public char Character { get; }
        }

        // Matched case-insensitively. Enter and Down are what the auto-completion steps need; the rest are the
        // obvious neighbours a caller will reach for.
        private static readonly Dictionary<string, NamedKey> NAMED_KEYS =
            new Dictionary<string, NamedKey>(StringComparer.OrdinalIgnoreCase)
            {
                { @"ENTER", new NamedKey(Keys.Return, '\r') }, { @"RETURN", new NamedKey(Keys.Return, '\r') },
                { @"TAB", new NamedKey(Keys.Tab, '\t') },
                { @"ESC", new NamedKey(Keys.Escape, (char) 27) }, { @"ESCAPE", new NamedKey(Keys.Escape, (char) 27) },
                { @"BACKSPACE", new NamedKey(Keys.Back, '\b') }, { @"BS", new NamedKey(Keys.Back, '\b') },
                { @"DEL", new NamedKey(Keys.Delete) }, { @"DELETE", new NamedKey(Keys.Delete) },
                { @"UP", new NamedKey(Keys.Up) }, { @"DOWN", new NamedKey(Keys.Down) },
                { @"LEFT", new NamedKey(Keys.Left) }, { @"RIGHT", new NamedKey(Keys.Right) },
                { @"HOME", new NamedKey(Keys.Home) }, { @"END", new NamedKey(Keys.End) },
                { @"PGUP", new NamedKey(Keys.PageUp) }, { @"PGDN", new NamedKey(Keys.PageDown) }
            };

        /// <summary>Sends <paramref name="keys"/> to <paramref name="control"/>. Ordinary characters are typed
        /// literally, <c>{NAME}</c> is a named key (ENTER, TAB, ESC, BACKSPACE, DEL, UP, DOWN, LEFT, RIGHT,
        /// HOME, END, PGUP, PGDN) and <c>~</c> is Enter. A character the syntax reserves is written inside
        /// braces -- <c>{{}</c>, <c>{}}</c>, <c>{~}</c>. Must be called on the control's UI thread.</summary>
        public static void Send(Control control, string keys)
        {
            if (string.IsNullOrEmpty(keys))
                return;
            var handle = control.Handle;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == '{')
                {
                    int close = keys.IndexOf('}', i + 1);
                    if (close < 0)
                        throw new ArgumentException(new LlmInstruction(@"Keys have a '{' with no matching '}'."));
                    SendBraced(handle, keys.Substring(i + 1, close - i - 1));
                    i = close;
                }
                else if (keys[i] == '~')
                {
                    SendKey(handle, NAMED_KEYS[@"ENTER"]);
                }
                else
                {
                    SendCharacter(handle, keys[i]);
                }
            }
        }

        // The contents of a {...} group: a named key, or a single character written literally so the characters
        // the syntax reserves ({ } ~) can still be typed.
        private static void SendBraced(IntPtr handle, string name)
        {
            if (NAMED_KEYS.TryGetValue(name, out var key))
            {
                SendKey(handle, key);
                return;
            }
            if (name.Length == 1)
            {
                SendCharacter(handle, name[0]);
                return;
            }
            throw new ArgumentException(LlmInstruction.Format(
                @"Unknown key name '{{{0}}}'. Use a named key (ENTER, TAB, ESC, BACKSPACE, DEL, UP, DOWN, LEFT, RIGHT, HOME, END, PGUP, PGDN) or a single character.",
                name));
        }

        // A typed character is a WM_CHAR -- the message that actually inserts text and drives auto-completion.
        private static void SendCharacter(IntPtr handle, char c)
        {
            User32.SendMessage(handle, User32.WinMessageType.WM_CHAR, (IntPtr) c, IntPtr.Zero);
        }

        // A named key, as the message pump delivers one: WM_KEYDOWN, the WM_CHAR that TranslateMessage would
        // produce when the key maps to a character, then WM_KEYUP. Sending only the down/up pair is what makes
        // a text box ignore Backspace -- it acts on the character, not the virtual key.
        private static void SendKey(IntPtr handle, NamedKey namedKey)
        {
            User32.SendMessage(handle, User32.WinMessageType.WM_KEYDOWN, (IntPtr) (int) namedKey.Key, IntPtr.Zero);
            if (namedKey.Character != '\0')
                SendCharacter(handle, namedKey.Character);
            User32.SendMessage(handle, User32.WinMessageType.WM_KEYUP, (IntPtr) (int) namedKey.Key, IntPtr.Zero);
        }
    }
}
